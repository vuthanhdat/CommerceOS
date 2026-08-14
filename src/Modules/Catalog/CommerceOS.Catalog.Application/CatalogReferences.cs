using CommerceOS.Catalog.Domain;

namespace CommerceOS.Catalog.Application;

public enum CatalogReferenceOutcome { Applied, NotFound, RevisionConflict, NameConflict, ReferenceInvalid }
public interface ICatalogReferenceStore
{
    Task<Category?> GetCategoryAsync(TrustedCatalogMutationContext context, CategoryId id, CancellationToken cancellationToken);
    Task<Brand?> GetBrandAsync(TrustedCatalogMutationContext context, BrandId id, CancellationToken cancellationToken);
    Task<CatalogReferenceOutcome> SaveCategoryAsync(TrustedCatalogMutationContext context, Category category, long? expectedRevision, CancellationToken cancellationToken);
    Task<CatalogReferenceOutcome> SaveBrandAsync(TrustedCatalogMutationContext context, Brand brand, long? expectedRevision, CancellationToken cancellationToken);
}

public sealed class CatalogReferenceService(ICatalogReferenceStore store)
{
    public Task<CatalogReferenceOutcome> CreateCategoryAsync(TrustedCatalogMutationContext context, Category category, CancellationToken cancellationToken) =>
        category.TenantId != context.TenantId ? Task.FromResult(CatalogReferenceOutcome.ReferenceInvalid) : store.SaveCategoryAsync(context, category, null, cancellationToken);
    public Task<CatalogReferenceOutcome> CreateBrandAsync(TrustedCatalogMutationContext context, Brand brand, CancellationToken cancellationToken) =>
        brand.TenantId != context.TenantId ? Task.FromResult(CatalogReferenceOutcome.ReferenceInvalid) : store.SaveBrandAsync(context, brand, null, cancellationToken);
    public async Task<CatalogReferenceOutcome> RetireCategoryAsync(TrustedCatalogMutationContext context, CategoryId id, long revision, CancellationToken cancellationToken)
    {
        var category = await store.GetCategoryAsync(context, id, cancellationToken);
        return category is null ? CatalogReferenceOutcome.NotFound : category.Revision != revision ? CatalogReferenceOutcome.RevisionConflict : await store.SaveCategoryAsync(context, category with { Status = CatalogReferenceStatus.Retired, Revision = revision + 1 }, revision, cancellationToken);
    }
    public async Task<CatalogReferenceOutcome> RetireBrandAsync(TrustedCatalogMutationContext context, BrandId id, long revision, CancellationToken cancellationToken)
    {
        var brand = await store.GetBrandAsync(context, id, cancellationToken);
        return brand is null ? CatalogReferenceOutcome.NotFound : brand.Revision != revision ? CatalogReferenceOutcome.RevisionConflict : await store.SaveBrandAsync(context, brand with { Status = CatalogReferenceStatus.Retired, Revision = revision + 1 }, revision, cancellationToken);
    }
}
