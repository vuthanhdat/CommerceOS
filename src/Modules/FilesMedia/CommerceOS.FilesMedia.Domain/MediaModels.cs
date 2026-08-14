namespace CommerceOS.FilesMedia.Domain;

public readonly record struct MediaTenantId(string Value);
public readonly record struct MediaAssetId(string Value);
public enum MediaAssetStatus { PendingUpload, Ready, Retired, Failed }
public sealed record MediaAsset(MediaAssetId Id, MediaTenantId TenantId, string ContentType, long ContentLength, MediaAssetStatus Status, string ObjectKey, long Revision)
{
    public static MediaAsset Start(MediaAssetId id, MediaTenantId tenantId, string contentType, long contentLength, string objectKey)
    {
        if (contentLength is <= 0 or > 10_000_000 || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only image uploads up to 10 MB are supported.");
        return new(id, tenantId, contentType.ToLowerInvariant(), contentLength, MediaAssetStatus.PendingUpload, objectKey, 1);
    }
}
