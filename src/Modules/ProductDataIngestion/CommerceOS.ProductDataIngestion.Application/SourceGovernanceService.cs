using CommerceOS.ProductDataIngestion.Domain;
using CommerceOS.SubscriptionBilling.Contracts;
namespace CommerceOS.ProductDataIngestion.Application;

public sealed record TrustedPdiTenantContext(PdiTenantId TenantId, string CorrelationId);
public enum PdiOutcome { Applied, NotFound, NotEligible, RevisionConflict }
public interface IPdiGovernanceStore
{
    Task<DataSource?> GetSourceAsync(DataSourceId id, CancellationToken ct);
    Task<TenantSourceEnrollment?> GetEnrollmentAsync(TrustedPdiTenantContext context, DataSourceId id, CancellationToken ct);
    Task<PdiOutcome> SaveSourceAsync(DataSource source, long? expectedRevision, CancellationToken ct);
    Task<PdiOutcome> SaveEnrollmentAsync(TrustedPdiTenantContext context, TenantSourceEnrollment enrollment, long? expectedRevision, CancellationToken ct);
}

public sealed class PlatformSourceGovernanceService(IPdiGovernanceStore store)
{
    public Task<PdiOutcome> SaveSourceAsync(DataSource source, long? expectedRevision, CancellationToken ct) =>
        store.SaveSourceAsync(source, expectedRevision, ct);
}
public sealed class SourceGovernanceService(IPdiGovernanceStore store, IEntitlementEvaluator entitlements, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<PdiOutcome> EnableForTenantAsync(TrustedPdiTenantContext context, DataSourceId id, CancellationToken ct) { var source = await store.GetSourceAsync(id, ct); if (source is null) return PdiOutcome.NotFound; if (!source.PlatformEligible) return PdiOutcome.NotEligible; var existing = await store.GetEnrollmentAsync(context, id, ct); return await store.SaveEnrollmentAsync(context, new(context.TenantId, id, true, (existing?.Revision ?? 0) + 1), existing?.Revision, ct); }
    public async Task<bool> IsEligibleForScheduledRunAsync(TrustedPdiTenantContext context, DataSourceId id, CancellationToken ct) { var source = await store.GetSourceAsync(id, ct); var enrollment = await store.GetEnrollmentAsync(context, id, ct); var decision = await entitlements.EvaluateEntitlementAsync(new(context.TenantId.Value, EntitlementKey.ScheduledProductIngestion, _clock.GetUtcNow(), context.CorrelationId), ct); return source?.PlatformEligible is true && enrollment?.Enabled is true && decision.Outcome is EntitlementDecisionOutcome.Granted && decision.CapabilityEnabled is true; }
}
