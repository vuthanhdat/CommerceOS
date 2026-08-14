using CommerceOS.SubscriptionBilling.Application.Usage;
using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.SubscriptionBilling.UnitTests;

public sealed class OrderVolumeUsageMeterTests
{
    [Fact]
    public async Task DuplicateConfirmedOrderCountsOnceAndThresholdIsWarningOnly()
    {
        var store = new Store(); var meter = new OrderVolumeUsageMeter(new Entitlements(), store);
        var fact = new OrderConfirmedUsageFact("event-1", "tenant-a", "order-1", DateTimeOffset.UtcNow, "c");
        Assert.Equal(OrderVolumeUsageOutcome.Applied, await meter.ConsumeAsync(fact, default));
        Assert.Equal(OrderVolumeUsageOutcome.AlreadyApplied, await meter.ConsumeAsync(fact, default));
        var usage = await meter.GetForTenantAsync("tenant-a", fact.OccurredAt, default);
        Assert.Equal(1, usage!.Count); Assert.False(usage.WarningReached);
    }
    private sealed class Entitlements : IEntitlementEvaluator { public Task<EffectiveEntitlementDecision> EvaluateEntitlementAsync(EvaluateEntitlementRequest x, CancellationToken ct) => Task.FromResult(new EffectiveEntitlementDecision(EntitlementDecisionOutcome.Granted, null, 2, "terms-v1", x.EvaluatedAt.AddDays(-1), x.EvaluatedAt.AddDays(29))); }
    private sealed class Store : IOrderVolumeUsageStore
    {
        private readonly HashSet<string> sources = []; private OrderVolumeUsage? usage;
        public Task<OrderVolumeUsageOutcome> ApplyAsync(OrderConfirmedUsageFact fact, OrderVolumeUsage template, CancellationToken ct) { if (!sources.Add(fact.EventId)) return Task.FromResult(OrderVolumeUsageOutcome.AlreadyApplied); var count = (usage?.Count ?? 0) + 1; usage = template with { Count = count, WarningReached = count >= template.Threshold }; return Task.FromResult(OrderVolumeUsageOutcome.Applied); }
        public Task<OrderVolumeUsage?> GetAsync(string tenant, DateTimeOffset at, CancellationToken ct) => Task.FromResult(usage?.TenantId == tenant ? usage : null);
    }
}
