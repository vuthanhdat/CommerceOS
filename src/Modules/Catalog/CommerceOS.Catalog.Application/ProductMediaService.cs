using CommerceOS.Catalog.Domain;
using CommerceOS.FilesMedia.Contracts;

namespace CommerceOS.Catalog.Application;

public sealed class ProductMediaService(ICatalogStore store, IManagedMediaAssetLookup assets)
{
    public async Task<CatalogOutcome> SetMediaAsync(TrustedCatalogMutationContext context, ProductId productId, IReadOnlyList<ProductMediaAssociation> media, long expectedRevision, CancellationToken cancellationToken)
    {
        var product = await store.GetAsync(context, productId, cancellationToken);
        if (product is null) return CatalogOutcome.NotFound;
        if (product.Revision != expectedRevision) return CatalogOutcome.RevisionConflict;
        foreach (var item in media)
        {
            var asset = await assets.GetReadyAssetAsync(context.TenantId.Value, item.AssetId, cancellationToken);
            if (asset is null || !asset.IsReady || !string.Equals(asset.TenantId, context.TenantId.Value, StringComparison.Ordinal)) return CatalogOutcome.InvalidState;
        }
        try { return await store.SaveWithClaimsAsync(context, product, product.SetMedia(media, expectedRevision), cancellationToken); }
        catch (ProductRuleException) { return CatalogOutcome.InvalidState; }
    }
}
