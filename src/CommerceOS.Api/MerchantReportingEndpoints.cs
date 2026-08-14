using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.Accounting.Application;
using CommerceOS.Accounting.Domain;
using CommerceOS.Reporting.Application;
using CommerceOS.Reporting.Domain;
using CommerceOS.Reporting.Infrastructure.Persistence;
using CommerceOS.Tenancy.Application.Authority;

namespace CommerceOS.Api;

public static class MerchantReportingEndpoints
{
    public static void AddMerchantReportingServices(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["COMMERCEOS_LOCALSTACK_ENDPOINT"]; var table = configuration["COMMERCEOS_REPORTING_TABLE"]; if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(table)) return;
        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" })); services.AddSingleton(new DynamoDbReportingOptions(table)); services.AddSingleton<DynamoDbReportingStore>(); services.AddSingleton<IReportingStore>(p => p.GetRequiredService<DynamoDbReportingStore>()); services.AddSingleton<IRefundProgressStore>(p => p.GetRequiredService<DynamoDbReportingStore>()); services.AddSingleton<OperationalKpiQuery>(); services.AddSingleton<IAccountingReportingQuery, AccountingReportingQuery>(); services.AddSingleton<FinancialBackOfficeQuery>();
    }
    public static void MapMerchantReportingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/reports/operational-kpis", async (DateOnly from, DateOnly through, HttpContext h, IMerchantRequestAuthorityResolver a, OperationalKpiQuery? q, CancellationToken ct) => { var s = await Scope(a, h, ct); if (s.Error is not null) return s.Error; return q is null ? Off(h) : Results.Ok(await q.GetAsync(s.Context!, from, through, ct)); });
        app.MapGet("/api/v1/reports/financial", async (DateOnly from, DateOnly through, string? cursor, int? pageSize, HttpContext h, IMerchantRequestAuthorityResolver a, FinancialBackOfficeQuery? q, CancellationToken ct) => { var s = await Scope(a, h, ct); if (s.Error is not null) return s.Error; return q is null ? Off(h) : Results.Ok(await q.GetAsync(s.Context!, new(from, through, cursor, pageSize ?? 20), ct)); });
        app.MapGet("/api/v1/reports/projections/{name}/freshness", async (string name, HttpContext h, IMerchantRequestAuthorityResolver a, IReportingStore? store, CancellationToken ct) => { var s = await Scope(a, h, ct); if (s.Error is not null) return s.Error; if (store is null) return Off(h); var checkpoint = await store.GetCheckpointAsync(s.Context!, name, ct); return Results.Ok(new { projectionName = name, checkpoint, isFresh = checkpoint is not null && !checkpoint.RebuildInProgress }); });
        app.MapGet("/api/v1/reports/refund-progress/{id}", async (string id, HttpContext h, IMerchantRequestAuthorityResolver a, IRefundProgressStore? store, CancellationToken ct) => { var s = await Scope(a, h, ct); if (s.Error is not null) return s.Error; if (store is null) return Off(h); var value = await store.GetAsync(s.Context!.TenantId.Value, id, ct); return value is null ? Results.NotFound() : Results.Ok(value); });
    }
    private static async Task<ReportScope> Scope(IMerchantRequestAuthorityResolver a, HttpContext h, CancellationToken ct) { var r = await a.ResolveReadAsync(h, ct); if (!r.IsAuthenticated) return new(null, Results.Unauthorized()); return r.ReadResolution?.Context is { } c ? new(new(new ReportingTenantId(c.TenantId.Value)), null) : new(null, Problem(r.CorrelationId, r.ReadResolution?.Failure?.Code.ToString() ?? "MEMBERSHIP_REQUIRED", r.ReadResolution?.Failure?.Code is TenantAuthorityFailureCode.TenantSelectionRequired ? 409 : 403)); }
    private static IResult Off(HttpContext h) => Problem(h.TraceIdentifier, "REPORTING_UNAVAILABLE", 503); private static IResult Problem(string correlation, string code, int status) => Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { { "code", code }, { "correlationId", correlation } }); private sealed record ReportScope(TrustedReportingContext? Context, IResult? Error);
    private sealed class AccountingReportingQuery(IAccountingStore store) : IAccountingReportingQuery
    { public async Task<FinancialReportPage> GetReadOnlyReportAsync(string tenant, FinancialReportRequest request, CancellationToken ct) { var context = new TrustedAccountingContext(new AccountingTenantId(tenant), "reporting-read"); var journals = await store.ListJournalsAsync(context, request.From, request.Through, request.Cursor, Math.Clamp(request.PageSize, 1, 100), ct); var all = await store.ListJournalsAsync(context, null, request.Through, null, 10_000, ct); var trial = all.Items.SelectMany(x => x.Lines).GroupBy(x => x.AccountId).Select(x => new TrialBalanceView(x.Key.Value, x.Where(y => y.Side is JournalSide.Debit).Sum(y => y.AmountVnd), x.Where(y => y.Side is JournalSide.Credit).Sum(y => y.AmountVnd))).ToArray(); return new(journals.Items.Select(x => new FinancialJournalView(x.Id, x.EffectiveDate, x.SourceIdentity, x.Lines.Where(y => y.Side is JournalSide.Debit).Sum(y => y.AmountVnd), x.Lines.Where(y => y.Side is JournalSide.Credit).Sum(y => y.AmountVnd))).ToArray(), journals.NextCursor, trial); } }
}
