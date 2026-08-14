using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.MockPaymentProvider.Application;
using CommerceOS.MockPaymentProvider.Domain;

namespace CommerceOS.MockPaymentProvider.Infrastructure.Persistence;

public sealed record DynamoDbProviderOptions(string TableName);
public sealed class DynamoDbProviderOperationStore(IAmazonDynamoDB client, DynamoDbProviderOptions options) : IProviderOperationStore
{
    public async Task<ProviderOperation?> GetAsync(string idempotencyKey, CancellationToken ct) { var result = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key($"OPERATION#{E(idempotencyKey)}") }, ct); return result.Item.Count == 0 ? null : Read(result.Item); }
    public async Task<ProviderPaymentIntent?> GetIntentAsync(string intentId, CancellationToken ct) { var result = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key($"INTENT#{E(intentId)}") }, ct); return result.Item.Count == 0 ? null : ReadIntent(result.Item); }
    public async Task<bool> PutAsync(ProviderOperation operation, CancellationToken ct)
    {
        var op = Item(operation); var intent = IntentItem(operation.Intent);
        try { await client.TransactWriteItemsAsync(new() { TransactItems = [new() { Put = new() { TableName = options.TableName, Item = op, ConditionExpression = "attribute_not_exists(PK)" } }, new() { Put = new() { TableName = options.TableName, Item = intent, ConditionExpression = "attribute_not_exists(PK)" } }] }, ct); return true; } catch (TransactionCanceledException) { return false; }
    }
    private static ProviderOperation Read(Dictionary<string, AttributeValue> x) => new(x["IdempotencyKey"].S, x["RequestFingerprint"].S, ReadIntent(x), x["CallerTimedOut"].BOOL is true);
    private static ProviderPaymentIntent ReadIntent(Dictionary<string, AttributeValue> x) => new(x["IntentId"].S, x["MerchantReference"].S, long.Parse(x["AmountVnd"].N, CultureInfo.InvariantCulture), Enum.Parse<ProviderPaymentStatus>(x["Status"].S), Enum.Parse<ProviderScenario>(x["Scenario"].S), long.Parse(x["Revision"].N, CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> Item(ProviderOperation x) { var item = IntentItem(x.Intent); item["PK"] = S("PROVIDER"); item["SK"] = S($"OPERATION#{E(x.IdempotencyKey)}"); item["IdempotencyKey"] = S(x.IdempotencyKey); item["RequestFingerprint"] = S(x.RequestFingerprint); item["CallerTimedOut"] = new() { BOOL = x.CallerTimedOut }; return item; }
    private static Dictionary<string, AttributeValue> IntentItem(ProviderPaymentIntent x) => new() { ["PK"] = S("PROVIDER"), ["SK"] = S($"INTENT#{E(x.Id)}"), ["IntentId"] = S(x.Id), ["MerchantReference"] = S(x.MerchantReference), ["AmountVnd"] = N(x.AmountVnd), ["Status"] = S(x.Status.ToString()), ["Scenario"] = S(x.Scenario.ToString()), ["Revision"] = N(x.Revision) };
    private static Dictionary<string, AttributeValue> Key(string sk) => new() { ["PK"] = S("PROVIDER"), ["SK"] = S(sk) }; private static string E(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static AttributeValue S(string value) => new() { S = value }; private static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
}
