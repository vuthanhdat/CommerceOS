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
