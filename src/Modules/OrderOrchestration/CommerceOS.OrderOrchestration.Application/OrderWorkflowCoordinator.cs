using CommerceOS.Inventory.Contracts;
using CommerceOS.OrderOrchestration.Domain;
using CommerceOS.Payments.Contracts;
using CommerceOS.Sales.Contracts;

namespace CommerceOS.OrderOrchestration.Application;

public sealed record StartOrderWorkflow(string TenantId, string OrderId, string ExecutionId, long AmountVnd, string SourceIdentity, string CorrelationId, long PlacedRevision, long ConfirmedRevision, string Scenario);
public interface IOrderWorkflowStateStore { Task<bool> TryStartAsync(OrderWorkflowState state, CancellationToken cancellationToken); Task SaveAsync(OrderWorkflowState state, CancellationToken cancellationToken); }
public sealed class OrderWorkflowCoordinator(IOrderWorkflowStateStore state, IOrderStockReservation inventory, IOrderPaymentCapture payments, ISalesOrderWorkflowProgress sales)
{
    public async Task<OrderWorkflowState> StartAsync(StartOrderWorkflow request, CancellationToken ct)
    {
        var current = new OrderWorkflowState(request.TenantId, request.OrderId, request.ExecutionId, OrderWorkflowStatus.Started, request.CorrelationId); if (!await state.TryStartAsync(current, ct)) return current;
        var reserved = await inventory.ReserveAsync(new(request.TenantId, request.OrderId, $"reserve:{request.SourceIdentity}", request.CorrelationId), ct);
        if (reserved is not OrderStockOutcome.Applied and not OrderStockOutcome.AlreadyApplied) return await Save(current with { Status = OrderWorkflowStatus.ReservationNeedsAttention }, ct);
        var payment = await payments.CaptureAsync(new(request.TenantId, request.OrderId, request.AmountVnd, "VND", $"capture:{request.SourceIdentity}", request.Scenario, request.CorrelationId), ct);
        if (payment.Outcome is PaymentCommandOutcome.Declined) return await Save(current with { Status = OrderWorkflowStatus.AwaitingPaymentRetry }, ct);
        if (payment.Outcome is PaymentCommandOutcome.OutcomeUnknown) return await Save(current with { Status = OrderWorkflowStatus.ReconcilingPayment }, ct);
        if (payment.Outcome is not PaymentCommandOutcome.Captured || await sales.ConfirmAsync(request.TenantId, request.OrderId, $"captured:{payment.ProviderOperationId}", request.PlacedRevision, request.CorrelationId, ct) is SalesProgressOutcome.Conflict || await sales.AllocateAsync(request.TenantId, request.OrderId, $"reserved:{request.SourceIdentity}", request.ConfirmedRevision, request.CorrelationId, ct) is SalesProgressOutcome.Conflict) return await Save(current with { Status = OrderWorkflowStatus.NeedsAttention }, ct);
        return await Save(current with { Status = OrderWorkflowStatus.Allocated }, ct);
    }
    public async Task<OrderWorkflowState> RecoverAsync(OrderWorkflowState current, string providerOperationId, CancellationToken ct)
    { if (current.Status is not OrderWorkflowStatus.ReconcilingPayment) return current; var result = await payments.ReconcileAsync(current.TenantId, current.OrderId, providerOperationId, current.CorrelationId, ct); return await Save(current with { Status = result.Outcome is PaymentCommandOutcome.NeedsAttention ? OrderWorkflowStatus.NeedsAttention : result.Outcome is PaymentCommandOutcome.OutcomeUnknown ? OrderWorkflowStatus.ReconcilingPayment : result.Outcome is PaymentCommandOutcome.Declined ? OrderWorkflowStatus.AwaitingPaymentRetry : OrderWorkflowStatus.NeedsAttention }, ct); }
    private async Task<OrderWorkflowState> Save(OrderWorkflowState value, CancellationToken ct) { await state.SaveAsync(value, ct); return value; }
}
public sealed class CancellationCoordinator(ISalesOrderCancellation sales, IOrderStockRelease inventory, IOrderPaymentCapture payments)
{
    public async Task<(SalesProgressOutcome Sales, OrderStockOutcome Stock, PaymentCommandOutcome Payment)> CancelAsync(CancelSalesOrder command, CancellationToken ct)
    { var cancelled = await sales.CancelAsync(command, ct); if (cancelled is not SalesProgressOutcome.Applied and not SalesProgressOutcome.AlreadyApplied) return (cancelled, OrderStockOutcome.NeedsAttention, PaymentCommandOutcome.Invalid); var stock = await inventory.ReleaseAsync(new(command.TrustedTenantId, command.OrderId, $"release:{command.SourceIdentity}", command.CorrelationId), ct); var payment = await payments.GetAsync(command.TrustedTenantId, command.OrderId, ct); return (cancelled, stock, payment?.Outcome ?? PaymentCommandOutcome.Invalid); }
}
