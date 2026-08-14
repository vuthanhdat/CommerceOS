using CommerceOS.Tenancy.Application.Persistence;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.Application.Authority;

public sealed class TenantAuthorityResolver : ITenantAuthorityResolver
{
    private readonly ITenancyStore _store;

    public TenantAuthorityResolver(ITenancyStore store)
    {
        _store = store;
    }

    public async Task<MerchantTenantDiscovery> DiscoverMerchantTenantsAsync(
        AuthenticatedMerchantPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var candidates = await _store.FindMembershipCandidatesAsync(principal.SubjectId, cancellationToken);
        var tenantIds = candidates
            .Select(candidate => candidate.TenantId)
            .Distinct()
            .ToArray();

        return new MerchantTenantDiscovery(tenantIds);
    }

    public Task<TenantAuthorityResolution<TrustedTenantReadContext>> ResolveTenantReadAuthorityAsync(
        MerchantAuthorityRequest request,
        CancellationToken cancellationToken) =>
        ResolveAuthorityAsync(
            request,
            static (tenant, membership, correlationId) => new TrustedTenantReadContext(
                tenant.Id,
                membership.SubjectId,
                membership.Id,
                membership.Role,
                tenant.Status,
                tenant.Profile,
                tenant.StorefrontSlug,
                tenant.Revision,
                membership.Revision,
                correlationId),
            cancellationToken);

    public Task<TenantAuthorityResolution<TrustedTenantMutationContext>> ResolveTenantMutationAuthorityAsync(
        MerchantAuthorityRequest request,
        CancellationToken cancellationToken) =>
        ResolveAuthorityAsync(
            request,
            static (tenant, membership, correlationId) => new TrustedTenantMutationContext(
                tenant.Id,
                membership.SubjectId,
                membership.Id,
                membership.Role,
                tenant.Revision,
                membership.Revision,
                correlationId),
            cancellationToken);

    private async Task<TenantAuthorityResolution<TContext>> ResolveAuthorityAsync<TContext>(
        MerchantAuthorityRequest request,
        Func<Tenant, Membership, string, TContext> createContext,
        CancellationToken cancellationToken)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(createContext);

        try
        {
            var discovery = await DiscoverMerchantTenantsAsync(request.Principal, cancellationToken);
            var selectedTenantId = request.RequestedTenant?.TenantId;
            var candidateTenantIds = discovery.CandidateTenantIds;

            if (selectedTenantId is not null)
            {
                if (!candidateTenantIds.Contains(selectedTenantId.Value))
                {
                    return Denied<TContext>(TenantAuthorityFailureCode.MembershipRequired);
                }

                var selected = await ValidateCandidateAsync(request.Principal.SubjectId, selectedTenantId.Value, cancellationToken);
                return CreateResolution(selected, request.CorrelationId, createContext, IsMutation<TContext>());
            }

            var validated = new List<ValidatedCandidate>();
            var sawInactiveMembership = false;

            foreach (var candidateTenantId in candidateTenantIds)
            {
                var candidate = await ValidateCandidateAsync(request.Principal.SubjectId, candidateTenantId, cancellationToken);
                if (candidate.Failure == TenantAuthorityFailureCode.MembershipInactive)
                {
                    sawInactiveMembership = true;
                }

                if (candidate.Tenant is not null && candidate.Membership is not null)
                {
                    validated.Add(candidate);
                }
            }

            if (validated.Count == 0)
            {
                return Denied<TContext>(
                    sawInactiveMembership
                        ? TenantAuthorityFailureCode.MembershipInactive
                        : TenantAuthorityFailureCode.MembershipRequired);
            }

            if (validated.Count > 1)
            {
                return Denied<TContext>(TenantAuthorityFailureCode.TenantSelectionRequired);
            }

            return CreateResolution(validated[0], request.CorrelationId, createContext, IsMutation<TContext>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Denied<TContext>(TenantAuthorityFailureCode.AuthorityUnavailable);
        }
    }

    private async Task<ValidatedCandidate> ValidateCandidateAsync(
        SubjectId subjectId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var scope = new TrustedTenantPersistenceScope(tenantId);
        var tenant = await _store.GetTenantAsync(scope, cancellationToken);
        var membership = await _store.GetMembershipForSubjectAsync(scope, subjectId, cancellationToken);

        if (tenant is null || membership is null || membership.TenantId != tenantId || membership.SubjectId != subjectId)
        {
            return ValidatedCandidate.Failed(TenantAuthorityFailureCode.MembershipRequired);
        }

        if (membership.Status is not MembershipStatus.Active)
        {
            return ValidatedCandidate.Failed(TenantAuthorityFailureCode.MembershipInactive);
        }

        return new ValidatedCandidate(tenant, membership, null);
    }

    private static TenantAuthorityResolution<TContext> CreateResolution<TContext>(
        ValidatedCandidate candidate,
        string correlationId,
        Func<Tenant, Membership, string, TContext> createContext,
        bool mutation)
        where TContext : class
    {
        if (candidate.Failure is not null)
        {
            return Denied<TContext>(candidate.Failure.Value);
        }

        if (mutation && candidate.Tenant!.Status is TenantStatus.Suspended)
        {
            return Denied<TContext>(TenantAuthorityFailureCode.TenantSuspended);
        }

        return new TenantAuthorityResolution<TContext>(
            createContext(candidate.Tenant!, candidate.Membership!, correlationId),
            null);
    }

    private static bool IsMutation<TContext>() => typeof(TContext) == typeof(TrustedTenantMutationContext);

    private static TenantAuthorityResolution<TContext> Denied<TContext>(TenantAuthorityFailureCode code)
        where TContext : class => new(null, new TenantAuthorityFailure(code));

    private sealed record ValidatedCandidate(Tenant? Tenant, Membership? Membership, TenantAuthorityFailureCode? Failure)
    {
        public static ValidatedCandidate Failed(TenantAuthorityFailureCode failure) => new(null, null, failure);
    }
}
