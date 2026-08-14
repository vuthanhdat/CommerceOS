using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.SubscriptionBilling.Application.Usage;

namespace CommerceOS.SubscriptionBilling.Infrastructure.Persistence;

/// <summary>Atomically claims one producer event and increments only its effective billing-period meter.</summary>
public sealed class DynamoDbOrderVolumeUsageStore(IAmazonDynamoDB client, DynamoDbSubscriptionBillingOptions options) : IOrderVolumeUsageStore
{
    public async Task<OrderVolumeUsageOutcome> ApplyAsync(OrderConfirmedUsageFact fact, OrderVolumeUsage usage, CancellationToken cancellationToken)
    {
        var pk = $"TENANT#{E(fact.TenantId)}";
        var meterKey = $"USAGE#ORDERVOLUME#{usage.PeriodFrom.UtcTicks:D20}";
        try
        {
            await client.TransactWriteItemsAsync(new()
            {
                TransactItems =
                [
                    new() { Put = new() { TableName = options.TableName, Item = new() { ["PK"] = S(pk), ["SK"] = S($"USAGESOURCE#{E(fact.EventId)}"), ["OrderId"] = S(fact.OrderId), ["MeterKey"] = S(meterKey), ["OccurredAt"] = S(fact.OccurredAt.ToString("O", CultureInfo.InvariantCulture)) }, ConditionExpression = "attribute_not_exists(PK)" } },
                    new() { Update = new() { TableName = options.TableName, Key = new() { ["PK"] = S(pk), ["SK"] = S(meterKey) }, UpdateExpression = "ADD #count :one SET Threshold = :threshold, PeriodFrom = :from, PeriodUntil = :until, SourceVersion = :version", ExpressionAttributeNames = new() { ["#count"] = "Count" }, ExpressionAttributeValues = new() { [":one"] = N(1), [":threshold"] = N(usage.Threshold), [":from"] = S(usage.PeriodFrom.ToString("O", CultureInfo.InvariantCulture)), [":until"] = S(usage.PeriodUntil.ToString("O", CultureInfo.InvariantCulture)), [":version"] = S(usage.PeriodSourceVersion) } } }
                ]
            }, cancellationToken);
            return OrderVolumeUsageOutcome.Applied;
        }
        catch (TransactionCanceledException) { return OrderVolumeUsageOutcome.AlreadyApplied; }
    }

    public async Task<OrderVolumeUsage?> GetAsync(string trustedTenantId, DateTimeOffset evaluatedAt, CancellationToken cancellationToken)
    {
        var result = await client.QueryAsync(new() { TableName = options.TableName, KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)", ExpressionAttributeValues = new() { [":pk"] = S($"TENANT#{E(trustedTenantId)}"), [":prefix"] = S("USAGE#ORDERVOLUME#") } }, cancellationToken);
        return result.Items.Select(Read).Where(x => x.PeriodFrom <= evaluatedAt && evaluatedAt < x.PeriodUntil).OrderByDescending(x => x.PeriodFrom).FirstOrDefault();
    }

    private static OrderVolumeUsage Read(Dictionary<string, AttributeValue> item)
    {
        var count = int.Parse(item["Count"].N, CultureInfo.InvariantCulture); var threshold = int.Parse(item["Threshold"].N, CultureInfo.InvariantCulture);
        return new(item["PK"].S[7..], item["SourceVersion"].S, DateTimeOffset.Parse(item["PeriodFrom"].S, CultureInfo.InvariantCulture), DateTimeOffset.Parse(item["PeriodUntil"].S, CultureInfo.InvariantCulture), count, threshold, count >= threshold);
    }
    private static string E(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static AttributeValue S(string value) => new() { S = value }; private static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
}
