using CommerceOS.SubscriptionBilling.Domain;

namespace CommerceOS.SubscriptionBilling.Application.Catalog;

public readonly record struct CatalogRecordId
{
    private CatalogRecordId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CatalogRecordId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Catalog record identity must not be empty.", nameof(value));
        }

        return new CatalogRecordId(value);
    }

    public static CatalogRecordId For(PlanVersion planVersion) =>
        For(planVersion.PlanId, planVersion.Id);

    public static CatalogRecordId For(TrialTermsVersion trialTermsVersion) =>
        For(trialTermsVersion.Id);

    public static CatalogRecordId For(PlanId planId, PlanVersionId planVersionId) =>
        new($"PLANVERSION#{planId.Value}#{planVersionId.Value}");

    public static CatalogRecordId For(TrialTermsVersionId trialTermsVersionId) =>
        new($"TRIALTERMS#{trialTermsVersionId.Value}");

    public override string ToString() => Value;
}

public sealed record CatalogRecord
{
    public CatalogRecord(CatalogRecordId id, PlanVersion? planVersion, TrialTermsVersion? trialTermsVersion)
    {
        if ((planVersion is null) == (trialTermsVersion is null))
        {
            throw new ArgumentException("A catalog record must contain exactly one terms version.");
        }

        Id = id;
        PlanVersion = planVersion;
        TrialTermsVersion = trialTermsVersion;
    }

    public CatalogRecordId Id { get; }

    public PlanVersion? PlanVersion { get; }

    public TrialTermsVersion? TrialTermsVersion { get; }

    public static CatalogRecord For(PlanVersion planVersion) => new(CatalogRecordId.For(planVersion), planVersion, null);

    public static CatalogRecord For(TrialTermsVersion trialTermsVersion) => new(CatalogRecordId.For(trialTermsVersion), null, trialTermsVersion);
}

public sealed record CatalogSeed(string Version, TrialTermsVersion TrialTermsVersion, IReadOnlyList<PlanVersion> PlanVersions)
{
    public IReadOnlyList<CatalogRecord> Records =>
        [CatalogRecord.For(TrialTermsVersion), .. PlanVersions.Select(CatalogRecord.For)];
}

public enum CatalogRecordCreateResult
{
    Created,
    AlreadyExists
}

/// <summary>
/// SubscriptionBilling-owned platform catalog persistence port. Runtime catalog reads
/// use direct record lookup or a bounded platform partition Query, never a Scan.
/// </summary>
public interface ISubscriptionCatalogStore
{
    Task<CatalogRecord?> GetAsync(CatalogRecordId id, CancellationToken cancellationToken);

    Task<CatalogRecordCreateResult> CreateIfAbsentAsync(CatalogRecord record, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanVersion>> ListAvailablePlanVersionsAsync(CancellationToken cancellationToken);
}

public enum CatalogBootstrapEntryOutcome
{
    Created,
    AlreadyApplied,
    VersionConflict
}

public sealed record CatalogBootstrapEntryResult(CatalogRecordId RecordId, CatalogBootstrapEntryOutcome Outcome);

public sealed record CatalogBootstrapResult(IReadOnlyList<CatalogBootstrapEntryResult> Entries)
{
    public bool Succeeded => Entries.All(entry => entry.Outcome is not CatalogBootstrapEntryOutcome.VersionConflict);
}
