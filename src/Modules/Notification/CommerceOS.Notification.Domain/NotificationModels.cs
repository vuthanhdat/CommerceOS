namespace CommerceOS.Notification.Domain;

public enum NotificationState { Unread, Read, Acknowledged }
public sealed record TenantNotification(string Id, string TenantId, string RecipientId, string SourceIdentity, string Summary, bool IsActionable, NotificationState State, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, DateTimeOffset? AcknowledgedAt)
{
    public TenantNotification Read(DateTimeOffset at) => State is NotificationState.Unread ? this with { State = NotificationState.Read, ReadAt = at } : this;
    public TenantNotification Acknowledge(DateTimeOffset at) => State is NotificationState.Acknowledged ? this : Read(at) with { State = NotificationState.Acknowledged, AcknowledgedAt = at };
}
