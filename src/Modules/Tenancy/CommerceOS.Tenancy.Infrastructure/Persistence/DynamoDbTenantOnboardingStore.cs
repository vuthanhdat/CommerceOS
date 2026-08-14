using System.Globalization;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Tenancy.Application.Onboarding;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.Infrastructure.Persistence;

/// <summary>
/// Implements ADR-009's Tenancy-local transaction. Subscription state is never
/// written here; only the durable command work source is recorded for recovery.
/// </summary>
public sealed class DynamoDbTenantOnboardingStore : ITenantOnboardingStore
{
    private const string PartitionKey = "PK";
    private const string SortKey = "SK";
    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbTenancyOptions _options;

    public DynamoDbTenantOnboardingStore(IAmazonDynamoDB client, DynamoDbTenancyOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<LocalOnboardingRegistrationResult> RegisterAsync(
        OnboardingOperation operation,
        TrialBootstrapWorkItem workItem,
        CancellationToken cancellationToken)
    {
        var existing = await GetAsync(
            TrustedOnboardingContext.FromVerifiedIdentity(operation.SubjectId, "stored@example.invalid"),
            operation.IdempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            return ExistingResult(existing, operation, workItem);
        }

        var operationKey = OperationKey(operation.SubjectId, operation.IdempotencyKey);
        var tenantPartition = TenantPartition(operation.Tenant.Id);
        var discoveryPartition = $"SUBJECT#{Encode(operation.SubjectId.Value)}";
        var operationLocator = LocatorKey(operation.Id);
        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    Put(OperationItem(operation, operationKey), "attribute_not_exists(PK)"),
                    Put(LocatorItem(operation, operationLocator), "attribute_not_exists(PK)"),
                    Put(TenantItem(operation.Tenant, tenantPartition), "attribute_not_exists(PK)"),
                    Put(MembershipItem(operation.InitialOwner, tenantPartition), "attribute_not_exists(PK)"),
                    Put(AuthorityItem(operation.InitialOwner, tenantPartition), "attribute_not_exists(PK)"),
                    Put(DiscoveryItem(operation.InitialOwner, discoveryPartition), "attribute_not_exists(PK)"),
                    Put(OwnerGuardItem(operation, tenantPartition), "attribute_not_exists(PK)"),
                    Put(MembershipCountGuardItem(operation, tenantPartition), "attribute_not_exists(PK)"),
                    Put(WorkItem(workItem, operationKey), "attribute_not_exists(PK)")
                ]
            }, cancellationToken);
            return new LocalOnboardingRegistrationResult(LocalOnboardingRegistrationOutcome.Created, operation, workItem);
        }
        catch (TransactionCanceledException)
        {
            existing = await GetAsync(
                TrustedOnboardingContext.FromVerifiedIdentity(operation.SubjectId, "stored@example.invalid"),
                operation.IdempotencyKey,
                cancellationToken);
            return existing is null
                ? new LocalOnboardingRegistrationResult(LocalOnboardingRegistrationOutcome.Conflict, null, null)
                : ExistingResult(existing, operation, workItem);
        }
    }

    public async Task<OnboardingOperation?> GetAsync(
        TrustedOnboardingContext context,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            Key = Key(OperationKey(context.SubjectId, idempotencyKey), "OPERATION")
        }, cancellationToken);
        return response.Item is null || response.Item.Count == 0 ? null : ReadOperation(response.Item);
    }

    public async Task<OnboardingOperation?> GetByOperationIdAsync(string operationId, CancellationToken cancellationToken)
    {
        var locator = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            Key = Key(LocatorKey(operationId), "LOCATOR")
        }, cancellationToken);
        if (locator.Item is null || locator.Item.Count == 0)
        {
            return null;
        }
        var subject = new SubjectId(locator.Item["SubjectId"].S);
        return await GetAsync(
            TrustedOnboardingContext.FromVerifiedIdentity(subject, "stored@example.invalid"),
            locator.Item["IdempotencyKey"].S,
            cancellationToken);
    }

    public async Task<bool> MarkCompletedAsync(string operationId, CancellationToken cancellationToken)
    {
        var operation = await GetByOperationIdAsync(operationId, cancellationToken);
        if (operation is null || operation.Status is OnboardingStatus.Completed)
        {
            return false;
        }
        var operationKey = OperationKey(operation.SubjectId, operation.IdempotencyKey);
        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem
                    {
                        Update = new Update
                        {
                            TableName = _options.TableName,
                            Key = Key(operationKey, "OPERATION"),
                            UpdateExpression = "SET #status = :completed",
                            ConditionExpression = "#status = :pending",
                            ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                [":pending"] = String(OnboardingStatus.PendingTrial.ToString()),
                                [":completed"] = String(OnboardingStatus.Completed.ToString())
                            }
                        }
                    },
                    new TransactWriteItem
                    {
                        Update = new Update
                        {
                            TableName = _options.TableName,
                            Key = Key(operationKey, $"WORKOUTBOX#{Encode(operation.Id)}"),
                            UpdateExpression = "SET #status = :completed",
                            ConditionExpression = "attribute_exists(PK)",
                            ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                [":completed"] = String(OnboardingStatus.Completed.ToString())
                            }
                        }
                    }
                ]
            }, cancellationToken);
            return true;
        }
        catch (TransactionCanceledException)
        {
            return false;
        }
    }

    private static LocalOnboardingRegistrationResult ExistingResult(
        OnboardingOperation existing,
        OnboardingOperation requested,
        TrialBootstrapWorkItem workItem) =>
        existing.RequestFingerprint == requested.RequestFingerprint
            ? new LocalOnboardingRegistrationResult(LocalOnboardingRegistrationOutcome.Replayed, existing, workItem)
            : new LocalOnboardingRegistrationResult(LocalOnboardingRegistrationOutcome.Conflict, null, null);

    private static Dictionary<string, AttributeValue> OperationItem(OnboardingOperation operation, string operationKey) => new()
    {
        [PartitionKey] = String(operationKey),
        [SortKey] = String("OPERATION"),
        ["OperationId"] = String(operation.Id),
        ["SubjectId"] = String(operation.SubjectId.Value),
        ["IdempotencyKey"] = String(operation.IdempotencyKey),
        ["RequestFingerprint"] = String(operation.RequestFingerprint),
        ["TenantId"] = String(operation.Tenant.Id.Value),
        ["MembershipId"] = String(operation.InitialOwner.Id.Value),
        ["DisplayName"] = String(operation.Tenant.Profile.DisplayName),
        ["TimeZoneIana"] = String(operation.Tenant.Profile.TimeZoneIana),
        ["CorrelationId"] = String(operation.CorrelationId),
        ["Status"] = String(operation.Status.ToString())
    };

    private static Dictionary<string, AttributeValue> LocatorItem(OnboardingOperation operation, string locatorKey) => new()
    {
        [PartitionKey] = String(locatorKey),
        [SortKey] = String("LOCATOR"),
        ["SubjectId"] = String(operation.SubjectId.Value),
        ["IdempotencyKey"] = String(operation.IdempotencyKey)
    };

    private static Dictionary<string, AttributeValue> TenantItem(Tenant tenant, string partition) => new()
    {
        [PartitionKey] = String(partition),
        [SortKey] = String("TENANT"),
        ["TenantId"] = String(tenant.Id.Value),
        ["Status"] = String(tenant.Status.ToString()),
        ["DisplayName"] = String(tenant.Profile.DisplayName),
        ["TimeZoneIana"] = String(tenant.Profile.TimeZoneIana),
        ["Revision"] = Number(tenant.Revision)
    };

    private static Dictionary<string, AttributeValue> MembershipItem(Membership membership, string partition) => new()
    {
        [PartitionKey] = String(partition),
        [SortKey] = String($"MEMBERSHIP#{Encode(membership.Id.Value)}"),
        ["MembershipId"] = String(membership.Id.Value),
        ["TenantId"] = String(membership.TenantId.Value),
        ["SubjectId"] = String(membership.SubjectId.Value),
        ["Role"] = String(membership.Role.ToString()),
        ["Status"] = String(membership.Status.ToString()),
        ["Revision"] = Number(membership.Revision)
    };

    private static Dictionary<string, AttributeValue> AuthorityItem(Membership membership, string partition) => new()
    {
        [PartitionKey] = String(partition),
        [SortKey] = String($"AUTHORITY#SUBJECT#{Encode(membership.SubjectId.Value)}"),
        ["MembershipId"] = String(membership.Id.Value),
        ["SubjectId"] = String(membership.SubjectId.Value),
        ["Revision"] = Number(membership.Revision)
    };

    private static Dictionary<string, AttributeValue> DiscoveryItem(Membership membership, string partition) => new()
    {
        [PartitionKey] = String(partition),
        [SortKey] = String($"MEMBERSHIP#{Encode(membership.Id.Value)}#TENANT#{Encode(membership.TenantId.Value)}"),
        ["TenantId"] = String(membership.TenantId.Value),
        ["MembershipId"] = String(membership.Id.Value),
        ["MembershipStatus"] = String(membership.Status.ToString()),
        ["MembershipRevision"] = Number(membership.Revision)
    };

    private static Dictionary<string, AttributeValue> OwnerGuardItem(OnboardingOperation operation, string partition) => new()
    {
        [PartitionKey] = String(partition),
        [SortKey] = String("OWNER-GUARD"),
        ["OwnerMembershipId"] = String(operation.InitialOwner.Id.Value)
    };

    private static Dictionary<string, AttributeValue> MembershipCountGuardItem(OnboardingOperation operation, string partition) => new()
    {
        [PartitionKey] = String(partition),
        [SortKey] = String("MEMBERSHIP-COUNT-GUARD"),
        ["ActiveMembershipCount"] = Number(1)
    };

    private static Dictionary<string, AttributeValue> WorkItem(TrialBootstrapWorkItem workItem, string operationKey) => new()
    {
        [PartitionKey] = String(operationKey),
        [SortKey] = String($"WORKOUTBOX#{Encode(workItem.OnboardingOperationId)}"),
        ["WorkId"] = String(workItem.WorkId),
        ["OperationId"] = String(workItem.OnboardingOperationId),
        ["TenantId"] = String(workItem.TenantId),
        ["SourceIdentity"] = String(workItem.SourceIdentity),
        ["CorrelationId"] = String(workItem.CorrelationId),
        ["Status"] = String(OnboardingStatus.PendingTrial.ToString())
    };

    private TransactWriteItem Put(Dictionary<string, AttributeValue> item, string condition) => new()
    {
        Put = new Put { TableName = _options.TableName, Item = item, ConditionExpression = condition }
    };

    private static string OperationKey(SubjectId subjectId, string idempotencyKey) =>
        $"ONBOARDING#SUBJECT#{Encode(subjectId.Value)}#KEY#{Encode(idempotencyKey)}";

    private static string LocatorKey(string operationId) => $"ONBOARDING#OPERATION#{Encode(operationId)}";

    private static string TenantPartition(TenantId tenantId) => $"TENANT#{Encode(tenantId.Value)}";

    private static Dictionary<string, AttributeValue> Key(string partitionKey, string sortKey) => new()
    {
        [PartitionKey] = String(partitionKey),
        [SortKey] = String(sortKey)
    };

    private static OnboardingOperation ReadOperation(Dictionary<string, AttributeValue> item)
    {
        var tenantId = new TenantId(item["TenantId"].S);
        return new OnboardingOperation(
            item["OperationId"].S,
            new SubjectId(item["SubjectId"].S),
            item["IdempotencyKey"].S,
            item["RequestFingerprint"].S,
            new Tenant(tenantId, TenantStatus.Active, new BusinessProfile(item["DisplayName"].S, item["TimeZoneIana"].S), 1),
            new Membership(new MembershipId(item["MembershipId"].S), tenantId, new SubjectId(item["SubjectId"].S), MerchantRole.Owner, MembershipStatus.Active, 1),
            Enum.Parse<OnboardingStatus>(item["Status"].S, false),
            item["CorrelationId"].S);
    }

    private static AttributeValue String(string value) => new() { S = value };

    private static AttributeValue Number(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
