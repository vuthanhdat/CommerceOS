using CommerceOS.Inventory.Domain;
using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.Inventory.Application;

public sealed record TrustedInventoryMutationContext(InventoryTenantId TenantId, string CorrelationId);
public enum InventoryOutcome { Applied, AlreadyApplied, NotFound, RevisionConflict, LimitReached, InvalidState }
public interface IInventoryStore
{
    Task<Warehouse?> GetWarehouseAsync(TrustedInventoryMutationContext context, WarehouseId id, CancellationToken ct);
    Task<InventoryOutcome> CreateOrReactivateWarehouseAsync(TrustedInventoryMutationContext context, Warehouse? previous, Warehouse updatedWarehouse, int maxWarehouses, CancellationToken cancellationToken);
    Task<InventoryOutcome> DisableWarehouseAsync(TrustedInventoryMutationContext context, Warehouse previous, CancellationToken ct);
    Task<StockItem?> GetStockItemAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, CancellationToken ct);
}

public sealed class WarehouseService(IInventoryStore store, IEntitlementEvaluator entitlements, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<InventoryOutcome> CreateAsync(TrustedInventoryMutationContext context, Warehouse warehouse, CancellationToken ct)
    {
        if (warehouse.TenantId != context.TenantId) throw new ArgumentException("Warehouse tenant must match trusted context.", nameof(warehouse));
        return await store.CreateOrReactivateWarehouseAsync(context, null, warehouse, await LimitAsync(context, ct), ct);
    }
    public async Task<InventoryOutcome> ReactivateAsync(TrustedInventoryMutationContext context, WarehouseId id, long expectedRevision, CancellationToken ct)
    {
        var previous = await store.GetWarehouseAsync(context, id, ct); if (previous is null) return InventoryOutcome.NotFound;
        if (previous.Revision != expectedRevision) return InventoryOutcome.RevisionConflict;
        if (previous.Status is WarehouseStatus.Active) return InventoryOutcome.AlreadyApplied;
        return await store.CreateOrReactivateWarehouseAsync(context, previous, previous with { Status = WarehouseStatus.Active, Revision = previous.Revision + 1 }, await LimitAsync(context, ct), ct);
    }
    public async Task<InventoryOutcome> DisableAsync(TrustedInventoryMutationContext context, WarehouseId id, long expectedRevision, CancellationToken ct)
    {
        var previous = await store.GetWarehouseAsync(context, id, ct); if (previous is null) return InventoryOutcome.NotFound;
        if (previous.Revision != expectedRevision) return InventoryOutcome.RevisionConflict;
        if (previous.Status is WarehouseStatus.Disabled) return InventoryOutcome.AlreadyApplied;
        return await store.DisableWarehouseAsync(context, previous with { Status = WarehouseStatus.Disabled, Revision = previous.Revision + 1 }, ct);
    }
    private async Task<int> LimitAsync(TrustedInventoryMutationContext context, CancellationToken ct)
    {
        var result = await entitlements.EvaluateEntitlementAsync(new(context.TenantId.Value, EntitlementKey.MaxWarehouses, _clock.GetUtcNow(), context.CorrelationId), ct);
        return result.Outcome is EntitlementDecisionOutcome.Granted && result.Limit is > 0 ? result.Limit.Value : 0;
    }
}
