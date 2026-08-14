using CommerceOS.Inventory.Application;
using CommerceOS.Inventory.Domain;
using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.Inventory.UnitTests;

public sealed class WarehouseServiceTests
{
    [Fact]
    public void ReserveIssueReleaseAndAdjustmentPreserveQuantityInvariants()
    {
        var stock = StockItem.Create(new("tenant-a"), new("product"), new("warehouse"), 5);
        var reserved = StockMath.Apply(stock, StockMovementType.Reserve, 5);
        Assert.Equal(0, reserved.Available);
        Assert.Throws<InventoryRuleException>(() => StockMath.Apply(reserved, StockMovementType.Reserve, 1));
        var issued = StockMath.Apply(reserved, StockMovementType.Issue, 5);
        Assert.Equal(0, issued.OnHand); Assert.Equal(0, issued.Reserved);
    }
    [Fact]
    public async Task ConcurrentFinalSlotCreatesOnlyOneWarehouse()
    {
        var store = new InMemoryStore(); var service = new WarehouseService(store, new Limit(1)); var context = new TrustedInventoryMutationContext(new("tenant-a"), "c");
        var outcomes = await Task.WhenAll(service.CreateAsync(context, Warehouse.Create(new("one"), context.TenantId, "One"), default), service.CreateAsync(context, Warehouse.Create(new("two"), context.TenantId, "Two"), default));
        Assert.Equal(1, outcomes.Count(x => x is InventoryOutcome.Applied)); Assert.Equal(1, outcomes.Count(x => x is InventoryOutcome.LimitReached));
    }
    [Fact]
    public void StockItemRejectsNegativeOrOverReservedQuantities()
    {
        var tenant = new InventoryTenantId("tenant-a");
        Assert.Throws<ArgumentOutOfRangeException>(() => StockItem.Create(tenant, new("product"), new("warehouse"), 1, 2));
        Assert.Equal(0, StockItem.Create(tenant, new("product"), new("warehouse")).Available);
    }
    private sealed class Limit(int max) : IEntitlementEvaluator { public Task<EffectiveEntitlementDecision> EvaluateEntitlementAsync(EvaluateEntitlementRequest r, CancellationToken ct) => Task.FromResult(new EffectiveEntitlementDecision(EntitlementDecisionOutcome.Granted, null, max, "v", null, null)); }
    private sealed class InMemoryStore : IInventoryStore
    {
        private readonly object _sync = new(); private readonly Dictionary<(InventoryTenantId, WarehouseId), Warehouse> _warehouses = []; private int _active;
        public Task<Warehouse?> GetWarehouseAsync(TrustedInventoryMutationContext c, WarehouseId id, CancellationToken ct) => Task.FromResult(_warehouses.GetValueOrDefault((c.TenantId, id)));
        public Task<InventoryOutcome> CreateOrReactivateWarehouseAsync(TrustedInventoryMutationContext c, Warehouse? previous, Warehouse updatedWarehouse, int maxWarehouses, CancellationToken ct) { lock (_sync) { if (_active >= maxWarehouses) return Task.FromResult(InventoryOutcome.LimitReached); _warehouses[(c.TenantId, updatedWarehouse.Id)] = updatedWarehouse; _active++; return Task.FromResult(InventoryOutcome.Applied); } }
        public Task<InventoryOutcome> DisableWarehouseAsync(TrustedInventoryMutationContext c, Warehouse previous, CancellationToken ct) { lock (_sync) { _warehouses[(c.TenantId, previous.Id)] = previous; _active--; return Task.FromResult(InventoryOutcome.Applied); } }
        public Task<StockItem?> GetStockItemAsync(TrustedInventoryMutationContext c, InventoryProductId productId, WarehouseId warehouseId, CancellationToken ct) => Task.FromResult<StockItem?>(null);
    }
}
