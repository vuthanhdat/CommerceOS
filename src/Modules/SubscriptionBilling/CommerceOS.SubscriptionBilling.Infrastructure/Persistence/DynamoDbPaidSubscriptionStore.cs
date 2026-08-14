using System.Globalization;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.SubscriptionBilling.Application.PaidLifecycle;
using CommerceOS.SubscriptionBilling.Application.Trial;

namespace CommerceOS.SubscriptionBilling.Infrastructure.Persistence;

public sealed class DynamoDbPaidSubscriptionStore : IPaidSubscriptionStore
{
    private const string PartitionKey = "PK";
    private const string SortKey = "SK";
    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbSubscriptionBillingOptions _options;

    public DynamoDbPaidSubscriptionStore(IAmazonDynamoDB client, DynamoDbSubscriptionBillingOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<PaidSubscription?> GetCurrentAsync(string tenantId, CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            Key = Key(TenantPartition(tenantId), CurrentKey)
        }, cancellationToken);
        return response.Item is null || response.Item.Count == 0 ? null : Read(response.Item);
    }

    public async Task<PaidLifecycleOutcome> ApplyPeriodAsync(PaidSubscriptionTransition transition, CancellationToken cancellationToken)
    {
        var current = await GetCurrentAsync(transition.Subscription.TenantId, cancellationToken);
        var operationKey = OperationKey(transition.OperationId);
        var existing = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            Key = Key(TenantPartition(transition.Subscription.TenantId), operationKey)
        }, cancellationToken);
        if (existing.Item is not null && existing.Item.Count != 0)
        {
            return PaidLifecycleOutcome.AlreadyApplied;
        }
        if (current is not null && current.Revision != transition.Subscription.Revision - 1)
        {
            return PaidLifecycleOutcome.RevisionConflict;
        }

        var transaction = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _options.TableName,
                        Item = Write(transition.Subscription, CurrentKey),
                        ConditionExpression = current is null ? "attribute_not_exists(PK)" : "Revision = :expectedRevision",
                        ExpressionAttributeValues = current is null ? null : new Dictionary<string, AttributeValue> { [":expectedRevision"] = Number(current.Revision) }
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _options.TableName,
                        Item = HistoryItem(transition, TenantPartition(transition.Subscription.TenantId), operationKey),
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                }
            ]
        };
        try
        {
            await _client.TransactWriteItemsAsync(transaction, cancellationToken);
            return PaidLifecycleOutcome.Applied;
        }
        catch (TransactionCanceledException)
        {
            return await HasOperationAsync(transition.Subscription.TenantId, operationKey, cancellationToken)
                ? PaidLifecycleOutcome.AlreadyApplied
                : PaidLifecycleOutcome.RevisionConflict;
        }
    }

    public async Task<PaidLifecycleOutcome> ScheduleDowngradeAsync(string tenantId, long expectedRevision, PendingDowngrade downgrade, CancellationToken cancellationToken)
    {
        var current = await GetCurrentAsync(tenantId, cancellationToken);
        if (current is null || current.Revision != expectedRevision) return PaidLifecycleOutcome.RevisionConflict;
        var updated = current with { PendingDowngrade = downgrade, Revision = current.Revision + 1 };
        return await ApplyPeriodAsync(new PaidSubscriptionTransition(downgrade.OperationId, PaidLifecycleOperation.Renewal, updated), cancellationToken);
    }

    public async Task<PaidLifecycleOutcome> MarkPastDueAsync(string tenantId, long expectedRevision, DateTimeOffset graceEndsAt, CancellationToken cancellationToken)
    {
        var current = await GetCurrentAsync(tenantId, cancellationToken);
        if (current is null || current.Revision != expectedRevision) return PaidLifecycleOutcome.RevisionConflict;
        if (current.Condition is SubscriptionCondition.PastDue) return PaidLifecycleOutcome.AlreadyApplied;
        var updated = current with { Condition = SubscriptionCondition.PastDue, EffectiveUntil = graceEndsAt, Revision = current.Revision + 1 };
        return await ApplyPeriodAsync(new PaidSubscriptionTransition($"past-due:{tenantId}:{expectedRevision}", PaidLifecycleOperation.Renewal, updated), cancellationToken);
    }

    public async Task<PaidLifecycleOutcome> MarkEndedAsync(string tenantId, long expectedRevision, CancellationToken cancellationToken)
    {
        var current = await GetCurrentAsync(tenantId, cancellationToken);
        if (current is null || current.Revision != expectedRevision) return PaidLifecycleOutcome.RevisionConflict;
        if (current.Condition is SubscriptionCondition.Ended) return PaidLifecycleOutcome.AlreadyApplied;
        var updated = current with { Condition = SubscriptionCondition.Ended, Revision = current.Revision + 1 };
        return await ApplyPeriodAsync(new PaidSubscriptionTransition($"ended:{tenantId}:{expectedRevision}", PaidLifecycleOperation.Renewal, updated), cancellationToken);
    }

    private async Task<bool> HasOperationAsync(string tenantId, string operationKey, CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest { TableName = _options.TableName, ConsistentRead = true, Key = Key(TenantPartition(tenantId), operationKey) }, cancellationToken);
        return response.Item is not null && response.Item.Count != 0;
    }

    private static PaidSubscription Read(Dictionary<string, AttributeValue> item)
    {
        var downgrade = item.TryGetValue("DowngradePlanId", out var planId) ? new PendingDowngrade(
            new PaidEntitlementSnapshot(planId.S, item["DowngradePlanVersionId"].S, Bool(item["DowngradeCoreCommerceCapabilities"]), Int(item, "DowngradeMaxActiveMemberships"), Int(item, "DowngradeMaxWarehouses"), Bool(item["DowngradeScheduledProductIngestion"]), Int(item, "DowngradeOrderVolumeWarningThreshold")),
            Enum.Parse<DowngradeStatus>(item["DowngradeStatus"].S, false), item["DowngradeOperationId"].S) : null;
        return new PaidSubscription(item["TenantId"].S, item["SubscriptionId"].S,
            new PaidEntitlementSnapshot(item["PlanId"].S, item["PlanVersionId"].S, Bool(item["CoreCommerceCapabilities"]), Int(item, "MaxActiveMemberships"), Int(item, "MaxWarehouses"), Bool(item["ScheduledProductIngestion"]), Int(item, "OrderVolumeWarningThreshold")),
            Date(item, "BillingAnchor"), Date(item, "EffectiveFrom"), Date(item, "EffectiveUntil"), Enum.Parse<SubscriptionCondition>(item["Condition"].S, false), Bool(item["CancelRenewalRequested"]), downgrade, Long(item, "Revision"));
    }

    private static Dictionary<string, AttributeValue> Write(PaidSubscription subscription, string sortKey)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [PartitionKey] = String(TenantPartition(subscription.TenantId)),
            [SortKey] = String(sortKey),
            ["Kind"] = String("PaidSubscription"),
            ["TenantId"] = String(subscription.TenantId),
            ["SubscriptionId"] = String(subscription.SubscriptionId),
            ["PlanId"] = String(subscription.Entitlements.PlanId),
            ["PlanVersionId"] = String(subscription.Entitlements.PlanVersionId),
            ["CoreCommerceCapabilities"] = BoolValue(subscription.Entitlements.CoreCommerceCapabilities),
            ["MaxActiveMemberships"] = Number(subscription.Entitlements.MaxActiveMemberships),
            ["MaxWarehouses"] = Number(subscription.Entitlements.MaxWarehouses),
            ["ScheduledProductIngestion"] = BoolValue(subscription.Entitlements.ScheduledProductIngestion),
            ["OrderVolumeWarningThreshold"] = Number(subscription.Entitlements.OrderVolumeWarningThreshold),
            ["BillingAnchor"] = String(subscription.BillingAnchor.ToString("O", CultureInfo.InvariantCulture)),
            ["EffectiveFrom"] = String(subscription.EffectiveFrom.ToString("O", CultureInfo.InvariantCulture)),
            ["EffectiveUntil"] = String(subscription.EffectiveUntil.ToString("O", CultureInfo.InvariantCulture)),
            ["Condition"] = String(subscription.Condition.ToString()),
            ["CancelRenewalRequested"] = BoolValue(subscription.CancelRenewalRequested),
            ["Revision"] = Number(subscription.Revision)
        };
        if (subscription.PendingDowngrade is { } downgrade)
        {
            item["DowngradePlanId"] = String(downgrade.Target.PlanId); item["DowngradePlanVersionId"] = String(downgrade.Target.PlanVersionId); item["DowngradeCoreCommerceCapabilities"] = BoolValue(downgrade.Target.CoreCommerceCapabilities);
            item["DowngradeMaxActiveMemberships"] = Number(downgrade.Target.MaxActiveMemberships); item["DowngradeMaxWarehouses"] = Number(downgrade.Target.MaxWarehouses); item["DowngradeScheduledProductIngestion"] = BoolValue(downgrade.Target.ScheduledProductIngestion);
            item["DowngradeOrderVolumeWarningThreshold"] = Number(downgrade.Target.OrderVolumeWarningThreshold); item["DowngradeStatus"] = String(downgrade.Status.ToString()); item["DowngradeOperationId"] = String(downgrade.OperationId);
        }
        return item;
    }

    private static Dictionary<string, AttributeValue> HistoryItem(PaidSubscriptionTransition transition, string partition, string sort) => new()
    {
        [PartitionKey] = String(partition),
        [SortKey] = String(sort),
        ["Kind"] = String("PaidSubscriptionHistory"),
        ["OperationId"] = String(transition.OperationId),
        ["Operation"] = String(transition.Operation.ToString()),
        ["SubscriptionId"] = String(transition.Subscription.SubscriptionId),
        ["PlanVersionId"] = String(transition.Subscription.Entitlements.PlanVersionId),
        ["Revision"] = Number(transition.Subscription.Revision),
        ["OccurredAt"] = String(transition.Subscription.EffectiveFrom.ToString("O", CultureInfo.InvariantCulture))
    };

    private const string CurrentKey = "PAIDSUBSCRIPTION#CURRENT";
    private static string OperationKey(string operationId) => $"PAIDSUBSCRIPTION#OPERATION#{Encode(operationId)}";
    private static string TenantPartition(string tenantId) => $"TENANT#{Encode(tenantId)}";
    private static Dictionary<string, AttributeValue> Key(string partition, string sort) => new() { [PartitionKey] = String(partition), [SortKey] = String(sort) };
    private static AttributeValue String(string value) => new() { S = value };
    private static AttributeValue Number(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
    private static AttributeValue BoolValue(bool value) => new() { BOOL = value };
    private static bool Bool(AttributeValue value) => value.BOOL ?? throw new InvalidOperationException("Required boolean value was missing.");
    private static long Long(Dictionary<string, AttributeValue> item, string name) => long.Parse(item[name].N, CultureInfo.InvariantCulture);
    private static int Int(Dictionary<string, AttributeValue> item, string name) => int.Parse(item[name].N, CultureInfo.InvariantCulture);
    private static DateTimeOffset Date(Dictionary<string, AttributeValue> item, string name) => DateTimeOffset.Parse(item[name].S, CultureInfo.InvariantCulture);
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
