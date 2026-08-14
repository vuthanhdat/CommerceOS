namespace CommerceOS.FilesMedia.Contracts;

public sealed record ManagedMediaAsset(string AssetId, string TenantId, bool IsReady, string ContentType);
/// <summary>Catalog calls this producer-owned contract; it never sees storage keys or S3 details.</summary>
public interface IManagedMediaAssetLookup
{
    Task<ManagedMediaAsset?> GetReadyAssetAsync(string trustedTenantId, string assetId, CancellationToken cancellationToken);
}
