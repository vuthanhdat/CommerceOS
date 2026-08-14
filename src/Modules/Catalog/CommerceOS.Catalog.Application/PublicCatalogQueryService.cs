using CommerceOS.Catalog.Contracts;
using CommerceOS.Catalog.Domain;

namespace CommerceOS.Catalog.Application;

/// <summary>Catalog owns filtering of anonymous projections; callers never receive Product entities.</summary>
public interface IPublicCatalogProjectionStore
{
    Task<IReadOnlyList<Product>> ListPublishedAsync(CatalogTenantId tenantId, string? cursor, int pageSize, CancellationToken cancellationToken);
    Task<Product?> GetPublishedBySlugAsync(CatalogTenantId tenantId, string slug, CancellationToken cancellationToken);
    Task<Product?> GetPublishedAsync(CatalogTenantId tenantId, ProductId productId, CancellationToken cancellationToken);
}

/// <summary>
/// Catalog owns the names associated with product reference ids. This narrow
/// projection avoids anonymous delivery reading reference persistence directly.
/// </summary>
public interface IPublicCatalogReferenceProjectionStore
{
    Task<CatalogReferenceLabels> GetLabelsAsync(CatalogTenantId tenantId, CategoryId? categoryId, BrandId? brandId, CancellationToken cancellationToken);
}

public sealed record CatalogReferenceLabels(string? CategoryName, string? BrandName);

public sealed class PublicCatalogQueryService(IPublicCatalogProjectionStore store, IPublicCatalogReferenceProjectionStore references) : IPublicCatalogQuery
{
    public async Task<PublicCatalogPage> ListAsync(string trustedTenantId, string? search, string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        var products = await store.ListPublishedAsync(new(trustedTenantId), cursor, Math.Clamp(pageSize, 1, 50), cancellationToken);
        var filtered = (await Task.WhenAll(products.Where(x => string.IsNullOrWhiteSpace(search) || x.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).Select(x => MapAsync(x, cancellationToken)))).OfType<PublicCatalogProduct>().ToArray();
        return new(filtered, products.Count == pageSize ? products[^1].Id.Value : null);
    }
    public async Task<PublicCatalogProduct?> GetBySlugAsync(string trustedTenantId, string slug, CancellationToken cancellationToken) => await MapAsync(await store.GetPublishedBySlugAsync(new(trustedTenantId), Product.Normalize(slug), cancellationToken), cancellationToken);
    public async Task<PublicCatalogProduct?> GetSellableAsync(string trustedTenantId, string productId, CancellationToken cancellationToken) => await MapAsync(await store.GetPublishedAsync(new(trustedTenantId), new(productId), cancellationToken), cancellationToken);

    private async Task<PublicCatalogProduct?> MapAsync(Product? product, CancellationToken cancellationToken)
    {
        if (product is not { Status: ProductStatus.Published, BasePrice: not null, Slug: not null } p) return null;
        var labels = await references.GetLabelsAsync(p.TenantId, p.CategoryId, p.BrandId, cancellationToken);
        return new PublicCatalogProduct(
            p.Id.Value, p.Slug, p.Name, p.Sku, p.BasePrice.Amount, p.BasePrice.Currency,
            CategoryName: labels.CategoryName,
            BrandName: labels.BrandName,
            Specifications: p.Specifications.OrderBy(x => x.DisplayOrder).Select(x => new PublicCatalogSpecification(x.Name, x.Value, x.Unit, x.DisplayOrder)).ToArray(),
            Media: p.Media.OrderBy(x => x.DisplayOrder).Select(x => new PublicCatalogMedia(x.AssetId, x.AltText, x.DisplayOrder)).ToArray());
    }
}
