using CommerceOS.Notification.Application;
using CommerceOS.Notification.Domain;

namespace CommerceOS.Notification.UnitTests;

public sealed class NotificationTests
{
    [Fact]
    public async Task RecipientActionsAreIsolatedAndAcknowledgementImpliesRead()
    {
        var store = new Store(); var consumer = new CriticalNotificationConsumer(new Directory(), store);
        var fact = new CriticalNotificationFact("event", "tenant", "source", "Needs attention", true, "c", DateTimeOffset.UtcNow);
        await consumer.ApplyAsync(fact, default); await consumer.ApplyAsync(fact, default);
        var service = new NotificationCommandService(store, new FixedTime());
        await service.AcknowledgeAsync("tenant", "notification:source:owner", "owner", default);
        var owner = await store.GetAsync("tenant", "notification:source:owner", "owner", default);
        var staff = await store.GetAsync("tenant", "notification:source:staff", "staff", default);
        Assert.Equal(NotificationState.Acknowledged, owner!.State); Assert.NotNull(owner.ReadAt); Assert.Equal(NotificationState.Unread, staff!.State); Assert.Null(await store.GetAsync("other", "notification:source:owner", "owner", default));
    }
    private sealed class Directory : INotificationRecipientDirectory { public Task<IReadOnlyList<NotificationRecipient>> GetRecipientsAsync(string tenant, bool operational, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationRecipient>>([new("owner", RecipientRole.Owner, false), new("staff", RecipientRole.Staff, true), new("viewer", RecipientRole.Viewer, true)]); }
    private sealed class Store : INotificationStore
    {
        private readonly Dictionary<string, TenantNotification> values = []; private static string Key(string tenant, string id, string recipient) => $"{tenant}:{id}:{recipient}";
        public Task<TenantNotification?> GetAsync(string tenant, string id, string recipient, CancellationToken ct) => Task.FromResult(values.GetValueOrDefault(Key(tenant, id, recipient)));
        public Task<NotificationOutcome> CreateAsync(TenantNotification x, CancellationToken ct) => Task.FromResult(values.TryAdd(Key(x.TenantId, x.Id, x.RecipientId), x) ? NotificationOutcome.Applied : NotificationOutcome.AlreadyApplied);
        public Task<NotificationOutcome> SaveAsync(TenantNotification before, TenantNotification after, CancellationToken ct) { values[Key(after.TenantId, after.Id, after.RecipientId)] = after; return Task.FromResult(NotificationOutcome.Applied); }
        public Task<IReadOnlyList<TenantNotification>> ListAsync(string tenant, string recipient, int size, CancellationToken ct) => Task.FromResult<IReadOnlyList<TenantNotification>>(values.Values.Where(x => x.TenantId == tenant && x.RecipientId == recipient).Take(size).ToArray());
    }
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero); }
}
