using CommerceOS.SubscriptionBilling.Domain;
using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.SubscriptionBilling.Application.Catalog;

public sealed class CatalogQueryService : ISubscriptionCatalogQuery
{
    private readonly ISubscriptionCatalogStore _store;

    public CatalogQueryService(ISubscriptionCatalogStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<SellablePlanVersion>> ListAvailablePlanVersionsAsync(CancellationToken cancellationToken) =>
        (await _store.ListAvailablePlanVersionsAsync(cancellationToken))
        .Select(planVersion => new SellablePlanVersion(
            planVersion.PlanId.Value,
            planVersion.Id.Value,
            planVersion.MonthlyPrice.Amount,
            planVersion.Entitlements.CoreCommerceCapabilities,
            planVersion.Entitlements.MaxActiveMemberships,
            planVersion.Entitlements.MaxWarehouses,
            planVersion.Entitlements.ScheduledProductIngestion,
            planVersion.Entitlements.OrderVolumeWarningThreshold))
        .ToArray();

    public async Task<TrialTermsCatalogVersion?> GetTrialTermsVersionAsync(
        string trialTermsVersionId,
        CancellationToken cancellationToken)
    {
        var record = await _store.GetAsync(CatalogRecordId.For(new TrialTermsVersionId(trialTermsVersionId)), cancellationToken);
        var trialTerms = record?.TrialTermsVersion;
        return trialTerms is null
            ? null
            : new TrialTermsCatalogVersion(
                trialTerms.Id.Value,
                trialTerms.DurationDays,
                trialTerms.Entitlements.CoreCommerceCapabilities,
                trialTerms.Entitlements.MaxActiveMemberships,
                trialTerms.Entitlements.MaxWarehouses,
                trialTerms.Entitlements.ScheduledProductIngestion,
                trialTerms.Entitlements.OrderVolumeWarningThreshold);
    }
}
