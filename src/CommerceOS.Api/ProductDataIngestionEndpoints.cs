using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.Catalog.Application;
using CommerceOS.Catalog.Contracts;
using CommerceOS.Catalog.Infrastructure.Persistence;
using CommerceOS.ProductDataIngestion.Application;
using CommerceOS.ProductDataIngestion.Domain;
using CommerceOS.ProductDataIngestion.Infrastructure.Persistence;
using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Api;

public sealed record PdiSourceResponse(string Id, string Name, string Status, string PolicyReview, string PolicyVersion, bool PlatformEligible, bool EnabledForTenant, long? EnrollmentRevision, long Revision);
public sealed record PdiManualIngestionInput(string SourceId, string Url);
public sealed record PdiReviewInput(string? Note);
public sealed record PdiCandidateResponse(string Id, string SourceSnapshotId, string SourceId, string SourceProductId, string ProductId, long ExpectedProductRevision, string Name, string? SourceSku, long? VndPrice, string Status, string? ReviewNote, long Revision);

public static class ProductDataIngestionEndpoints
{
    public static void AddProductDataIngestionServices(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["COMMERCEOS_LOCALSTACK_ENDPOINT"];
        var table = configuration["COMMERCEOS_PRODUCT_DATA_INGESTION_TABLE"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(table)) return;

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" }));
        services.AddSingleton(new DynamoDbPdiGovernanceOptions(table));
        services.AddSingleton(new DynamoDbPdiIngestionOptions(table, configuration["COMMERCEOS_PRODUCT_DATA_RAW_SNAPSHOTS_BUCKET"] ?? "product-data-raw-snapshots"));
        services.AddSingleton<DynamoDbPdiGovernanceStore>();
        services.AddSingleton<DynamoDbPdiIngestionStore>();
        services.AddSingleton<IPdiGovernanceStore>(provider => provider.GetRequiredService<DynamoDbPdiGovernanceStore>());
        services.AddSingleton<IImportCandidateStore>(provider => provider.GetRequiredService<DynamoDbPdiIngestionStore>());
        services.AddSingleton<IManualAcquisitionWorkStore>(provider => provider.GetRequiredService<DynamoDbPdiIngestionStore>());
        services.AddSingleton<ISourceSnapshotStore>(provider => provider.GetRequiredService<DynamoDbPdiIngestionStore>());
        services.AddSingleton<IScheduledSourceRefreshStore>(provider => provider.GetRequiredService<DynamoDbPdiIngestionStore>());
        services.AddSingleton<SourceGovernanceService>();
        services.AddSingleton<ManualUrlIngestionService>();
        services.AddSingleton<ICatalogImportStore>(provider => provider.GetRequiredService<DynamoDbCatalogStore>());
        services.AddSingleton<ImportCandidateApplicationService>();
        services.AddSingleton<IApprovedImportCandidateApplier>(provider => provider.GetRequiredService<ImportCandidateApplicationService>());
        services.AddSingleton<ImportCandidateReviewService>();
    }

    public static void MapProductDataIngestionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/product-data/sources", async (HttpContext http, IMerchantRequestAuthorityResolver authority, SourceGovernanceService? sources, CancellationToken ct) =>
        {
            var scope = await ReadScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (sources is null) return Unavailable(http);
            var items = await sources.ListForTenantAsync(scope.Context!, ct);
            return Results.Ok(items.Select(item => new PdiSourceResponse(item.Source.Id.Value, item.Source.Name, item.Source.Status.ToString(), item.Source.PolicyReview.ToString(), item.Source.PolicyVersion, item.Source.PlatformEligible, item.Enrollment?.Enabled is true, item.Enrollment?.Revision, item.Source.Revision)));
        });

        app.MapPost("/api/v1/product-data/sources/{id}/enable", async (string id, HttpContext http, IMerchantRequestAuthorityResolver authority, SourceGovernanceService? sources, CancellationToken ct) =>
        {
            var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (sources is null) return Unavailable(http);
            return Outcome(await sources.EnableForTenantAsync(scope.Context!, new DataSourceId(id), ct), http.TraceIdentifier);
        });

        app.MapPost("/api/v1/product-data/manual-ingestions", async (PdiManualIngestionInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, ManualUrlIngestionService? ingestion, CancellationToken ct) =>
        {
            var scope = await MutationScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (ingestion is null) return Unavailable(http);
            var idempotencyKey = http.Request.Headers["Idempotency-Key"].ToString();
            if (string.IsNullOrWhiteSpace(idempotencyKey)) return Problem(422, "IDEMPOTENCY_KEY_REQUIRED", http.TraceIdentifier);
            return Outcome(await ingestion.RequestAsync(scope.Context!, new DataSourceId(input.SourceId), input.Url, idempotencyKey, ct), http.TraceIdentifier, Results.Accepted());
        });

        app.MapGet("/api/v1/product-data/import-candidates", async (string? status, string? search, HttpContext http, IMerchantRequestAuthorityResolver authority, ImportCandidateReviewService? candidates, CancellationToken ct) =>
        {
            var scope = await ReadScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (candidates is null) return Unavailable(http);
            if (!string.IsNullOrWhiteSpace(status) && !Enum.TryParse<ImportCandidateStatus>(status, true, out _)) return Problem(400, "IMPORT_CANDIDATE_FILTER_INVALID", http.TraceIdentifier);
            var parsedStatus = string.IsNullOrWhiteSpace(status) ? (ImportCandidateStatus?)null : Enum.Parse<ImportCandidateStatus>(status, true);
            return Results.Ok((await candidates.ListAsync(scope.Context!, parsedStatus, search, ct)).Select(Map));
        });

        app.MapGet("/api/v1/product-data/import-candidates/{id}", async (string id, HttpContext http, IMerchantRequestAuthorityResolver authority, ImportCandidateReviewService? candidates, CancellationToken ct) =>
        {
            var scope = await ReadScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (candidates is null) return Unavailable(http);
            var candidate = await candidates.GetAsync(scope.Context!, id, ct);
            return candidate is null ? Results.NotFound() : Results.Ok(Map(candidate));
        });

        MapReview(app, "approve", (service, context, id, note, ct) => service.ApproveAsync(context, id, note, ct));
        MapReview(app, "reject", (service, context, id, note, ct) => service.RejectAsync(context, id, note, ct));
        app.MapPost("/api/v1/product-data/import-candidates/{id}/apply", async (string id, HttpContext http, IMerchantRequestAuthorityResolver authority, ImportCandidateReviewService? candidates, CancellationToken ct) =>
        {
            var scope = await ReviewScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure;
            if (candidates is null) return Unavailable(http);
            return Outcome(await candidates.ApplyApprovedAsync(scope.Context!, id, ct), http.TraceIdentifier);
        });
    }

    private static void MapReview(WebApplication app, string action, Func<ImportCandidateReviewService, TrustedPdiReviewContext, string, string?, CancellationToken, Task<PdiOutcome>> execute) => app.MapPost($"/api/v1/product-data/import-candidates/{{id}}/{action}", async (string id, PdiReviewInput input, HttpContext http, IMerchantRequestAuthorityResolver authority, ImportCandidateReviewService? candidates, CancellationToken ct) =>
    { var scope = await ReviewScope(authority, http, ct); if (scope.Failure is not null) return scope.Failure; if (candidates is null) return Unavailable(http); return Outcome(await execute(candidates, scope.Context!, id, input.Note, ct), http.TraceIdentifier); });

    private static async Task<PdiScope> ReadScope(IMerchantRequestAuthorityResolver authority, HttpContext http, CancellationToken ct)
    { var result = await authority.ResolveReadAsync(http, ct); if (!result.IsAuthenticated) return new(null, Results.Unauthorized()); if (result.ReadResolution?.Context is null) return new(null, Failure(result.ReadResolution?.Failure, result.CorrelationId)); return new(new(new PdiTenantId(result.ReadResolution.Context.TenantId.Value), result.CorrelationId), null); }
    private static async Task<PdiScope> MutationScope(IMerchantRequestAuthorityResolver authority, HttpContext http, CancellationToken ct)
    { var result = await authority.ResolveMutationAsync(http, ct); if (result?.Context is null) return new(null, result is null ? Results.Unauthorized() : Failure(result.Failure, http.TraceIdentifier)); if (result.Context.Role is not MerchantRole.Owner and not MerchantRole.Admin) return new(null, Problem(403, "PRODUCT_DATA_MUTATION_FORBIDDEN", http.TraceIdentifier)); return new(new(new PdiTenantId(result.Context.TenantId.Value), result.Context.CorrelationId), null); }
    private static async Task<PdiReviewScope> ReviewScope(IMerchantRequestAuthorityResolver authority, HttpContext http, CancellationToken ct)
    { var result = await authority.ResolveMutationAsync(http, ct); if (result?.Context is null) return new(null, result is null ? Results.Unauthorized() : Failure(result.Failure, http.TraceIdentifier)); var canReview = result.Context.Role is MerchantRole.Owner or MerchantRole.Admin; if (!canReview) return new(null, Problem(403, "IMPORT_REVIEW_FORBIDDEN", http.TraceIdentifier)); return new(new(new PdiTenantId(result.Context.TenantId.Value), result.Context.CorrelationId, result.Context.SubjectId.Value, true), null); }
    private static PdiCandidateResponse Map(ImportCandidate candidate) => new(candidate.Id, candidate.SourceSnapshotId, candidate.SourceId.Value, candidate.SourceProductId, candidate.ProductId, candidate.ExpectedProductRevision, candidate.Name, candidate.SourceSku, candidate.VndPrice, candidate.Status.ToString(), candidate.ReviewNote, candidate.Revision);
    private static IResult Outcome(PdiOutcome outcome, string correlationId, IResult? success = null) => outcome switch { PdiOutcome.Applied => success ?? Results.NoContent(), PdiOutcome.NotFound => Results.NotFound(), PdiOutcome.RevisionConflict => Problem(409, "PDI_REVISION_CONFLICT", correlationId), _ => Problem(422, "PDI_NOT_ELIGIBLE", correlationId) };
    private static IResult Failure(TenantAuthorityFailure? failure, string correlationId) => failure?.Code switch { TenantAuthorityFailureCode.TenantSelectionRequired => Problem(409, "TENANT_SELECTION_REQUIRED", correlationId), TenantAuthorityFailureCode.AuthorityUnavailable => Unavailable(correlationId), _ => Problem(403, failure?.Code.ToString() ?? "MEMBERSHIP_REQUIRED", correlationId) };
    private static IResult Unavailable(HttpContext http) => Unavailable(http.TraceIdentifier);
    private static IResult Unavailable(string correlationId) => Problem(503, "PRODUCT_DATA_UNAVAILABLE", correlationId);
    private static IResult Problem(int status, string code, string correlationId) => Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = correlationId });
    private sealed record PdiScope(TrustedPdiTenantContext? Context, IResult? Failure);
    private sealed record PdiReviewScope(TrustedPdiReviewContext? Context, IResult? Failure);
}
