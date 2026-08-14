using CommerceOS.Catalog.Domain;

namespace CommerceOS.Catalog.Application;

/// <summary>Issued at the delivery boundary only after Merchant Access has resolved mutation authority.</summary>
public sealed record TrustedCatalogMutationContext(CatalogTenantId TenantId, string CorrelationId);
public enum CatalogOutcome { Applied, NotFound, RevisionConflict, SkuConflict, SlugConflict, InvalidState }

public interface ICatalogStore
{
    Task<Product?> GetAsync(TrustedCatalogMutationContext context, ProductId id, CancellationToken ct);
    Task<CatalogOutcome> CreateAsync(TrustedCatalogMutationContext context, Product product, CancellationToken ct);
    Task<CatalogOutcome> SaveWithClaimsAsync(TrustedCatalogMutationContext context, Product before, Product after, CancellationToken ct);
}

public sealed class ProductService(ICatalogStore store)
{
    public Task<CatalogOutcome> CreateDraftAsync(TrustedCatalogMutationContext context, Product product, CancellationToken cancellationToken)
    {
        if (context.TenantId != product.TenantId) throw new ArgumentException("Product tenant must match trusted context.", nameof(product));
        return store.CreateAsync(context, product, cancellationToken);
    }

    public async Task<CatalogOutcome> ChangeAsync(TrustedCatalogMutationContext context, ProductId id, string name, string? sku, string? slug, Money? price, long expectedRevision, CancellationToken cancellationToken)
    {
        var before = await store.GetAsync(context, id, cancellationToken);
        if (before is null) return CatalogOutcome.NotFound;
        try { return await store.SaveWithClaimsAsync(context, before, before.Change(name, sku, slug, price, expectedRevision), cancellationToken); }
        catch (ProductRuleException exception) { return Map(exception.Rule); }
    }

    public Task<CatalogOutcome> PublishAsync(TrustedCatalogMutationContext context, ProductId id, long revision, CancellationToken ct) => TransitionAsync(context, id, p =>
    {
        var published = p.Publish(revision);
        return published.Slug is null ? published with { Slug = Product.Normalize(published.Name) } : published;
    }, ct);
    public Task<CatalogOutcome> UnpublishAsync(TrustedCatalogMutationContext context, ProductId id, long revision, CancellationToken ct) => TransitionAsync(context, id, p => p.Unpublish(revision), ct);
    public Task<CatalogOutcome> ArchiveAsync(TrustedCatalogMutationContext context, ProductId id, long revision, CancellationToken ct) => TransitionAsync(context, id, p => p.Archive(revision), ct);

    private async Task<CatalogOutcome> TransitionAsync(TrustedCatalogMutationContext context, ProductId id, Func<Product, Product> transition, CancellationToken ct)
    {
        var before = await store.GetAsync(context, id, ct); if (before is null) return CatalogOutcome.NotFound;
        try { return await store.SaveWithClaimsAsync(context, before, transition(before), ct); }
        catch (ProductRuleException exception) { return Map(exception.Rule); }
    }
    private static CatalogOutcome Map(ProductRule rule) => rule switch { ProductRule.StaleRevision => CatalogOutcome.RevisionConflict, ProductRule.SkuImmutable => CatalogOutcome.InvalidState, _ => CatalogOutcome.InvalidState };
}
