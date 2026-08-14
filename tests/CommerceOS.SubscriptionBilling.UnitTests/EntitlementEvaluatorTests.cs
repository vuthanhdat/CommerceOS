using CommerceOS.SubscriptionBilling.Application.Entitlements;
using CommerceOS.SubscriptionBilling.Application.Trial;
using CommerceOS.SubscriptionBilling.Contracts;
using System.Globalization;

namespace CommerceOS.SubscriptionBilling.UnitTests;

public sealed class EntitlementEvaluatorTests
{
    [Fact]
    public async Task MissingOrExpiredSubscriptionFailsClosed()
    {
        var store = new Store();
        var evaluator = new EntitlementEvaluator(store);
        var now = Utc("2026-08-14T00:00:00Z");

        var missing = await evaluator.EvaluateEntitlementAsync(new("none", EntitlementKey.MaxWarehouses, now, "c"), CancellationToken.None);
        store.Value = Subscription(now.AddDays(-30), now);
        var expired = await evaluator.EvaluateEntitlementAsync(new("tenant", EntitlementKey.MaxWarehouses, now, "c"), CancellationToken.None);

        Assert.Equal(EntitlementDecisionOutcome.SubscriptionRequired, missing.Outcome);
        Assert.Equal(EntitlementDecisionOutcome.SubscriptionEnded, expired.Outcome);
    }

    [Fact]
    public async Task CurrentTrialReturnsOnlyRequiredDecisionAndProvenance()
    {
        var now = Utc("2026-08-14T00:00:00Z");
        var evaluator = new EntitlementEvaluator(new Store { Value = Subscription(now, now.AddDays(30)) });

        var limit = await evaluator.EvaluateEntitlementAsync(new("tenant", EntitlementKey.MaxActiveMemberships, now, "c"), CancellationToken.None);
        var capability = await evaluator.EvaluateEntitlementAsync(new("tenant", EntitlementKey.ScheduledProductIngestion, now, "c"), CancellationToken.None);

        Assert.Equal(EntitlementDecisionOutcome.Granted, limit.Outcome);
        Assert.Equal(3, limit.Limit);
        Assert.True(capability.CapabilityEnabled);
        Assert.Equal("trial-v1", capability.EntitlementSourceVersion);
    }

    [Fact]
    public async Task PastDueRetainsTermsWhileEndedRemovesOperationalEntitlements()
    {
        var now = Utc("2026-08-14T00:00:00Z");
        var store = new Store { Value = Subscription(now.AddDays(-1), now.AddDays(6)) with { Condition = SubscriptionCondition.PastDue } };
        var evaluator = new EntitlementEvaluator(store);

        var duringGrace = await evaluator.EvaluateEntitlementAsync(new("tenant", EntitlementKey.MaxWarehouses, now, "c"), CancellationToken.None);
        store.Value = store.Value with { Condition = SubscriptionCondition.Ended };
        var ended = await evaluator.EvaluateEntitlementAsync(new("tenant", EntitlementKey.MaxWarehouses, now, "c"), CancellationToken.None);

        Assert.Equal(EntitlementDecisionOutcome.Granted, duringGrace.Outcome);
        Assert.Equal(1, duringGrace.Limit);
        Assert.Equal(EntitlementDecisionOutcome.SubscriptionEnded, ended.Outcome);
    }

    private static TrialSubscription Subscription(DateTimeOffset from, DateTimeOffset until) => new("tenant", "onb", "source", new("trial-v1", 30, true, 3, 1, true, 500), from, until);

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private sealed class Store : ITrialSubscriptionStore
    {
        public TrialSubscription? Value { get; set; }
        public Task<TrialSubscription?> GetForOnboardingAsync(string tenantId, string onboardingOperationId, CancellationToken cancellationToken) => Task.FromResult(Value);
        public Task<TrialSubscription?> GetCurrentForTenantAsync(string tenantId, CancellationToken cancellationToken) => Task.FromResult(tenantId == "tenant" ? Value : null);
        public Task<bool> CreateIfAbsentAsync(TrialSubscription subscription, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
