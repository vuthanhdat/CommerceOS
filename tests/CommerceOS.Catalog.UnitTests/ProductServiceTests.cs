using CommerceOS.Catalog.Application;
using CommerceOS.Catalog.Domain;

namespace CommerceOS.Catalog.UnitTests;

public sealed class ProductServiceTests
{
    [Fact]
    public void SpecificationsAreUniqueByNormalizedNameAndKeepDisplayOrder()
    {
        var product = Product.Draft(new("product"), new("tenant"), "Tea", null, null);
        var updated = product.SetSpecifications([new("Size", "Large", null, 2), new("Color", "Green", null, 1)], 1);
        Assert.Equal(["Color", "Size"], updated.Specifications.Select(x => x.Name));
        Assert.Throws<ProductRuleException>(() => product.SetSpecifications([new("Size", "L", null, 1), new(" size ", "M", null, 2)], 1));
    }
    [Fact]
    public async Task DraftCanOmitSkuButPublishingRequiresSkuAndVndMoney()
    {
        var store = new InMemoryStore(); var service = new ProductService(store); var context = Context(); var product = Product.Draft(new("product-1"), context.TenantId, "Tea", null, null);
        Assert.Equal(CatalogOutcome.Applied, await service.CreateDraftAsync(context, product, default));
        Assert.Equal(CatalogOutcome.InvalidState, await service.PublishAsync(context, product.Id, 1, default));
        Assert.Equal(CatalogOutcome.Applied, await service.ChangeAsync(context, product.Id, "Tea", "TEA-1", null, new Money(0, "VND"), 1, default));
        Assert.Equal(CatalogOutcome.Applied, await service.PublishAsync(context, product.Id, 2, default));
    }

    [Fact]
    public async Task SkuIsTenantUniqueAndImmutableAfterFirstPublicationWhileSlugMayChange()
    {
        var store = new InMemoryStore(); var service = new ProductService(store); var context = Context();
        var one = Product.Draft(new("product-1"), context.TenantId, "Tea one", "TEA", new Money(0, "VND")); var two = Product.Draft(new("product-2"), context.TenantId, "Tea two", "tea", new Money(1, "VND"));
        Assert.Equal(CatalogOutcome.Applied, await service.CreateDraftAsync(context, one, default));
        Assert.Equal(CatalogOutcome.SkuConflict, await service.CreateDraftAsync(context, two, default));
        Assert.Equal(CatalogOutcome.Applied, await service.PublishAsync(context, one.Id, 1, default));
        Assert.Equal(CatalogOutcome.InvalidState, await service.ChangeAsync(context, one.Id, "Tea one", "OTHER", "other", new Money(0, "VND"), 2, default));
        Assert.Equal(CatalogOutcome.Applied, await service.ChangeAsync(context, one.Id, "Tea one", "TEA", "new-tea", new Money(0, "VND"), 2, default));
    }

    [Fact]
    public async Task ArchivedProductIsTerminalAndTenantIdsCannotReadAnotherTenant()
    {
        var store = new InMemoryStore(); var service = new ProductService(store); var context = Context(); var product = Product.Draft(new("product-1"), context.TenantId, "Tea", "TEA", new Money(0, "VND"));
        await service.CreateDraftAsync(context, product, default); Assert.Equal(CatalogOutcome.Applied, await service.ArchiveAsync(context, product.Id, 1, default));
        Assert.Equal(CatalogOutcome.InvalidState, await service.PublishAsync(context, product.Id, 2, default));
        Assert.Null(await store.GetAsync(new(new("tenant-b"), "c"), product.Id, default));
    }

    private static TrustedCatalogMutationContext Context() => new(new("tenant-a"), "correlation");
    private sealed class InMemoryStore : ICatalogStore
    {
        private readonly Dictionary<(CatalogTenantId, ProductId), Product> _items = []; private readonly Dictionary<(CatalogTenantId, string), ProductId> _claims = [];
        public Task<Product?> GetAsync(TrustedCatalogMutationContext c, ProductId id, CancellationToken ct) => Task.FromResult(_items.GetValueOrDefault((c.TenantId, id)));
        public Task<CatalogOutcome> CreateAsync(TrustedCatalogMutationContext c, Product p, CancellationToken ct) { if (_items.ContainsKey((c.TenantId, p.Id))) return Task.FromResult(CatalogOutcome.RevisionConflict); if (!Claim(c.TenantId, "SKU", p.Sku, p.Id) || !Claim(c.TenantId, "SLUG", p.Slug, p.Id)) return Task.FromResult(CatalogOutcome.SkuConflict); _items.Add((c.TenantId, p.Id), p); return Task.FromResult(CatalogOutcome.Applied); }
        public Task<CatalogOutcome> SaveWithClaimsAsync(TrustedCatalogMutationContext c, Product before, Product after, CancellationToken ct) { if (!_items.TryGetValue((c.TenantId, before.Id), out var current) || current.Revision != before.Revision) return Task.FromResult(CatalogOutcome.RevisionConflict); if (!Claim(c.TenantId, "SKU", after.Sku, after.Id) || !Claim(c.TenantId, "SLUG", after.Slug, after.Id)) return Task.FromResult(CatalogOutcome.SkuConflict); if (!string.Equals(before.Slug, after.Slug, StringComparison.OrdinalIgnoreCase) && before.Slug is not null) _claims.Remove((c.TenantId, $"SLUG:{Product.Normalize(before.Slug)}")); _items[(c.TenantId, before.Id)] = after; return Task.FromResult(CatalogOutcome.Applied); }
        private bool Claim(CatalogTenantId tenant, string type, string? value, ProductId product) { if (value is null) return true; var key = (tenant, $"{type}:{Product.Normalize(value)}"); return !_claims.TryGetValue(key, out var owner) || owner == product ? (_claims[key] = product) == product : false; }
    }
}
