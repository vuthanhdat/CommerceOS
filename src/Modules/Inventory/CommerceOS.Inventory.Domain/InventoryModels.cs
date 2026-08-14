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
