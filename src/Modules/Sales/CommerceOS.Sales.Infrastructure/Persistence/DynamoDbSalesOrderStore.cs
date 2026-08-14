using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Sales.Application;
using CommerceOS.Sales.Domain;

namespace CommerceOS.Sales.Infrastructure.Persistence;

public sealed record DynamoDbSalesOptions(string TableName);
/// <summary>One Sales-local transaction records the immutable order, idempotency claim, process, and start outbox.</summary>
public sealed class DynamoDbSalesOrderStore(IAmazonDynamoDB client, DynamoDbSalesOptions options) : ISalesOrderStore, IRefundStore
{
    public async Task<SalesStoreOutcome> PlaceAsync(TrustedSalesContext context, SalesOrder order, string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var idempotencyKeyItem = Key(context.TenantId, $"IDEMPOTENCY#{E(idempotencyKey)}");
        var existing = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = idempotencyKeyItem }, cancellationToken);
        if (existing.Item.Count != 0) return existing.Item["RequestHash"].S == requestHash ? SalesStoreOutcome.Replayed : SalesStoreOutcome.Conflict;
        try
        {
            await client.TransactWriteItemsAsync(new() { TransactItems = [new() { Put = new() { TableName = options.TableName, Item = OrderItem(order), ConditionExpression = "attribute_not_exists(PK)" } }, new() { Put = new() { TableName = options.TableName, Item = new() { ["PK"] = S(P(context.TenantId)), ["SK"] = S($"IDEMPOTENCY#{E(idempotencyKey)}"), ["RequestHash"] = S(requestHash), ["OrderId"] = S(order.Id.Value) }, ConditionExpression = "attribute_not_exists(PK)" } }, new() { Put = new() { TableName = options.TableName, Item = ProcessItem(order), ConditionExpression = "attribute_not_exists(PK)" } }, new() { Put = new() { TableName = options.TableName, Item = OutboxItem(order, context.CorrelationId), ConditionExpression = "attribute_not_exists(PK)" } }] }, cancellationToken);
            return SalesStoreOutcome.Applied;
        }
        catch (TransactionCanceledException)
        {
            existing = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = idempotencyKeyItem }, cancellationToken);
            return existing.Item.Count != 0 && existing.Item["RequestHash"].S == requestHash ? SalesStoreOutcome.Replayed : SalesStoreOutcome.Conflict;
        }
    }
    public async Task<SalesOrder?> GetAsync(TrustedSalesContext context, SalesOrderId orderId, CancellationToken cancellationToken)
    { var response = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, $"ORDER#{E(orderId.Value)}") }, cancellationToken); return response.Item.Count == 0 ? null : Read(response.Item); }
    public async Task<SalesStoreOutcome> SaveAsync(TrustedSalesContext context, SalesOrder before, SalesOrder after, CancellationToken cancellationToken)
    {
        try { await client.PutItemAsync(new() { TableName = options.TableName, Item = OrderItem(after), ConditionExpression = "Revision = :revision", ExpressionAttributeValues = new() { [":revision"] = N(before.Revision) } }, cancellationToken); return SalesStoreOutcome.Applied; }
        catch (ConditionalCheckFailedException) { return SalesStoreOutcome.Conflict; }
    }
    public async Task<SalesOrderPage> ListAsync(TrustedSalesContext context, string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        var response = await client.QueryAsync(new() { TableName = options.TableName, KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)", ExpressionAttributeValues = new() { [":pk"] = S(P(context.TenantId)), [":prefix"] = S("ORDER#") }, Limit = pageSize, ExclusiveStartKey = string.IsNullOrWhiteSpace(cursor) ? null : Key(context.TenantId, $"ORDER#{E(cursor)}") }, cancellationToken);
        var orders = response.Items.Select(Read).ToArray(); return new(orders, response.LastEvaluatedKey.Count == 0 ? null : orders.LastOrDefault()?.Id.Value);
    }
    public async Task<RefundRequest?> GetRefundAsync(TrustedSalesContext context, string refundRequestId, CancellationToken ct)
    {
        var response = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, $"REFUND#{E(refundRequestId)}") }, ct);
        return response.Item.Count == 0 ? null : ReadRefund(response.Item);
    }
    public async Task<SalesStoreOutcome> CreateRefundAsync(TrustedSalesContext context, RefundRequest request, CancellationToken ct)
    {
        if (request.TenantId != context.TenantId) return SalesStoreOutcome.Conflict;
        try { await client.PutItemAsync(new() { TableName = options.TableName, Item = RefundItem(request), ConditionExpression = "attribute_not_exists(PK)" }, ct); return SalesStoreOutcome.Applied; }
        catch (ConditionalCheckFailedException) { var existing = await GetRefundAsync(context, request.Id, ct); return existing?.RequestSourceIdentity == request.RequestSourceIdentity ? SalesStoreOutcome.Replayed : SalesStoreOutcome.Conflict; }
    }
    public async Task<SalesStoreOutcome> DecideRefundAsync(TrustedSalesContext context, RefundRequest before, RefundRequest after, CancellationToken ct)
    {
        if (before.TenantId != context.TenantId || after.TenantId != context.TenantId) return SalesStoreOutcome.Conflict;
        var writes = new List<TransactWriteItem>
        {
            new() { Put = new Put { TableName = options.TableName, Item = RefundItem(after), ConditionExpression = "Revision = :revision AND #status = :requested", ExpressionAttributeNames = new() { ["#status"] = "Status" }, ExpressionAttributeValues = new() { [":revision"] = N(before.Revision), [":requested"] = S(RefundRequestStatus.Requested.ToString()) } } },
            new() { Put = new Put { TableName = options.TableName, Item = RefundAuditIntent(after, context.CorrelationId), ConditionExpression = "attribute_not_exists(PK)" } }
        };
        if (after.Status is RefundRequestStatus.Approved) writes.Add(new() { Put = new Put { TableName = options.TableName, Item = RefundApprovedOutbox(after, context.CorrelationId), ConditionExpression = "attribute_not_exists(PK)" } });
        try { await client.TransactWriteItemsAsync(new() { TransactItems = writes }, ct); return SalesStoreOutcome.Applied; } catch (TransactionCanceledException) { return SalesStoreOutcome.Conflict; }
    }
    private static SalesOrder Read(Dictionary<string, AttributeValue> item) => new(new(item["OrderId"].S), new(item["TenantId"].S), JsonSerializer.Deserialize<SalesOrderLine[]>(item["Lines"].S) ?? [], long.Parse(item["TotalVnd"].N, CultureInfo.InvariantCulture), JsonSerializer.Deserialize<GuestSnapshot>(item["Guest"].S)!, Enum.Parse<SalesOrderStatus>(item["Status"].S), long.Parse(item["Revision"].N, CultureInfo.InvariantCulture), new(item["ProcessId"].S, item["ExecutionIdentity"].S, item["StartPending"].BOOL ?? false), new HashSet<string>(JsonSerializer.Deserialize<string[]>(item["Sources"].S) ?? [], StringComparer.Ordinal));
    private static RefundRequest ReadRefund(Dictionary<string, AttributeValue> item) => new(item["RefundRequestId"].S, new(item["TenantId"].S), new(item["OrderId"].S), item["PaymentId"].S, long.Parse(item["AmountVnd"].N, CultureInfo.InvariantCulture), item["Currency"].S, JsonSerializer.Deserialize<RefundLine[]>(item["Lines"].S) ?? [], Enum.Parse<RefundRequestStatus>(item["Status"].S), item["RequestSourceIdentity"].S, item["RequestedBy"].S, item.TryGetValue("DecisionSourceIdentity", out var decisionSource) ? decisionSource.S : null, item.TryGetValue("DecidedBy", out var decidedBy) ? decidedBy.S : null, DateTimeOffset.Parse(item["RequestedAt"].S, CultureInfo.InvariantCulture), item.TryGetValue("DecidedAt", out var decidedAt) ? DateTimeOffset.Parse(decidedAt.S, CultureInfo.InvariantCulture) : null, long.Parse(item["Revision"].N, CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> OrderItem(SalesOrder order) => new() { ["PK"] = S(P(order.TenantId)), ["SK"] = S($"ORDER#{E(order.Id.Value)}"), ["OrderId"] = S(order.Id.Value), ["TenantId"] = S(order.TenantId.Value), ["Lines"] = S(JsonSerializer.Serialize(order.Lines)), ["TotalVnd"] = N(order.TotalVnd), ["Guest"] = S(JsonSerializer.Serialize(order.Guest)), ["Status"] = S(order.Status.ToString()), ["Revision"] = N(order.Revision), ["ProcessId"] = S(order.Process.Id), ["ExecutionIdentity"] = S(order.Process.WorkflowExecutionIdentity), ["StartPending"] = new() { BOOL = order.Process.StartPending }, ["Sources"] = S(JsonSerializer.Serialize(order.AcceptedSources)) };
    private static Dictionary<string, AttributeValue> ProcessItem(SalesOrder o) => new() { ["PK"] = S(P(o.TenantId)), ["SK"] = S($"PROCESS#{E(o.Process.Id)}"), ["OrderId"] = S(o.Id.Value), ["ExecutionIdentity"] = S(o.Process.WorkflowExecutionIdentity), ["Status"] = S("StartPending") };
    private static Dictionary<string, AttributeValue> OutboxItem(SalesOrder o, string correlationId) => new() { ["PK"] = S(P(o.TenantId)), ["SK"] = S($"OUTBOX#ORDERPLACED#{E(o.Id.Value)}"), ["EventId"] = S($"event-{o.Process.Id}"), ["EventType"] = S("OrderPlaced"), ["EventVersion"] = S("1"), ["AggregateId"] = S(o.Id.Value), ["CorrelationId"] = S(correlationId), ["CausationId"] = S(o.Id.Value), ["Status"] = S("Pending") };
    private static Dictionary<string, AttributeValue> RefundItem(RefundRequest r) => new Dictionary<string, AttributeValue> { ["PK"] = S(P(r.TenantId)), ["SK"] = S($"REFUND#{E(r.Id)}"), ["RefundRequestId"] = S(r.Id), ["TenantId"] = S(r.TenantId.Value), ["OrderId"] = S(r.OrderId.Value), ["PaymentId"] = S(r.PaymentId), ["AmountVnd"] = N(r.AmountVnd), ["Currency"] = S(r.Currency), ["Lines"] = S(JsonSerializer.Serialize(r.Lines)), ["Status"] = S(r.Status.ToString()), ["RequestSourceIdentity"] = S(r.RequestSourceIdentity), ["RequestedBy"] = S(r.RequestedBy), ["RequestedAt"] = S(r.RequestedAt.ToString("O", CultureInfo.InvariantCulture)), ["Revision"] = N(r.Revision) }.WithOptional("DecisionSourceIdentity", r.DecisionSourceIdentity).WithOptional("DecidedBy", r.DecidedBy).WithOptional("DecidedAt", r.DecidedAt?.ToString("O", CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> RefundApprovedOutbox(RefundRequest r, string correlationId) => new() { ["PK"] = S(P(r.TenantId)), ["SK"] = S($"OUTBOX#REFUNDAPPROVED#{E(r.Id)}"), ["EventId"] = S($"event-refund-{r.Id}"), ["EventType"] = S("RefundApproved"), ["EventVersion"] = S("1"), ["AggregateId"] = S(r.Id), ["OrderId"] = S(r.OrderId.Value), ["PaymentId"] = S(r.PaymentId), ["AmountVnd"] = N(r.AmountVnd), ["Currency"] = S(r.Currency), ["Lines"] = S(JsonSerializer.Serialize(r.Lines)), ["CorrelationId"] = S(correlationId), ["CausationId"] = S(r.DecisionSourceIdentity!), ["Status"] = S("Pending") };
    private static Dictionary<string, AttributeValue> RefundAuditIntent(RefundRequest r, string correlationId) => new() { ["PK"] = S(P(r.TenantId)), ["SK"] = S($"AUDIT-OUTBOX#REFUND#{E(r.Id)}#{E(r.DecisionSourceIdentity!)}"), ["Action"] = S(r.Status is RefundRequestStatus.Approved ? "RefundApproved" : "RefundRejected"), ["ActorId"] = S(r.DecidedBy!), ["TargetId"] = S(r.Id), ["CorrelationId"] = S(correlationId), ["Status"] = S("Pending") };
    private static Dictionary<string, AttributeValue> Key(SalesTenantId tenantId, string sk) => new() { ["PK"] = S(P(tenantId)), ["SK"] = S(sk) };
    private static string P(SalesTenantId id) => $"TENANT#{E(id.Value)}"; private static string E(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static AttributeValue S(string value) => new() { S = value }; private static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
}

internal static class AttributeValueMapExtensions
{
    public static Dictionary<string, AttributeValue> WithOptional(this Dictionary<string, AttributeValue> values, string key, string? value) { if (!string.IsNullOrWhiteSpace(value)) values[key] = new() { S = value }; return values; }
}
