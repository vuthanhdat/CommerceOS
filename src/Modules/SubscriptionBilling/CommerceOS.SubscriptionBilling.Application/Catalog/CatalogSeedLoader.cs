using System.Text.Json;
using CommerceOS.SubscriptionBilling.Domain;

namespace CommerceOS.SubscriptionBilling.Application.Catalog;

public static class CatalogSeedLoader
{
    public static CatalogSeed Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var document = JsonSerializer.Deserialize<CatalogSeedDocument>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Catalog seed is empty.");

        return new CatalogSeed(
            document.Version,
            ToTrialTerms(document.Trial),
            document.PaidPlans.Select(ToPlanVersion).ToArray());
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static TrialTermsVersion ToTrialTerms(TermsDocument document) => new(
        new TrialTermsVersionId(document.Version),
        document.DurationDays,
        ToEntitlements(document));

    private static PlanVersion ToPlanVersion(PaidPlanDocument document) => new(
        new PlanId(document.Plan),
        new PlanVersionId(document.Version),
        new VndMoney(document.MonthlyPriceVnd),
        ToEntitlements(document),
        document.IsAvailableForNewPurchase);

    private static EntitlementTerms ToEntitlements(TermsDocument document) => new(
        document.CoreCommerceCapabilities,
        document.MaxActiveMemberships,
        document.MaxWarehouses,
        document.ScheduledProductIngestion,
        document.OrderVolumeWarningThreshold);

    private static EntitlementTerms ToEntitlements(PaidPlanDocument document) => new(
        document.CoreCommerceCapabilities,
        document.MaxActiveMemberships,
        document.MaxWarehouses,
        document.ScheduledProductIngestion,
        document.OrderVolumeWarningThreshold);

    private sealed record CatalogSeedDocument(string Version, TermsDocument Trial, IReadOnlyList<PaidPlanDocument> PaidPlans);

    private sealed record TermsDocument(
        string Version,
        int DurationDays,
        bool CoreCommerceCapabilities,
        int MaxActiveMemberships,
        int MaxWarehouses,
        bool ScheduledProductIngestion,
        int OrderVolumeWarningThreshold);

    private sealed record PaidPlanDocument(
        string Plan,
        string Version,
        long MonthlyPriceVnd,
        bool CoreCommerceCapabilities,
        int MaxActiveMemberships,
        int MaxWarehouses,
        bool ScheduledProductIngestion,
        int OrderVolumeWarningThreshold,
        bool IsAvailableForNewPurchase);
}
