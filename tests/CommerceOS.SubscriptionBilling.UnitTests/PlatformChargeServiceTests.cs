using CommerceOS.SubscriptionBilling.Application.PlatformCharges;
using CommerceOS.SubscriptionBilling.Domain;
using CommerceOS.SubscriptionBilling.Infrastructure.Provider;

namespace CommerceOS.SubscriptionBilling.UnitTests;

public sealed class PlatformChargeServiceTests
{
    [Fact]
    public async Task EquivalentRetryHasOneLogicalChargeEffectAndIncompatibleReuseConflicts()
    {
        var state = new DeterministicSaasBillingProviderState();
        var store = new InMemoryChargeStore();
        var service = new PlatformChargeService(store, new DeterministicSaasBillingProvider(state));
        var command = Command("renewal-1");

        var first = await service.RecordAttemptAsync(command, CancellationToken.None);
        var replay = await service.RecordAttemptAsync(command, CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(replay.Created);
        Assert.Equal(PlatformChargeOutcome.Succeeded, replay.Charge.Outcome);
        Assert.Single(store.Charges);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAttemptAsync(
            new RecordPlatformChargeAttemptCommand("tenant-1", "subscription-1", "starter-v2", "renewal-1", new VndMoney(199_000), "corr-1"), CancellationToken.None));
    }

    [Fact]
    public async Task TimeoutAfterCommitIsUnknownUntilInquiryConvergesToVerifiedSuccess()
    {
        var state = new DeterministicSaasBillingProviderState();
        state.Configure("renewal-1", SimulatedSaasBillingScenario.TimeoutAfterCommit);
        var service = new PlatformChargeService(new InMemoryChargeStore(), new DeterministicSaasBillingProvider(state));

        var attempted = await service.RecordAttemptAsync(Command("renewal-1"), CancellationToken.None);
        var reconciled = await service.ReconcileAsync("tenant-1", "renewal-1", CancellationToken.None);

        Assert.Equal(PlatformChargeOutcome.OutcomeUnknown, attempted.Charge.Outcome);
        Assert.Equal(PlatformChargeOutcome.Succeeded, reconciled.Outcome);
    }

    [Fact]
    public async Task NetworkAndMissingCallbackNeverGuessFailure()
    {
        var state = new DeterministicSaasBillingProviderState();
        state.Configure("network", SimulatedSaasBillingScenario.NetworkFailure);
        state.Configure("missing", SimulatedSaasBillingScenario.MissingCallback);
        var service = new PlatformChargeService(new InMemoryChargeStore(), new DeterministicSaasBillingProvider(state));

        var network = await service.RecordAttemptAsync(Command("network"), CancellationToken.None);
        var missing = await service.RecordAttemptAsync(Command("missing"), CancellationToken.None);
        var reconciled = await service.ReconcileAsync("tenant-1", "missing", CancellationToken.None);

        Assert.Equal(PlatformChargeOutcome.OutcomeUnknown, network.Charge.Outcome);
        Assert.Equal(PlatformChargeOutcome.OutcomeUnknown, missing.Charge.Outcome);
        Assert.Equal(PlatformChargeOutcome.OutcomeUnknown, reconciled.Outcome);
    }

    [Fact]
    public async Task DuplicateAndOutOfOrderEvidenceCannotRegressASettledCharge()
    {
        var state = new DeterministicSaasBillingProviderState();
        state.Configure("renewal-1", SimulatedSaasBillingScenario.MissingCallback);
        var store = new InMemoryChargeStore();
        var service = new PlatformChargeService(store, new DeterministicSaasBillingProvider(state));
        var charge = (await service.RecordAttemptAsync(Command("renewal-1"), CancellationToken.None)).Charge;
        var success = new PlatformChargeEvidence("event-1", charge.Id, charge.ProviderOperationId, PlatformChargeEvidenceKind.VerifiedSuccess, DateTimeOffset.UtcNow);
        var declinedLater = new PlatformChargeEvidence("event-2", charge.Id, charge.ProviderOperationId, PlatformChargeEvidenceKind.DefinitiveNoCommit, DateTimeOffset.UtcNow.AddMinutes(1));

        var settled = await service.RecordProviderEvidenceAsync("tenant-1", "renewal-1", success, CancellationToken.None);
        var duplicate = await service.RecordProviderEvidenceAsync("tenant-1", "renewal-1", success, CancellationToken.None);
        var outOfOrder = await service.RecordProviderEvidenceAsync("tenant-1", "renewal-1", declinedLater, CancellationToken.None);

        Assert.Equal(PlatformChargeOutcome.Succeeded, settled.Outcome);
        Assert.Equal(PlatformChargeOutcome.Succeeded, duplicate.Outcome);
        Assert.Equal(PlatformChargeOutcome.Succeeded, outOfOrder.Outcome);
        Assert.Equal(2, store.Evidence.Count);
    }

    [Fact]
    public async Task ProviderStateCanBeReusedByANewProviderInstanceForReconciliation()
    {
        var state = new DeterministicSaasBillingProviderState();
        state.Configure("renewal-1", SimulatedSaasBillingScenario.TimeoutAfterCommit);
        var store = new InMemoryChargeStore();
        var initial = new PlatformChargeService(store, new DeterministicSaasBillingProvider(state));
        await initial.RecordAttemptAsync(Command("renewal-1"), CancellationToken.None);
        var afterProviderRestart = new PlatformChargeService(store, new DeterministicSaasBillingProvider(state));

        var reconciled = await afterProviderRestart.ReconcileAsync("tenant-1", "renewal-1", CancellationToken.None);

        Assert.Equal(PlatformChargeOutcome.Succeeded, reconciled.Outcome);
    }

    private static RecordPlatformChargeAttemptCommand Command(string identity) =>
        new("tenant-1", "subscription-1", "starter-v1", identity, new VndMoney(199_000), "corr-1");

    private sealed class InMemoryChargeStore : IPlatformChargeStore
    {
        public Dictionary<(string TenantId, string LogicalIdentity), PlatformCharge> Charges { get; } = [];
        public HashSet<string> Evidence { get; } = [];

        public Task<PlatformCharge?> GetByLogicalIdentityAsync(string tenantId, string logicalChargeIdentity, CancellationToken cancellationToken) =>
            Task.FromResult(Charges.GetValueOrDefault((tenantId, logicalChargeIdentity)));

        public Task<PlatformChargeCreateResult> CreateIfAbsentAsync(PlatformCharge charge, CancellationToken cancellationToken)
        {
            var key = (charge.TenantId, charge.LogicalChargeIdentity);
            if (!Charges.TryAdd(key, charge))
            {
                return Task.FromResult(PlatformChargeCreateResult.AlreadyExists);
            }

            return Task.FromResult(PlatformChargeCreateResult.Created);
        }

        public Task<PlatformChargeEvidenceApplyResult> ApplyEvidenceAsync(PlatformCharge current, PlatformChargeEvidence evidence, PlatformCharge updated, CancellationToken cancellationToken)
        {
            if (!Evidence.Add(evidence.EvidenceId))
            {
                return Task.FromResult(PlatformChargeEvidenceApplyResult.Duplicate);
            }

            var key = (current.TenantId, current.LogicalChargeIdentity);
            if (!Charges.TryGetValue(key, out var actual) || actual.Revision != current.Revision)
            {
                return Task.FromResult(PlatformChargeEvidenceApplyResult.RevisionConflict);
            }

            Charges[key] = updated;
            return Task.FromResult(PlatformChargeEvidenceApplyResult.Applied);
        }

        public Task<bool> MarkOutcomeUnknownAsync(PlatformCharge current, CancellationToken cancellationToken)
        {
            var key = (current.TenantId, current.LogicalChargeIdentity);
            if (!Charges.TryGetValue(key, out var actual) || actual.Revision != current.Revision || actual.Outcome is not PlatformChargeOutcome.Pending)
            {
                return Task.FromResult(false);
            }

            Charges[key] = current with { Outcome = PlatformChargeOutcome.OutcomeUnknown, Revision = current.Revision + 1 };
            return Task.FromResult(true);
        }
    }
}
