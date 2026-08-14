namespace CommerceOS.SubscriptionBilling.Contracts;

public enum EntitlementKey
{
    CoreCommerceCapabilities,
    MaxActiveMemberships,
    MaxWarehouses,
    ScheduledProductIngestion,
    OrderVolumeWarningThreshold
}

public enum EntitlementDecisionOutcome
{
    Granted,
    Denied,
    SubscriptionRequired,
    SubscriptionEnded
}

/// <summary>A Tenant identifier is supplied only after caller-owned trusted authority resolution.</summary>
public sealed record EvaluateEntitlementRequest(string TrustedTenantId, EntitlementKey Key, DateTimeOffset EvaluatedAt, string CorrelationId);

/// <summary>Decision/provenance only; callers do not receive Subscription persistence or plan marketing data.</summary>
public sealed record EffectiveEntitlementDecision(EntitlementDecisionOutcome Outcome, bool? CapabilityEnabled, int? Limit, string? EntitlementSourceVersion, DateTimeOffset? EffectiveFrom, DateTimeOffset? EffectiveUntil);

public interface IEntitlementEvaluator
{
    Task<EffectiveEntitlementDecision> EvaluateEntitlementAsync(EvaluateEntitlementRequest request, CancellationToken cancellationToken);
}
