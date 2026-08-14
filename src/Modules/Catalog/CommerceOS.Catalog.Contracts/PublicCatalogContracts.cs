namespace CommerceOS.Catalog.Contracts;

/// <summary>Safe, tenant-scoped catalog data intended for anonymous storefront composition.</summary>
public sealed record PublicCatalogProduct(
    string ProductId,
    string Slug,
    string Name,
    string? Sku,
    long UnitPriceVnd,
    string Currency,
    long? EffectiveUnitPriceVnd = null,
    string? AppliedPromotionId = null,
    DateTimeOffset? PromotionEffectiveUntil = null,
    string? CategoryName = null,
    string? BrandName = null,
    IReadOnlyList<PublicCatalogSpecification>? Specifications = null,
    IReadOnlyList<PublicCatalogMedia>? Media = null);

/// <summary>Safe presentation fields from the Catalog-owned product record.</summary>
public sealed record PublicCatalogSpecification(string Name, string Value, string? Unit, int DisplayOrder);
public sealed record PublicCatalogMedia(string AssetId, string AltText, int DisplayOrder);

public sealed record PublicCatalogPage(IReadOnlyList<PublicCatalogProduct> Items, string? NextCursor);

public interface IPublicCatalogQuery
{
    Task<PublicCatalogPage> ListAsync(string trustedTenantId, string? search, string? cursor, int pageSize, CancellationToken cancellationToken);
    Task<PublicCatalogProduct?> GetBySlugAsync(string trustedTenantId, string slug, CancellationToken cancellationToken);
    Task<PublicCatalogProduct?> GetSellableAsync(string trustedTenantId, string productId, CancellationToken cancellationToken);
}
