namespace CommerceOS.Procurement.Domain;

public readonly record struct ProcurementTenantId(string Value);
public readonly record struct SupplierId(string Value);
public readonly record struct PurchaseOrderId(string Value);
public enum SupplierStatus { Active, Archived }
public enum PurchaseOrderStatus { Draft, Submitted, Cancelled }
public sealed record Supplier(SupplierId Id, ProcurementTenantId TenantId, string DisplayName, SupplierStatus Status, long Revision);
public sealed record PurchaseOrderLine(string ProductId, string ProductNameSnapshot, string? SkuSnapshot, long Quantity, long UnitPriceVnd)
{
    public static PurchaseOrderLine Create(string productId, string productNameSnapshot, string? skuSnapshot, long quantity, long unitPriceVnd)
    { if (quantity <= 0 || unitPriceVnd < 0) throw new ArgumentOutOfRangeException(nameof(quantity)); return new(productId, productNameSnapshot, skuSnapshot, quantity, unitPriceVnd); }
}
public sealed record PurchaseOrder(PurchaseOrderId Id, ProcurementTenantId TenantId, SupplierId SupplierId, IReadOnlyList<PurchaseOrderLine> Lines, PurchaseOrderStatus Status, long Revision)
{
    public PurchaseOrder Submit(long expectedRevision) { if (Revision != expectedRevision) throw new InvalidOperationException("PO_REVISION_STALE"); if (Status is not PurchaseOrderStatus.Draft || Lines.Count == 0) throw new InvalidOperationException("PO_NOT_SUBMITTABLE"); return this with { Status = PurchaseOrderStatus.Submitted, Revision = Revision + 1 }; }
}
public enum GoodsReceiptStatus { Draft, Confirmed, Corrected }
public sealed record GoodsReceiptLine(string ProductId, long Quantity);
public sealed record GoodsReceipt(string Id, PurchaseOrderId PurchaseOrderId, ProcurementTenantId TenantId, IReadOnlyList<GoodsReceiptLine> Lines, GoodsReceiptStatus Status, string? CorrectsReceiptId, long Revision);
public sealed record GoodsReceiptCorrection(string Id, string ReceiptId, ProcurementTenantId TenantId, IReadOnlyList<GoodsReceiptLine> Lines, string SourceIdentity, long Revision);
public sealed record GoodsReceiptRecorded(string ReceiptId, ProcurementTenantId TenantId, IReadOnlyList<GoodsReceiptLine> Lines, string SourceIdentity, string CorrelationId);
