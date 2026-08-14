using CommerceOS.Catalog.Contracts;
using CommerceOS.ProductDataIngestion.Application;
using CommerceOS.ProductDataIngestion.Domain;
using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.ProductDataIngestion.UnitTests;

public sealed class PdiPipelineAndReviewTests
{
    private static readonly ManualAcquisitionRequest Work = new("request-1", new("tenant-a"), new("source"), new Uri("https://world.openfoodfacts.org/api/v3.6/product/3274080005003.json"), "manual-url:1", "correlation");

    [Fact]
    public async Task DuplicateDeliveryPersistsOneImmutableEvidenceSet()
    {
        var evidence = new Evidence(); var worker = new PdiCrawlWorker(new AdapterResolver(new Adapter(new(PdiFailureKind.None, "fixture-payload", "3274080005003", "Fixture name", "fixture-sku", 1000, "fixture-v1"))), evidence, evidence, evidence);
        Assert.Equal(PdiWorkerDisposition.Completed, (await worker.ProcessAsync(Work, 1, default)).Disposition);
        Assert.Equal(PdiWorkerDisposition.Duplicate, (await worker.ProcessAsync(Work, 1, default)).Disposition);
        Assert.Single(evidence.Source); Assert.Single(evidence.Normalized); Assert.Empty(evidence.DeadLetters);
    }

    [Fact]
    public async Task PermanentFailureGoesStraightToDeadLetterButTransientFailureIsBoundedlyRetried()
    {
        var evidence = new Evidence();
        var permanent = new PdiCrawlWorker(new AdapterResolver(new Adapter(new(PdiFailureKind.ParserRejected, null, null, null, null, null, "v1", "fixture shape changed"))), evidence, evidence, evidence);
        Assert.Equal(PdiWorkerDisposition.DeadLettered, (await permanent.ProcessAsync(Work, 1, default)).Disposition);
        var transient = new PdiCrawlWorker(new AdapterResolver(new Adapter(new(PdiFailureKind.Transient, null, null, null, null, null, "v1"))), evidence, evidence, evidence, maximumAttempts: 3);
        Assert.Equal(PdiWorkerDisposition.Retry, (await transient.ProcessAsync(Work, 1, default)).Disposition);
        Assert.Equal(PdiWorkerDisposition.DeadLettered, (await transient.ProcessAsync(Work, 3, default)).Disposition);
        Assert.Equal(2, evidence.DeadLetters.Count);
    }

    [Fact]
    public async Task ReviewIsTenantScopedAndCatalogApplyOccursOnlyAfterApproval()
    {
        var store = new CandidateStore(); var catalog = new Catalog(); var service = new ImportCandidateReviewService(store, catalog);
        var tenant = new TrustedPdiTenantContext(new("tenant-a"), "c");
        var candidate = new ImportCandidate("candidate-1", new("tenant-a"), "snapshot-1", new("source"), "source-product", "product-1", 2, "Reviewed name", "sku", 1000, ImportCandidateStatus.Ready, null, 0);
        Assert.Equal(PdiOutcome.Applied, await service.CreateReadyAsync(tenant, candidate, default));
        Assert.Equal(PdiOutcome.NotFound, await service.ApproveAsync(new(new("tenant-b"), "c", "actor", true), candidate.Id, null, default));
        var reviewer = new TrustedPdiReviewContext(new("tenant-a"), "c", "owner", true);
        Assert.Equal(PdiOutcome.NotEligible, await service.ApplyApprovedAsync(reviewer, candidate.Id, default));
        Assert.Equal(PdiOutcome.Applied, await service.ApproveAsync(reviewer, candidate.Id, "verified", default));
        Assert.Equal(PdiOutcome.Applied, await service.ApplyApprovedAsync(reviewer, candidate.Id, default));
        Assert.Equal(1, catalog.ApplyCalls);
        Assert.Equal(ImportCandidateStatus.Applied, (await service.GetAsync(tenant, candidate.Id, default))!.Status);
    }

    [Fact]
    public async Task ScheduledDispatchRechecksEntitlementAndDedupesDueWindow()
    {
        var governance = new Governance(true); var schedules = new ScheduleStore(); var work = new WorkStore(); var dispatcher = new ScheduledRefreshDispatcher(schedules, new SourceGovernanceService(governance, governance), work);
        var context = new TrustedPdiTenantContext(new("tenant-a"), "c"); var schedule = new ScheduledSourceRefresh("schedule-1", context.TenantId, new("source"), Work.Url, true, 1);
        await schedules.SaveAsync(context, schedule, null, default);
        var due = new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(ScheduledRefreshDispatchOutcome.Enqueued, await dispatcher.DispatchAsync(context, schedule.Id, due, default));
        Assert.Equal(ScheduledRefreshDispatchOutcome.Duplicate, await dispatcher.DispatchAsync(context, schedule.Id, due, default));
        governance.Eligible = false;
        Assert.Equal(ScheduledRefreshDispatchOutcome.Suppressed, await dispatcher.DispatchAsync(context, schedule.Id, due.AddMinutes(1), default));
        Assert.NotNull((await schedules.GetScheduleAsync(context, schedule.Id, default))!.LastSuppressedAt);
    }

    private sealed class Adapter(AdapterFetchResult result) : IApprovedSourceAdapter { public Task<AdapterFetchResult> FetchAsync(ManualAcquisitionRequest w, CancellationToken ct) => Task.FromResult(result); }
    private sealed class AdapterResolver(IApprovedSourceAdapter adapter) : IAuthorizedSourceAdapterResolver { public Task<IApprovedSourceAdapter?> GetAdapterAsync(DataSourceId sourceId, CancellationToken ct) => Task.FromResult<IApprovedSourceAdapter?>(adapter); }
    private sealed class Evidence : IRawPdiSnapshotStore, ISourceSnapshotStore, IPdiDeadLetterSink
    {
        public HashSet<string> Source { get; } = []; public HashSet<string> Normalized { get; } = []; public List<string> DeadLetters { get; } = [];
        public Task<string> StoreIfAbsentAsync(ManualAcquisitionRequest w, string hash, string payload, CancellationToken ct) => Task.FromResult($"raw/{hash}");
        public Task<bool> SaveSourceSnapshotIfAbsentAsync(SourceSnapshot s, CancellationToken ct) => Task.FromResult(Source.Add(s.Id));
        public Task<bool> SaveNormalizedSnapshotIfAbsentAsync(NormalizedSourceSnapshot s, CancellationToken ct) => Task.FromResult(Normalized.Add(s.Id));
        public Task SendAsync(ManualAcquisitionRequest w, PdiFailureKind k, int attempt, string? detail, CancellationToken ct) { DeadLetters.Add(w.WorkIdentity); return Task.CompletedTask; }
    }
    private sealed class CandidateStore : IImportCandidateStore
    {
        private readonly Dictionary<string, ImportCandidate> values = [];
        public Task<ImportCandidate?> GetAsync(TrustedPdiTenantContext c, string id, CancellationToken ct) => Task.FromResult(values.TryGetValue(id, out var x) && x.TenantId == c.TenantId ? x : null);
        public Task<IReadOnlyList<ImportCandidate>> ListAsync(TrustedPdiTenantContext c, ImportCandidateStatus? status, string? search, CancellationToken ct) => Task.FromResult<IReadOnlyList<ImportCandidate>>(values.Values.Where(x => x.TenantId == c.TenantId && (status is null || x.Status == status)).ToArray());
        public Task<PdiOutcome> SaveIfRevisionAsync(TrustedPdiTenantContext c, ImportCandidate x, long? expected, CancellationToken ct)
        { if (x.TenantId != c.TenantId || (expected is null ? values.ContainsKey(x.Id) : !values.TryGetValue(x.Id, out var old) || old.Revision != expected)) return Task.FromResult(PdiOutcome.RevisionConflict); values[x.Id] = x; return Task.FromResult(PdiOutcome.Applied); }
    }
    private sealed class Catalog : IApprovedImportCandidateApplier { public int ApplyCalls { get; private set; } public Task<ImportCandidateApplicationResult> ApplyAsync(ApplyApprovedImportCandidate c, CancellationToken ct) { ApplyCalls++; return Task.FromResult(new ImportCandidateApplicationResult(ImportCandidateApplicationOutcome.Applied, 3)); } }
    private sealed class WorkStore : IManualAcquisitionWorkStore { private readonly HashSet<string> work = []; public Task<PdiOutcome> EnqueueIfAbsentAsync(ManualAcquisitionRequest r, CancellationToken ct) => Task.FromResult(work.Add(r.WorkIdentity) ? PdiOutcome.Applied : PdiOutcome.RevisionConflict); }
    private sealed class ScheduleStore : IScheduledSourceRefreshStore
    {
        private ScheduledSourceRefresh? value;
        public Task<ScheduledSourceRefresh?> GetScheduleAsync(TrustedPdiTenantContext c, string id, CancellationToken ct) => Task.FromResult(value?.TenantId == c.TenantId && value.Id == id ? value : null);
        public Task<PdiOutcome> SaveAsync(TrustedPdiTenantContext c, ScheduledSourceRefresh x, long? expected, CancellationToken ct) { if (x.TenantId != c.TenantId || (expected is not null && value?.Revision != expected)) return Task.FromResult(PdiOutcome.RevisionConflict); value = x; return Task.FromResult(PdiOutcome.Applied); }
    }
    private sealed class Governance(bool eligible) : IPdiGovernanceStore, IEntitlementEvaluator
    {
        public bool Eligible { get; set; } = eligible;
        public Task<DataSource?> GetSourceAsync(DataSourceId id, CancellationToken ct) => Task.FromResult<DataSource?>(new(id, "Source", Eligible ? SourceStatus.Enabled : SourceStatus.Paused, PolicyReviewStatus.Current, "v1", 1, 1));
        public Task<IReadOnlyList<DataSource>> ListSourcesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DataSource>>([]);
        public Task<TenantSourceEnrollment?> GetEnrollmentAsync(TrustedPdiTenantContext c, DataSourceId id, CancellationToken ct) => Task.FromResult<TenantSourceEnrollment?>(new(c.TenantId, id, true, 1));
        public Task<PdiOutcome> SaveSourceAsync(DataSource x, long? e, CancellationToken ct) => Task.FromResult(PdiOutcome.Applied);
        public Task<PdiOutcome> SaveEnrollmentAsync(TrustedPdiTenantContext c, TenantSourceEnrollment x, long? e, CancellationToken ct) => Task.FromResult(PdiOutcome.Applied);
        public Task<EffectiveEntitlementDecision> EvaluateEntitlementAsync(EvaluateEntitlementRequest r, CancellationToken ct) => Task.FromResult(new EffectiveEntitlementDecision(Eligible ? EntitlementDecisionOutcome.Granted : EntitlementDecisionOutcome.Denied, Eligible, null, "test", null, null));
    }
}
