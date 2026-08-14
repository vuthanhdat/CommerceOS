using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.SubscriptionBilling.Application.Trial;

namespace CommerceOS.SubscriptionBilling.Infrastructure.Persistence;

public sealed class DynamoDbTrialSubscriptionStore : ITrialSubscriptionStore
{
    private const string PartitionKey = "PK";
    private const string SortKey = "SK";
    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbSubscriptionBillingOptions _options;

    public DynamoDbTrialSubscriptionStore(IAmazonDynamoDB client, DynamoDbSubscriptionBillingOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<TrialSubscription?> GetForOnboardingAsync(
        string tenantId,
        string onboardingOperationId,
        CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            Key = Key(TenantPartition(tenantId), SubscriptionKey(onboardingOperationId))
        }, cancellationToken);
        return response.Item is null || response.Item.Count == 0 ? null : Read(response.Item);
    }

    public async Task<bool> CreateIfAbsentAsync(TrialSubscription subscription, CancellationToken cancellationToken)
    {
        try
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = _options.TableName,
                Item = Write(subscription),
                ConditionExpression = "attribute_not_exists(PK)"
            }, cancellationToken);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    public async Task<TrialSubscription?> GetCurrentForTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            KeyConditionExpression = "#pk = :pk AND begins_with(#sk, :prefix)",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = PartitionKey, ["#sk"] = SortKey },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = String(TenantPartition(tenantId)),
                [":prefix"] = String("SUBSCRIPTION#TRIAL#")
            }
        }, cancellationToken);
        return response.Items.Count == 0 ? null : Read(response.Items[0]);
    }

    private static TrialSubscription Read(Dictionary<string, AttributeValue> item) => new(
        item["TenantId"].S,
        item["OnboardingOperationId"].S,
        item["SourceIdentity"].S,
        new TrialEntitlementSnapshot(
            item["TrialTermsVersionId"].S,
            int.Parse(item["DurationDays"].N, CultureInfo.InvariantCulture),
            item["CoreCommerceCapabilities"].BOOL ?? false,
            int.Parse(item["MaxActiveMemberships"].N, CultureInfo.InvariantCulture),
            int.Parse(item["MaxWarehouses"].N, CultureInfo.InvariantCulture),
            item["ScheduledProductIngestion"].BOOL ?? false,
            int.Parse(item["OrderVolumeWarningThreshold"].N, CultureInfo.InvariantCulture)),
        DateTimeOffset.Parse(item["EffectiveFrom"].S, CultureInfo.InvariantCulture),
        DateTimeOffset.Parse(item["EffectiveUntil"].S, CultureInfo.InvariantCulture),
        Enum.Parse<SubscriptionCondition>(item["Condition"].S, false));

    private static Dictionary<string, AttributeValue> Write(TrialSubscription subscription) => new()
    {
        [PartitionKey] = String(TenantPartition(subscription.TenantId)),
        [SortKey] = String(SubscriptionKey(subscription.OnboardingOperationId)),
        ["Kind"] = String("TrialSubscription"),
        ["TenantId"] = String(subscription.TenantId),
        ["OnboardingOperationId"] = String(subscription.OnboardingOperationId),
        ["SourceIdentity"] = String(subscription.SourceIdentity),
        ["TrialTermsVersionId"] = String(subscription.Entitlements.TrialTermsVersionId),
        ["DurationDays"] = Number(subscription.Entitlements.DurationDays),
        ["CoreCommerceCapabilities"] = Bool(subscription.Entitlements.CoreCommerceCapabilities),
        ["MaxActiveMemberships"] = Number(subscription.Entitlements.MaxActiveMemberships),
        ["MaxWarehouses"] = Number(subscription.Entitlements.MaxWarehouses),
        ["ScheduledProductIngestion"] = Bool(subscription.Entitlements.ScheduledProductIngestion),
        ["OrderVolumeWarningThreshold"] = Number(subscription.Entitlements.OrderVolumeWarningThreshold)
        ,
        ["EffectiveFrom"] = String(subscription.EffectiveFrom.ToString("O", CultureInfo.InvariantCulture))
        ,
        ["EffectiveUntil"] = String(subscription.EffectiveUntil.ToString("O", CultureInfo.InvariantCulture))
        ,
        ["Condition"] = String(subscription.Condition.ToString())
    };

    private static Dictionary<string, AttributeValue> Key(string partition, string sort) => new()
    {
        [PartitionKey] = String(partition),
        [SortKey] = String(sort)
    };

    private static string TenantPartition(string tenantId) => $"TENANT#{tenantId}";

    private static string SubscriptionKey(string operationId) => $"SUBSCRIPTION#TRIAL#ONBOARDING#{operationId}";

    private static AttributeValue String(string value) => new() { S = value };

    private static AttributeValue Number(int value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    private static AttributeValue Bool(bool value) => new() { BOOL = value };
}
