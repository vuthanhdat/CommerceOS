namespace CommerceOS.SubscriptionBilling.Domain;

public readonly record struct PlanId
{
    public PlanId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("PlanId must not be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct PlanVersionId
{
    public PlanVersionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("PlanVersionId must not be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct TrialTermsVersionId
{
    public TrialTermsVersionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("TrialTermsVersionId must not be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct VndMoney
{
    public VndMoney(long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "VND amount must not be negative.");
        }

        Amount = amount;
    }

    public long Amount { get; }
}

public sealed record EntitlementTerms
{
    public EntitlementTerms(
        bool coreCommerceCapabilities,
        int maxActiveMemberships,
        int maxWarehouses,
        bool scheduledProductIngestion,
        int orderVolumeWarningThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxActiveMemberships, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWarehouses, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(orderVolumeWarningThreshold, 1);

        CoreCommerceCapabilities = coreCommerceCapabilities;
        MaxActiveMemberships = maxActiveMemberships;
        MaxWarehouses = maxWarehouses;
        ScheduledProductIngestion = scheduledProductIngestion;
        OrderVolumeWarningThreshold = orderVolumeWarningThreshold;
    }

    public bool CoreCommerceCapabilities { get; }

    public int MaxActiveMemberships { get; }

    public int MaxWarehouses { get; }

    public bool ScheduledProductIngestion { get; }

    public int OrderVolumeWarningThreshold { get; }
}

public sealed record PlanVersion(
    PlanId PlanId,
    PlanVersionId Id,
    VndMoney MonthlyPrice,
    EntitlementTerms Entitlements,
    bool IsAvailableForNewPurchase);

public sealed record TrialTermsVersion
{
    public TrialTermsVersion(TrialTermsVersionId id, int durationDays, EntitlementTerms entitlements)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(durationDays, 1);

        ArgumentNullException.ThrowIfNull(entitlements);
        Id = id;
        DurationDays = durationDays;
        Entitlements = entitlements;
    }

    public TrialTermsVersionId Id { get; }

    public int DurationDays { get; }

    public EntitlementTerms Entitlements { get; }
}
