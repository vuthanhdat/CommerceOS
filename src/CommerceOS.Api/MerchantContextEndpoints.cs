using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Api;

public sealed record SelectCurrentTenantRequest(string TenantId);

public sealed record MerchantMembershipResponse(
    string TenantId,
    string DisplayName,
    string TimeZoneIana,
    string TenantStatus,
    string MembershipId,
    string Role,
    long TenantRevision,
    long MembershipRevision);

public sealed record MerchantCapabilities(
    bool CanReadMerchantData,
    bool CanManageCatalog,
    bool CanMutate,
    bool IsReadOnly);

public sealed record MerchantContextResponse(
    MerchantMembershipResponse Tenant,
    MerchantCapabilities Capabilities);

/// <summary>
/// Delivery edge for identity that has already been verified. The test-header
/// implementation is available only under an explicit local configuration flag.
/// It intentionally carries no role, membership, or tenant claim from the browser.
/// </summary>
public interface IMerchantIdentityResolver
{
    AuthenticatedMerchantPrincipal? Resolve(HttpContext context);
}

internal sealed class DisabledMerchantIdentityResolver : IMerchantIdentityResolver
{
    public AuthenticatedMerchantPrincipal? Resolve(HttpContext context) => null;
}

internal sealed class DevelopmentHeaderMerchantIdentityResolver : IMerchantIdentityResolver
{
    public AuthenticatedMerchantPrincipal? Resolve(HttpContext context)
    {
        var subject = context.Request.Headers["X-CommerceOS-Test-Subject"].ToString();
        return string.IsNullOrWhiteSpace(subject)
            ? null
            : new AuthenticatedMerchantPrincipal(new SubjectId(subject));
    }
}

public sealed record MerchantRequestAuthority(
    AuthenticatedMerchantPrincipal? Principal,
    TenantAuthorityResolution<TrustedTenantReadContext>? ReadResolution,
    string CorrelationId)
{
    public bool IsAuthenticated => Principal is not null;
}

public interface IMerchantRequestAuthorityResolver
{
    Task<MerchantRequestAuthority> ResolveReadAsync(HttpContext context, CancellationToken cancellationToken);
    Task<TenantAuthorityResolution<TrustedTenantMutationContext>?> ResolveMutationAsync(HttpContext context, CancellationToken cancellationToken);
}

internal sealed class MerchantRequestAuthorityResolver(
    IMerchantIdentityResolver identity,
    ITenantAuthorityResolver? authority) : IMerchantRequestAuthorityResolver
{
    private const string CurrentTenantCookie = "commerceos_current_tenant";

    public async Task<MerchantRequestAuthority> ResolveReadAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var principal = identity.Resolve(context);
        var correlationId = context.TraceIdentifier;
        if (principal is null || authority is null)
        {
            return new MerchantRequestAuthority(principal, null, correlationId);
        }

        var selectedTenant = context.Request.Cookies.TryGetValue(CurrentTenantCookie, out var selected)
            && !string.IsNullOrWhiteSpace(selected)
            ? new RequestedTenantSelection(new TenantId(selected))
            : null;
        var resolution = await authority.ResolveTenantReadAuthorityAsync(
            new MerchantAuthorityRequest(principal, selectedTenant, correlationId),
            cancellationToken);
        return new MerchantRequestAuthority(principal, resolution, correlationId);
    }

    public async Task<TenantAuthorityResolution<TrustedTenantMutationContext>?> ResolveMutationAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var principal = identity.Resolve(context);
        if (principal is null || authority is null) return null;
        var selectedTenant = context.Request.Cookies.TryGetValue(CurrentTenantCookie, out var selected) && !string.IsNullOrWhiteSpace(selected)
            ? new RequestedTenantSelection(new TenantId(selected)) : null;
        return await authority.ResolveTenantMutationAuthorityAsync(new MerchantAuthorityRequest(principal, selectedTenant, context.TraceIdentifier), cancellationToken);
    }
}

public static class MerchantContextEndpoints
{
    private const string CurrentTenantCookie = "commerceos_current_tenant";

    public static void AddMerchantContextServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMerchantIdentityResolver>(configuration["COMMERCEOS_TEST_IDENTITY_ENABLED"] == "1"
            ? new DevelopmentHeaderMerchantIdentityResolver()
            : new DisabledMerchantIdentityResolver());
        services.AddSingleton<IMerchantRequestAuthorityResolver, MerchantRequestAuthorityResolver>();
    }

    public static void MapMerchantContextEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/me", (HttpContext http, IMerchantIdentityResolver identity) =>
        {
            var principal = identity.Resolve(http);
            return principal is null
                ? Results.Unauthorized()
                : Results.Ok(new { subjectId = principal.SubjectId.Value });
        });

        app.MapGet("/api/v1/me/memberships", async (
            HttpContext http,
            IMerchantIdentityResolver identity,
            ITenantAuthorityResolver? authority,
            CancellationToken cancellationToken) =>
        {
            var principal = identity.Resolve(http);
            if (principal is null) return Results.Unauthorized();
            if (authority is null) return Problem(StatusCodes.Status503ServiceUnavailable, "AUTHORITY_UNAVAILABLE", http.TraceIdentifier);

            var discovery = await authority.DiscoverMerchantTenantsAsync(principal, cancellationToken);
            var memberships = new List<MerchantMembershipResponse>();
            foreach (var tenantId in discovery.CandidateTenantIds)
            {
                var result = await authority.ResolveTenantReadAuthorityAsync(
                    new MerchantAuthorityRequest(principal, new RequestedTenantSelection(tenantId), http.TraceIdentifier),
                    cancellationToken);
                if (result.Context is not null)
                {
                    memberships.Add(MapMembership(result.Context));
                }
            }
            return Results.Ok(memberships);
        });

        app.MapPost("/api/v1/me/current-tenant", async (
            SelectCurrentTenantRequest request,
            HttpContext http,
            IMerchantIdentityResolver identity,
            ITenantAuthorityResolver? authority,
            CancellationToken cancellationToken) =>
        {
            var principal = identity.Resolve(http);
            if (principal is null) return Results.Unauthorized();
            if (authority is null) return Problem(StatusCodes.Status503ServiceUnavailable, "AUTHORITY_UNAVAILABLE", http.TraceIdentifier);
            if (string.IsNullOrWhiteSpace(request.TenantId)) return Problem(StatusCodes.Status400BadRequest, "TENANT_SELECTION_INVALID", http.TraceIdentifier);

            var resolution = await authority.ResolveTenantReadAuthorityAsync(
                new MerchantAuthorityRequest(principal, new RequestedTenantSelection(new TenantId(request.TenantId)), http.TraceIdentifier),
                cancellationToken);
            if (resolution.Context is null) return Failure(resolution.Failure, http.TraceIdentifier);

            http.Response.Cookies.Append(CurrentTenantCookie, resolution.Context.TenantId.Value, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = http.Request.IsHttps,
                Path = "/"
            });
            return Results.NoContent();
        });

        app.MapGet("/api/v1/merchant-context", async (
            HttpContext http,
            IMerchantRequestAuthorityResolver requestAuthority,
            CancellationToken cancellationToken) =>
        {
            var result = await requestAuthority.ResolveReadAsync(http, cancellationToken);
            if (!result.IsAuthenticated) return Results.Unauthorized();
            if (result.ReadResolution is null) return Problem(StatusCodes.Status503ServiceUnavailable, "AUTHORITY_UNAVAILABLE", result.CorrelationId);
            if (result.ReadResolution.Context is null) return Failure(result.ReadResolution.Failure, result.CorrelationId);

            var context = result.ReadResolution.Context;
            return Results.Ok(new MerchantContextResponse(MapMembership(context), Capabilities(context)));
        });
    }

    private static MerchantMembershipResponse MapMembership(TrustedTenantReadContext context) => new(
        context.TenantId.Value,
        context.BusinessProfile.DisplayName,
        context.BusinessProfile.TimeZoneIana,
        context.TenantStatus.ToString(),
        context.MembershipId.Value,
        context.Role.ToString(),
        context.TenantRevision,
        context.MembershipRevision);

    private static MerchantCapabilities Capabilities(TrustedTenantReadContext context)
    {
        var canManageCatalog = context.TenantStatus is TenantStatus.Active && context.Role is MerchantRole.Owner or MerchantRole.Admin;
        return new MerchantCapabilities(true, canManageCatalog, canManageCatalog, !canManageCatalog);
    }

    private static IResult Failure(TenantAuthorityFailure? failure, string correlationId) => failure?.Code switch
    {
        TenantAuthorityFailureCode.TenantSelectionRequired => Problem(StatusCodes.Status409Conflict, "TENANT_SELECTION_REQUIRED", correlationId),
        TenantAuthorityFailureCode.AuthorityUnavailable => Problem(StatusCodes.Status503ServiceUnavailable, "AUTHORITY_UNAVAILABLE", correlationId),
        _ => Problem(StatusCodes.Status403Forbidden, failure?.Code.ToString() ?? "MEMBERSHIP_REQUIRED", correlationId)
    };

    private static IResult Problem(int status, string code, string correlationId) => Results.Problem(
        statusCode: status,
        title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = correlationId });
}
