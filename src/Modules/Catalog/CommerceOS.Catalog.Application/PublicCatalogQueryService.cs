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
public sealed class PublicCatalogQueryService(IPublicCatalogProjectionStore store) : IPublicCatalogQuery
{
    public async Task<PublicCatalogPage> ListAsync(string trustedTenantId, string? search, string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        var products = await store.ListPublishedAsync(new(trustedTenantId), cursor, Math.Clamp(pageSize, 1, 50), cancellationToken);
        var filtered = products.Where(x => string.IsNullOrWhiteSpace(search) || x.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).Select(Map).OfType<PublicCatalogProduct>().ToArray();
        return new(filtered, products.Count == pageSize ? products[^1].Id.Value : null);
    }
    public async Task<PublicCatalogProduct?> GetBySlugAsync(string trustedTenantId, string slug, CancellationToken cancellationToken) => Map(await store.GetPublishedBySlugAsync(new(trustedTenantId), Product.Normalize(slug), cancellationToken));
    public async Task<PublicCatalogProduct?> GetSellableAsync(string trustedTenantId, string productId, CancellationToken cancellationToken) => Map(await store.GetPublishedAsync(new(trustedTenantId), new(productId), cancellationToken));
    private static PublicCatalogProduct? Map(Product? product) => product is { Status: ProductStatus.Published, BasePrice: not null, Slug: not null } p ? new(p.Id.Value, p.Slug, p.Name, p.Sku, p.BasePrice.Amount, p.BasePrice.Currency) : null;
}
