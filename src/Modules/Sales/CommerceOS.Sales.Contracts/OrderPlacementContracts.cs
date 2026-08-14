namespace CommerceOS.Sales.Contracts;

public sealed record ValidatedCheckoutLine(string ProductId, string Sku, string Name, long Quantity, long UnitPriceVnd, string Currency, long? BaseUnitPriceVnd = null, string? PromotionId = null, long? AppliedPromotionalUnitPriceVnd = null, DateTimeOffset? PriceEvaluatedAt = null)
{
    public long AcceptedBaseUnitPriceVnd => BaseUnitPriceVnd ?? UnitPriceVnd;
}
public sealed record GuestCheckoutData(string Name, string Email, string? Phone, string? Address);
public sealed record PlaceAcceptedOrder(string TrustedTenantId, string IdempotencyKey, IReadOnlyList<ValidatedCheckoutLine> Lines, long TotalVnd, GuestCheckoutData Guest, string CorrelationId);
public enum OrderPlacementOutcome { Accepted, Replayed, Conflict, Invalid }
public sealed record OrderPlacementResult(OrderPlacementOutcome Outcome, string? OrderId, string? Status);
public interface ISalesOrderPlacement { Task<OrderPlacementResult> PlaceAsync(PlaceAcceptedOrder command, CancellationToken cancellationToken); }
public sealed record CancelSalesOrder(string TrustedTenantId, string OrderId, string SourceIdentity, long ExpectedRevision, string CorrelationId);
public enum SalesProgressOutcome { Applied, AlreadyApplied, Conflict, NotFound }
public interface ISalesOrderCancellation { Task<SalesProgressOutcome> CancelAsync(CancelSalesOrder command, CancellationToken cancellationToken); }
public interface ISalesOrderWorkflowProgress { Task<SalesProgressOutcome> ConfirmAsync(string trustedTenantId, string orderId, string sourceIdentity, long expectedRevision, string correlationId, CancellationToken cancellationToken); Task<SalesProgressOutcome> AllocateAsync(string trustedTenantId, string orderId, string sourceIdentity, long expectedRevision, string correlationId, CancellationToken cancellationToken); }

public enum TrustedRefundRole { Owner, Admin, Staff, Viewer }
public sealed record RefundRequestLine(string ProductId, long Quantity, string OriginalIssueReference);
public sealed record RequestSalesRefund(string TrustedTenantId, string OrderId, string PaymentId, long AmountVnd, string Currency, IReadOnlyList<RefundRequestLine> Lines, string SourceIdentity, string ActorId, TrustedRefundRole Role, string CorrelationId);
public sealed record DecideSalesRefund(string TrustedTenantId, string RefundRequestId, long ExpectedRevision, string SourceIdentity, string ActorId, TrustedRefundRole Role, bool Approve, string CorrelationId);
public enum RefundCommandOutcome { Requested, Approved, Rejected, AlreadyApplied, NotFound, Forbidden, Conflict, Invalid }
public sealed record RefundCommandResult(RefundCommandOutcome Outcome, string? RefundRequestId, string? Status);
public interface ISalesRefundReview { Task<RefundCommandResult> RequestAsync(RequestSalesRefund command, CancellationToken ct); Task<RefundCommandResult> DecideAsync(DecideSalesRefund command, CancellationToken ct); }
