using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.Catalog.Application;
using CommerceOS.Catalog.Contracts;
using CommerceOS.Catalog.Infrastructure.Persistence;
using CommerceOS.Inventory.Application;
using CommerceOS.Inventory.Contracts;
using CommerceOS.Inventory.Infrastructure.Persistence;
using CommerceOS.Pricing.Application;
using CommerceOS.Pricing.Contracts;
using CommerceOS.Pricing.Infrastructure.Persistence;
using CommerceOS.Sales.Application;
using CommerceOS.Sales.Contracts;
using CommerceOS.Sales.Infrastructure.Persistence;
using CommerceOS.Storefront.Application;
using CommerceOS.Tenancy.Application;
using CommerceOS.Tenancy.Contracts;
using CommerceOS.Tenancy.Infrastructure.Persistence;

namespace CommerceOS.Api;

public sealed record StorefrontCartLineRequest(string ProductId, long Quantity, long EstimatedUnitPriceVnd);
public sealed record StorefrontGuestRequest(string Name, string Email, string? Phone, string? Address);
public sealed record StorefrontCheckoutRequest(IReadOnlyList<StorefrontCartLineRequest> Lines, long EstimatedTotalVnd, bool Reconfirmed, StorefrontGuestRequest Guest);
public sealed record StorefrontMoneyResponse(long Amount, string Currency);
public sealed record StorefrontProductSpecificationResponse(string Name, string Value, string? Unit, int DisplayOrder);
public sealed record StorefrontProductMediaResponse(string AssetId, string AltText, int DisplayOrder);
public sealed record StorefrontProductResponse(string ProductId, string Slug, string Name, string? Sku, StorefrontMoneyResponse BasePrice, StorefrontMoneyResponse EffectivePrice, string? PromotionId, DateTimeOffset? PromotionEffectiveUntil, long AvailableQuantity, string? CategoryName, string? BrandName, IReadOnlyList<StorefrontProductSpecificationResponse> Specifications, IReadOnlyList<StorefrontProductMediaResponse> Media);
public sealed record StorefrontCheckoutResponse(string Code, IReadOnlyList<ValidatedCheckoutLine> Lines, long TotalVnd, string Currency);
public sealed record StorefrontOrderConfirmationResponse(string OrderId, string Status, IReadOnlyList<ValidatedCheckoutLine> Lines, long TotalVnd, string Currency);

public static class StorefrontDeliveryEndpoints
{
    public static void AddStorefrontDeliveryServices(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["COMMERCEOS_LOCALSTACK_ENDPOINT"];
        var tenancy = configuration["COMMERCEOS_TENANCY_TABLE"];
        var catalog = configuration["COMMERCEOS_CATALOG_TABLE"];
        var inventory = configuration["COMMERCEOS_INVENTORY_TABLE"];
        var pricing = configuration["COMMERCEOS_PRICING_TABLE"];
        var sales = configuration["COMMERCEOS_SALES_TABLE"];
        if (new[] { endpoint, tenancy, catalog, inventory, pricing, sales }.Any(string.IsNullOrWhiteSpace)) return;

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" }));
        services.AddSingleton(new DynamoDbTenancyOptions(tenancy!));
        services.AddSingleton<DynamoDbTenancyStore>();
        services.AddSingleton<IPublicTenantDirectoryStore>(provider => provider.GetRequiredService<DynamoDbTenancyStore>());
        services.AddSingleton<IPublicTenantResolver, PublicTenantDirectory>();
        services.AddSingleton(new DynamoDbCatalogOptions(catalog!));
        services.AddSingleton<DynamoDbCatalogStore>();
        services.AddSingleton<IPublicCatalogProjectionStore>(provider => provider.GetRequiredService<DynamoDbCatalogStore>());
        services.AddSingleton<IPublicCatalogReferenceProjectionStore>(provider => provider.GetRequiredService<DynamoDbCatalogStore>());
        services.AddSingleton<IPublicCatalogQuery, PublicCatalogQueryService>();
        services.AddSingleton(new DynamoDbInventoryOptions(inventory!));
        services.AddSingleton<DynamoDbInventoryStore>();
        services.AddSingleton<IInventoryStore>(provider => provider.GetRequiredService<DynamoDbInventoryStore>());
        services.AddSingleton<IStockOperationStore>(provider => provider.GetRequiredService<DynamoDbInventoryStore>());
        services.AddSingleton<IStockAvailabilityStore>(provider => provider.GetRequiredService<DynamoDbInventoryStore>());
        services.AddSingleton<IInventoryAvailabilityQuery, InventoryAvailabilityQuery>();
        services.AddSingleton<WarehouseService>();
        services.AddSingleton<StockOperationService>();
        services.AddSingleton(new DynamoDbPricingOptions(pricing!));
        services.AddSingleton<IPromotionStore, DynamoDbPromotionStore>();
        services.AddSingleton<IEffectivePriceQuery, EffectivePriceQueryService>();
        services.AddSingleton(new DynamoDbSalesOptions(sales!));
        services.AddSingleton<DynamoDbSalesOrderStore>();
        services.AddSingleton<ISalesOrderStore>(provider => provider.GetRequiredService<DynamoDbSalesOrderStore>());
        services.AddSingleton<IRefundStore>(provider => provider.GetRequiredService<DynamoDbSalesOrderStore>());
        services.AddSingleton<SalesOrderService>();
        services.AddSingleton<ISalesOrderPlacement>(provider => provider.GetRequiredService<SalesOrderService>());
        services.AddSingleton<RefundReviewService>();
        services.AddSingleton<StorefrontCheckoutService>();
    }

    public static void MapStorefrontDeliveryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/storefronts/{storefrontSlug}", async (string storefrontSlug, HttpContext http, IPublicTenantResolver? tenants, CancellationToken ct) =>
        {
            if (tenants is null) return Unavailable(http);
            var tenant = await tenants.ResolveActiveAsync(storefrontSlug, http.TraceIdentifier, ct);
            return tenant is null ? Results.NotFound() : Results.Ok(new { slug = tenant.StorefrontSlug, displayName = tenant.DisplayName });
        }).AllowAnonymous();

        app.MapGet("/api/v1/storefronts/{storefrontSlug}/products", async (string storefrontSlug, string? search, string? cursor, int? pageSize, HttpContext http, StorefrontCheckoutService? storefront, IInventoryAvailabilityQuery? inventory, IPublicTenantResolver? tenants, CancellationToken ct) =>
        {
            if (storefront is null || inventory is null || tenants is null) return Unavailable(http);
            var tenant = await tenants.ResolveActiveAsync(storefrontSlug, http.TraceIdentifier, ct);
            if (tenant is null) return Results.NotFound();
            var page = await storefront.ListProductsAsync(storefrontSlug, search, cursor, pageSize ?? 20, http.TraceIdentifier, ct);
            if (page is null) return Results.NotFound();
            var products = await Task.WhenAll(page.Items.Select(product => MapProduct(product, tenant.TenantId, http.TraceIdentifier, inventory, ct)));
            return Results.Ok(new { items = products, nextCursor = page.NextCursor });
        }).AllowAnonymous();

        app.MapGet("/api/v1/storefronts/{storefrontSlug}/products/{productSlug}", async (string storefrontSlug, string productSlug, HttpContext http, StorefrontCheckoutService? storefront, IInventoryAvailabilityQuery? inventory, IPublicTenantResolver? tenants, CancellationToken ct) =>
        {
            if (storefront is null || inventory is null || tenants is null) return Unavailable(http);
            var tenant = await tenants.ResolveActiveAsync(storefrontSlug, http.TraceIdentifier, ct);
            if (tenant is null) return Results.NotFound();
            var product = await storefront.GetProductAsync(storefrontSlug, productSlug, http.TraceIdentifier, ct);
            return product is null ? Results.NotFound() : Results.Ok(await MapProduct(product, tenant.TenantId, http.TraceIdentifier, inventory, ct));
        }).AllowAnonymous();

        app.MapPost("/api/v1/storefronts/{storefrontSlug}/checkout/validate", async (string storefrontSlug, StorefrontCheckoutRequest request, HttpContext http, StorefrontCheckoutService? storefront, CancellationToken ct) =>
        {
            if (storefront is null) return Unavailable(http);
            var result = await storefront.ValidateAsync(ToIntent(storefrontSlug, request, http.Request.Headers["Idempotency-Key"].ToString()), http.TraceIdentifier, ct);
            return result.Outcome switch
            {
                CheckoutValidationOutcome.Validated => Results.Ok(new StorefrontCheckoutResponse("CHECKOUT_VALIDATED", result.Lines, result.TotalVnd, "VND")),
                CheckoutValidationOutcome.ReconfirmationRequired => Results.Conflict(new StorefrontCheckoutResponse("CHECKOUT_RECONFIRMATION_REQUIRED", result.Lines, result.TotalVnd, "VND")),
                _ => Results.UnprocessableEntity(new { code = "CHECKOUT_INVALID" })
            };
        }).AllowAnonymous();

        app.MapPost("/api/v1/storefronts/{storefrontSlug}/orders", async (string storefrontSlug, StorefrontCheckoutRequest request, HttpContext http, StorefrontCheckoutService? storefront, CancellationToken ct) =>
        {
            if (storefront is null) return Unavailable(http);
            var key = http.Request.Headers["Idempotency-Key"].ToString();
            if (string.IsNullOrWhiteSpace(key)) return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "IDEMPOTENCY_KEY_REQUIRED", extensions: new Dictionary<string, object?> { ["code"] = "IDEMPOTENCY_KEY_REQUIRED", ["correlationId"] = http.TraceIdentifier });
            var intent = ToIntent(storefrontSlug, request, key);
            var validation = await storefront.ValidateAsync(intent, http.TraceIdentifier, ct);
            if (validation.Outcome is CheckoutValidationOutcome.ReconfirmationRequired) return Results.Conflict(new StorefrontCheckoutResponse("CHECKOUT_RECONFIRMATION_REQUIRED", validation.Lines, validation.TotalVnd, "VND"));
            if (validation.Outcome is not CheckoutValidationOutcome.Validated) return Results.UnprocessableEntity(new { code = "CHECKOUT_INVALID" });
            var placed = await storefront.PlaceAsync(intent, http.TraceIdentifier, ct);
            return placed.Outcome switch
            {
                OrderPlacementOutcome.Accepted => Results.Created($"/api/v1/storefronts/{storefrontSlug}/order-confirmation/{placed.OrderId}", new StorefrontOrderConfirmationResponse(placed.OrderId!, placed.Status!, validation.Lines, validation.TotalVnd, "VND")),
                OrderPlacementOutcome.Replayed => Results.Ok(new StorefrontOrderConfirmationResponse(placed.OrderId!, placed.Status!, validation.Lines, validation.TotalVnd, "VND")),
                OrderPlacementOutcome.Conflict => Results.Conflict(new { code = "ORDER_IDEMPOTENCY_CONFLICT" }),
                _ => Results.UnprocessableEntity(new { code = "CHECKOUT_INVALID" })
            };
        }).AllowAnonymous();
    }

    private static CheckoutIntent ToIntent(string storefrontSlug, StorefrontCheckoutRequest request, string idempotencyKey) => new(
        storefrontSlug,
        request.Lines.Select(line => new CartLineIntent(line.ProductId, line.Quantity, line.EstimatedUnitPriceVnd)).ToArray(),
        request.EstimatedTotalVnd,
        request.Reconfirmed,
        idempotencyKey,
        new GuestCheckoutData(request.Guest.Name, request.Guest.Email, request.Guest.Phone, request.Guest.Address));

    private static async Task<StorefrontProductResponse> MapProduct(PublicCatalogProduct product, string tenantId, string correlationId, IInventoryAvailabilityQuery inventory, CancellationToken ct)
    {
        var availability = await inventory.GetAvailabilityAsync(tenantId, product.ProductId, ct);
        var effective = product.EffectiveUnitPriceVnd ?? product.UnitPriceVnd;
        return new StorefrontProductResponse(product.ProductId, product.Slug, product.Name, product.Sku, new(product.UnitPriceVnd, product.Currency), new(effective, product.Currency), product.AppliedPromotionId, product.PromotionEffectiveUntil, availability.AvailableQuantity, product.CategoryName, product.BrandName, (product.Specifications ?? []).Select(x => new StorefrontProductSpecificationResponse(x.Name, x.Value, x.Unit, x.DisplayOrder)).ToArray(), (product.Media ?? []).Select(x => new StorefrontProductMediaResponse(x.AssetId, x.AltText, x.DisplayOrder)).ToArray());
    }

    private static IResult Unavailable(HttpContext http) => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "STOREFRONT_UNAVAILABLE", extensions: new Dictionary<string, object?> { ["code"] = "STOREFRONT_UNAVAILABLE", ["correlationId"] = http.TraceIdentifier });
}
