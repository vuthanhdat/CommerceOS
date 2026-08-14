using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Payments.Application;

namespace CommerceOS.Payments.Infrastructure.Persistence;

/// <summary>Payments-owned refund ledger; one conditional record per tenant/payment preserves provider-operation identity across retries.</summary>
public sealed class DynamoDbApprovedRefundStore(IAmazonDynamoDB client, DynamoDbPaymentsOptions options) : IApprovedRefundStore
{
    public async Task<PaymentRefundLedger?> GetAsync(string trustedTenantId, string paymentId, CancellationToken cancellationToken)
    {
        var result = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(trustedTenantId, paymentId) }, cancellationToken);
        return result.Item.Count == 0 ? null : new(result.Item["TenantId"].S, result.Item["PaymentId"].S, long.Parse(result.Item["CapturedAmountVnd"].N, CultureInfo.InvariantCulture), JsonSerializer.Deserialize<PaymentRefundOperation[]>(result.Item["Operations"].S) ?? []);
    }
    public async Task<bool> SaveAsync(PaymentRefundLedger ledger, CancellationToken cancellationToken)
    {
        var prior = await GetAsync(ledger.TenantId, ledger.PaymentId, cancellationToken);
        var item = new Dictionary<string, AttributeValue> { ["PK"] = S($"TENANT#{E(ledger.TenantId)}"), ["SK"] = S($"REFUND-LEDGER#{E(ledger.PaymentId)}"), ["TenantId"] = S(ledger.TenantId), ["PaymentId"] = S(ledger.PaymentId), ["CapturedAmountVnd"] = N(ledger.CapturedAmountVnd), ["Operations"] = S(JsonSerializer.Serialize(ledger.Operations)), ["Revision"] = N((prior?.Operations.Count ?? 0) + 1) };
        try { await client.PutItemAsync(new() { TableName = options.TableName, Item = item, ConditionExpression = prior is null ? "attribute_not_exists(PK)" : "Revision = :revision", ExpressionAttributeValues = prior is null ? null : new() { [":revision"] = N(prior.Operations.Count) } }, cancellationToken); return true; }
        catch (ConditionalCheckFailedException) { return false; }
    }
    private static Dictionary<string, AttributeValue> Key(string tenant, string payment) => new() { ["PK"] = S($"TENANT#{E(tenant)}"), ["SK"] = S($"REFUND-LEDGER#{E(payment)}") };
    private static string E(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static AttributeValue S(string value) => new() { S = value };
    private static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
}
