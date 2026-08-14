using CommerceOS.Catalog.Contracts;
using CommerceOS.Inventory.Contracts;
using CommerceOS.Pricing.Contracts;
using CommerceOS.Sales.Contracts;
using CommerceOS.Tenancy.Contracts;

namespace CommerceOS.Storefront.Application;

public sealed record CartLineIntent(string ProductId, long Quantity, long EstimatedUnitPriceVnd);
public sealed record CheckoutIntent(string StorefrontSlug, IReadOnlyList<CartLineIntent> Lines, long EstimatedTotalVnd, bool Reconfirmed, string IdempotencyKey, GuestCheckoutData Guest);
public enum CheckoutValidationOutcome { Validated, ReconfirmationRequired, Invalid }
public sealed record CheckoutValidationResult(CheckoutValidationOutcome Outcome, string? Code, IReadOnlyList<ValidatedCheckoutLine> Lines, long TotalVnd, string? TenantId);

public sealed class StorefrontCheckoutService(IPublicTenantResolver tenants, IPublicCatalogQuery catalog, IInventoryAvailabilityQuery inventory, ISalesOrderPlacement sales, IEffectivePriceQuery pricing)
{
    public async Task<PublicCatalogPage?> ListProductsAsync(string storefrontSlug, string? search, string? cursor, int pageSize, string correlationId, CancellationToken ct)
    {
        var tenant = await tenants.ResolveActiveAsync(storefrontSlug, correlationId, ct);
        if (tenant is null) return null;
        var page = await catalog.ListAsync(tenant.TenantId, search, cursor, Math.Clamp(pageSize, 1, 50), ct);
        var products = await Task.WhenAll(page.Items.Select(x => ApplyPublicPriceAsync(tenant.TenantId, x, correlationId, ct)));
        return new PublicCatalogPage(products, page.NextCursor);
    }

    public async Task<PublicCatalogProduct?> GetProductAsync(string storefrontSlug, string slug, string correlationId, CancellationToken ct)
    {
        var tenant = await tenants.ResolveActiveAsync(storefrontSlug, correlationId, ct);
        if (tenant is null) return null;
        var product = await catalog.GetBySlugAsync(tenant.TenantId, slug, ct);
        return product is null ? null : await ApplyPublicPriceAsync(tenant.TenantId, product, correlationId, ct);
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
            var price = product is null ? null : await pricing.GetEffectivePriceAsync(tenant.TenantId, line.ProductId, DateTimeOffset.UtcNow, correlationId, ct);
            if (product is null || price is null || availability.AvailableQuantity < line.Quantity) return Invalid();
            validated.Add(new ValidatedCheckoutLine(product.ProductId, product.Sku ?? string.Empty, product.Name, line.Quantity, price.EffectiveUnitPriceVnd, product.Currency, price.BaseUnitPriceVnd, price.PromotionId, price.AppliedPromotionalUnitPriceVnd, price.EvaluatedAt));
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

    private async Task<PublicCatalogProduct> ApplyPublicPriceAsync(string tenantId, PublicCatalogProduct product, string correlationId, CancellationToken ct)
    {
        var price = await pricing.GetEffectivePriceAsync(tenantId, product.ProductId, DateTimeOffset.UtcNow, correlationId, ct);
        return price is { HasAppliedPromotion: true } ? product with { EffectiveUnitPriceVnd = price.EffectiveUnitPriceVnd, AppliedPromotionId = price.PromotionId, PromotionEffectiveUntil = price.PromotionEffectiveUntil } : product with { EffectiveUnitPriceVnd = product.UnitPriceVnd, AppliedPromotionId = null, PromotionEffectiveUntil = null };
    }
}
