using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.Application.Persistence;

/// <summary>
/// A tenant scope issued only by Tenancy authority resolution. It is intentionally
/// not constructible by transport code or callers carrying an untrusted TenantId.
/// </summary>
public sealed class TrustedTenantPersistenceScope
{
    internal TrustedTenantPersistenceScope(TenantId tenantId)
    {
        TenantId = tenantId;
    }

    public TenantId TenantId { get; }
}

public sealed record MembershipDiscoveryCandidate(TenantId TenantId, MembershipId MembershipId);

public enum ConditionalWriteResult
{
    Applied,
    RevisionConflict
}

/// <summary>
/// Module-private Tenancy persistence port. Subject discovery is deliberately
/// separate from tenant-scoped access and must be revalidated before authority.
/// </summary>
public interface ITenancyStore
{
    Task<Tenant?> GetTenantAsync(TrustedTenantPersistenceScope scope, CancellationToken cancellationToken);

    Task<Membership?> GetMembershipAsync(
        TrustedTenantPersistenceScope scope,
        MembershipId membershipId,
        CancellationToken cancellationToken);

    Task<Membership?> GetMembershipForSubjectAsync(
        TrustedTenantPersistenceScope scope,
        SubjectId subjectId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MembershipDiscoveryCandidate>> FindMembershipCandidatesAsync(
        SubjectId subjectId,
        CancellationToken cancellationToken);

    Task<ConditionalWriteResult> SaveTenantAsync(
        TrustedTenantPersistenceScope scope,
        Tenant tenant,
        long? expectedRevision,
        CancellationToken cancellationToken);

    Task<ConditionalWriteResult> SaveMembershipAsync(
        TrustedTenantPersistenceScope scope,
        Membership membership,
        long? expectedRevision,
        CancellationToken cancellationToken);
}
