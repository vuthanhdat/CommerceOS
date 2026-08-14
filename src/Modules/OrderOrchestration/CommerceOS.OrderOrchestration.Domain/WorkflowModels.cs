namespace CommerceOS.OrderOrchestration.Domain;

public enum OrderWorkflowStatus { Started, ReservationNeedsAttention, AwaitingPaymentRetry, ReconcilingPayment, Allocated, NeedsAttention }
public sealed record OrderWorkflowState(string TenantId, string OrderId, string ExecutionId, OrderWorkflowStatus Status, string CorrelationId);
