namespace CommerceOS.Payments.Contracts;

public enum PaymentCommandOutcome { Captured, Declined, OutcomeUnknown, NeedsAttention, Conflict, Invalid }
public sealed record CaptureOrderPayment(string TrustedTenantId, string OrderId, long AmountVnd, string Currency, string SourceIdentity, string Scenario, string CorrelationId);
public sealed record PaymentCommandResult(PaymentCommandOutcome Outcome, string? AttemptId, string? ProviderOperationId);
public sealed record PaymentStatusView(string OrderId, PaymentCommandOutcome Outcome, string? ProviderOperationId);
public interface IOrderPaymentCapture { Task<PaymentCommandResult> CaptureAsync(CaptureOrderPayment command, CancellationToken cancellationToken); Task<PaymentCommandResult> ReconcileAsync(string trustedTenantId, string orderId, string providerOperationId, string correlationId, CancellationToken cancellationToken); Task<PaymentStatusView?> GetAsync(string trustedTenantId, string orderId, CancellationToken cancellationToken); }
