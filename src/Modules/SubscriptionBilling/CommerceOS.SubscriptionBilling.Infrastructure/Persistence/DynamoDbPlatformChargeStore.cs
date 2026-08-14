using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.SubscriptionBilling.Application.PlatformCharges;
using CommerceOS.SubscriptionBilling.Domain;

namespace CommerceOS.SubscriptionBilling.Infrastructure.Persistence;

public sealed class DynamoDbPlatformChargeStore : IPlatformChargeStore
{
    private const string PartitionKey = "PK";
    private const string SortKey = "SK";
    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbSubscriptionBillingOptions _options;

    public DynamoDbPlatformChargeStore(IAmazonDynamoDB client, DynamoDbSubscriptionBillingOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<PlatformCharge?> GetByLogicalIdentityAsync(string tenantId, string logicalChargeIdentity, CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            Key = Key(TenantPartition(tenantId), ChargeKey(logicalChargeIdentity))
        }, cancellationToken);
        return response.Item is null || response.Item.Count == 0 ? null : ReadCharge(response.Item);
    }

    public async Task<PlatformChargeCreateResult> CreateIfAbsentAsync(PlatformCharge charge, CancellationToken cancellationToken)
    {
        try
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = _options.TableName,
                Item = ChargeItem(charge),
                ConditionExpression = "attribute_not_exists(PK)"
            }, cancellationToken);
            return PlatformChargeCreateResult.Created;
        }
        catch (ConditionalCheckFailedException)
        {
            return PlatformChargeCreateResult.AlreadyExists;
        }
    }

    public async Task<PlatformChargeEvidenceApplyResult> ApplyEvidenceAsync(
        PlatformCharge current,
        PlatformChargeEvidence evidence,
        PlatformCharge updated,
        CancellationToken cancellationToken)
    {
        var partition = TenantPartition(current.TenantId);
        var evidenceKey = EvidenceKey(current.LogicalChargeIdentity, evidence.EvidenceId);
        var transaction = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _options.TableName,
                        Item = EvidenceItem(evidence, partition, evidenceKey),
                        ConditionExpression = "attribute_not_exists(PK)"
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _options.TableName,
                        Item = ChargeItem(updated),
                        ConditionExpression = "Revision = :expectedRevision",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":expectedRevision"] = Number(current.Revision)
                        }
                    }
                }
            ]
        };

        try
        {
            await _client.TransactWriteItemsAsync(transaction, cancellationToken);
            return PlatformChargeEvidenceApplyResult.Applied;
        }
        catch (TransactionCanceledException)
        {
            var existingEvidence = await _client.GetItemAsync(new GetItemRequest
            {
                TableName = _options.TableName,
                ConsistentRead = true,
                Key = Key(partition, evidenceKey)
            }, cancellationToken);
            return existingEvidence.Item is not null && existingEvidence.Item.Count != 0
                ? PlatformChargeEvidenceApplyResult.Duplicate
                : PlatformChargeEvidenceApplyResult.RevisionConflict;
        }
    }

    public async Task<bool> MarkOutcomeUnknownAsync(PlatformCharge current, CancellationToken cancellationToken)
    {
        try
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = _options.TableName,
                Item = ChargeItem(current with { Outcome = PlatformChargeOutcome.OutcomeUnknown, Revision = current.Revision + 1 }),
                ConditionExpression = "Revision = :expectedRevision AND #outcome = :pending",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#outcome"] = "Outcome" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":expectedRevision"] = Number(current.Revision),
                    [":pending"] = String(PlatformChargeOutcome.Pending.ToString())
                }
            }, cancellationToken);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    private static PlatformCharge ReadCharge(Dictionary<string, AttributeValue> item) => new(
        new PlatformChargeId(item["ChargeId"].S),
        item["TenantId"].S,
        item["SubscriptionReference"].S,
        item["TermsReference"].S,
        item["LogicalChargeIdentity"].S,
        new VndMoney(long.Parse(item["AmountVnd"].N, CultureInfo.InvariantCulture)),
        item["ProviderOperationId"].S,
        Enum.Parse<PlatformChargeOutcome>(item["Outcome"].S, false),
        long.Parse(item["Revision"].N, CultureInfo.InvariantCulture),
        DateTimeOffset.Parse(item["CreatedAt"].S, CultureInfo.InvariantCulture));

    private static Dictionary<string, AttributeValue> ChargeItem(PlatformCharge charge) => new()
    {
        [PartitionKey] = String(TenantPartition(charge.TenantId)),
        [SortKey] = String(ChargeKey(charge.LogicalChargeIdentity)),
        ["Kind"] = String("PlatformCharge"),
        ["ChargeId"] = String(charge.Id.Value),
        ["TenantId"] = String(charge.TenantId),
        ["SubscriptionReference"] = String(charge.SubscriptionReference),
        ["TermsReference"] = String(charge.TermsReference),
        ["LogicalChargeIdentity"] = String(charge.LogicalChargeIdentity),
        ["AmountVnd"] = Number(charge.Amount.Amount),
        ["ProviderOperationId"] = String(charge.ProviderOperationId),
        ["Outcome"] = String(charge.Outcome.ToString()),
        ["Revision"] = Number(charge.Revision),
        ["CreatedAt"] = String(charge.CreatedAt.ToString("O", CultureInfo.InvariantCulture))
    };

    private static Dictionary<string, AttributeValue> EvidenceItem(PlatformChargeEvidence evidence, string partition, string sort) => new()
    {
        [PartitionKey] = String(partition),
        [SortKey] = String(sort),
        ["Kind"] = String("PlatformChargeEvidence"),
        ["EvidenceId"] = String(evidence.EvidenceId),
        ["ChargeId"] = String(evidence.ChargeId.Value),
        ["ProviderOperationId"] = String(evidence.ProviderOperationId),
        ["EvidenceKind"] = String(evidence.Kind.ToString()),
        ["OccurredAt"] = String(evidence.OccurredAt.ToString("O", CultureInfo.InvariantCulture))
    };

    private static string TenantPartition(string tenantId) => $"TENANT#{Encode(tenantId)}";
    private static string ChargeKey(string logicalIdentity) => $"CHARGE#{Encode(logicalIdentity)}";
    private static string EvidenceKey(string logicalIdentity, string evidenceId) => $"EVIDENCE#{Encode(logicalIdentity)}#{Encode(evidenceId)}";
    private static Dictionary<string, AttributeValue> Key(string partition, string sort) => new() { [PartitionKey] = String(partition), [SortKey] = String(sort) };
    private static AttributeValue String(string value) => new() { S = value };
    private static AttributeValue Number(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
    private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
