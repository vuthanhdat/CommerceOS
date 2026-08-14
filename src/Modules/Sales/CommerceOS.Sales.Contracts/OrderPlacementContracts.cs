namespace CommerceOS.Sales.Contracts;

public sealed record ValidatedCheckoutLine(string ProductId, string Sku, string Name, long Quantity, long UnitPriceVnd, string Currency);
public sealed record GuestCheckoutData(string Name, string Email, string? Phone, string? Address);
public sealed record PlaceAcceptedOrder(string TrustedTenantId, string IdempotencyKey, IReadOnlyList<ValidatedCheckoutLine> Lines, long TotalVnd, GuestCheckoutData Guest, string CorrelationId);
public enum OrderPlacementOutcome { Accepted, Replayed, Conflict, Invalid }
public sealed record OrderPlacementResult(OrderPlacementOutcome Outcome, string? OrderId, string? Status);
public interface ISalesOrderPlacement { Task<OrderPlacementResult> PlaceAsync(PlaceAcceptedOrder command, CancellationToken cancellationToken); }
