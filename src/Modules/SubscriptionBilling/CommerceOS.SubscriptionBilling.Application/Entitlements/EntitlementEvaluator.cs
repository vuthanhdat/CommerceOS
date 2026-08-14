using CommerceOS.SubscriptionBilling.Application.PaidLifecycle;
using CommerceOS.SubscriptionBilling.Application.Trial;
using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.SubscriptionBilling.Application.Entitlements;

public sealed class EntitlementEvaluator : IEntitlementEvaluator
{
    private readonly ITrialSubscriptionStore _store;
    private readonly IPaidSubscriptionStore? _paidSubscriptions;

    public EntitlementEvaluator(ITrialSubscriptionStore store, IPaidSubscriptionStore? paidSubscriptions = null)
    {
        _store = store;
        _paidSubscriptions = paidSubscriptions;
    }

    public async Task<EffectiveEntitlementDecision> EvaluateEntitlementAsync(EvaluateEntitlementRequest request, CancellationToken cancellationToken)
    {
        var paid = _paidSubscriptions is null ? null : await _paidSubscriptions.GetCurrentAsync(request.TrustedTenantId, cancellationToken);
        if (paid is not null)
        {
            return Evaluate(
                paid.Condition,
                paid.EffectiveFrom,
                paid.EffectiveUntil,
                paid.Entitlements.PlanVersionId,
                paid.Entitlements.CoreCommerceCapabilities,
                paid.Entitlements.MaxActiveMemberships,
                paid.Entitlements.MaxWarehouses,
                paid.Entitlements.ScheduledProductIngestion,
                paid.Entitlements.OrderVolumeWarningThreshold,
                request,
                false);
        }
        var subscription = await _store.GetCurrentForTenantAsync(request.TrustedTenantId, cancellationToken);
        if (subscription is null)
        {
            return new(EntitlementDecisionOutcome.SubscriptionRequired, null, null, null, null, null);
        }
        return Evaluate(subscription.Condition, subscription.EffectiveFrom, subscription.EffectiveUntil, subscription.Entitlements.TrialTermsVersionId,
            subscription.Entitlements.CoreCommerceCapabilities, subscription.Entitlements.MaxActiveMemberships, subscription.Entitlements.MaxWarehouses,
            subscription.Entitlements.ScheduledProductIngestion, subscription.Entitlements.OrderVolumeWarningThreshold, request, true);
    }

    private static EffectiveEntitlementDecision Evaluate(
        SubscriptionCondition condition,
        DateTimeOffset effectiveFrom,
        DateTimeOffset effectiveUntil,
        string sourceVersion,
        bool coreCommerceCapabilities,
        int maxActiveMemberships,
        int maxWarehouses,
        bool scheduledProductIngestion,
        int orderVolumeWarningThreshold,
        EvaluateEntitlementRequest request,
        bool requirePeriodCurrent)
    {
        if (condition is SubscriptionCondition.Ended || (requirePeriodCurrent && (request.EvaluatedAt < effectiveFrom || request.EvaluatedAt >= effectiveUntil)))
        {
            return new(EntitlementDecisionOutcome.SubscriptionEnded, null, null, sourceVersion, effectiveFrom, effectiveUntil);
        }
        var (enabled, limit) = request.Key switch
        {
            EntitlementKey.CoreCommerceCapabilities => (coreCommerceCapabilities, (int?)null),
            EntitlementKey.ScheduledProductIngestion => (scheduledProductIngestion, (int?)null),
            EntitlementKey.MaxActiveMemberships => (true, maxActiveMemberships),
            EntitlementKey.MaxWarehouses => (true, maxWarehouses),
            EntitlementKey.OrderVolumeWarningThreshold => (true, orderVolumeWarningThreshold),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
        return enabled
            ? new(EntitlementDecisionOutcome.Granted, limit is null ? true : null, limit, sourceVersion, effectiveFrom, effectiveUntil)
            : new(EntitlementDecisionOutcome.Denied, false, null, sourceVersion, effectiveFrom, effectiveUntil);
    }
}
