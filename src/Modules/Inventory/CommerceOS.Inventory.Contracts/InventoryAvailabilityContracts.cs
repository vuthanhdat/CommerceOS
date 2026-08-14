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
