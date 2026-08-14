namespace CommerceOS.Catalog.Contracts;

/// <summary>Safe, tenant-scoped catalog data intended for anonymous storefront composition.</summary>
public sealed record PublicCatalogProduct(
    string ProductId,
    string Slug,
    string Name,
    string? Sku,
    long UnitPriceVnd,
    string Currency);

public sealed record PublicCatalogPage(IReadOnlyList<PublicCatalogProduct> Items, string? NextCursor);

public interface IPublicCatalogQuery
{
    Task<PublicCatalogPage> ListAsync(string trustedTenantId, string? search, string? cursor, int pageSize, CancellationToken cancellationToken);
    Task<PublicCatalogProduct?> GetBySlugAsync(string trustedTenantId, string slug, CancellationToken cancellationToken);
    Task<PublicCatalogProduct?> GetSellableAsync(string trustedTenantId, string productId, CancellationToken cancellationToken);
}
