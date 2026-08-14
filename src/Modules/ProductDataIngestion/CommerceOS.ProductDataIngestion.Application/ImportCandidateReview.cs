using CommerceOS.Catalog.Contracts;
using CommerceOS.ProductDataIngestion.Domain;

namespace CommerceOS.ProductDataIngestion.Application;

public enum ImportCandidateStatus { Ready, Approved, Applied, Rejected, Superseded }
public sealed record TrustedPdiReviewContext(PdiTenantId TenantId, string CorrelationId, string ActorId, bool CanReviewImportCandidates);
public sealed record ImportCandidate(string Id, PdiTenantId TenantId, string SourceSnapshotId, DataSourceId SourceId, string SourceProductId, string ProductId, long ExpectedProductRevision, string Name, string? SourceSku, long? VndPrice, ImportCandidateStatus Status, string? ReviewNote, long Revision);
public interface IImportCandidateStore
{
    Task<ImportCandidate?> GetAsync(TrustedPdiTenantContext context, string candidateId, CancellationToken cancellationToken);
    Task<PdiOutcome> SaveIfRevisionAsync(TrustedPdiTenantContext context, ImportCandidate candidate, long? expectedRevision, CancellationToken cancellationToken);
}
public sealed class ImportCandidateReviewService(IImportCandidateStore candidates, IApprovedImportCandidateApplier catalog)
{
    public Task<ImportCandidate?> GetAsync(TrustedPdiTenantContext context, string candidateId, CancellationToken ct) => candidates.GetAsync(context, candidateId, ct);
    public async Task<PdiOutcome> CreateReadyAsync(TrustedPdiTenantContext context, ImportCandidate candidate, CancellationToken ct)
    {
        if (candidate.TenantId != context.TenantId || candidate.Status is not ImportCandidateStatus.Ready) return PdiOutcome.NotEligible;
        return await candidates.SaveIfRevisionAsync(context, candidate with { Revision = 1 }, null, ct);
    }
    public async Task<PdiOutcome> ApproveAsync(TrustedPdiReviewContext context, string candidateId, string? note, CancellationToken ct) =>
        await ReviewAsync(context, candidateId, ImportCandidateStatus.Approved, note, ct);
    public async Task<PdiOutcome> RejectAsync(TrustedPdiReviewContext context, string candidateId, string? note, CancellationToken ct) =>
        await ReviewAsync(context, candidateId, ImportCandidateStatus.Rejected, note, ct);
    public async Task<PdiOutcome> ApplyApprovedAsync(TrustedPdiReviewContext context, string candidateId, CancellationToken ct)
    {
        if (!context.CanReviewImportCandidates) return PdiOutcome.NotEligible;
        var tenant = new TrustedPdiTenantContext(context.TenantId, context.CorrelationId);
        var candidate = await candidates.GetAsync(tenant, candidateId, ct);
        if (candidate is null) return PdiOutcome.NotFound;
        if (candidate.Status is ImportCandidateStatus.Applied) return PdiOutcome.Applied;
        if (candidate.Status is not ImportCandidateStatus.Approved) return PdiOutcome.NotEligible;
        var outcome = await catalog.ApplyAsync(new(context.TenantId.Value, candidate.Id, candidate.SourceId.Value, candidate.SourceProductId, candidate.ProductId, candidate.ExpectedProductRevision, candidate.Name, candidate.SourceSku, candidate.VndPrice, context.CorrelationId, candidate.Id), ct);
        if (outcome.Outcome is not (ImportCandidateApplicationOutcome.Applied or ImportCandidateApplicationOutcome.AlreadyApplied)) return PdiOutcome.RevisionConflict;
        return await candidates.SaveIfRevisionAsync(tenant, candidate with { Status = ImportCandidateStatus.Applied, Revision = candidate.Revision + 1 }, candidate.Revision, ct);
    }
    private async Task<PdiOutcome> ReviewAsync(TrustedPdiReviewContext context, string candidateId, ImportCandidateStatus next, string? note, CancellationToken ct)
    {
        if (!context.CanReviewImportCandidates) return PdiOutcome.NotEligible;
        var tenant = new TrustedPdiTenantContext(context.TenantId, context.CorrelationId);
        var candidate = await candidates.GetAsync(tenant, candidateId, ct);
        if (candidate is null) return PdiOutcome.NotFound;
        if (candidate.Status is not ImportCandidateStatus.Ready) return PdiOutcome.RevisionConflict;
        return await candidates.SaveIfRevisionAsync(tenant, candidate with { Status = next, ReviewNote = note, Revision = candidate.Revision + 1 }, candidate.Revision, ct);
    }
}
