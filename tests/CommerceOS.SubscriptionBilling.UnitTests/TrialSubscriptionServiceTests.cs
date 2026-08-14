using CommerceOS.SubscriptionBilling.Application.Trial;
using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.SubscriptionBilling.UnitTests;

public sealed class TrialSubscriptionServiceTests
{
    [Fact]
    public async Task AcceptedTrialCopiesTheApprovedImmutableTrialTerms()
    {
        var catalog = new TrialCatalog();
        var store = new InMemoryTrialStore();
        var service = new TrialSubscriptionService(catalog, store);
        var command = new StartTrialSubscriptionCommand("tenant-1", "onb-1", "merchant-onboarding:onb-1", "corr-1");

        var result = await service.StartTrialSubscriptionAsync(command, CancellationToken.None);

        Assert.Equal(TrialSubscriptionStartOutcome.Accepted, result.Outcome);
        var trial = Assert.Single(store.Values);
        Assert.Equal("trial-v1", trial.Entitlements.TrialTermsVersionId);
        Assert.Equal(30, trial.Entitlements.DurationDays);
        Assert.True(trial.Entitlements.CoreCommerceCapabilities);
        Assert.Equal(3, trial.Entitlements.MaxActiveMemberships);
        Assert.Equal(1, trial.Entitlements.MaxWarehouses);
        Assert.True(trial.Entitlements.ScheduledProductIngestion);
        Assert.Equal(500, trial.Entitlements.OrderVolumeWarningThreshold);
    }

    [Fact]
    public async Task EquivalentRetryIsAlreadyAppliedAndIncompatibleSourceConflicts()
    {
        var store = new InMemoryTrialStore();
        var service = new TrialSubscriptionService(new TrialCatalog(), store);
        var command = new StartTrialSubscriptionCommand("tenant-1", "onb-1", "merchant-onboarding:onb-1", "corr-1");

        await service.StartTrialSubscriptionAsync(command, CancellationToken.None);
        var replay = await service.StartTrialSubscriptionAsync(command, CancellationToken.None);
        var conflict = await service.StartTrialSubscriptionAsync(
            command with { SourceIdentity = "merchant-onboarding:another" }, CancellationToken.None);

        Assert.Equal(TrialSubscriptionStartOutcome.AlreadyApplied, replay.Outcome);
        Assert.Equal(TrialSubscriptionStartOutcome.SourceConflict, conflict.Outcome);
        Assert.Single(store.Values);
    }

    private sealed class TrialCatalog : ISubscriptionCatalogQuery
    {
        public Task<IReadOnlyList<SellablePlanVersion>> ListAvailablePlanVersionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SellablePlanVersion>>([]);

        public Task<TrialTermsCatalogVersion?> GetTrialTermsVersionAsync(string trialTermsVersionId, CancellationToken cancellationToken) =>
            Task.FromResult<TrialTermsCatalogVersion?>(new TrialTermsCatalogVersion("trial-v1", 30, true, 3, 1, true, 500));
    }

    private sealed class InMemoryTrialStore : ITrialSubscriptionStore
    {
        public List<TrialSubscription> Values { get; } = [];

        public Task<TrialSubscription?> GetForOnboardingAsync(string tenantId, string onboardingOperationId, CancellationToken cancellationToken) =>
            Task.FromResult(Values.SingleOrDefault(item => item.TenantId == tenantId && item.OnboardingOperationId == onboardingOperationId));

        public Task<TrialSubscription?> GetCurrentForTenantAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(Values.SingleOrDefault(item => item.TenantId == tenantId));

        public Task<bool> CreateIfAbsentAsync(TrialSubscription subscription, CancellationToken cancellationToken)
        {
            if (Values.Any(item => item.TenantId == subscription.TenantId && item.OnboardingOperationId == subscription.OnboardingOperationId))
            {
                return Task.FromResult(false);
            }
            Values.Add(subscription);
            return Task.FromResult(true);
        }
    }
}
