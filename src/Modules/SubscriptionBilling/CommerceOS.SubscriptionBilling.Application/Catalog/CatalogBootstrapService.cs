namespace CommerceOS.SubscriptionBilling.Application.Catalog;

public sealed class CatalogBootstrapService
{
    private readonly ISubscriptionCatalogStore _store;

    public CatalogBootstrapService(ISubscriptionCatalogStore store)
    {
        _store = store;
    }

    public async Task<CatalogBootstrapResult> BootstrapAsync(CatalogSeed seed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ValidateSeed(seed);

        var entries = new List<CatalogBootstrapEntryResult>();
        foreach (var desired in seed.Records)
        {
            var existing = await _store.GetAsync(desired.Id, cancellationToken);
            if (existing is not null)
            {
                entries.Add(new CatalogBootstrapEntryResult(
                    desired.Id,
                    existing == desired ? CatalogBootstrapEntryOutcome.AlreadyApplied : CatalogBootstrapEntryOutcome.VersionConflict));
                continue;
            }

            var create = await _store.CreateIfAbsentAsync(desired, cancellationToken);
            if (create is CatalogRecordCreateResult.Created)
            {
                entries.Add(new CatalogBootstrapEntryResult(desired.Id, CatalogBootstrapEntryOutcome.Created));
                continue;
            }

            var racedExisting = await _store.GetAsync(desired.Id, cancellationToken);
            entries.Add(new CatalogBootstrapEntryResult(
                desired.Id,
                racedExisting == desired ? CatalogBootstrapEntryOutcome.AlreadyApplied : CatalogBootstrapEntryOutcome.VersionConflict));
        }

        return new CatalogBootstrapResult(entries);
    }

    private static void ValidateSeed(CatalogSeed seed)
    {
        if (string.IsNullOrWhiteSpace(seed.Version))
        {
            throw new ArgumentException("Catalog seed version must not be empty.", nameof(seed));
        }

        if (seed.PlanVersions.Count != 3 || seed.PlanVersions.Select(version => version.PlanId).Distinct().Count() != 3)
        {
            throw new ArgumentException("The initial catalog seed must contain one version for each paid plan.", nameof(seed));
        }

        if (seed.Records.Select(record => record.Id).Distinct().Count() != seed.Records.Count)
        {
            throw new ArgumentException("Catalog record identities must be unique.", nameof(seed));
        }
    }
}
