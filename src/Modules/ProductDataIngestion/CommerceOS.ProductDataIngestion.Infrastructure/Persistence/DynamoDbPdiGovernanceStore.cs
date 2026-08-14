using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.ProductDataIngestion.Application;
using CommerceOS.ProductDataIngestion.Domain;

namespace CommerceOS.ProductDataIngestion.Infrastructure.Persistence;

public sealed record DynamoDbPdiGovernanceOptions(string TableName);
public sealed class DynamoDbPdiGovernanceStore(IAmazonDynamoDB client, DynamoDbPdiGovernanceOptions options) : IPdiGovernanceStore
{
    public async Task<DataSource?> GetSourceAsync(DataSourceId id, CancellationToken ct) { var item = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key("SOURCE", id.Value) }, ct); return item.Item.Count == 0 ? null : new(new(item.Item["SourceId"].S), item.Item["Name"].S, Enum.Parse<SourceStatus>(item.Item["Status"].S), Enum.Parse<PolicyReviewStatus>(item.Item["PolicyReview"].S), item.Item["PolicyVersion"].S, int.Parse(item.Item["MaxRequestsPerMinute"].N, CultureInfo.InvariantCulture), long.Parse(item.Item["Revision"].N, CultureInfo.InvariantCulture)); }
    public async Task<IReadOnlyList<DataSource>> ListSourcesAsync(CancellationToken ct)
    {
        var response = await client.QueryAsync(new QueryRequest { TableName = options.TableName, KeyConditionExpression = "PK = :pk", ExpressionAttributeValues = new() { [":pk"] = S("SOURCE") } }, ct);
        return response.Items.Select(ReadSource).OrderBy(source => source.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public async Task<TenantSourceEnrollment?> GetEnrollmentAsync(TrustedPdiTenantContext context, DataSourceId id, CancellationToken ct) { var item = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key($"TENANT#{E(context.TenantId.Value)}", $"ENROLLMENT#{E(id.Value)}") }, ct); return item.Item.Count == 0 ? null : new(new(item.Item["TenantId"].S), new(item.Item["SourceId"].S), item.Item["Enabled"].BOOL is true, long.Parse(item.Item["Revision"].N, CultureInfo.InvariantCulture)); }
    public async Task<PdiOutcome> SaveSourceAsync(DataSource source, long? expectedRevision, CancellationToken ct) => await PutAsync(Key("SOURCE", source.Id.Value), new() { ["SourceId"] = S(source.Id.Value), ["Name"] = S(source.Name), ["Status"] = S(source.Status.ToString()), ["PolicyReview"] = S(source.PolicyReview.ToString()), ["PolicyVersion"] = S(source.PolicyVersion), ["MaxRequestsPerMinute"] = N(source.MaxRequestsPerMinute), ["Revision"] = N(source.Revision) }, expectedRevision, ct);
    public async Task<PdiOutcome> SaveEnrollmentAsync(TrustedPdiTenantContext context, TenantSourceEnrollment enrollment, long? expectedRevision, CancellationToken ct) { if (enrollment.TenantId != context.TenantId) return PdiOutcome.NotEligible; return await PutAsync(Key($"TENANT#{E(context.TenantId.Value)}", $"ENROLLMENT#{E(enrollment.SourceId.Value)}"), new() { ["TenantId"] = S(enrollment.TenantId.Value), ["SourceId"] = S(enrollment.SourceId.Value), ["Enabled"] = new() { BOOL = enrollment.Enabled }, ["Revision"] = N(enrollment.Revision) }, expectedRevision, ct); }
    private async Task<PdiOutcome> PutAsync(Dictionary<string, AttributeValue> key, Dictionary<string, AttributeValue> fields, long? expectedRevision, CancellationToken ct) { try { await client.PutItemAsync(new() { TableName = options.TableName, Item = key.Concat(fields).ToDictionary(x => x.Key, x => x.Value), ConditionExpression = expectedRevision is null ? "attribute_not_exists(PK)" : "Revision = :revision", ExpressionAttributeValues = expectedRevision is null ? null : new() { [":revision"] = N(expectedRevision.Value) } }, ct); return PdiOutcome.Applied; } catch (ConditionalCheckFailedException) { return PdiOutcome.RevisionConflict; } }
    private static DataSource ReadSource(Dictionary<string, AttributeValue> item) => new(new(item["SourceId"].S), item["Name"].S, Enum.Parse<SourceStatus>(item["Status"].S), Enum.Parse<PolicyReviewStatus>(item["PolicyReview"].S), item["PolicyVersion"].S, int.Parse(item["MaxRequestsPerMinute"].N, CultureInfo.InvariantCulture), long.Parse(item["Revision"].N, CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> Key(string pk, string sk) => new() { ["PK"] = S(pk), ["SK"] = S(sk) }; private static string E(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static AttributeValue S(string value) => new() { S = value }; private static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
}
