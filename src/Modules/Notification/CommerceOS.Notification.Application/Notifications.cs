using CommerceOS.Notification.Domain;

namespace CommerceOS.Notification.Application;

public enum RecipientRole { Owner, Admin, Staff, Viewer }
public sealed record NotificationRecipient(string SubjectId, RecipientRole Role, bool HasOperationalCapability);
public sealed record CriticalNotificationFact(string EventId, string TenantId, string SourceIdentity, string Summary, bool IsOperational, string CorrelationId, DateTimeOffset OccurredAt);
public enum NotificationOutcome { Applied, AlreadyApplied, Forbidden, NotFound, Invalid }
public interface INotificationRecipientDirectory { Task<IReadOnlyList<NotificationRecipient>> GetRecipientsAsync(string trustedTenantId, bool operational, CancellationToken ct); }
public interface INotificationStore { Task<TenantNotification?> GetAsync(string trustedTenantId, string notificationId, string recipientId, CancellationToken ct); Task<NotificationOutcome> CreateAsync(TenantNotification notification, CancellationToken ct); Task<NotificationOutcome> SaveAsync(TenantNotification before, TenantNotification after, CancellationToken ct); Task<IReadOnlyList<TenantNotification>> ListAsync(string trustedTenantId, string recipientId, int pageSize, CancellationToken ct); }

/// <summary>Recipient selection happens at delivery using trusted membership data; a notification never changes its source exception.</summary>
public sealed class CriticalNotificationConsumer(INotificationRecipientDirectory recipients, INotificationStore store)
{
    public async Task<NotificationOutcome> ApplyAsync(CriticalNotificationFact fact, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fact.EventId) || string.IsNullOrWhiteSpace(fact.TenantId) || string.IsNullOrWhiteSpace(fact.SourceIdentity) || string.IsNullOrWhiteSpace(fact.Summary) || string.IsNullOrWhiteSpace(fact.CorrelationId)) return NotificationOutcome.Invalid;
        var targets = await recipients.GetRecipientsAsync(fact.TenantId, fact.IsOperational, ct); var any = false;
        foreach (var target in targets.Where(x => Eligible(x, fact.IsOperational)).DistinctBy(x => x.SubjectId))
        {
            var notification = new TenantNotification($"notification:{fact.SourceIdentity}:{target.SubjectId}", fact.TenantId, target.SubjectId, fact.SourceIdentity, fact.Summary, true, NotificationState.Unread, fact.OccurredAt, null, null);
            var outcome = await store.CreateAsync(notification, ct);
            if (outcome is not NotificationOutcome.Applied and not NotificationOutcome.AlreadyApplied) return outcome;
            any |= outcome is NotificationOutcome.Applied;
        }
        return any ? NotificationOutcome.Applied : NotificationOutcome.AlreadyApplied;
    }
    private static bool Eligible(NotificationRecipient x, bool operational) => x.Role is RecipientRole.Owner or RecipientRole.Admin || operational && x.Role is RecipientRole.Staff && x.HasOperationalCapability;
}
public sealed class NotificationCommandService(INotificationStore store, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public Task<IReadOnlyList<TenantNotification>> ListAsync(string trustedTenantId, string recipientId, int pageSize, CancellationToken ct) => store.ListAsync(trustedTenantId, recipientId, Math.Clamp(pageSize, 1, 100), ct);
    public Task<NotificationOutcome> ReadAsync(string tenant, string notificationId, string recipient, CancellationToken ct) => ChangeAsync(tenant, notificationId, recipient, x => x.Read(_clock.GetUtcNow()), ct);
    public Task<NotificationOutcome> AcknowledgeAsync(string tenant, string notificationId, string recipient, CancellationToken ct) => ChangeAsync(tenant, notificationId, recipient, x => x.Acknowledge(_clock.GetUtcNow()), ct);
    private async Task<NotificationOutcome> ChangeAsync(string tenant, string id, string recipient, Func<TenantNotification, TenantNotification> action, CancellationToken ct)
    { var before = await store.GetAsync(tenant, id, recipient, ct); return before is null ? NotificationOutcome.NotFound : await store.SaveAsync(before, action(before), ct); }
}
