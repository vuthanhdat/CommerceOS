using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Application.Persistence;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.UnitTests;

public sealed class TenantAuthorityResolverTests
{
    [Fact]
    public async Task SingleCurrentActiveMembershipAutoSelectsAndUsesCurrentAuthorityState()
    {
        var store = new InMemoryTenancyStore();
        var tenant = Tenant("tenant-a", TenantStatus.Active, 4);
        var membership = Membership(tenant.Id, "member-a", "subject-a", MerchantRole.Admin, MembershipStatus.Active, 7);
        store.Add(tenant, membership);
        var resolver = new TenantAuthorityResolver(store);

        var result = await resolver.ResolveTenantMutationAuthorityAsync(
            Request("subject-a"),
            CancellationToken.None);

        Assert.True(result.IsAuthorized);
        Assert.NotNull(result.Context);
        Assert.Equal(tenant.Id, result.Context.TenantId);
        Assert.Equal(membership.Id, result.Context.MembershipId);
        Assert.Equal(MerchantRole.Admin, result.Context.Role);
        Assert.Equal(tenant.Revision, result.Context.TenantRevision);
        Assert.Equal(membership.Revision, result.Context.MembershipRevision);
        Assert.Equal("correlation-1", result.Context.CorrelationId);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task MultipleEligibleTenantsRequireAnIntentionalSelection()
    {
        var store = new InMemoryTenancyStore();
        var first = Tenant("tenant-a", TenantStatus.Active, 1);
        var second = Tenant("tenant-b", TenantStatus.Suspended, 1);
        store.Add(first, Membership(first.Id, "member-a", "subject-a", MerchantRole.Owner, MembershipStatus.Active, 1));
        store.Add(second, Membership(second.Id, "member-b", "subject-a", MerchantRole.Viewer, MembershipStatus.Active, 1));
        var resolver = new TenantAuthorityResolver(store);

        var ambiguous = await resolver.ResolveTenantReadAuthorityAsync(Request("subject-a"), CancellationToken.None);
        var selected = await resolver.ResolveTenantReadAuthorityAsync(
            Request("subject-a", second.Id),
            CancellationToken.None);

        Assert.False(ambiguous.IsAuthorized);
        Assert.Equal(TenantAuthorityFailureCode.TenantSelectionRequired, ambiguous.Failure!.Code);
        Assert.True(selected.IsAuthorized);
        Assert.Equal(second.Id, selected.Context!.TenantId);
        Assert.Equal(TenantStatus.Suspended, selected.Context.TenantStatus);
    }

    [Fact]
    public async Task KnownForeignTenantIdCannotOverrideAuthenticatedSubjectsDiscoveryScope()
    {
        var store = new InMemoryTenancyStore();
        var ownTenant = Tenant("tenant-a", TenantStatus.Active, 1);
        var foreignTenant = Tenant("tenant-b", TenantStatus.Active, 1);
        store.Add(ownTenant, Membership(ownTenant.Id, "member-a", "subject-a", MerchantRole.Owner, MembershipStatus.Active, 1));
        store.Add(foreignTenant, Membership(foreignTenant.Id, "member-b", "subject-b", MerchantRole.Owner, MembershipStatus.Active, 1));
        var resolver = new TenantAuthorityResolver(store);

        var result = await resolver.ResolveTenantReadAuthorityAsync(
            Request("subject-a", foreignTenant.Id),
            CancellationToken.None);

        Assert.False(result.IsAuthorized);
        Assert.Equal(TenantAuthorityFailureCode.MembershipRequired, result.Failure!.Code);
    }

    [Fact]
    public async Task SuspendedTenantAllowsReadButDeniesMutation()
    {
        var store = new InMemoryTenancyStore();
        var tenant = Tenant("tenant-a", TenantStatus.Suspended, 2);
        store.Add(tenant, Membership(tenant.Id, "member-a", "subject-a", MerchantRole.Owner, MembershipStatus.Active, 3));
        var resolver = new TenantAuthorityResolver(store);

        var read = await resolver.ResolveTenantReadAuthorityAsync(Request("subject-a"), CancellationToken.None);
        var mutation = await resolver.ResolveTenantMutationAuthorityAsync(Request("subject-a"), CancellationToken.None);

        Assert.True(read.IsAuthorized);
        Assert.Equal(TenantStatus.Suspended, read.Context!.TenantStatus);
        Assert.False(mutation.IsAuthorized);
        Assert.Equal(TenantAuthorityFailureCode.TenantSuspended, mutation.Failure!.Code);
    }

    [Fact]
    public async Task DisabledMembershipFailsClosed()
    {
        var store = new InMemoryTenancyStore();
        var tenant = Tenant("tenant-a", TenantStatus.Active, 1);
        store.Add(tenant, Membership(tenant.Id, "member-a", "subject-a", MerchantRole.Staff, MembershipStatus.Disabled, 2));
        var resolver = new TenantAuthorityResolver(store);

        var result = await resolver.ResolveTenantReadAuthorityAsync(Request("subject-a"), CancellationToken.None);

        Assert.False(result.IsAuthorized);
        Assert.Equal(TenantAuthorityFailureCode.MembershipInactive, result.Failure!.Code);
    }

    [Fact]
    public async Task EachResolutionRevalidatesRoleAndTenantStatusWithoutTokenClaimCaching()
    {
        var store = new InMemoryTenancyStore();
        var tenant = Tenant("tenant-a", TenantStatus.Active, 1);
        var membership = Membership(tenant.Id, "member-a", "subject-a", MerchantRole.Admin, MembershipStatus.Active, 1);
        store.Add(tenant, membership);
        var resolver = new TenantAuthorityResolver(store);

        var beforeChange = await resolver.ResolveTenantMutationAuthorityAsync(Request("subject-a"), CancellationToken.None);
        store.Replace(
            tenant with { Status = TenantStatus.Suspended, Revision = 2 },
            membership with { Role = MerchantRole.Viewer, Revision = 2 });
        var afterChangeRead = await resolver.ResolveTenantReadAuthorityAsync(Request("subject-a"), CancellationToken.None);
        var afterChangeMutation = await resolver.ResolveTenantMutationAuthorityAsync(Request("subject-a"), CancellationToken.None);

        Assert.Equal(MerchantRole.Admin, beforeChange.Context!.Role);
        Assert.True(afterChangeRead.IsAuthorized);
        Assert.Equal(MerchantRole.Viewer, afterChangeRead.Context!.Role);
        Assert.Equal(TenantStatus.Suspended, afterChangeRead.Context.TenantStatus);
        Assert.False(afterChangeMutation.IsAuthorized);
        Assert.Equal(TenantAuthorityFailureCode.TenantSuspended, afterChangeMutation.Failure!.Code);
    }

    [Fact]
    public async Task PersistenceFailureFailsClosedWithoutLeakingTenantDetails()
    {
        var store = new InMemoryTenancyStore { ThrowOnDiscovery = true };
        var resolver = new TenantAuthorityResolver(store);

        var result = await resolver.ResolveTenantReadAuthorityAsync(Request("subject-a"), CancellationToken.None);

        Assert.False(result.IsAuthorized);
        Assert.Equal(TenantAuthorityFailureCode.AuthorityUnavailable, result.Failure!.Code);
    }

    private static MerchantAuthorityRequest Request(string subjectId, TenantId? tenantId = null) => new(
        new AuthenticatedMerchantPrincipal(new SubjectId(subjectId)),
        tenantId is null ? null : new RequestedTenantSelection(tenantId.Value),
        "correlation-1");

    private static Tenant Tenant(string id, TenantStatus status, long revision) => new(
        new TenantId(id),
        status,
        new BusinessProfile("Merchant", "Asia/Ho_Chi_Minh"),
        revision);

    private static Membership Membership(
        TenantId tenantId,
        string membershipId,
        string subjectId,
        MerchantRole role,
        MembershipStatus status,
        long revision) => new(
        new MembershipId(membershipId),
        tenantId,
        new SubjectId(subjectId),
        role,
        status,
        revision);

    private sealed class InMemoryTenancyStore : ITenancyStore
    {
        private readonly Dictionary<TenantId, Tenant> _tenants = [];
        private readonly Dictionary<(TenantId TenantId, SubjectId SubjectId), Membership> _memberships = [];

        public bool ThrowOnDiscovery { get; init; }

        public void Add(Tenant tenant, Membership membership)
        {
            _tenants.Add(tenant.Id, tenant);
            _memberships.Add((membership.TenantId, membership.SubjectId), membership);
        }

        public void Replace(Tenant tenant, Membership membership)
        {
            _tenants[tenant.Id] = tenant;
            _memberships[(membership.TenantId, membership.SubjectId)] = membership;
        }

        public Task<Tenant?> GetTenantAsync(TrustedTenantPersistenceScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(_tenants.GetValueOrDefault(scope.TenantId));

        public Task<Membership?> GetMembershipAsync(
            TrustedTenantPersistenceScope scope,
            MembershipId membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_memberships.Values.SingleOrDefault(membership =>
                membership.TenantId == scope.TenantId && membership.Id == membershipId));

        public Task<Membership?> GetMembershipForSubjectAsync(
            TrustedTenantPersistenceScope scope,
            SubjectId subjectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_memberships.GetValueOrDefault((scope.TenantId, subjectId)));

        public Task<IReadOnlyList<MembershipDiscoveryCandidate>> FindMembershipCandidatesAsync(
            SubjectId subjectId,
            CancellationToken cancellationToken)
        {
            if (ThrowOnDiscovery)
            {
                throw new InvalidOperationException("Persistence is unavailable.");
            }

            IReadOnlyList<MembershipDiscoveryCandidate> candidates = _memberships.Values
                .Where(membership => membership.SubjectId == subjectId)
                .Select(membership => new MembershipDiscoveryCandidate(membership.TenantId, membership.Id))
                .ToArray();
            return Task.FromResult(candidates);
        }

        public Task<ConditionalWriteResult> SaveTenantAsync(
            TrustedTenantPersistenceScope scope,
            Tenant tenant,
            long? expectedRevision,
            CancellationToken cancellationToken) => Task.FromResult(ConditionalWriteResult.Applied);

        public Task<ConditionalWriteResult> SaveMembershipAsync(
            TrustedTenantPersistenceScope scope,
            Membership membership,
            long? expectedRevision,
            CancellationToken cancellationToken) => Task.FromResult(ConditionalWriteResult.Applied);
    }
}
