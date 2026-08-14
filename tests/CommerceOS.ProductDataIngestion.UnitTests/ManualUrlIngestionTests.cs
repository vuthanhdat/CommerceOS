using CommerceOS.ProductDataIngestion.Application;
using CommerceOS.ProductDataIngestion.Domain;

namespace CommerceOS.ProductDataIngestion.UnitTests;

public sealed class ManualUrlIngestionTests
{
    [Fact]
    public async Task EnabledReviewedSourceAcceptsOnlyBoundedOfficialProductUrlsAndDedupesWork()
    {
        var store = new Store(); var service = new ManualUrlIngestionService(store, store); var context = new TrustedPdiTenantContext(new("tenant"), "c");
        Assert.Equal(PdiOutcome.NotEligible, await service.RequestAsync(context, new("open-food-facts"), "https://evil.test/api/v3.6/product/1.json", "key", default));
        Assert.Equal(PdiOutcome.Applied, await service.RequestAsync(context, new("open-food-facts"), "https://world.openfoodfacts.org/api/v3.6/product/3274080005003.json", "key", default));
        Assert.Equal(PdiOutcome.RevisionConflict, await service.RequestAsync(context, new("open-food-facts"), "https://world.openfoodfacts.org/api/v3.6/product/3274080005003.json", "key", default));
    }
    private sealed class Store : IPdiGovernanceStore, IManualAcquisitionWorkStore
    {
        private readonly HashSet<string> work = [];
        public Task<DataSource?> GetSourceAsync(DataSourceId id, CancellationToken ct) => Task.FromResult<DataSource?>(id.Value == "open-food-facts" ? new(id, "Open Food Facts", SourceStatus.Enabled, PolicyReviewStatus.Current, "policy-2026-08-14", 10, 1) : null);
        public Task<IReadOnlyList<DataSource>> ListSourcesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DataSource>>([]);
        public Task<TenantSourceEnrollment?> GetEnrollmentAsync(TrustedPdiTenantContext c, DataSourceId id, CancellationToken ct) => Task.FromResult<TenantSourceEnrollment?>(null);
        public Task<PdiOutcome> SaveSourceAsync(DataSource s, long? revision, CancellationToken ct) => Task.FromResult(PdiOutcome.Applied);
        public Task<PdiOutcome> SaveEnrollmentAsync(TrustedPdiTenantContext c, TenantSourceEnrollment e, long? revision, CancellationToken ct) => Task.FromResult(PdiOutcome.Applied);
        public Task<PdiOutcome> EnqueueIfAbsentAsync(ManualAcquisitionRequest request, CancellationToken ct) => Task.FromResult(work.Add(request.WorkIdentity) ? PdiOutcome.Applied : PdiOutcome.RevisionConflict);
    }
}
