using CommerceOS.Sales.Application;
using CommerceOS.Sales.Contracts;
using CommerceOS.Sales.Domain;

namespace CommerceOS.Sales.UnitTests;

public sealed class SalesOrderServiceTests
{
    [Fact]
    public async Task EquivalentCheckoutIsReplayedButChangedRequestConflicts()
    {
        var store = new MemoryStore(); var service = new SalesOrderService(store); var first = Command();
        var accepted = await service.PlaceAsync(first, default);
        Assert.Equal(OrderPlacementOutcome.Accepted, accepted.Outcome);
        Assert.Equal(OrderPlacementOutcome.Replayed, (await service.PlaceAsync(first, default)).Outcome);
        Assert.Equal(OrderPlacementOutcome.Conflict, (await service.PlaceAsync(first with { Lines = [new("product", "SKU", "Tea", 1, 2, "VND")], TotalVnd = 2 }, default)).Outcome);
        Assert.Single(store.Orders);
    }
    [Fact]
    public async Task OrderSnapshotIsImmutableAndTransitionsNeedExpectedRevisionAndEvidence()
    {
        var store = new MemoryStore(); var service = new SalesOrderService(store); var result = await service.PlaceAsync(Command(), default); var context = new TrustedSalesContext(new("tenant-a"), "c"); var order = store.Orders.Single().Value;
        Assert.Equal(SalesStoreOutcome.Conflict, await service.ApplyTransitionAsync(context, new(result.OrderId!), SalesOrderStatus.Confirmed, "payment:c1", 99, default));
        Assert.Equal(SalesStoreOutcome.Applied, await service.ApplyTransitionAsync(context, order.Id, SalesOrderStatus.Confirmed, "payment:c1", 1, default));
        Assert.Equal("Tea", store.Orders.Single().Value.Lines.Single().Name);
        Assert.True(store.Orders.Single().Value.Process.StartPending);
    }
    private static PlaceAcceptedOrder Command() => new("tenant-a", "key", [new("product", "SKU", "Tea", 1, 1, "VND")], 1, new("Guest", "guest@example.test", null, null), "c");
    private sealed class MemoryStore : ISalesOrderStore
    {
        public Dictionary<(SalesTenantId, SalesOrderId), SalesOrder> Orders { get; } = []; private readonly Dictionary<(SalesTenantId, string), string> _keys = [];
        public Task<SalesStoreOutcome> PlaceAsync(TrustedSalesContext c, SalesOrder order, string key, string hash, CancellationToken ct) { lock (Orders) { if (_keys.TryGetValue((c.TenantId, key), out var old)) return Task.FromResult(old == hash ? SalesStoreOutcome.Replayed : SalesStoreOutcome.Conflict); _keys[(c.TenantId, key)] = hash; Orders[(c.TenantId, order.Id)] = order; return Task.FromResult(SalesStoreOutcome.Applied); } }
        public Task<SalesOrder?> GetAsync(TrustedSalesContext c, SalesOrderId id, CancellationToken ct) => Task.FromResult(Orders.GetValueOrDefault((c.TenantId, id)));
        public Task<SalesStoreOutcome> SaveAsync(TrustedSalesContext c, SalesOrder before, SalesOrder after, CancellationToken ct) { if (!Orders.TryGetValue((c.TenantId, before.Id), out var current) || current.Revision != before.Revision) return Task.FromResult(SalesStoreOutcome.Conflict); Orders[(c.TenantId, before.Id)] = after; return Task.FromResult(SalesStoreOutcome.Applied); }
        public Task<SalesOrderPage> ListAsync(TrustedSalesContext c, string? cursor, int pageSize, CancellationToken ct) => Task.FromResult(new SalesOrderPage(Orders.Where(x => x.Key.Item1 == c.TenantId).Select(x => x.Value).Take(pageSize).ToArray(), null));
    }
}
