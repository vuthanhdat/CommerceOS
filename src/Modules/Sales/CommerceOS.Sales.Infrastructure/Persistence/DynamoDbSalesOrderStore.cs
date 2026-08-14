using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Sales.Application;
using CommerceOS.Sales.Domain;

namespace CommerceOS.Sales.Infrastructure.Persistence;

public sealed record DynamoDbSalesOptions(string TableName);
/// <summary>One Sales-local transaction records the immutable order, idempotency claim, process, and start outbox.</summary>
public sealed class DynamoDbSalesOrderStore(IAmazonDynamoDB client, DynamoDbSalesOptions options) : ISalesOrderStore
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
    private static SalesOrder Read(Dictionary<string, AttributeValue> item) => new(new(item["OrderId"].S), new(item["TenantId"].S), JsonSerializer.Deserialize<SalesOrderLine[]>(item["Lines"].S) ?? [], long.Parse(item["TotalVnd"].N, CultureInfo.InvariantCulture), JsonSerializer.Deserialize<GuestSnapshot>(item["Guest"].S)!, Enum.Parse<SalesOrderStatus>(item["Status"].S), long.Parse(item["Revision"].N, CultureInfo.InvariantCulture), new(item["ProcessId"].S, item["ExecutionIdentity"].S, item["StartPending"].BOOL ?? false), new HashSet<string>(JsonSerializer.Deserialize<string[]>(item["Sources"].S) ?? [], StringComparer.Ordinal));
    private static Dictionary<string, AttributeValue> OrderItem(SalesOrder order) => new() { ["PK"] = S(P(order.TenantId)), ["SK"] = S($"ORDER#{E(order.Id.Value)}"), ["OrderId"] = S(order.Id.Value), ["TenantId"] = S(order.TenantId.Value), ["Lines"] = S(JsonSerializer.Serialize(order.Lines)), ["TotalVnd"] = N(order.TotalVnd), ["Guest"] = S(JsonSerializer.Serialize(order.Guest)), ["Status"] = S(order.Status.ToString()), ["Revision"] = N(order.Revision), ["ProcessId"] = S(order.Process.Id), ["ExecutionIdentity"] = S(order.Process.WorkflowExecutionIdentity), ["StartPending"] = new() { BOOL = order.Process.StartPending }, ["Sources"] = S(JsonSerializer.Serialize(order.AcceptedSources)) };
    private static Dictionary<string, AttributeValue> ProcessItem(SalesOrder o) => new() { ["PK"] = S(P(o.TenantId)), ["SK"] = S($"PROCESS#{E(o.Process.Id)}"), ["OrderId"] = S(o.Id.Value), ["ExecutionIdentity"] = S(o.Process.WorkflowExecutionIdentity), ["Status"] = S("StartPending") };
    private static Dictionary<string, AttributeValue> OutboxItem(SalesOrder o, string correlationId) => new() { ["PK"] = S(P(o.TenantId)), ["SK"] = S($"OUTBOX#ORDERPLACED#{E(o.Id.Value)}"), ["EventId"] = S($"event-{o.Process.Id}"), ["EventType"] = S("OrderPlaced"), ["EventVersion"] = S("1"), ["AggregateId"] = S(o.Id.Value), ["CorrelationId"] = S(correlationId), ["CausationId"] = S(o.Id.Value), ["Status"] = S("Pending") };
    private static Dictionary<string, AttributeValue> Key(SalesTenantId tenantId, string sk) => new() { ["PK"] = S(P(tenantId)), ["SK"] = S(sk) };
    private static string P(SalesTenantId id) => $"TENANT#{E(id.Value)}"; private static string E(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static AttributeValue S(string value) => new() { S = value }; private static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
}
