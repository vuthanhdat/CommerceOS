namespace CommerceOS.Inventory.Domain;

public readonly record struct InventoryTenantId
{
    public InventoryTenantId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Tenant ID is required.", nameof(value)); Value = value; }
    public string Value { get; }
    public override string ToString() => Value;
}
public readonly record struct WarehouseId
{
    public WarehouseId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Warehouse ID is required.", nameof(value)); Value = value; }
    public string Value { get; }
    public override string ToString() => Value;
}
public readonly record struct InventoryProductId
{
    public InventoryProductId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Product ID is required.", nameof(value)); Value = value; }
    public string Value { get; }
    public override string ToString() => Value;
}
public enum WarehouseStatus { Active, Disabled }
public sealed record Warehouse(WarehouseId Id, InventoryTenantId TenantId, string Name, WarehouseStatus Status, long Revision)
{
    public static Warehouse Create(WarehouseId id, InventoryTenantId tenantId, string name) => new(id, tenantId, string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Warehouse name is required.", nameof(name)) : name.Trim(), WarehouseStatus.Active, 1);
}
public sealed record StockItem(InventoryTenantId TenantId, InventoryProductId ProductId, WarehouseId WarehouseId, long OnHand, long Reserved, long Revision)
{
    public long Available => OnHand - Reserved;
    public static StockItem Create(InventoryTenantId tenantId, InventoryProductId productId, WarehouseId warehouseId, long onHand = 0, long reserved = 0, long revision = 1)
    {
        if (onHand < 0 || reserved < 0 || reserved > onHand) throw new ArgumentOutOfRangeException(nameof(reserved), "Inventory quantities must remain non-negative and available.");
        return new(tenantId, productId, warehouseId, onHand, reserved, revision);
    }
}

public enum StockReservationStatus { Active, Released, Issued }
public enum StockMovementType { Receive, Reserve, Release, Issue, Return, AdjustmentIncrease, AdjustmentDecrease }
public sealed record StockReservation(string Id, InventoryTenantId TenantId, string OrderId, InventoryProductId ProductId, WarehouseId WarehouseId, long Quantity, StockReservationStatus Status, long Revision);
public sealed record StockMovement(string Id, InventoryTenantId TenantId, InventoryProductId ProductId, WarehouseId WarehouseId, StockMovementType Type, long Quantity, string SourceIdentity, string CorrelationId, DateTimeOffset OccurredAt);
public sealed class InventoryRuleException(string code) : InvalidOperationException(code) { public string Code { get; } = code; }
public static class StockMath
{
    public static StockItem Apply(StockItem stock, StockMovementType type, long quantity)
    {
        if (quantity <= 0) throw new InventoryRuleException("QUANTITY_INVALID");
        var next = type switch
        {
            StockMovementType.Receive or StockMovementType.Return or StockMovementType.AdjustmentIncrease => stock with { OnHand = stock.OnHand + quantity, Revision = stock.Revision + 1 },
            StockMovementType.Reserve when stock.Available >= quantity => stock with { Reserved = stock.Reserved + quantity, Revision = stock.Revision + 1 },
            StockMovementType.Release => stock with { Reserved = stock.Reserved - quantity, Revision = stock.Revision + 1 },
            StockMovementType.Issue when stock.Reserved >= quantity => stock with { OnHand = stock.OnHand - quantity, Reserved = stock.Reserved - quantity, Revision = stock.Revision + 1 },
            StockMovementType.AdjustmentDecrease when stock.Available >= quantity => stock with { OnHand = stock.OnHand - quantity, Revision = stock.Revision + 1 },
            _ => throw new InventoryRuleException(type is StockMovementType.Reserve ? "INSUFFICIENT_AVAILABLE_STOCK" : "ADJUSTMENT_WOULD_CONSUME_RESERVED_STOCK")
        };
        return StockItem.Create(next.TenantId, next.ProductId, next.WarehouseId, next.OnHand, next.Reserved, next.Revision);
    }
}
