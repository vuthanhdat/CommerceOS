using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.Application.Authority;

/// <summary>
/// Identity evidence supplied by a delivery adapter only after authentication has succeeded.
/// It deliberately carries no client-provided tenant, membership, role, or entitlement claims.
/// </summary>
public sealed record AuthenticatedMerchantPrincipal(SubjectId SubjectId);

/// <summary>
/// A tenant selection can identify the tenant the caller intends to use, but is never authority.
/// </summary>
public sealed record RequestedTenantSelection(TenantId TenantId);

public sealed class MerchantAuthorityRequest
{
    public MerchantAuthorityRequest(
        AuthenticatedMerchantPrincipal principal,
        RequestedTenantSelection? requestedTenant,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("CorrelationId must not be empty.", nameof(correlationId));
        }

        Principal = principal;
        RequestedTenant = requestedTenant;
        CorrelationId = correlationId;
    }

    public AuthenticatedMerchantPrincipal Principal { get; }

    public RequestedTenantSelection? RequestedTenant { get; }

    public string CorrelationId { get; }
}

/// <summary>
/// Discovery is a selection aid only. Each candidate must still be revalidated by an authority resolver.
/// </summary>
public sealed record MerchantTenantDiscovery(IReadOnlyList<TenantId> CandidateTenantIds);

public enum TenantAuthorityFailureCode
{
    MembershipRequired,
    MembershipInactive,
    TenantSelectionRequired,
    TenantSuspended,
    AuthorityUnavailable
}

/// <summary>
/// Failure intentionally omits tenant, membership, and aggregate identifiers so delivery can remain non-disclosing.
/// </summary>
public sealed record TenantAuthorityFailure(TenantAuthorityFailureCode Code);

/// <summary>
/// High-privilege identity evidence issued by the platform administration delivery edge.
/// It is intentionally distinct from a merchant authority context and never grants a Membership.
/// </summary>
public sealed class TrustedPlatformAdminContext
{
    private TrustedPlatformAdminContext(SubjectId platformSubjectId, string correlationId)
    {
        PlatformSubjectId = platformSubjectId;
        CorrelationId = correlationId;
    }

    public SubjectId PlatformSubjectId { get; }

    public string CorrelationId { get; }

    public static TrustedPlatformAdminContext FromAuthenticatedPlatformAdmin(SubjectId platformSubjectId, string correlationId) =>
        new(platformSubjectId, RequireCorrelation(correlationId));

    private static string RequireCorrelation(string correlationId) =>
        string.IsNullOrWhiteSpace(correlationId)
            ? throw new ArgumentException("CorrelationId must not be empty.", nameof(correlationId))
            : correlationId;
}

/// <summary>
/// Read-only platform support evidence. This cannot be supplied to a lifecycle command.
/// </summary>
public sealed class TrustedPlatformSupportReadContext
{
    private TrustedPlatformSupportReadContext(SubjectId platformSubjectId, string correlationId)
    {
        PlatformSubjectId = platformSubjectId;
        CorrelationId = correlationId;
    }

    public SubjectId PlatformSubjectId { get; }

    public string CorrelationId { get; }

    public static TrustedPlatformSupportReadContext FromAuthenticatedPlatformSupport(SubjectId platformSubjectId, string correlationId) =>
        new(platformSubjectId, string.IsNullOrWhiteSpace(correlationId)
            ? throw new ArgumentException("CorrelationId must not be empty.", nameof(correlationId))
            : correlationId);
}

public sealed class TrustedTenantReadContext
{
    internal TrustedTenantReadContext(
        TenantId tenantId,
        SubjectId subjectId,
        MembershipId membershipId,
        MerchantRole role,
        TenantStatus tenantStatus,
        BusinessProfile businessProfile,
        string? storefrontSlug,
        long tenantRevision,
        long membershipRevision,
        string correlationId)
    {
        TenantId = tenantId;
        SubjectId = subjectId;
        MembershipId = membershipId;
        Role = role;
        TenantStatus = tenantStatus;
        BusinessProfile = businessProfile;
        StorefrontSlug = storefrontSlug;
        TenantRevision = tenantRevision;
        MembershipRevision = membershipRevision;
        CorrelationId = correlationId;
    }

    public TenantId TenantId { get; }

    public SubjectId SubjectId { get; }

    public MembershipId MembershipId { get; }

    public MerchantRole Role { get; }

    public TenantStatus TenantStatus { get; }

    public BusinessProfile BusinessProfile { get; }

    public string? StorefrontSlug { get; }

    public long TenantRevision { get; }

    public long MembershipRevision { get; }

    public string CorrelationId { get; }
}

public sealed class TrustedTenantMutationContext
{
    internal TrustedTenantMutationContext(
        TenantId tenantId,
        SubjectId subjectId,
        MembershipId membershipId,
        MerchantRole role,
        long tenantRevision,
        long membershipRevision,
        string correlationId)
    {
        TenantId = tenantId;
        SubjectId = subjectId;
        MembershipId = membershipId;
        Role = role;
        TenantRevision = tenantRevision;
        MembershipRevision = membershipRevision;
        CorrelationId = correlationId;
    }

    public TenantId TenantId { get; }

    public SubjectId SubjectId { get; }

    public MembershipId MembershipId { get; }

    public MerchantRole Role { get; }

    public long TenantRevision { get; }

    public long MembershipRevision { get; }

    public string CorrelationId { get; }
}

public sealed record TenantAuthorityResolution<TContext>(TContext? Context, TenantAuthorityFailure? Failure)
    where TContext : class
{
    public bool IsAuthorized => Context is not null;
}

public interface ITenantAuthorityResolver
{
    Task<MerchantTenantDiscovery> DiscoverMerchantTenantsAsync(
        AuthenticatedMerchantPrincipal principal,
        CancellationToken cancellationToken);

    Task<TenantAuthorityResolution<TrustedTenantReadContext>> ResolveTenantReadAuthorityAsync(
        MerchantAuthorityRequest request,
        CancellationToken cancellationToken);

    Task<TenantAuthorityResolution<TrustedTenantMutationContext>> ResolveTenantMutationAuthorityAsync(
        MerchantAuthorityRequest request,
        CancellationToken cancellationToken);
}
