using CommerceOS.ProductDataIngestion.Domain;
using System.Globalization;

namespace CommerceOS.ProductDataIngestion.Application;

public sealed record ScheduledSourceRefresh(string Id, PdiTenantId TenantId, DataSourceId SourceId, Uri SourceUrl, bool Enabled, long Revision, DateTimeOffset? LastSuppressedAt = null);
public enum ScheduledRefreshDispatchOutcome { Enqueued, Suppressed, Duplicate, NotFound }
public interface IScheduledSourceRefreshStore
{
    Task<ScheduledSourceRefresh?> GetScheduleAsync(TrustedPdiTenantContext context, string scheduleId, CancellationToken cancellationToken);
    Task<PdiOutcome> SaveAsync(TrustedPdiTenantContext context, ScheduledSourceRefresh schedule, long? expectedRevision, CancellationToken cancellationToken);
}
public sealed class ScheduledRefreshDispatcher(IScheduledSourceRefreshStore schedules, SourceGovernanceService governance, IManualAcquisitionWorkStore work, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<ScheduledRefreshDispatchOutcome> DispatchAsync(TrustedPdiTenantContext context, string scheduleId, DateTimeOffset dueAt, CancellationToken ct)
    {
        var schedule = await schedules.GetScheduleAsync(context, scheduleId, ct);
        if (schedule is null) return ScheduledRefreshDispatchOutcome.NotFound;
        if (!schedule.Enabled || !await governance.IsEligibleForScheduledRunAsync(context, schedule.SourceId, ct))
        {
            await schedules.SaveAsync(context, schedule with { LastSuppressedAt = _clock.GetUtcNow(), Revision = schedule.Revision + 1 }, schedule.Revision, ct);
            return ScheduledRefreshDispatchOutcome.Suppressed;
        }
        var window = dueAt.UtcDateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        var identity = $"scheduled-refresh:{schedule.Id}:{window}";
        var result = await work.EnqueueIfAbsentAsync(new($"scheduled-{schedule.Id}-{window}", context.TenantId, schedule.SourceId, schedule.SourceUrl, identity, context.CorrelationId), ct);
        return result is PdiOutcome.Applied ? ScheduledRefreshDispatchOutcome.Enqueued : ScheduledRefreshDispatchOutcome.Duplicate;
    }
}
