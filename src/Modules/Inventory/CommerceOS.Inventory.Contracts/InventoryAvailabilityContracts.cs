namespace CommerceOS.Inventory.Contracts;

/// <summary>Producer-owned availability fact. It is informative only and never a reservation.</summary>
public sealed record ProductAvailability(string ProductId, long AvailableQuantity);

public interface IInventoryAvailabilityQuery
{
    Task<ProductAvailability> GetAvailabilityAsync(string trustedTenantId, string productId, CancellationToken cancellationToken);
}
public enum OrderStockOutcome { Applied, AlreadyApplied, Insufficient, NeedsAttention }
public sealed record ReserveOrderStock(string TrustedTenantId, string OrderId, string SourceIdentity, string CorrelationId);
public sealed record ReleaseOrderStock(string TrustedTenantId, string OrderId, string SourceIdentity, string CorrelationId);
public interface IOrderStockReservation { Task<OrderStockOutcome> ReserveAsync(ReserveOrderStock command, CancellationToken cancellationToken); }
public interface IOrderStockRelease { Task<OrderStockOutcome> ReleaseAsync(ReleaseOrderStock command, CancellationToken cancellationToken); }

/// <summary>Procurement-owned fact. Warehouse identity is explicit: Inventory never resolves it from a foreign table.</summary>
public sealed record ConfirmedGoodsReceiptLine(string ProductId, string WarehouseId, long QuantityDelta);
public sealed record ConfirmedGoodsReceiptFact(string EventId, string TenantId, string ReceiptId, IReadOnlyList<ConfirmedGoodsReceiptLine> Lines, string CorrelationId, DateTimeOffset OccurredAt);
public enum GoodsReceiptInventoryOutcome { Applied, AlreadyApplied, NeedsAttention, Invalid }
public interface IConfirmedGoodsReceiptConsumer { Task<GoodsReceiptInventoryOutcome> ApplyAsync(ConfirmedGoodsReceiptFact fact, CancellationToken cancellationToken); }

/// <summary>Sales-owned approval fact. Original issue references are explicit so Inventory never reads Sales state.</summary>
public sealed record ApprovedRefundReturnLine(string ProductId, string WarehouseId, long Quantity, string OriginalIssueReference);
public sealed record RefundApprovedInventoryFact(string EventId, string TenantId, string RefundApprovalId, string OrderId, IReadOnlyList<ApprovedRefundReturnLine> Lines, string CorrelationId, DateTimeOffset OccurredAt);
public sealed record StockReturnedFact(string EventId, string TenantId, string ReturnId, string RefundApprovalId, string OrderId, string ProductId, long Quantity, string OriginalIssueReference, string CorrelationId, DateTimeOffset OccurredAt);
public enum RefundReturnOutcome { Applied, AlreadyApplied, NeedsAttention, Invalid }
public interface IRefundApprovedInventoryConsumer { Task<RefundReturnOutcome> ApplyAsync(RefundApprovedInventoryFact fact, CancellationToken cancellationToken); }
