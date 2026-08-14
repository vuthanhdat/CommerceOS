using CommerceOS.SubscriptionBilling.Application.Trial;
using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.SubscriptionBilling.Application.Entitlements;

public sealed class EntitlementEvaluator : IEntitlementEvaluator
{
    private readonly ITrialSubscriptionStore _store;

    public EntitlementEvaluator(ITrialSubscriptionStore store) => _store = store;

    public async Task<EffectiveEntitlementDecision> EvaluateEntitlementAsync(EvaluateEntitlementRequest request, CancellationToken cancellationToken)
    {
        var subscription = await _store.GetCurrentForTenantAsync(request.TrustedTenantId, cancellationToken);
        if (subscription is null)
        {
            return new(EntitlementDecisionOutcome.SubscriptionRequired, null, null, null, null, null);
        }
        if (subscription.Condition is SubscriptionCondition.Ended || request.EvaluatedAt < subscription.EffectiveFrom || request.EvaluatedAt >= subscription.EffectiveUntil)
        {
            return new(EntitlementDecisionOutcome.SubscriptionEnded, null, null, subscription.Entitlements.TrialTermsVersionId, subscription.EffectiveFrom, subscription.EffectiveUntil);
        }
        var (enabled, limit) = request.Key switch
        {
            EntitlementKey.CoreCommerceCapabilities => (subscription.Entitlements.CoreCommerceCapabilities, (int?)null),
            EntitlementKey.ScheduledProductIngestion => (subscription.Entitlements.ScheduledProductIngestion, (int?)null),
            EntitlementKey.MaxActiveMemberships => (true, subscription.Entitlements.MaxActiveMemberships),
            EntitlementKey.MaxWarehouses => (true, subscription.Entitlements.MaxWarehouses),
            EntitlementKey.OrderVolumeWarningThreshold => (true, subscription.Entitlements.OrderVolumeWarningThreshold),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
        return enabled
            ? new(EntitlementDecisionOutcome.Granted, limit is null ? true : null, limit, subscription.Entitlements.TrialTermsVersionId, subscription.EffectiveFrom, subscription.EffectiveUntil)
            : new(EntitlementDecisionOutcome.Denied, false, null, subscription.Entitlements.TrialTermsVersionId, subscription.EffectiveFrom, subscription.EffectiveUntil);
    }
}
