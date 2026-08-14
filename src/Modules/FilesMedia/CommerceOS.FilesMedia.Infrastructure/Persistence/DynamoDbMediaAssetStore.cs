using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using CommerceOS.FilesMedia.Application;
using CommerceOS.FilesMedia.Contracts;
using CommerceOS.FilesMedia.Domain;

namespace CommerceOS.FilesMedia.Infrastructure.Persistence;

public sealed record DynamoDbFilesMediaOptions(string TableName, string BucketName);
public sealed class DynamoDbMediaAssetStore(IAmazonDynamoDB client, DynamoDbFilesMediaOptions options) : IMediaAssetStore, IManagedMediaAssetLookup
{
    public async Task<MediaAsset?> GetAsync(TrustedMediaMutationContext context, MediaAssetId id, CancellationToken cancellationToken)
    {
        var item = await client.GetItemAsync(new GetItemRequest { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, id) }, cancellationToken);
        return item.Item.Count == 0 ? null : Read(item.Item);
    }
    public async Task<MediaOutcome> SaveAsync(TrustedMediaMutationContext context, MediaAsset asset, long? expectedRevision, CancellationToken cancellationToken)
    {
        if (asset.TenantId != context.TenantId) return MediaOutcome.InvalidUpload;
        try { await client.PutItemAsync(new PutItemRequest { TableName = options.TableName, Item = Item(asset), ConditionExpression = expectedRevision is null ? "attribute_not_exists(PK)" : "Revision = :revision", ExpressionAttributeValues = expectedRevision is null ? null : new() { [":revision"] = N(expectedRevision.Value) } }, cancellationToken); return MediaOutcome.Applied; }
        catch (ConditionalCheckFailedException) { return MediaOutcome.RevisionConflict; }
    }
    public async Task<ManagedMediaAsset?> GetReadyAssetAsync(string trustedTenantId, string assetId, CancellationToken cancellationToken)
    {
        var asset = await GetAsync(new(new(trustedTenantId), "catalog-media-lookup"), new(assetId), cancellationToken);
        return asset is null ? null : new(asset.Id.Value, asset.TenantId.Value, asset.Status is MediaAssetStatus.Ready, asset.ContentType);
    }
    private static MediaAsset Read(Dictionary<string, AttributeValue> x) => new(new(x["AssetId"].S), new(x["TenantId"].S), x["ContentType"].S, long.Parse(x["ContentLength"].N, CultureInfo.InvariantCulture), Enum.Parse<MediaAssetStatus>(x["Status"].S), x["ObjectKey"].S, long.Parse(x["Revision"].N, CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> Item(MediaAsset x) => new() { ["PK"] = S(P(x.TenantId)), ["SK"] = S($"ASSET#{E(x.Id.Value)}"), ["AssetId"] = S(x.Id.Value), ["TenantId"] = S(x.TenantId.Value), ["ContentType"] = S(x.ContentType), ["ContentLength"] = N(x.ContentLength), ["Status"] = S(x.Status.ToString()), ["ObjectKey"] = S(x.ObjectKey), ["Revision"] = N(x.Revision) };
    private static Dictionary<string, AttributeValue> Key(MediaTenantId t, MediaAssetId id) => new() { ["PK"] = S(P(t)), ["SK"] = S($"ASSET#{E(id.Value)}") }; private static string P(MediaTenantId t) => $"TENANT#{E(t.Value)}"; private static string E(string x) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(x)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static AttributeValue S(string x) => new() { S = x }; private static AttributeValue N(long x) => new() { N = x.ToString(CultureInfo.InvariantCulture) };
}
public sealed class S3ObjectUploadGateway(IAmazonS3 client, DynamoDbFilesMediaOptions options) : IObjectUploadGateway
{
    public async Task<bool> ObjectExistsWithExpectedMetadataAsync(string objectKey, string contentType, long contentLength, CancellationToken cancellationToken)
    {
        try { var metadata = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = options.BucketName, Key = objectKey }, cancellationToken); return metadata.ContentLength == contentLength && string.Equals(metadata.Headers.ContentType, contentType, StringComparison.OrdinalIgnoreCase); }
        catch (AmazonS3Exception exception) when (exception.StatusCode is System.Net.HttpStatusCode.NotFound) { return false; }
    }
}
