using CommerceOS.Catalog.Contracts;
using CommerceOS.Inventory.Contracts;
using CommerceOS.Sales.Contracts;
using CommerceOS.Storefront.Application;
using CommerceOS.Tenancy.Contracts;

namespace CommerceOS.Storefront.UnitTests;

public sealed class StorefrontCheckoutTests
{
    [Fact]
    public async Task PriceChangeRequiresReconfirmationAndNeverUsesBrowserTenant()
    {
        var service = Service(active: true, price: 11, available: 2); var intent = Intent() with { EstimatedTotalVnd = 10 };
        var result = await service.ValidateAsync(intent, "c", default);
        Assert.Equal(CheckoutValidationOutcome.ReconfirmationRequired, result.Outcome);
        Assert.Equal("tenant-a", result.TenantId);
    }
    [Fact]
    public async Task SuspendedTenantAndInsufficientStockAreNonDisclosingInvalidResults()
    {
        Assert.Equal(CheckoutValidationOutcome.Invalid, (await Service(false, 10, 2).ValidateAsync(Intent(), "c", default)).Outcome);
        Assert.Equal(CheckoutValidationOutcome.Invalid, (await Service(true, 10, 0).ValidateAsync(Intent(), "c", default)).Outcome);
    }
    private static CheckoutIntent Intent() => new("store-a", [new("product-a", 1, 10)], 10, false, "key", new("Guest", "g@example.test", null, null));
    private static StorefrontCheckoutService Service(bool active, long price, long available) => new(new Tenant(active), new Catalog(price), new Inventory(available), new Sales());
    private sealed class Tenant(bool active) : IPublicTenantResolver { public Task<PublicTenantContext?> ResolveActiveAsync(string slug, string c, CancellationToken ct) => Task.FromResult<PublicTenantContext?>(active ? new("tenant-a", slug, c) : null); }
    private sealed class Catalog(long price) : IPublicCatalogQuery { private readonly PublicCatalogProduct _product = new("product-a", "tea", "Tea", "SKU", price, "VND"); public Task<PublicCatalogPage> ListAsync(string tenant, string? search, string? cursor, int size, CancellationToken ct) => Task.FromResult(new PublicCatalogPage([_product], null)); public Task<PublicCatalogProduct?> GetBySlugAsync(string tenant, string slug, CancellationToken ct) => Task.FromResult<PublicCatalogProduct?>(_product); public Task<PublicCatalogProduct?> GetSellableAsync(string tenant, string product, CancellationToken ct) => Task.FromResult<PublicCatalogProduct?>(_product); }
    private sealed class Inventory(long available) : IInventoryAvailabilityQuery { public Task<ProductAvailability> GetAvailabilityAsync(string tenant, string product, CancellationToken ct) => Task.FromResult(new ProductAvailability(product, available)); }
    private sealed class Sales : ISalesOrderPlacement { public Task<OrderPlacementResult> PlaceAsync(PlaceAcceptedOrder command, CancellationToken ct) => Task.FromResult(new OrderPlacementResult(OrderPlacementOutcome.Accepted, "order", "Placed")); }
}
