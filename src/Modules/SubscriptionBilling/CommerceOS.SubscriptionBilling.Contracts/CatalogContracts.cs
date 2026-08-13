namespace CommerceOS.SubscriptionBilling.Contracts;

/// <summary>
/// Display/query contract for currently sellable SubscriptionBilling terms. Consumers may not use
/// Plan names or these values as authority; entitlement decisions remain SubscriptionBilling-owned.
/// </summary>
public sealed record SellablePlanVersion(
    string PlanId,
    string PlanVersionId,
    long MonthlyPriceVnd,
    bool CoreCommerceCapabilities,
    int MaxActiveMemberships,
    int MaxWarehouses,
    bool ScheduledProductIngestion,
    int OrderVolumeWarningThreshold);

public sealed record TrialTermsCatalogVersion(
    string TrialTermsVersionId,
    int DurationDays,
    bool CoreCommerceCapabilities,
    int MaxActiveMemberships,
    int MaxWarehouses,
    bool ScheduledProductIngestion,
    int OrderVolumeWarningThreshold);

public interface ISubscriptionCatalogQuery
{
    Task<IReadOnlyList<SellablePlanVersion>> ListAvailablePlanVersionsAsync(CancellationToken cancellationToken);

    Task<TrialTermsCatalogVersion?> GetTrialTermsVersionAsync(
        string trialTermsVersionId,
        CancellationToken cancellationToken);
}
