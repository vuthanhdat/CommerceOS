using CommerceOS.Inventory.Contracts;
using CommerceOS.OrderOrchestration.Application;
using CommerceOS.OrderOrchestration.Domain;
using CommerceOS.Payments.Contracts;
using CommerceOS.Sales.Contracts;

namespace CommerceOS.OrderOrchestration.UnitTests;

public sealed class OrderWorkflowCoordinatorTests
{
    [Fact]
    public async Task CapturedPaymentConfirmsAndAllocatesThroughOwnerContracts()
    {
        var state = new MemoryState(); var flow = new OrderWorkflowCoordinator(state, new Stock(OrderStockOutcome.Applied), new Payment(PaymentCommandOutcome.Captured), new Sales());
        var result = await flow.StartAsync(new("tenant", "order", "exec", 10, "placed", "c", 1, 2, "Success"), default);
        Assert.Equal(OrderWorkflowStatus.Allocated, result.Status);
    }
    [Fact]
    public async Task UnknownNeverReleasesStockAndEventuallyNeedsAttention()
    {
        var state = new MemoryState(); var flow = new OrderWorkflowCoordinator(state, new Stock(OrderStockOutcome.Applied), new Payment(PaymentCommandOutcome.OutcomeUnknown, PaymentCommandOutcome.NeedsAttention), new Sales());
        var started = await flow.StartAsync(new("tenant", "order", "exec", 10, "placed", "c", 1, 2, "TimeoutAfterCommit"), default);
        Assert.Equal(OrderWorkflowStatus.ReconcilingPayment, started.Status);
        Assert.Equal(OrderWorkflowStatus.NeedsAttention, (await flow.RecoverAsync(started, "provider", default)).Status);
    }
    private sealed class MemoryState : IOrderWorkflowStateStore { public Task<bool> TryStartAsync(OrderWorkflowState state, CancellationToken ct) => Task.FromResult(true); public Task SaveAsync(OrderWorkflowState state, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Stock(OrderStockOutcome outcome) : IOrderStockReservation { public Task<OrderStockOutcome> ReserveAsync(ReserveOrderStock command, CancellationToken ct) => Task.FromResult(outcome); }
    private sealed class Payment(PaymentCommandOutcome capture, PaymentCommandOutcome reconcile = PaymentCommandOutcome.OutcomeUnknown) : IOrderPaymentCapture { public Task<PaymentCommandResult> CaptureAsync(CaptureOrderPayment command, CancellationToken ct) => Task.FromResult(new PaymentCommandResult(capture, "attempt", "provider")); public Task<PaymentCommandResult> ReconcileAsync(string tenant, string order, string provider, string correlation, CancellationToken ct) => Task.FromResult(new PaymentCommandResult(reconcile, "attempt", provider)); public Task<PaymentStatusView?> GetAsync(string tenant, string order, CancellationToken ct) => Task.FromResult<PaymentStatusView?>(null); }
    private sealed class Sales : ISalesOrderWorkflowProgress { public Task<SalesProgressOutcome> ConfirmAsync(string tenant, string order, string source, long revision, string correlation, CancellationToken ct) => Task.FromResult(SalesProgressOutcome.Applied); public Task<SalesProgressOutcome> AllocateAsync(string tenant, string order, string source, long revision, string correlation, CancellationToken ct) => Task.FromResult(SalesProgressOutcome.Applied); }
}
