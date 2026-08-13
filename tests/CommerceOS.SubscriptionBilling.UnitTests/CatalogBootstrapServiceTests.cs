using CommerceOS.SubscriptionBilling.Application.Catalog;
using CommerceOS.SubscriptionBilling.Domain;

namespace CommerceOS.SubscriptionBilling.UnitTests;

public sealed class CatalogBootstrapServiceTests
{
    [Fact]
    public void VersionControlledSeedMatchesApprovedTrialAndPaidCatalog()
    {
        var seed = LoadInitialSeed();

        Assert.Equal("2026-08-13-v1", seed.Version);
        Assert.Equal("trial-v1", seed.TrialTermsVersion.Id.Value);
        Assert.Equal(30, seed.TrialTermsVersion.DurationDays);
        Assert.Equal(new EntitlementTerms(true, 3, 1, true, 500), seed.TrialTermsVersion.Entitlements);
        Assert.Equal(
            [
                new PlanVersion(new PlanId("Starter"), new PlanVersionId("starter-v1"), new VndMoney(199000), new EntitlementTerms(true, 3, 1, false, 500), true),
                new PlanVersion(new PlanId("Growth"), new PlanVersionId("growth-v1"), new VndMoney(499000), new EntitlementTerms(true, 10, 3, true, 2000), true),
                new PlanVersion(new PlanId("Business"), new PlanVersionId("business-v1"), new VndMoney(999000), new EntitlementTerms(true, 30, 10, true, 10000), true)
            ],
            seed.PlanVersions);
    }

    [Fact]
    public async Task BootstrapIsIdempotentForEquivalentImmutableTerms()
    {
        var store = new InMemorySubscriptionCatalogStore();
        var bootstrapper = new CatalogBootstrapService(store);
        var seed = LoadInitialSeed();

        var first = await bootstrapper.BootstrapAsync(seed, CancellationToken.None);
        var second = await bootstrapper.BootstrapAsync(seed, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.All(first.Entries, entry => Assert.Equal(CatalogBootstrapEntryOutcome.Created, entry.Outcome));
        Assert.True(second.Succeeded);
        Assert.All(second.Entries, entry => Assert.Equal(CatalogBootstrapEntryOutcome.AlreadyApplied, entry.Outcome));
    }

    [Fact]
    public async Task ReusingImmutablePlanVersionIdentityWithDifferentTermsIsExplicitConflict()
    {
        var store = new InMemorySubscriptionCatalogStore();
        var bootstrapper = new CatalogBootstrapService(store);
        var initial = LoadInitialSeed();
        await bootstrapper.BootstrapAsync(initial, CancellationToken.None);
        var changedStarter = initial.PlanVersions.Single(plan => plan.PlanId.Value == "Starter") with
        {
            MonthlyPrice = new VndMoney(299000)
        };
        var conflicting = new CatalogSeed(
            initial.Version,
            initial.TrialTermsVersion,
            initial.PlanVersions.Select(plan => plan.PlanId.Value == "Starter" ? changedStarter : plan).ToArray());

        var result = await bootstrapper.BootstrapAsync(conflicting, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Entries,
            entry => entry.RecordId == CatalogRecordId.For(changedStarter) && entry.Outcome == CatalogBootstrapEntryOutcome.VersionConflict);
    }

    [Fact]
    public async Task CatalogQueryReturnsOnlyAvailablePaidPlanVersionsAndTrialRemainsSeparate()
    {
        var store = new InMemorySubscriptionCatalogStore();
        var seed = LoadInitialSeed();
        await new CatalogBootstrapService(store).BootstrapAsync(seed, CancellationToken.None);
        var query = new CatalogQueryService(store);

        var plans = await query.ListAvailablePlanVersionsAsync(CancellationToken.None);
        var trial = await query.GetTrialTermsVersionAsync(seed.TrialTermsVersion.Id.Value, CancellationToken.None);

        Assert.Equal(["Business", "Growth", "Starter"], plans.Select(plan => plan.PlanId));
        Assert.NotNull(trial);
        Assert.Equal(seed.TrialTermsVersion.Id.Value, trial.TrialTermsVersionId);
        Assert.Equal(seed.TrialTermsVersion.DurationDays, trial.DurationDays);
        Assert.Equal(seed.TrialTermsVersion.Entitlements.MaxActiveMemberships, trial.MaxActiveMemberships);
        Assert.Equal(seed.TrialTermsVersion.Entitlements.ScheduledProductIngestion, trial.ScheduledProductIngestion);
        Assert.DoesNotContain(plans, plan => plan.PlanId == "Trial");
    }

    private static CatalogSeed LoadInitialSeed()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "initial-catalog.v1.json"));
        return CatalogSeedLoader.Load(stream);
    }

    private sealed class InMemorySubscriptionCatalogStore : ISubscriptionCatalogStore
    {
        private readonly Dictionary<CatalogRecordId, CatalogRecord> _records = [];

        public Task<CatalogRecord?> GetAsync(CatalogRecordId id, CancellationToken cancellationToken) =>
            Task.FromResult(_records.GetValueOrDefault(id));

        public Task<CatalogRecordCreateResult> CreateIfAbsentAsync(CatalogRecord record, CancellationToken cancellationToken) =>
            Task.FromResult(
                _records.TryAdd(record.Id, record)
                    ? CatalogRecordCreateResult.Created
                    : CatalogRecordCreateResult.AlreadyExists);

        public Task<IReadOnlyList<PlanVersion>> ListAvailablePlanVersionsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<PlanVersion> plans = _records.Values
                .Select(record => record.PlanVersion)
                .OfType<PlanVersion>()
                .Where(planVersion => planVersion.IsAvailableForNewPurchase)
                .OrderBy(planVersion => planVersion.PlanId.Value, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(plans);
        }
    }
}
