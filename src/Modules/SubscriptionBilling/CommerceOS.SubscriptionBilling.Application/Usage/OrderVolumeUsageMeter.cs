using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.SubscriptionBilling.Application.Usage;

/// <summary>Consumer-required business facts; the Sales persistence model is deliberately absent.</summary>
public sealed record OrderConfirmedUsageFact(string EventId, string TenantId, string OrderId, DateTimeOffset OccurredAt, string CorrelationId);
public sealed record OrderVolumeUsage(string TenantId, string PeriodSourceVersion, DateTimeOffset PeriodFrom, DateTimeOffset PeriodUntil, int Count, int Threshold, bool WarningReached);
public enum OrderVolumeUsageOutcome { Applied, AlreadyApplied, SubscriptionNotMetered }

public interface IOrderVolumeUsageStore
{
    Task<OrderVolumeUsageOutcome> ApplyAsync(OrderConfirmedUsageFact fact, OrderVolumeUsage usage, CancellationToken cancellationToken);
    Task<OrderVolumeUsage?> GetAsync(string trustedTenantId, DateTimeOffset evaluatedAt, CancellationToken cancellationToken);
}

public sealed class OrderVolumeUsageMeter(IEntitlementEvaluator entitlements, IOrderVolumeUsageStore store)
{
    public async Task<OrderVolumeUsageOutcome> ConsumeAsync(OrderConfirmedUsageFact fact, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fact.EventId) || string.IsNullOrWhiteSpace(fact.TenantId) || string.IsNullOrWhiteSpace(fact.OrderId))
            return OrderVolumeUsageOutcome.SubscriptionNotMetered;

        var decision = await entitlements.EvaluateEntitlementAsync(new(fact.TenantId, EntitlementKey.OrderVolumeWarningThreshold, fact.OccurredAt, fact.CorrelationId), cancellationToken);
        if (decision.Outcome is not EntitlementDecisionOutcome.Granted || decision.Limit is not > 0 || decision.EntitlementSourceVersion is null || decision.EffectiveFrom is null || decision.EffectiveUntil is null)
            return OrderVolumeUsageOutcome.SubscriptionNotMetered;

        return await store.ApplyAsync(fact, new OrderVolumeUsage(fact.TenantId, decision.EntitlementSourceVersion, decision.EffectiveFrom.Value, decision.EffectiveUntil.Value, 0, decision.Limit.Value, false), cancellationToken);
    }

    public Task<OrderVolumeUsage?> GetForTenantAsync(string trustedTenantId, DateTimeOffset evaluatedAt, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(trustedTenantId) ? Task.FromResult<OrderVolumeUsage?>(null) : store.GetAsync(trustedTenantId, evaluatedAt, cancellationToken);
}
