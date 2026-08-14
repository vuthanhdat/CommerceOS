using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.Catalog.Application;
using CommerceOS.Catalog.Contracts;
using CommerceOS.Catalog.Domain;
using CommerceOS.Catalog.Infrastructure.Persistence;
using CommerceOS.FilesMedia.Contracts;
using CommerceOS.FilesMedia.Infrastructure.Persistence;
using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Api;

public sealed record CatalogProductInput(string Name, string? Sku, string? Slug, long? BasePriceVnd, long? ExpectedRevision = null);
public sealed record CatalogReferencesInput(string? CategoryId, string? BrandId, long ExpectedRevision);
public sealed record CatalogSpecificationsInput(IReadOnlyList<CatalogSpecificationInput> Items, long ExpectedRevision);
public sealed record CatalogSpecificationInput(string Name, string Value, string? Unit, int DisplayOrder);
public sealed record CatalogMediaInput(IReadOnlyList<CatalogMediaItemInput> Items, long ExpectedRevision);
public sealed record CatalogMediaItemInput(string AssetId, string AltText, int DisplayOrder);
public sealed record CatalogRevisionInput(long ExpectedRevision);
public sealed record CatalogReferenceInput(string Name);
public sealed record CatalogReferenceResponse(string Id, string Name, string Status, long Revision);
public sealed record CatalogProductResponse(string Id, string Name, string? Sku, string? Slug, long? BasePriceVnd, string? CategoryId, string? BrandId, string Status, long Revision, IReadOnlyList<CatalogSpecificationInput> Specifications, IReadOnlyList<CatalogMediaItemInput> Media);
public sealed record CatalogProductPageResponse(IReadOnlyList<CatalogProductResponse> Items, string? NextCursor);

public static class MerchantCatalogEndpoints
{
    public static void AddMerchantCatalogServices(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["COMMERCEOS_LOCALSTACK_ENDPOINT"];
        var catalogTable = configuration["COMMERCEOS_CATALOG_TABLE"];
        var filesMediaTable = configuration["COMMERCEOS_FILESMEDIA_TABLE"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(catalogTable)) return;

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" }));
        services.AddSingleton(new DynamoDbCatalogOptions(catalogTable));
        services.AddSingleton<DynamoDbCatalogStore>();
        services.AddSingleton<ICatalogStore>(provider => provider.GetRequiredService<DynamoDbCatalogStore>());
        services.AddSingleton<IMerchantCatalogReadStore>(provider => provider.GetRequiredService<DynamoDbCatalogStore>());
        services.AddSingleton<ICatalogReferenceStore>(provider => provider.GetRequiredService<DynamoDbCatalogStore>());
        services.AddSingleton<ICatalogProductEligibilityQuery>(provider => provider.GetRequiredService<DynamoDbCatalogStore>());
        services.AddSingleton<ProductService>();
        services.AddSingleton<MerchantCatalogQueryService>();
        services.AddSingleton<CatalogReferenceService>();
        if (!string.IsNullOrWhiteSpace(filesMediaTable))
        {
            services.AddSingleton(new DynamoDbFilesMediaOptions(filesMediaTable, configuration["COMMERCEOS_FILESMEDIA_BUCKET"] ?? "files-media"));
            services.AddSingleton<DynamoDbMediaAssetStore>();
            services.AddSingleton<IManagedMediaAssetLookup>(provider => provider.GetRequiredService<DynamoDbMediaAssetStore>());
            services.AddSingleton<ProductMediaService>();
        }
    }

    public static void MapMerchantCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/catalog/products", async (string? search, string? status, string? categoryId, string? brandId, string? cursor, int? pageSize, HttpContext http, IMerchantRequestAuthorityResolver authority, MerchantCatalogQueryService? products, CancellationToken ct) =>
        {
            var scope = await ReadScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (products is null) return Unavailable(http);
            if (!Enum.TryParse<ProductStatus>(status, true, out var parsedStatus) && !string.IsNullOrWhiteSpace(status)) return Problem(400, "CATALOG_FILTER_INVALID", http.TraceIdentifier);
            var page = await products.ListAsync(scope.Context!, search, string.IsNullOrWhiteSpace(status) ? null : parsedStatus, OptionalCategory(categoryId), OptionalBrand(brandId), cursor, pageSize ?? 20, ct);
            return Results.Ok(new CatalogProductPageResponse(page.Items.Select(Map).ToArray(), page.NextCursor));
        });

        app.MapGet("/api/v1/catalog/products/{id}", async (string id, HttpContext http, IMerchantRequestAuthorityResolver authority, ICatalogStore? products, CancellationToken ct) =>
        {
            var scope = await ReadScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (products is null) return Unavailable(http);
            var product = await products.GetAsync(scope.Context!, new ProductId(id), ct);
            return product is null ? Results.NotFound() : Results.Ok(Map(product));
        });

        app.MapPost("/api/v1/catalog/products", async (CatalogProductInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, ProductService? products, CancellationToken ct) =>
        {
            var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (products is null) return Unavailable(http);
            try
            {
                var product = Product.Draft(new ProductId(Guid.NewGuid().ToString("N")), scope.Context!.TenantId, input.Name, input.Sku, input.BasePriceVnd is null ? null : new Money(input.BasePriceVnd.Value, "VND"));
                var outcome = await products.CreateDraftAsync(scope.Context, product, ct);
                return Outcome(outcome, http.TraceIdentifier, Results.Created($"/api/v1/catalog/products/{product.Id.Value}", Map(product)));
            }
            catch (Exception exception) when (exception is ArgumentException or ProductRuleException) { return Problem(422, "CATALOG_VALIDATION_FAILED", http.TraceIdentifier); }
        });

        app.MapPatch("/api/v1/catalog/products/{id}", async (string id, CatalogProductInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, ProductService? products, CancellationToken ct) =>
        {
            var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (products is null) return Unavailable(http);
            try { return input.ExpectedRevision is null ? Problem(422, "REVISION_REQUIRED", http.TraceIdentifier) : Outcome(await products.ChangeAsync(scope.Context!, new ProductId(id), input.Name, input.Sku, input.Slug, input.BasePriceVnd is null ? null : new Money(input.BasePriceVnd.Value, "VND"), input.ExpectedRevision.Value, ct), http.TraceIdentifier); }
            catch (Exception exception) when (exception is ArgumentException or ProductRuleException) { return Problem(422, "CATALOG_VALIDATION_FAILED", http.TraceIdentifier); }
        });

        MapAction(app, "publish", (service, context, id, revision, ct) => service.PublishAsync(context, id, revision, ct));
        MapAction(app, "unpublish", (service, context, id, revision, ct) => service.UnpublishAsync(context, id, revision, ct));
        MapAction(app, "archive", (service, context, id, revision, ct) => service.ArchiveAsync(context, id, revision, ct));

        app.MapPut("/api/v1/catalog/products/{id}/references", async (string id, CatalogReferencesInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, ProductService? products, ICatalogReferenceStore? references, CancellationToken ct) =>
        {
            var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (products is null || references is null) return Unavailable(http);
            var category = OptionalCategory(input.CategoryId); var brand = OptionalBrand(input.BrandId);
            if (category is not null && (await references.GetCategoryAsync(scope.Context!, category.Value, ct)) is not { Status: CatalogReferenceStatus.Active }) return Problem(422, "CATEGORY_INVALID", http.TraceIdentifier);
            if (brand is not null && (await references.GetBrandAsync(scope.Context!, brand.Value, ct)) is not { Status: CatalogReferenceStatus.Active }) return Problem(422, "BRAND_INVALID", http.TraceIdentifier);
            return Outcome(await products.AssignReferencesAsync(scope.Context!, new ProductId(id), category, brand, input.ExpectedRevision, ct), http.TraceIdentifier);
        });

        app.MapPut("/api/v1/catalog/products/{id}/specifications", async (string id, CatalogSpecificationsInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, ProductService? products, CancellationToken ct) =>
        {
            var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (products is null) return Unavailable(http);
            try { return Outcome(await products.SetSpecificationsAsync(scope.Context!, new ProductId(id), input.Items.Select(x => new ProductSpecification(x.Name, x.Value, x.Unit, x.DisplayOrder)).ToArray(), input.ExpectedRevision, ct), http.TraceIdentifier); }
            catch (Exception exception) when (exception is ArgumentException or ProductRuleException) { return Problem(422, "SPECIFICATIONS_INVALID", http.TraceIdentifier); }
        });

        app.MapPut("/api/v1/catalog/products/{id}/media", async (string id, CatalogMediaInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, ProductMediaService? media, CancellationToken ct) =>
        {
            var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (media is null) return Unavailable(http);
            return Outcome(await media.SetMediaAsync(scope.Context!, new ProductId(id), input.Items.Select(x => new ProductMediaAssociation(x.AssetId, x.AltText, x.DisplayOrder)).ToArray(), input.ExpectedRevision, ct), http.TraceIdentifier);
        });

        MapCategoryReferences(app);
        MapBrandReferences(app);
    }

    private static void MapAction(WebApplication app, string action, Func<ProductService, TrustedCatalogMutationContext, ProductId, long, CancellationToken, Task<CatalogOutcome>> execute) => app.MapPost($"/api/v1/catalog/products/{{id}}/{action}", async (string id, CatalogRevisionInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, ProductService? products, CancellationToken ct) =>
    { var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure; if (products is null) return Unavailable(http); return Outcome(await execute(products, scope.Context!, new ProductId(id), input.ExpectedRevision, ct), http.TraceIdentifier); });

    private static void MapCategoryReferences(WebApplication app)
    {
        app.MapGet("/api/v1/catalog/categories", async (HttpContext http, IMerchantRequestAuthorityResolver authority, CatalogReferenceService? references, CancellationToken ct) => { var scope = await ReadScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure; if (references is null) return Unavailable(http); return Results.Ok((await references.ListCategoriesAsync(scope.Context!, ct)).Select(MapReference)); });
        app.MapPost("/api/v1/catalog/categories", async (CatalogReferenceInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, CatalogReferenceService? references, CancellationToken ct) => { var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure; if (references is null || string.IsNullOrWhiteSpace(input.Name)) return Problem(422, "REFERENCE_INVALID", http.TraceIdentifier); return ReferenceOutcome(await references.CreateCategoryAsync(scope.Context!, new Category(new(Guid.NewGuid().ToString("N")), scope.Context!.TenantId, input.Name.Trim(), CatalogReferenceStatus.Active, 1), ct), http.TraceIdentifier); });
        app.MapPost("/api/v1/catalog/categories/{id}/retire", async (string id, CatalogRevisionInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, CatalogReferenceService? references, CancellationToken ct) => { var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure; if (references is null) return Unavailable(http); return ReferenceOutcome(await references.RetireCategoryAsync(scope.Context!, new CategoryId(id), input.ExpectedRevision, ct), http.TraceIdentifier); });
    }
    private static void MapBrandReferences(WebApplication app)
    {
        app.MapGet("/api/v1/catalog/brands", async (HttpContext http, IMerchantRequestAuthorityResolver authority, CatalogReferenceService? references, CancellationToken ct) => { var scope = await ReadScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure; if (references is null) return Unavailable(http); return Results.Ok((await references.ListBrandsAsync(scope.Context!, ct)).Select(MapReference)); });
        app.MapPost("/api/v1/catalog/brands", async (CatalogReferenceInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, CatalogReferenceService? references, CancellationToken ct) => { var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure; if (references is null || string.IsNullOrWhiteSpace(input.Name)) return Problem(422, "REFERENCE_INVALID", http.TraceIdentifier); return ReferenceOutcome(await references.CreateBrandAsync(scope.Context!, new Brand(new(Guid.NewGuid().ToString("N")), scope.Context!.TenantId, input.Name.Trim(), CatalogReferenceStatus.Active, 1), ct), http.TraceIdentifier); });
        app.MapPost("/api/v1/catalog/brands/{id}/retire", async (string id, CatalogRevisionInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, CatalogReferenceService? references, CancellationToken ct) => { var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure; if (references is null) return Unavailable(http); return ReferenceOutcome(await references.RetireBrandAsync(scope.Context!, new BrandId(id), input.ExpectedRevision, ct), http.TraceIdentifier); });
    }

    private static async Task<CatalogScope> ReadScope(IMerchantRequestAuthorityResolver authority, HttpContext http, CancellationToken ct)
    { var result = await authority.ResolveReadAsync(http, ct); if (!result.IsAuthenticated) return new(null, Results.Unauthorized()); if (result.ReadResolution?.Context is null) return new(null, Failure(result.ReadResolution?.Failure, result.CorrelationId)); return new(new TrustedCatalogMutationContext(new CatalogTenantId(result.ReadResolution.Context.TenantId.Value), result.CorrelationId), null); }
    private static async Task<CatalogScope> MutationScope(IMerchantRequestAuthorityResolver authority, HttpContext http, CancellationToken ct)
    { var result = await authority.ResolveMutationAsync(http, ct); if (result?.Context is null) return new(null, result is null ? Results.Unauthorized() : Failure(result.Failure, http.TraceIdentifier)); if (result.Context.Role is not MerchantRole.Owner and not MerchantRole.Admin) return new(null, Problem(403, "CATALOG_MUTATION_FORBIDDEN", http.TraceIdentifier)); return new(new TrustedCatalogMutationContext(new CatalogTenantId(result.Context.TenantId.Value), result.Context.CorrelationId), null); }
    private static CatalogProductResponse Map(Product product) => new(product.Id.Value, product.Name, product.Sku, product.Slug, product.BasePrice?.Amount, product.CategoryId?.Value, product.BrandId?.Value, product.Status.ToString(), product.Revision, product.Specifications.Select(x => new CatalogSpecificationInput(x.Name, x.Value, x.Unit, x.DisplayOrder)).ToArray(), product.Media.Select(x => new CatalogMediaItemInput(x.AssetId, x.AltText, x.DisplayOrder)).ToArray());
    private static CatalogReferenceResponse MapReference<T>(T reference) where T : class => reference switch { Category category => new(category.Id.Value, category.Name, category.Status.ToString(), category.Revision), Brand brand => new(brand.Id.Value, brand.Name, brand.Status.ToString(), brand.Revision), _ => throw new ArgumentOutOfRangeException(nameof(reference)) };
    private static CategoryId? OptionalCategory(string? value) => string.IsNullOrWhiteSpace(value) ? null : new CategoryId(value);
    private static BrandId? OptionalBrand(string? value) => string.IsNullOrWhiteSpace(value) ? null : new BrandId(value);
    private static IResult Outcome(CatalogOutcome outcome, string correlationId, IResult? success = null) => outcome switch { CatalogOutcome.Applied => success ?? Results.NoContent(), CatalogOutcome.NotFound => Results.NotFound(), CatalogOutcome.RevisionConflict => Problem(409, "REVISION_CONFLICT", correlationId), CatalogOutcome.SkuConflict => Problem(409, "CATALOG_IDENTIFIER_CONFLICT", correlationId), _ => Problem(422, "CATALOG_RULE_VIOLATION", correlationId) };
    private static IResult ReferenceOutcome(CatalogReferenceOutcome outcome, string correlationId) => outcome switch { CatalogReferenceOutcome.Applied => Results.NoContent(), CatalogReferenceOutcome.NotFound => Results.NotFound(), CatalogReferenceOutcome.RevisionConflict => Problem(409, "REVISION_CONFLICT", correlationId), CatalogReferenceOutcome.NameConflict => Problem(409, "REFERENCE_NAME_CONFLICT", correlationId), _ => Problem(422, "REFERENCE_INVALID", correlationId) };
    private static IResult Failure(TenantAuthorityFailure? failure, string correlationId) => failure?.Code switch { TenantAuthorityFailureCode.TenantSelectionRequired => Problem(409, "TENANT_SELECTION_REQUIRED", correlationId), TenantAuthorityFailureCode.AuthorityUnavailable => Unavailable(correlationId), _ => Problem(403, failure?.Code.ToString() ?? "MEMBERSHIP_REQUIRED", correlationId) };
    private static IResult Unavailable(HttpContext http) => Unavailable(http.TraceIdentifier);
    private static IResult Unavailable(string correlationId) => Problem(503, "CATALOG_UNAVAILABLE", correlationId);
    private static IResult Problem(int status, string code, string correlationId) => Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = correlationId });
    private sealed record CatalogScope(TrustedCatalogMutationContext? Context, IResult? Failure);
}
