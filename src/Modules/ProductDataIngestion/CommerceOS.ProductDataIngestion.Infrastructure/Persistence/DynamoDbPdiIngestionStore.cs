using System.Globalization;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using CommerceOS.ProductDataIngestion.Application;
using CommerceOS.ProductDataIngestion.Domain;

namespace CommerceOS.ProductDataIngestion.Infrastructure.Persistence;

public sealed record DynamoDbPdiIngestionOptions(string TableName, string RawSnapshotBucket);

/// <summary>LocalStack-compatible PDI evidence/configuration persistence. Every tenant item is partitioned by trusted tenant context.</summary>
public sealed class DynamoDbPdiIngestionStore(IAmazonDynamoDB client, DynamoDbPdiIngestionOptions options) : IManualAcquisitionWorkStore, ISourceSnapshotStore, IImportCandidateStore, IScheduledSourceRefreshStore
{
    public async Task<PdiOutcome> EnqueueIfAbsentAsync(ManualAcquisitionRequest request, CancellationToken cancellationToken) => await PutIfAbsentAsync(Key($"TENANT#{E(request.TenantId.Value)}", $"WORK#{E(request.WorkIdentity)}"), new() { ["Id"] = S(request.Id), ["TenantId"] = S(request.TenantId.Value), ["SourceId"] = S(request.SourceId.Value), ["Url"] = S(request.Url.AbsoluteUri), ["WorkIdentity"] = S(request.WorkIdentity), ["CorrelationId"] = S(request.CorrelationId) }, cancellationToken);
    public Task<bool> SaveSourceSnapshotIfAbsentAsync(SourceSnapshot snapshot, CancellationToken cancellationToken) => PutBooleanAsync(Key($"TENANT#{E(snapshot.TenantId.Value)}", $"SOURCE-SNAPSHOT#{E(snapshot.Id)}"), new() { ["Id"] = S(snapshot.Id), ["SourceId"] = S(snapshot.SourceId.Value), ["SourceProductId"] = S(snapshot.SourceProductId), ["SourceUrl"] = S(snapshot.SourceUrl.AbsoluteUri), ["RawObjectKey"] = S(snapshot.RawObjectKey), ["ContentHash"] = S(snapshot.ContentHash), ["AdapterVersion"] = S(snapshot.AdapterVersion), ["CapturedAt"] = S(snapshot.CapturedAt.ToString("O", CultureInfo.InvariantCulture)) }, cancellationToken);
    public Task<bool> SaveNormalizedSnapshotIfAbsentAsync(NormalizedSourceSnapshot snapshot, CancellationToken cancellationToken) => PutBooleanAsync(Key($"TENANT#{E(snapshot.TenantId.Value)}", $"NORMALIZED-SNAPSHOT#{E(snapshot.Id)}"), new() { ["Id"] = S(snapshot.Id), ["SourceSnapshotId"] = S(snapshot.SourceSnapshotId), ["SourceId"] = S(snapshot.SourceId.Value), ["SourceProductId"] = S(snapshot.SourceProductId), ["Name"] = S(snapshot.Name), ["SourceSku"] = S(snapshot.SourceSku ?? string.Empty), ["VndPrice"] = snapshot.VndPrice is null ? new AttributeValue { NULL = true } : N(snapshot.VndPrice.Value), ["NormalizedHash"] = S(snapshot.NormalizedHash), ["SchemaVersion"] = S(snapshot.SchemaVersion), ["CapturedAt"] = S(snapshot.CapturedAt.ToString("O", CultureInfo.InvariantCulture)) }, cancellationToken);
    public async Task<ImportCandidate?> GetAsync(TrustedPdiTenantContext context, string candidateId, CancellationToken cancellationToken)
    {
        var item = await client.GetItemAsync(new GetItemRequest { TableName = options.TableName, ConsistentRead = true, Key = Key($"TENANT#{E(context.TenantId.Value)}", $"IMPORT-CANDIDATE#{E(candidateId)}") }, cancellationToken);
        return item.Item.Count == 0 ? null : Candidate(context.TenantId, item.Item);
    }
    public async Task<PdiOutcome> SaveIfRevisionAsync(TrustedPdiTenantContext context, ImportCandidate candidate, long? expectedRevision, CancellationToken cancellationToken)
    {
        if (candidate.TenantId != context.TenantId) return PdiOutcome.NotEligible;
        return await PutVersionedAsync(Key($"TENANT#{E(context.TenantId.Value)}", $"IMPORT-CANDIDATE#{E(candidate.Id)}"), CandidateItem(candidate), expectedRevision, cancellationToken);
    }
    public async Task<ScheduledSourceRefresh?> GetScheduleAsync(TrustedPdiTenantContext context, string scheduleId, CancellationToken cancellationToken)
    {
        var item = await client.GetItemAsync(new GetItemRequest { TableName = options.TableName, ConsistentRead = true, Key = Key($"TENANT#{E(context.TenantId.Value)}", $"SCHEDULE#{E(scheduleId)}") }, cancellationToken);
        return item.Item.Count == 0 ? null : new(scheduleId, context.TenantId, new(item.Item["SourceId"].S), new Uri(item.Item["SourceUrl"].S), item.Item["Enabled"].BOOL is true, long.Parse(item.Item["Revision"].N, CultureInfo.InvariantCulture), item.Item.TryGetValue("LastSuppressedAt", out var suppressed) && suppressed.NULL is not true ? DateTimeOffset.Parse(suppressed.S, CultureInfo.InvariantCulture) : null);
    }
    public async Task<PdiOutcome> SaveAsync(TrustedPdiTenantContext context, ScheduledSourceRefresh schedule, long? expectedRevision, CancellationToken cancellationToken)
    {
        if (schedule.TenantId != context.TenantId) return PdiOutcome.NotEligible;
        return await PutVersionedAsync(Key($"TENANT#{E(context.TenantId.Value)}", $"SCHEDULE#{E(schedule.Id)}"), new() { ["SourceId"] = S(schedule.SourceId.Value), ["SourceUrl"] = S(schedule.SourceUrl.AbsoluteUri), ["Enabled"] = new() { BOOL = schedule.Enabled }, ["Revision"] = N(schedule.Revision), ["LastSuppressedAt"] = schedule.LastSuppressedAt is null ? new AttributeValue { NULL = true } : S(schedule.LastSuppressedAt.Value.ToString("O", CultureInfo.InvariantCulture)) }, expectedRevision, cancellationToken);
    }
    private async Task<PdiOutcome> PutIfAbsentAsync(Dictionary<string, AttributeValue> key, Dictionary<string, AttributeValue> fields, CancellationToken cancellationToken) { try { await client.PutItemAsync(new PutItemRequest { TableName = options.TableName, Item = key.Concat(fields).ToDictionary(x => x.Key, x => x.Value), ConditionExpression = "attribute_not_exists(PK)" }, cancellationToken); return PdiOutcome.Applied; } catch (ConditionalCheckFailedException) { return PdiOutcome.RevisionConflict; } }
    private async Task<bool> PutBooleanAsync(Dictionary<string, AttributeValue> key, Dictionary<string, AttributeValue> fields, CancellationToken cancellationToken) => await PutIfAbsentAsync(key, fields, cancellationToken) is PdiOutcome.Applied;
    private async Task<PdiOutcome> PutVersionedAsync(Dictionary<string, AttributeValue> key, Dictionary<string, AttributeValue> fields, long? expectedRevision, CancellationToken cancellationToken)
    {
        try { await client.PutItemAsync(new PutItemRequest { TableName = options.TableName, Item = key.Concat(fields).ToDictionary(x => x.Key, x => x.Value), ConditionExpression = expectedRevision is null ? "attribute_not_exists(PK)" : "Revision = :expected", ExpressionAttributeValues = expectedRevision is null ? null : new() { [":expected"] = N(expectedRevision.Value) } }, cancellationToken); return PdiOutcome.Applied; } catch (ConditionalCheckFailedException) { return PdiOutcome.RevisionConflict; }
    }
    private static ImportCandidate Candidate(PdiTenantId tenant, Dictionary<string, AttributeValue> x) => new(x["Id"].S, tenant, x["SourceSnapshotId"].S, new(x["SourceId"].S), x["SourceProductId"].S, x["ProductId"].S, long.Parse(x["ExpectedProductRevision"].N, CultureInfo.InvariantCulture), x["Name"].S, x["SourceSku"].NULL is true ? null : x["SourceSku"].S, x["VndPrice"].NULL is true ? null : long.Parse(x["VndPrice"].N, CultureInfo.InvariantCulture), Enum.Parse<ImportCandidateStatus>(x["Status"].S), x["ReviewNote"].NULL is true ? null : x["ReviewNote"].S, long.Parse(x["Revision"].N, CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> CandidateItem(ImportCandidate x) => new() { ["Id"] = S(x.Id), ["SourceSnapshotId"] = S(x.SourceSnapshotId), ["SourceId"] = S(x.SourceId.Value), ["SourceProductId"] = S(x.SourceProductId), ["ProductId"] = S(x.ProductId), ["ExpectedProductRevision"] = N(x.ExpectedProductRevision), ["Name"] = S(x.Name), ["SourceSku"] = x.SourceSku is null ? new AttributeValue { NULL = true } : S(x.SourceSku), ["VndPrice"] = x.VndPrice is null ? new AttributeValue { NULL = true } : N(x.VndPrice.Value), ["Status"] = S(x.Status.ToString()), ["ReviewNote"] = x.ReviewNote is null ? new AttributeValue { NULL = true } : S(x.ReviewNote), ["Revision"] = N(x.Revision) };
    private static Dictionary<string, AttributeValue> Key(string pk, string sk) => new() { ["PK"] = S(pk), ["SK"] = S(sk) }; private static string E(string x) => Convert.ToBase64String(Encoding.UTF8.GetBytes(x)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static AttributeValue S(string x) => new() { S = x }; private static AttributeValue N(long x) => new() { N = x.ToString(CultureInfo.InvariantCulture) };
}

public sealed class S3RawPdiSnapshotStore(IAmazonS3 client, DynamoDbPdiIngestionOptions options) : IRawPdiSnapshotStore
{
    public async Task<string> StoreIfAbsentAsync(ManualAcquisitionRequest work, string contentHash, string rawPayload, CancellationToken cancellationToken)
    {
        var key = $"raw/{work.SourceId.Value}/{contentHash}.json";
        try { await client.PutObjectAsync(new PutObjectRequest { BucketName = options.RawSnapshotBucket, Key = key, ContentBody = rawPayload, ContentType = "application/json", IfNoneMatch = "*" }, cancellationToken); }
        catch (AmazonS3Exception ex) when (ex.StatusCode is System.Net.HttpStatusCode.PreconditionFailed or System.Net.HttpStatusCode.Conflict) { }
        return key;
    }
}
