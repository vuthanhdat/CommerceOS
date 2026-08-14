using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Application.PlatformAdministration;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.UnitTests;

public sealed class TenantLifecycleAdministrationTests
{
    [Fact]
    public async Task PlatformAdminSuspendsWithoutChangingMembershipOrCommercialHistory()
    {
        var tenant = Tenant(TenantStatus.Active, 4);
        var store = new InMemoryPlatformStore(tenant);
        var service = new TenantLifecycleAdministrationService(store, TimeProvider.System);
        var context = TrustedPlatformAdminContext.FromAuthenticatedPlatformAdmin(new SubjectId("platform-admin"), "correlation-1");

        var result = await service.ExecuteAsync(context, Command(tenant, TenantLifecycleAction.Suspend, "operation-1"), CancellationToken.None);

        Assert.Equal(TenantLifecycleOutcome.Applied, result.Outcome);
        Assert.Equal(TenantStatus.Suspended, result.Tenant!.Status);
        Assert.Equal(5, result.Tenant.Revision);
        Assert.Single(store.AuditIntents);
        Assert.Equal("platform-admin", store.AuditIntents[0].PlatformSubjectId.Value);
        Assert.Equal("required support action", store.AuditIntents[0].Reason);
        Assert.Equal(0, store.MembershipOrSubscriptionWrites);
    }

    [Fact]
    public async Task EquivalentOperationReplayReturnsTheOriginalOutcomeWithoutAnotherEffect()
    {
        var tenant = Tenant(TenantStatus.Active, 4);
        var store = new InMemoryPlatformStore(tenant);
        var service = new TenantLifecycleAdministrationService(store);
        var context = TrustedPlatformAdminContext.FromAuthenticatedPlatformAdmin(new SubjectId("platform-admin"), "correlation-1");
        var command = Command(tenant, TenantLifecycleAction.Suspend, "operation-1");

        var first = await service.ExecuteAsync(context, command, CancellationToken.None);
        var replay = await service.ExecuteAsync(context, command, CancellationToken.None);

        Assert.Equal(TenantLifecycleOutcome.Applied, first.Outcome);
        Assert.Equal(TenantLifecycleOutcome.Applied, replay.Outcome);
        Assert.Equal(5, replay.Tenant!.Revision);
        Assert.Single(store.AuditIntents);
    }

    [Fact]
    public async Task StaleAndInvalidTransitionsAreAuditedButDoNotChangeTenant()
    {
        var tenant = Tenant(TenantStatus.Suspended, 4);
        var store = new InMemoryPlatformStore(tenant);
        var service = new TenantLifecycleAdministrationService(store);
        var context = TrustedPlatformAdminContext.FromAuthenticatedPlatformAdmin(new SubjectId("platform-admin"), "correlation-1");

        var stale = await service.ExecuteAsync(context, Command(tenant, TenantLifecycleAction.Reactivate, "operation-1", 3), CancellationToken.None);
        var invalid = await service.ExecuteAsync(context, Command(tenant, TenantLifecycleAction.Suspend, "operation-2"), CancellationToken.None);

        Assert.Equal(TenantLifecycleOutcome.RevisionConflict, stale.Outcome);
        Assert.Equal(TenantLifecycleOutcome.InvalidTransition, invalid.Outcome);
        Assert.Equal(TenantStatus.Suspended, store.Tenant!.Status);
        Assert.Equal(4, store.Tenant.Revision);
        Assert.Equal(2, store.AuditIntents.Count);
    }

    [Fact]
    public async Task SupportQueryIsReadOnlyAndDoesNotRequireMerchantMembership()
    {
        var tenant = Tenant(TenantStatus.Suspended, 4);
        var store = new InMemoryPlatformStore(tenant);
        var service = new TenantPlatformSupportQueryService(store);
        var context = TrustedPlatformSupportReadContext.FromAuthenticatedPlatformSupport(new SubjectId("support-user"), "correlation-1");

        var result = await service.GetTenantAsync(context, tenant.Id, CancellationToken.None);

        Assert.Equal(tenant, result);
        Assert.Equal(0, store.MembershipOrSubscriptionWrites);
        Assert.Empty(store.AuditIntents);
    }

    [Fact]
    public void LifecycleCommandRequiresReasonRevisionAndOperationIdentity()
    {
        var tenant = Tenant(TenantStatus.Active, 1);

        Assert.Throws<ArgumentException>(() => new TenantLifecycleCommand(tenant.Id, TenantLifecycleAction.Suspend, 1, "operation", " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TenantLifecycleCommand(tenant.Id, TenantLifecycleAction.Suspend, 0, "operation", "reason"));
        Assert.Throws<ArgumentException>(() => new TenantLifecycleCommand(tenant.Id, TenantLifecycleAction.Suspend, 1, " ", "reason"));
    }

    private static Tenant Tenant(TenantStatus status, long revision) => new(
        new TenantId("tenant-a"), status, new BusinessProfile("Merchant", "Asia/Ho_Chi_Minh"), revision);

    private static TenantLifecycleCommand Command(Tenant tenant, TenantLifecycleAction action, string operationId, long? expectedRevision = null) =>
        new(tenant.Id, action, expectedRevision ?? tenant.Revision, operationId, "required support action");

    private sealed class InMemoryPlatformStore : IPlatformTenantAdministrationStore
    {
        private readonly Dictionary<string, TenantLifecycleResult> _operations = [];

        public InMemoryPlatformStore(Tenant tenant) => Tenant = tenant;

        public Tenant? Tenant { get; private set; }
        public List<TenantLifecycleAuditIntent> AuditIntents { get; } = [];
        public int MembershipOrSubscriptionWrites { get; private set; }

        public Task<Tenant?> GetForPlatformSupportAsync(TenantId tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(Tenant?.Id == tenantId ? Tenant : null);

        public Task<TenantLifecycleResult> TransitionAsync(
            TenantLifecycleCommand command,
            TenantLifecycleAuditIntent auditIntent,
            CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(command.OperationId, out var replay))
            {
                return Task.FromResult(replay with { Tenant = Tenant });
            }

            var target = command.Action is TenantLifecycleAction.Suspend ? TenantStatus.Suspended : TenantStatus.Active;
            var outcome = Tenant is null ? TenantLifecycleOutcome.NotFound
                : Tenant.Revision != command.ExpectedRevision ? TenantLifecycleOutcome.RevisionConflict
                : Tenant.Status == target ? TenantLifecycleOutcome.InvalidTransition
                : TenantLifecycleOutcome.Applied;
            if (outcome is TenantLifecycleOutcome.Applied)
            {
                Tenant = Tenant! with { Status = target, Revision = Tenant.Revision + 1 };
            }

            AuditIntents.Add(auditIntent with { Outcome = outcome });
            var result = new TenantLifecycleResult(outcome, Tenant);
            _operations.Add(command.OperationId, result);
            return Task.FromResult(result);
        }
    }
}
