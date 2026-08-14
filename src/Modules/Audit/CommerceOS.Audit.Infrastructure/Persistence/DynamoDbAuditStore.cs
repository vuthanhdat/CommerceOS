using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Audit.Application;
using CommerceOS.Audit.Domain;

namespace CommerceOS.Audit.Infrastructure.Persistence;

public sealed record DynamoDbAuditOptions(string TableName);
public sealed class DynamoDbAuditStore(IAmazonDynamoDB client, DynamoDbAuditOptions options) : IAuditStore
{
    public async Task<bool> AppendAsync(AuditEvidence evidence, CancellationToken ct)
    {
        var pk = evidence.Audience is AuditAudience.PlatformSecurity ? "PLATFORM-SECURITY" : $"TENANT#{E(evidence.TenantId ?? throw new ArgumentException("Tenant evidence requires TenantId."))}";
        try { await client.TransactWriteItemsAsync(new() { TransactItems = [new() { Put = new() { TableName = options.TableName, Item = EvidenceItem(pk, evidence), ConditionExpression = "attribute_not_exists(PK)" } }, new() { Put = new() { TableName = options.TableName, Item = new() { ["PK"] = S(pk), ["SK"] = S($"SOURCE#{E(evidence.SourceIdentity)}"), ["EvidenceId"] = S(evidence.Id) }, ConditionExpression = "attribute_not_exists(PK)" } }] }, ct); return true; } catch (TransactionCanceledException) { return false; }
    }
    public Task<IReadOnlyList<AuditEvidence>> ListTenantAsync(string tenantId, DateTimeOffset from, int limit, CancellationToken ct) => ListAsync($"TENANT#{E(tenantId)}", from, limit, ct);
    public Task<IReadOnlyList<AuditEvidence>> ListPlatformSecurityAsync(DateTimeOffset from, int limit, CancellationToken ct) => ListAsync("PLATFORM-SECURITY", from, limit, ct);
    private async Task<IReadOnlyList<AuditEvidence>> ListAsync(string pk, DateTimeOffset from, int limit, CancellationToken ct) { var result = await client.QueryAsync(new() { TableName = options.TableName, KeyConditionExpression = "PK = :pk AND SK >= :from", ExpressionAttributeValues = new() { [":pk"] = S(pk), [":from"] = S($"EVIDENCE#{from.UtcTicks:D20}") }, Limit = limit }, ct); return result.Items.Where(x => x.ContainsKey("Action")).Select(Read).ToArray(); }
    private static Dictionary<string, AttributeValue> EvidenceItem(string pk, AuditEvidence x) => new() { ["PK"] = S(pk), ["SK"] = S($"EVIDENCE#{x.OccurredAt.UtcTicks:D20}#{E(x.Id)}"), ["Id"] = S(x.Id), ["SourceIdentity"] = S(x.SourceIdentity), ["TenantId"] = S(x.TenantId ?? string.Empty), ["Audience"] = S(x.Audience.ToString()), ["ActorId"] = S(x.ActorId), ["Action"] = S(x.Action), ["Outcome"] = S(x.Outcome), ["SafeReason"] = S(x.SafeReason), ["CorrelationId"] = S(x.CorrelationId), ["OccurredAt"] = S(x.OccurredAt.ToString("O", CultureInfo.InvariantCulture)) };
    private static AuditEvidence Read(Dictionary<string, AttributeValue> x) => new(x["Id"].S, x["SourceIdentity"].S, string.IsNullOrEmpty(x["TenantId"].S) ? null : x["TenantId"].S, Enum.Parse<AuditAudience>(x["Audience"].S), x["ActorId"].S, x["Action"].S, x["Outcome"].S, x["SafeReason"].S, x["CorrelationId"].S, DateTimeOffset.Parse(x["OccurredAt"].S, CultureInfo.InvariantCulture));
    private static string E(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static AttributeValue S(string value) => new() { S = value };
}
