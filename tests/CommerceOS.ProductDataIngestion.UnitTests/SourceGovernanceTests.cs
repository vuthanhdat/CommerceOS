using CommerceOS.ProductDataIngestion.Application;
using CommerceOS.ProductDataIngestion.Domain;
using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.ProductDataIngestion.UnitTests;

public sealed class SourceGovernanceTests
{
    [Fact]
    public async Task TenantCannotEnableDisabledOrStaleSource()
    {
        var store = new Store(new(new("source"), "Source", SourceStatus.Disabled, PolicyReviewStatus.Current, "v1", 10, 1));
        var service = new SourceGovernanceService(store, new Entitlements());
        var outcome = await service.EnableForTenantAsync(new(new("tenant"), "c"), new("source"), default);
        Assert.Equal(PdiOutcome.NotEligible, outcome);
    }

    [Fact]
    public async Task ScheduledEligibilityRequiresPlatformTenantAndEntitlement()
    {
        var source = new DataSource(new("source"), "Source", SourceStatus.Enabled, PolicyReviewStatus.Current, "v2", 10, 1);
        var store = new Store(source);
        var context = new TrustedPdiTenantContext(new("tenant"), "c");
        var service = new SourceGovernanceService(store, new Entitlements());
        Assert.Equal(PdiOutcome.Applied, await service.EnableForTenantAsync(context, source.Id, default));
        Assert.True(await service.IsEligibleForScheduledRunAsync(context, source.Id, default));
    }

    private sealed class Store(DataSource source) : IPdiGovernanceStore
    {
        private TenantSourceEnrollment? enrollment;
        public Task<DataSource?> GetSourceAsync(DataSourceId id, CancellationToken ct) => Task.FromResult<DataSource?>(source.Id == id ? source : null);
        public Task<TenantSourceEnrollment?> GetEnrollmentAsync(TrustedPdiTenantContext context, DataSourceId id, CancellationToken ct) => Task.FromResult(enrollment?.TenantId == context.TenantId && enrollment.SourceId == id ? enrollment : null);
        public Task<PdiOutcome> SaveSourceAsync(DataSource value, long? expectedRevision, CancellationToken ct) => Task.FromResult(PdiOutcome.Applied);
        public Task<PdiOutcome> SaveEnrollmentAsync(TrustedPdiTenantContext context, TenantSourceEnrollment value, long? expectedRevision, CancellationToken ct) { enrollment = value; return Task.FromResult(PdiOutcome.Applied); }
    }

    private sealed class Entitlements : IEntitlementEvaluator
    {
        public Task<EffectiveEntitlementDecision> EvaluateEntitlementAsync(EvaluateEntitlementRequest request, CancellationToken cancellationToken) => Task.FromResult(new EffectiveEntitlementDecision(EntitlementDecisionOutcome.Granted, true, null, "test", null, null));
    }
}
