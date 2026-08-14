using CommerceOS.SubscriptionBilling.Application.Catalog;
using CommerceOS.SubscriptionBilling.Application.PaidLifecycle;
using CommerceOS.SubscriptionBilling.Application.PlatformCharges;
using CommerceOS.SubscriptionBilling.Application.Trial;
using CommerceOS.SubscriptionBilling.Domain;
using CommerceOS.SubscriptionBilling.Infrastructure.Provider;

namespace CommerceOS.SubscriptionBilling.UnitTests;

public sealed class PaidSubscriptionLifecycleTests
{
    [Fact]
    public async Task ActivationAndUpgradeApplyOnlyAfterVerifiedChargeAndStartFreshMonth()
    {
        var clock = new AdjustableClock(new DateTimeOffset(2026, 1, 31, 10, 0, 0, TimeSpan.Zero));
        var state = new DeterministicSaasBillingProviderState();
        var store = new Store();
        var service = Service(store, state, new Usage(true), clock);

        var activation = await service.ActivateOrUpgradeAsync(Activate("Starter", "starter-v1", "activate"), CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(2));
        var upgrade = await service.ActivateOrUpgradeAsync(Activate("Growth", "growth-v1", "upgrade"), CancellationToken.None);

        Assert.Equal(PaidLifecycleOutcome.Applied, activation.Outcome);
        Assert.Equal(new DateTimeOffset(2026, 2, 28, 10, 0, 0, TimeSpan.Zero), activation.Subscription!.EffectiveUntil);
        Assert.Equal(PaidLifecycleOutcome.Applied, upgrade.Outcome);
        Assert.Equal("growth-v1", upgrade.Subscription!.Entitlements.PlanVersionId);
        Assert.Equal(clock.GetUtcNow(), upgrade.Subscription.EffectiveFrom);
        Assert.Equal(clock.GetUtcNow().AddMonths(1), upgrade.Subscription.EffectiveUntil);
    }

    [Fact]
    public async Task UnknownUpgradeLeavesExistingTermsAuthoritativeUntilReconciliation()
    {
        var clock = new AdjustableClock(DateTimeOffset.UtcNow);
        var state = new DeterministicSaasBillingProviderState();
        var store = new Store();
        var service = Service(store, state, new Usage(true), clock);
        await service.ActivateOrUpgradeAsync(Activate("Starter", "starter-v1", "activate"), CancellationToken.None);
        state.Configure("upgrade", SimulatedSaasBillingScenario.TimeoutAfterCommit);

        var unknown = await service.ActivateOrUpgradeAsync(Activate("Growth", "growth-v1", "upgrade"), CancellationToken.None);

        Assert.Equal(PaidLifecycleOutcome.AwaitingChargeEvidence, unknown.Outcome);
        Assert.Equal("starter-v1", (await store.GetCurrentAsync("tenant-1", CancellationToken.None))!.Entitlements.PlanVersionId);
        var reconciled = await service.ActivateOrUpgradeAsync(Activate("Growth", "growth-v1", "upgrade"), CancellationToken.None);
        Assert.Equal(PaidLifecycleOutcome.Applied, reconciled.Outcome);
        Assert.Equal("growth-v1", reconciled.Subscription!.Entitlements.PlanVersionId);
    }

    [Fact]
    public async Task DowngradeWaitsForRenewalAndBlocksWithoutMutatingForeignResources()
    {
        var clock = new AdjustableClock(DateTimeOffset.UtcNow);
        var state = new DeterministicSaasBillingProviderState();
        var store = new Store();
        var usage = new Usage(false);
        var service = Service(store, state, usage, clock);
        var active = await service.ActivateOrUpgradeAsync(Activate("Growth", "growth-v1", "activate"), CancellationToken.None);
        var scheduled = await service.RequestDowngradeAsync(new RequestDowngradeCommand("tenant-1", "Starter", "starter-v1", active.Subscription!.Revision, "downgrade"), CancellationToken.None);
        clock.Set(scheduled.Subscription!.EffectiveUntil);

        var due = await service.ProcessDueAsync("tenant-1", "renewal", "corr", CancellationToken.None);

        Assert.Equal(PaidLifecycleOutcome.BlockedByUsage, due.Outcome);
        Assert.Equal("growth-v1", due.Subscription!.Entitlements.PlanVersionId);
        Assert.Equal(DowngradeStatus.BlockedByUsage, due.Subscription.PendingDowngrade!.Status);
        Assert.Equal(1, usage.Assessments);
    }

    [Fact]
    public async Task CancellationAndDefinitiveRenewalFailureRespectPeriodAndSevenDayGrace()
    {
        var clock = new AdjustableClock(DateTimeOffset.UtcNow);
        var state = new DeterministicSaasBillingProviderState();
        var store = new Store();
        var service = Service(store, state, new Usage(true), clock);
        var active = await service.ActivateOrUpgradeAsync(Activate("Starter", "starter-v1", "activate"), CancellationToken.None);
        var cancelled = await service.CancelRenewalAsync("tenant-1", active.Subscription!.Revision, CancellationToken.None);

        Assert.Equal(PaidLifecycleOutcome.Applied, cancelled.Outcome);
        Assert.Equal(SubscriptionCondition.Active, cancelled.Subscription!.Condition);
        Assert.Equal(PaidLifecycleOutcome.NotDue, (await service.ProcessDueAsync("tenant-1", "end", "corr", CancellationToken.None)).Outcome);
        clock.Set(cancelled.Subscription.EffectiveUntil);
        Assert.Equal(PaidLifecycleOutcome.Ended, (await service.ProcessDueAsync("tenant-1", "end", "corr", CancellationToken.None)).Outcome);

        var reactivated = await service.ActivateOrUpgradeAsync(Activate("Starter", "starter-v1", "reactivate"), CancellationToken.None);
        clock.Set(reactivated.Subscription!.EffectiveUntil);
        state.Configure("renewal", SimulatedSaasBillingScenario.DefinitiveNoCommit);
        var pastDue = await service.ProcessDueAsync("tenant-1", "renewal", "corr", CancellationToken.None);

        Assert.Equal(PaidLifecycleOutcome.Applied, pastDue.Outcome);
        Assert.Equal(SubscriptionCondition.PastDue, pastDue.Subscription!.Condition);
        var graceEnds = clock.GetUtcNow().AddDays(7);
        Assert.Equal(graceEnds, pastDue.Subscription.EffectiveUntil);
        clock.Set(graceEnds);
        Assert.Equal(PaidLifecycleOutcome.Ended, (await service.ProcessDueAsync("tenant-1", "grace-end", "corr", CancellationToken.None)).Outcome);
    }

    private static ActivatePaidSubscriptionCommand Activate(string planId, string version, string operation) => new("tenant-1", planId, version, operation, "corr");

    private static PaidSubscriptionLifecycleService Service(Store store, DeterministicSaasBillingProviderState state, Usage usage, TimeProvider clock) =>
        new(new Catalog(), store, new ChargeStore(), new DeterministicSaasBillingProvider(state, clock), usage, clock);

    private sealed class Catalog : ISubscriptionCatalogStore
    {
        private static readonly IReadOnlyList<PlanVersion> Plans =
        [
            Plan("Starter", "starter-v1", 199_000, 3, 1, false),
            Plan("Growth", "growth-v1", 499_000, 10, 3, true)
        ];
        public Task<CatalogRecord?> GetAsync(CatalogRecordId id, CancellationToken cancellationToken) => Task.FromResult(Plans.Select(CatalogRecord.For).SingleOrDefault(record => record.Id == id));
        public Task<CatalogRecordCreateResult> CreateIfAbsentAsync(CatalogRecord record, CancellationToken cancellationToken) => Task.FromResult(CatalogRecordCreateResult.Created);
        public Task<IReadOnlyList<PlanVersion>> ListAvailablePlanVersionsAsync(CancellationToken cancellationToken) => Task.FromResult(Plans);
        private static PlanVersion Plan(string id, string version, long price, int members, int warehouses, bool scheduled) => new(new PlanId(id), new PlanVersionId(version), new VndMoney(price), new EntitlementTerms(true, members, warehouses, scheduled, 500), true);
    }

    private sealed class Usage(bool fits) : ISubscriptionUsageAssessor
    {
        public int Assessments { get; private set; }
        public Task<bool> FitsTargetAsync(string trustedTenantId, PaidEntitlementSnapshot target, CancellationToken cancellationToken) { Assessments++; return Task.FromResult(fits); }
    }

    private sealed class Store : IPaidSubscriptionStore
    {
        private readonly HashSet<string> _operations = [];
        public PaidSubscription? Current { get; private set; }
        public Task<PaidSubscription?> GetCurrentAsync(string tenantId, CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task<PaidLifecycleOutcome> ApplyPeriodAsync(PaidSubscriptionTransition transition, CancellationToken cancellationToken)
        {
            if (!_operations.Add(transition.OperationId)) return Task.FromResult(PaidLifecycleOutcome.AlreadyApplied);
            if (Current is not null && Current.Revision != transition.Subscription.Revision - 1) return Task.FromResult(PaidLifecycleOutcome.RevisionConflict);
            Current = transition.Subscription; return Task.FromResult(PaidLifecycleOutcome.Applied);
        }
        public async Task<PaidLifecycleOutcome> ScheduleDowngradeAsync(string tenantId, long expectedRevision, PendingDowngrade downgrade, CancellationToken cancellationToken) =>
            Current is null || Current.Revision != expectedRevision ? PaidLifecycleOutcome.RevisionConflict : await ApplyPeriodAsync(new PaidSubscriptionTransition(downgrade.OperationId, PaidLifecycleOperation.Renewal, Current with { PendingDowngrade = downgrade, Revision = Current.Revision + 1 }), cancellationToken);
        public async Task<PaidLifecycleOutcome> MarkPastDueAsync(string tenantId, long expectedRevision, DateTimeOffset graceEndsAt, CancellationToken cancellationToken) =>
            Current is null || Current.Revision != expectedRevision ? PaidLifecycleOutcome.RevisionConflict : await ApplyPeriodAsync(new PaidSubscriptionTransition($"past-due:{expectedRevision}", PaidLifecycleOperation.Renewal, Current with { Condition = SubscriptionCondition.PastDue, EffectiveUntil = graceEndsAt, Revision = Current.Revision + 1 }), cancellationToken);
        public async Task<PaidLifecycleOutcome> MarkEndedAsync(string tenantId, long expectedRevision, CancellationToken cancellationToken) =>
            Current is null || Current.Revision != expectedRevision ? PaidLifecycleOutcome.RevisionConflict : await ApplyPeriodAsync(new PaidSubscriptionTransition($"ended:{expectedRevision}", PaidLifecycleOperation.Renewal, Current with { Condition = SubscriptionCondition.Ended, Revision = Current.Revision + 1 }), cancellationToken);
    }

    private sealed class ChargeStore : IPlatformChargeStore
    {
        private readonly Dictionary<string, PlatformCharge> _charges = [];
        public Task<PlatformCharge?> GetByLogicalIdentityAsync(string tenantId, string logicalChargeIdentity, CancellationToken cancellationToken) => Task.FromResult(_charges.GetValueOrDefault(logicalChargeIdentity));
        public Task<PlatformChargeCreateResult> CreateIfAbsentAsync(PlatformCharge charge, CancellationToken cancellationToken) { if (!_charges.TryAdd(charge.LogicalChargeIdentity, charge)) return Task.FromResult(PlatformChargeCreateResult.AlreadyExists); return Task.FromResult(PlatformChargeCreateResult.Created); }
        public Task<PlatformChargeEvidenceApplyResult> ApplyEvidenceAsync(PlatformCharge current, PlatformChargeEvidence evidence, PlatformCharge updated, CancellationToken cancellationToken) { _charges[current.LogicalChargeIdentity] = updated; return Task.FromResult(PlatformChargeEvidenceApplyResult.Applied); }
        public Task<bool> MarkOutcomeUnknownAsync(PlatformCharge current, CancellationToken cancellationToken) { _charges[current.LogicalChargeIdentity] = current with { Outcome = PlatformChargeOutcome.OutcomeUnknown, Revision = current.Revision + 1 }; return Task.FromResult(true); }
    }

    private sealed class AdjustableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Set(DateTimeOffset value) => _now = value;
        public void Advance(TimeSpan value) => _now = _now.Add(value);
    }
}
