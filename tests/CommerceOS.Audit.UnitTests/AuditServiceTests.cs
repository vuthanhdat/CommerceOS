using CommerceOS.Audit.Application;
using CommerceOS.Audit.Domain;

namespace CommerceOS.Audit.UnitTests;

public sealed class AuditServiceTests
{
    [Fact]
    public async Task DuplicateSourceIsRecordedOnlyOnceAndTenantQueryIsScoped()
    {
        var store = new Store();
        var service = new AuditService(store);
        var evidence = new AuditEvidence("e1", "source-1", "tenant-a", AuditAudience.Tenant, "actor", "membership.changed", "Accepted", "safe", "c", DateTimeOffset.UtcNow);
        Assert.True(await service.AppendAsync(evidence, default));
        Assert.False(await service.AppendAsync(evidence with { Id = "e2" }, default));
        Assert.Single(await service.ListTenantAsync(new("tenant-a", "Owner", "c"), DateTimeOffset.MinValue, 200, default));
        Assert.Empty(await service.ListTenantAsync(new("tenant-b", "Owner", "c"), DateTimeOffset.MinValue, 10, default));
        Assert.Empty(await service.ListTenantAsync(new("tenant-a", "Staff", "c"), DateTimeOffset.MinValue, 10, default));
    }

    [Fact]
    public async Task PlatformSecurityEvidenceNeedsExplicitPrivilegedPath()
    {
        var store = new Store();
        var service = new AuditService(store);
        await service.AppendAsync(new("e", "source", null, AuditAudience.PlatformSecurity, "operator", "policy.changed", "Accepted", "safe", "c", DateTimeOffset.UtcNow), default);
        Assert.Empty(await service.ListPlatformSecurityAsync(new("operator", false, "c"), DateTimeOffset.MinValue, 10, default));
        Assert.Single(await service.ListPlatformSecurityAsync(new("operator", true, "c"), DateTimeOffset.MinValue, 10, default));
    }

    [Fact]
    public async Task VersionedPlatformLifecycleDeliveryIsIdempotentAndDoesNotExposeTenantIdentityToPlatformEvidence()
    {
        var store = new Store(); var consumer = new AuditDeliveryConsumer(new AuditService(store));
        var fact = new AuditDeliveryFact("event", 1, "tenant-lifecycle:operation", null, AuditAudience.PlatformSecurity, "platform-admin", "tenant.suspended", "Accepted", "support-investigation", "c", DateTimeOffset.UtcNow);
        Assert.Equal(AuditDeliveryOutcome.Appended, await consumer.ConsumeAsync(fact, default));
        Assert.Equal(AuditDeliveryOutcome.AlreadyRecorded, await consumer.ConsumeAsync(fact, default));
        Assert.Single(await new AuditService(store).ListPlatformSecurityAsync(new("operator", true, "c"), DateTimeOffset.MinValue, 10, default));
    }

    private sealed class Store : IAuditStore
    {
        private readonly List<AuditEvidence> evidence = [];
        public Task<bool> AppendAsync(AuditEvidence item, CancellationToken ct) { if (evidence.Any(x => x.SourceIdentity == item.SourceIdentity)) return Task.FromResult(false); evidence.Add(item); return Task.FromResult(true); }
        public Task<IReadOnlyList<AuditEvidence>> ListTenantAsync(string tenantId, DateTimeOffset from, int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditEvidence>>(evidence.Where(x => x.Audience is AuditAudience.Tenant && x.TenantId == tenantId && x.OccurredAt >= from).Take(limit).ToArray());
        public Task<IReadOnlyList<AuditEvidence>> ListPlatformSecurityAsync(DateTimeOffset from, int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditEvidence>>(evidence.Where(x => x.Audience is AuditAudience.PlatformSecurity && x.OccurredAt >= from).Take(limit).ToArray());
    }
}
