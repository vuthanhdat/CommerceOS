using CommerceOS.Catalog.Contracts;
using CommerceOS.Inventory.Contracts;
using CommerceOS.Sales.Contracts;
using CommerceOS.Tenancy.Contracts;

namespace CommerceOS.Storefront.Application;

public sealed record CartLineIntent(string ProductId, long Quantity, long EstimatedUnitPriceVnd);
public sealed record CheckoutIntent(string StorefrontSlug, IReadOnlyList<CartLineIntent> Lines, long EstimatedTotalVnd, bool Reconfirmed, string IdempotencyKey, GuestCheckoutData Guest);
public enum CheckoutValidationOutcome { Validated, ReconfirmationRequired, Invalid }
public sealed record CheckoutValidationResult(CheckoutValidationOutcome Outcome, string? Code, IReadOnlyList<ValidatedCheckoutLine> Lines, long TotalVnd, string? TenantId);

public sealed class StorefrontCheckoutService(IPublicTenantResolver tenants, IPublicCatalogQuery catalog, IInventoryAvailabilityQuery inventory, ISalesOrderPlacement sales)
{
    public async Task<PublicCatalogPage?> ListProductsAsync(string storefrontSlug, string? search, string? cursor, int pageSize, string correlationId, CancellationToken ct)
    {
        var tenant = await tenants.ResolveActiveAsync(storefrontSlug, correlationId, ct);
        return tenant is null ? null : await catalog.ListAsync(tenant.TenantId, search, cursor, Math.Clamp(pageSize, 1, 50), ct);
    }

    public async Task<PublicCatalogProduct?> GetProductAsync(string storefrontSlug, string slug, string correlationId, CancellationToken ct)
    {
        var tenant = await tenants.ResolveActiveAsync(storefrontSlug, correlationId, ct);
        return tenant is null ? null : await catalog.GetBySlugAsync(tenant.TenantId, slug, ct);
    }

    public async Task<CheckoutValidationResult> ValidateAsync(CheckoutIntent intent, string correlationId, CancellationToken ct)
    {
        if (intent.Lines.Count is 0 or > 50 || intent.Lines.Any(x => string.IsNullOrWhiteSpace(x.ProductId) || x.Quantity <= 0 || x.EstimatedUnitPriceVnd < 0)) return Invalid();
        var tenant = await tenants.ResolveActiveAsync(intent.StorefrontSlug, correlationId, ct);
        if (tenant is null) return Invalid();
        var validated = new List<ValidatedCheckoutLine>();
        foreach (var line in intent.Lines)
        {
            var product = await catalog.GetSellableAsync(tenant.TenantId, line.ProductId, ct);
            var availability = await inventory.GetAvailabilityAsync(tenant.TenantId, line.ProductId, ct);
            if (product is null || availability.AvailableQuantity < line.Quantity) return Invalid();
            validated.Add(new ValidatedCheckoutLine(product.ProductId, product.Sku ?? string.Empty, product.Name, line.Quantity, product.UnitPriceVnd, product.Currency));
        }
        var total = validated.Sum(x => checked(x.Quantity * x.UnitPriceVnd));
        if (intent.EstimatedTotalVnd != total || intent.Lines.Zip(validated).Any(x => x.First.EstimatedUnitPriceVnd != x.Second.UnitPriceVnd))
            return new(CheckoutValidationOutcome.ReconfirmationRequired, "CHECKOUT_RECONFIRMATION_REQUIRED", validated, total, tenant.TenantId);
        return new(CheckoutValidationOutcome.Validated, null, validated, total, tenant.TenantId);
    }

    public async Task<OrderPlacementResult> PlaceAsync(CheckoutIntent intent, string correlationId, CancellationToken ct)
    {
        if (!intent.Reconfirmed) return new(OrderPlacementOutcome.Invalid, null, null);
        var validation = await ValidateAsync(intent, correlationId, ct);
        if (validation.Outcome is not CheckoutValidationOutcome.Validated || validation.TenantId is null) return new(OrderPlacementOutcome.Invalid, null, null);
        return await sales.PlaceAsync(new PlaceAcceptedOrder(validation.TenantId, intent.IdempotencyKey, validation.Lines, validation.TotalVnd, intent.Guest, correlationId), ct);
    }

    private static CheckoutValidationResult Invalid() => new(CheckoutValidationOutcome.Invalid, "CHECKOUT_INVALID", [], 0, null);
}
