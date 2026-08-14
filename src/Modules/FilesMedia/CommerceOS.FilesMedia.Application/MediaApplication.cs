using CommerceOS.FilesMedia.Contracts;
using CommerceOS.FilesMedia.Domain;

namespace CommerceOS.FilesMedia.Application;

public sealed record TrustedMediaMutationContext(MediaTenantId TenantId, string CorrelationId);
public enum MediaOutcome { Applied, NotFound, RevisionConflict, InvalidUpload }
public interface IMediaAssetStore
{
    Task<MediaAsset?> GetAsync(TrustedMediaMutationContext context, MediaAssetId id, CancellationToken cancellationToken);
    Task<MediaOutcome> SaveAsync(TrustedMediaMutationContext context, MediaAsset asset, long? expectedRevision, CancellationToken cancellationToken);
}
public interface IObjectUploadGateway { Task<bool> ObjectExistsWithExpectedMetadataAsync(string objectKey, string contentType, long contentLength, CancellationToken cancellationToken); }
public sealed class MediaAssetService(IMediaAssetStore store, IObjectUploadGateway objects)
{
    public Task<MediaOutcome> InitiateAsync(TrustedMediaMutationContext context, MediaAsset asset, CancellationToken cancellationToken) => asset.TenantId != context.TenantId ? Task.FromResult(MediaOutcome.InvalidUpload) : store.SaveAsync(context, asset, null, cancellationToken);
    public async Task<MediaOutcome> FinalizeAsync(TrustedMediaMutationContext context, MediaAssetId id, long expectedRevision, CancellationToken cancellationToken)
    {
        var asset = await store.GetAsync(context, id, cancellationToken);
        if (asset is null) return MediaOutcome.NotFound;
        if (asset.Revision != expectedRevision) return MediaOutcome.RevisionConflict;
        if (!await objects.ObjectExistsWithExpectedMetadataAsync(asset.ObjectKey, asset.ContentType, asset.ContentLength, cancellationToken)) return MediaOutcome.InvalidUpload;
        return await store.SaveAsync(context, asset with { Status = MediaAssetStatus.Ready, Revision = asset.Revision + 1 }, expectedRevision, cancellationToken);
    }
}
