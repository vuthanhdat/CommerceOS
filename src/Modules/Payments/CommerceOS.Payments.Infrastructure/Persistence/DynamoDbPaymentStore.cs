using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Payments.Application;
using CommerceOS.Payments.Domain;

namespace CommerceOS.Payments.Infrastructure.Persistence;

public sealed record DynamoDbPaymentsOptions(string TableName);
public sealed class DynamoDbPaymentStore(IAmazonDynamoDB client, DynamoDbPaymentsOptions options) : IPaymentStore
{
    public async Task<PaymentObligation?> GetAsync(TrustedPaymentContext context, string orderId, CancellationToken cancellationToken) { var item = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, orderId) }, cancellationToken); return item.Item.Count == 0 ? null : Read(item.Item); }
    public async Task<PaymentStoreOutcome> CreateAsync(TrustedPaymentContext context, PaymentObligation obligation, CancellationToken cancellationToken) { try { await client.PutItemAsync(new() { TableName = options.TableName, Item = Item(obligation), ConditionExpression = "attribute_not_exists(PK)" }, cancellationToken); return PaymentStoreOutcome.Applied; } catch (ConditionalCheckFailedException) { return PaymentStoreOutcome.Conflict; } }
    public async Task<PaymentStoreOutcome> SaveAsync(TrustedPaymentContext context, PaymentObligation before, PaymentObligation after, string evidenceId, CancellationToken cancellationToken)
    {
        try { await client.TransactWriteItemsAsync(new() { TransactItems = [new() { Put = new() { TableName = options.TableName, Item = Item(after), ConditionExpression = "Revision = :revision", ExpressionAttributeValues = new() { [":revision"] = N(before.Revision) } } }, new() { Put = new() { TableName = options.TableName, Item = new() { ["PK"] = S(P(context.TenantId)), ["SK"] = S($"EVIDENCE#{E(evidenceId)}"), ["OrderId"] = S(after.OrderId) }, ConditionExpression = "attribute_not_exists(PK)" } }] }, cancellationToken); return PaymentStoreOutcome.Applied; } catch (TransactionCanceledException) { return PaymentStoreOutcome.Conflict; }
    }
    private static PaymentObligation Read(Dictionary<string, AttributeValue> item) => new(item["OrderId"].S, item["TenantId"].S, long.Parse(item["AmountVnd"].N, CultureInfo.InvariantCulture), item["Currency"].S, JsonSerializer.Deserialize<PaymentAttempt[]>(item["Attempts"].S) ?? [], long.Parse(item["Revision"].N, CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> Item(PaymentObligation payment) => new() { ["PK"] = S(P(payment.TenantId)), ["SK"] = S($"PAYMENT#{E(payment.OrderId)}"), ["OrderId"] = S(payment.OrderId), ["TenantId"] = S(payment.TenantId), ["AmountVnd"] = N(payment.AmountVnd), ["Currency"] = S(payment.Currency), ["Attempts"] = S(JsonSerializer.Serialize(payment.Attempts)), ["Revision"] = N(payment.Revision) };
    private static Dictionary<string, AttributeValue> Key(string tenantId, string orderId) => new() { ["PK"] = S(P(tenantId)), ["SK"] = S($"PAYMENT#{E(orderId)}") }; private static string P(string value) => $"TENANT#{E(value)}"; private static string E(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static AttributeValue S(string value) => new() { S = value }; private static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
}
