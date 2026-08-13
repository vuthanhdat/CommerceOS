using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.SubscriptionBilling.Application.Catalog;
using CommerceOS.SubscriptionBilling.Domain;

namespace CommerceOS.SubscriptionBilling.Infrastructure.Persistence;

public sealed record DynamoDbSubscriptionBillingOptions(string TableName);

public sealed class DynamoDbSubscriptionCatalogStore : ISubscriptionCatalogStore
{
    private const string PartitionKey = "PK";
    private const string SortKey = "SK";
    private const string CatalogPartition = "CATALOG";
    private const string PlanVersionPrefix = "PLANVERSION#";
    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbSubscriptionBillingOptions _options;

    public DynamoDbSubscriptionCatalogStore(IAmazonDynamoDB client, DynamoDbSubscriptionBillingOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<CatalogRecord?> GetAsync(CatalogRecordId id, CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            Key = Key(CatalogPartition, id.Value)
        }, cancellationToken);

        return response.Item is null || response.Item.Count == 0 ? null : ReadRecord(response.Item);
    }

    public async Task<CatalogRecordCreateResult> CreateIfAbsentAsync(CatalogRecord record, CancellationToken cancellationToken)
    {
        try
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = _options.TableName,
                Item = WriteRecord(record),
                ConditionExpression = "attribute_not_exists(PK)"
            }, cancellationToken);
            return CatalogRecordCreateResult.Created;
        }
        catch (ConditionalCheckFailedException)
        {
            return CatalogRecordCreateResult.AlreadyExists;
        }
    }

    public async Task<IReadOnlyList<PlanVersion>> ListAvailablePlanVersionsAsync(CancellationToken cancellationToken)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            KeyConditionExpression = "#pk = :pk AND begins_with(#sk, :prefix)",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#pk"] = PartitionKey,
                ["#sk"] = SortKey
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = String(CatalogPartition),
                [":prefix"] = String(PlanVersionPrefix)
            }
        }, cancellationToken);

        return response.Items
            .Select(ReadRecord)
            .Select(record => record.PlanVersion!)
            .Where(planVersion => planVersion.IsAvailableForNewPurchase)
            .OrderBy(planVersion => planVersion.PlanId.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static CatalogRecord ReadRecord(Dictionary<string, AttributeValue> item)
    {
        var record = item["Kind"].S switch
        {
            "PlanVersion" => CatalogRecord.For(new PlanVersion(
                new PlanId(item["PlanId"].S),
                new PlanVersionId(item["PlanVersionId"].S),
                new VndMoney(long.Parse(item["MonthlyPriceVnd"].N, CultureInfo.InvariantCulture)),
                ReadEntitlements(item),
                Bool(item["IsAvailableForNewPurchase"]))),
            "TrialTermsVersion" => CatalogRecord.For(new TrialTermsVersion(
                new TrialTermsVersionId(item["TrialTermsVersionId"].S),
                int.Parse(item["DurationDays"].N, CultureInfo.InvariantCulture),
                ReadEntitlements(item))),
            _ => throw new InvalidOperationException("Unknown SubscriptionBilling catalog record kind.")
        };

        if (record.Id.Value != item[SortKey].S)
        {
            throw new InvalidOperationException("SubscriptionBilling catalog record identity does not match its contents.");
        }

        return record;
    }

    private static Dictionary<string, AttributeValue> WriteRecord(CatalogRecord record)
    {
        var item = Key(CatalogPartition, record.Id.Value);
        if (record.PlanVersion is { } planVersion)
        {
            item["Kind"] = String("PlanVersion");
            item["PlanId"] = String(planVersion.PlanId.Value);
            item["PlanVersionId"] = String(planVersion.Id.Value);
            item["MonthlyPriceVnd"] = Number(planVersion.MonthlyPrice.Amount);
            item["IsAvailableForNewPurchase"] = new AttributeValue { BOOL = planVersion.IsAvailableForNewPurchase };
            WriteEntitlements(item, planVersion.Entitlements);
            return item;
        }

        var trialTermsVersion = record.TrialTermsVersion!;
        item["Kind"] = String("TrialTermsVersion");
        item["TrialTermsVersionId"] = String(trialTermsVersion.Id.Value);
        item["DurationDays"] = Number(trialTermsVersion.DurationDays);
        WriteEntitlements(item, trialTermsVersion.Entitlements);
        return item;
    }

    private static EntitlementTerms ReadEntitlements(Dictionary<string, AttributeValue> item) => new(
        Bool(item["CoreCommerceCapabilities"]),
        int.Parse(item["MaxActiveMemberships"].N, CultureInfo.InvariantCulture),
        int.Parse(item["MaxWarehouses"].N, CultureInfo.InvariantCulture),
        Bool(item["ScheduledProductIngestion"]),
        int.Parse(item["OrderVolumeWarningThreshold"].N, CultureInfo.InvariantCulture));

    private static void WriteEntitlements(Dictionary<string, AttributeValue> item, EntitlementTerms entitlements)
    {
        item["CoreCommerceCapabilities"] = new AttributeValue { BOOL = entitlements.CoreCommerceCapabilities };
        item["MaxActiveMemberships"] = Number(entitlements.MaxActiveMemberships);
        item["MaxWarehouses"] = Number(entitlements.MaxWarehouses);
        item["ScheduledProductIngestion"] = new AttributeValue { BOOL = entitlements.ScheduledProductIngestion };
        item["OrderVolumeWarningThreshold"] = Number(entitlements.OrderVolumeWarningThreshold);
    }

    private static Dictionary<string, AttributeValue> Key(string partitionKey, string sortKey) => new()
    {
        [PartitionKey] = String(partitionKey),
        [SortKey] = String(sortKey)
    };

    private static AttributeValue String(string value) => new() { S = value };

    private static AttributeValue Number(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    private static bool Bool(AttributeValue attributeValue) => attributeValue.BOOL
        ?? throw new InvalidOperationException("SubscriptionBilling catalog boolean attribute is missing.");

}
