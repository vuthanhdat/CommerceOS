using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.Accounting.Application;
using CommerceOS.Accounting.Domain;
using CommerceOS.Accounting.Infrastructure.Persistence;
using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Api;

public sealed record AccountInput(string Code, string DisplayName);
public sealed record JournalLineInput(string AccountId, string Side, long AmountVnd);
public sealed record JournalInput(DateOnly EffectiveDate, IReadOnlyList<JournalLineInput> Lines);
public sealed record AccountingDateRange(DateOnly? From, DateOnly? Through, string? Cursor, int? PageSize);
public static class MerchantAccountingEndpoints
{
    public static void AddMerchantAccountingServices(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["COMMERCEOS_LOCALSTACK_ENDPOINT"]; var table = configuration["COMMERCEOS_ACCOUNTING_TABLE"]; if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(table)) return;
        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" })); services.AddSingleton(new DynamoDbAccountingOptions(table)); services.AddSingleton<DynamoDbAccountingStore>(); services.AddSingleton<IAccountingStore>(x => x.GetRequiredService<DynamoDbAccountingStore>()); services.AddSingleton<AccountingChartService>(); services.AddSingleton<JournalService>();
    }
    public static void MapMerchantAccountingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/accounting/accounts", async (HttpContext h, IMerchantRequestAuthorityResolver a, AccountingChartService? s, CancellationToken ct) => { var x = await Read(a, h, ct); if (x.Error is not null) return x.Error; return s is null ? Off(h) : Results.Ok(await s.ListAsync(x.Context!, ct)); });
        app.MapPost("/api/v1/accounting/accounts/bootstrap", async (HttpContext h, IMerchantRequestAuthorityResolver a, AccountingChartService? s, CancellationToken ct) => { var x = await Mutate(a, h, ct); if (x.Error is not null) return x.Error; return s is null ? Off(h) : Outcome(await s.BootstrapAsync(x.Context!, ct), h.TraceIdentifier); });
        app.MapPost("/api/v1/accounting/accounts", async (AccountInput i, HttpContext h, IMerchantRequestAuthorityResolver a, AccountingChartService? s, CancellationToken ct) => { var x = await Mutate(a, h, ct); if (x.Error is not null) return x.Error; if (s is null) return Off(h); var v = new Account(new(Guid.NewGuid().ToString("N")), x.Context!.TenantId, i.Code, i.DisplayName, AccountRole.NonControl, AccountStatus.Active, false, 1); return Outcome(await s.AddNonControlAsync(x.Context, v, ct), h.TraceIdentifier, Results.Created($"/api/v1/accounting/accounts/{v.Id.Value}", v)); });
        app.MapPost("/api/v1/accounting/accounts/{id}/deactivate", async (string id, HttpContext h, IMerchantRequestAuthorityResolver a, AccountingChartService? s, CancellationToken ct) => { var x = await Mutate(a, h, ct); if (x.Error is not null) return x.Error; return s is null ? Off(h) : Outcome(await s.DeactivateAsync(x.Context!, new(id), ct), h.TraceIdentifier); });
        app.MapGet("/api/v1/accounting/journals", async (DateOnly? from, DateOnly? through, string? cursor, int? pageSize, HttpContext h, IMerchantRequestAuthorityResolver a, JournalService? s, CancellationToken ct) => { var x = await Read(a, h, ct); if (x.Error is not null) return x.Error; return s is null ? Off(h) : Results.Ok(await s.GeneralLedgerAsync(x.Context!, from, through, cursor, pageSize ?? 20, ct)); });
        app.MapGet("/api/v1/accounting/journals/{id}", async (string id, HttpContext h, IMerchantRequestAuthorityResolver a, IAccountingStore? s, CancellationToken ct) => { var x = await Read(a, h, ct); if (x.Error is not null) return x.Error; if (s is null) return Off(h); var j = await s.GetJournalAsync(x.Context!, id, ct); return j is null ? Results.NotFound() : Results.Ok(j); });
        app.MapPost("/api/v1/accounting/journals", async (JournalInput i, HttpContext h, IMerchantRequestAuthorityResolver a, JournalService? s, CancellationToken ct) => { var x = await Mutate(a, h, ct); if (x.Error is not null) return x.Error; if (s is null) return Off(h); try { var j = Journal.Create(Guid.NewGuid().ToString("N"), x.Context!.TenantId, i.EffectiveDate, DateTimeOffset.UtcNow, Source(h), x.Context.CorrelationId, i.Lines.Select(y => new JournalLine(new(y.AccountId), Enum.Parse<JournalSide>(y.Side, true), y.AmountVnd)).ToArray()); return Outcome(await s.PostAsync(x.Context, j, ct), h.TraceIdentifier, Results.Created($"/api/v1/accounting/journals/{j.Id}", j)); } catch (Exception) { return Bad(422, "JOURNAL_INVALID", h.TraceIdentifier); } });
        app.MapPost("/api/v1/accounting/journals/{id}/reverse", async (string id, DateOnly effectiveDate, HttpContext h, IMerchantRequestAuthorityResolver a, JournalService? s, CancellationToken ct) => { var x = await Mutate(a, h, ct); if (x.Error is not null) return x.Error; return s is null ? Off(h) : Outcome(await s.ReverseAsync(x.Context!, id, Guid.NewGuid().ToString("N"), effectiveDate, Source(h), ct), h.TraceIdentifier); });
        app.MapGet("/api/v1/accounting/trial-balance", async (DateOnly through, HttpContext h, IMerchantRequestAuthorityResolver a, JournalService? s, CancellationToken ct) => { var x = await Read(a, h, ct); if (x.Error is not null) return x.Error; return s is null ? Off(h) : Results.Ok(await s.TrialBalanceAsync(x.Context!, through, ct)); });
    }
    private static async Task<Scope> Read(IMerchantRequestAuthorityResolver a, HttpContext h, CancellationToken ct) { var r = await a.ResolveReadAsync(h, ct); if (!r.IsAuthenticated) return new(null, Results.Unauthorized()); return r.ReadResolution?.Context is { } c ? new(new(new(c.TenantId.Value), c.CorrelationId), null) : new(null, BAD(r.ReadResolution?.Failure, r.CorrelationId)); }
    private static async Task<Scope> Mutate(IMerchantRequestAuthorityResolver a, HttpContext h, CancellationToken ct) { var r = await a.ResolveMutationAsync(h, ct); return r?.Context is { } c && c.Role is not MerchantRole.Viewer ? new(new(new(c.TenantId.Value), c.CorrelationId), null) : new(null, r is null ? Results.Unauthorized() : Bad(403, "ACCOUNTING_MUTATION_FORBIDDEN", h.TraceIdentifier)); }
    private static string Source(HttpContext h) => h.Request.Headers["Idempotency-Key"].ToString(); private static IResult Outcome(AccountingOutcome x, string c, IResult? ok = null) => x is AccountingOutcome.Applied or AccountingOutcome.AlreadyApplied ? ok ?? Results.NoContent() : x is AccountingOutcome.NotFound ? Results.NotFound() : Bad(x is AccountingOutcome.Conflict ? 409 : 422, $"ACCOUNTING_{x.ToString().ToUpperInvariant()}", c); private static IResult BAD(TenantAuthorityFailure? f, string c) => Bad(f?.Code is TenantAuthorityFailureCode.TenantSelectionRequired ? 409 : 403, f?.Code.ToString() ?? "MEMBERSHIP_REQUIRED", c); private static IResult Off(HttpContext h) => Bad(503, "ACCOUNTING_UNAVAILABLE", h.TraceIdentifier); private static IResult Bad(int s, string c, string i) => Results.Problem(statusCode: s, title: c, extensions: new Dictionary<string, object?> { { "code", c }, { "correlationId", i } }); private sealed record Scope(TrustedAccountingContext? Context, IResult? Error);
}
