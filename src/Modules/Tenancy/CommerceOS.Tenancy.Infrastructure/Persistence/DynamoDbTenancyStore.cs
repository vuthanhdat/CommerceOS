using System.Globalization;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Tenancy.Application.Persistence;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.Infrastructure.Persistence;

public sealed record DynamoDbTenancyOptions(string TableName);

public sealed class DynamoDbTenancyStore : ITenancyStore
{
    private const string PartitionKey = "PK";
    private const string SortKey = "SK";
    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbTenancyOptions _options;

    public DynamoDbTenancyStore(IAmazonDynamoDB client, DynamoDbTenancyOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<Tenant?> GetTenantAsync(TrustedTenantPersistenceScope scope, CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            Key = Key(TenantPartition(scope.TenantId), "TENANT")
        }, cancellationToken);

        return response.Item.Count == 0 ? null : ReadTenant(response.Item);
    }

    public async Task<Membership?> GetMembershipAsync(
        TrustedTenantPersistenceScope scope,
        MembershipId membershipId,
        CancellationToken cancellationToken)
    {
        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            Key = Key(TenantPartition(scope.TenantId), $"MEMBERSHIP#{Encode(membershipId.Value)}")
        }, cancellationToken);

        return response.Item.Count == 0 ? null : ReadMembership(response.Item);
    }

    public async Task<Membership?> GetMembershipForSubjectAsync(
        TrustedTenantPersistenceScope scope,
        SubjectId subjectId,
        CancellationToken cancellationToken)
    {
        var authority = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            Key = Key(TenantPartition(scope.TenantId), $"AUTHORITY#SUBJECT#{Encode(subjectId.Value)}")
        }, cancellationToken);

        if (authority.Item.Count == 0 || !authority.Item.TryGetValue("MembershipId", out var membershipId))
        {
            return null;
        }

        return await GetMembershipAsync(scope, new MembershipId(membershipId.S), cancellationToken);
    }

    public async Task<IReadOnlyList<MembershipDiscoveryCandidate>> FindMembershipCandidatesAsync(
        SubjectId subjectId,
        CancellationToken cancellationToken)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            ConsistentRead = true,
            KeyConditionExpression = "#pk = :pk",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = PartitionKey },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = String($"SUBJECT#{Encode(subjectId.Value)}")
            }
        }, cancellationToken);

        return response.Items.Select(item => new MembershipDiscoveryCandidate(
            new TenantId(item["TenantId"].S),
            new MembershipId(item["MembershipId"].S))).ToArray();
    }

    public async Task<ConditionalWriteResult> SaveTenantAsync(
        TrustedTenantPersistenceScope scope,
        Tenant tenant,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        if (scope.TenantId != tenant.Id)
        {
            throw new ArgumentException("Tenant writes must use the matching trusted tenant scope.", nameof(tenant));
        }

        var request = new PutItemRequest
        {
            TableName = _options.TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                [PartitionKey] = String(TenantPartition(tenant.Id)),
                [SortKey] = String("TENANT"),
                ["TenantId"] = String(tenant.Id.Value),
                ["Status"] = String(tenant.Status.ToString()),
                ["DisplayName"] = String(tenant.Profile.DisplayName),
                ["TimeZoneIana"] = String(tenant.Profile.TimeZoneIana),
                ["Revision"] = Number(tenant.Revision)
            },
            ConditionExpression = expectedRevision is null ? "attribute_not_exists(PK)" : "Revision = :expectedRevision",
        };

        if (expectedRevision is not null)
        {
            request.ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":expectedRevision"] = Number(expectedRevision.Value)
            };
        }

        try
        {
            await _client.PutItemAsync(request, cancellationToken);
            return ConditionalWriteResult.Applied;
        }
        catch (ConditionalCheckFailedException)
        {
            return ConditionalWriteResult.RevisionConflict;
        }
    }

    private static Tenant ReadTenant(Dictionary<string, AttributeValue> item) => new(
        new TenantId(item["TenantId"].S),
        Enum.Parse<TenantStatus>(item["Status"].S, false),
        new BusinessProfile(item["DisplayName"].S, item["TimeZoneIana"].S),
        long.Parse(item["Revision"].N, CultureInfo.InvariantCulture));

    private static Membership ReadMembership(Dictionary<string, AttributeValue> item) => new(
        new MembershipId(item["MembershipId"].S),
        new TenantId(item["TenantId"].S),
        new SubjectId(item["SubjectId"].S),
        Enum.Parse<MerchantRole>(item["Role"].S, false),
        Enum.Parse<MembershipStatus>(item["Status"].S, false),
        long.Parse(item["Revision"].N, CultureInfo.InvariantCulture));

    private static Dictionary<string, AttributeValue> Key(string partitionKey, string sortKey) => new()
    {
        [PartitionKey] = String(partitionKey),
        [SortKey] = String(sortKey)
    };

    private static AttributeValue String(string value) => new() { S = value };

    private static AttributeValue Number(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    private static string TenantPartition(TenantId tenantId) => $"TENANT#{Encode(tenantId.Value)}";

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
