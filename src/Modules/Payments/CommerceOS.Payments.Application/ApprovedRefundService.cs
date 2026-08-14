namespace CommerceOS.Payments.Application;

public enum RefundOperationStatus { OutcomeUnknown, Refunded, Declined }
public sealed record PaymentRefundOperation(string RefundApprovalId, string ProviderOperationId, long AmountVnd, RefundOperationStatus Status, int ReconciliationCount);
public sealed record PaymentRefundLedger(string TenantId, string PaymentId, long CapturedAmountVnd, IReadOnlyList<PaymentRefundOperation> Operations)
{
    public long VerifiedRefundedAmountVnd => Operations.Where(x => x.Status is RefundOperationStatus.Refunded).Sum(x => x.AmountVnd);
}
public sealed record ApprovedRefundPaymentFact(string EventId, string TenantId, string RefundApprovalId, string PaymentId, long AmountVnd, string Currency, string CorrelationId, DateTimeOffset OccurredAt);
public sealed record PaymentRefundedFact(string EventId, string TenantId, string RefundApprovalId, string PaymentId, string ProviderOperationId, long AmountVnd, string Currency, string CorrelationId, DateTimeOffset OccurredAt);
public enum RefundProviderOutcome { Refunded, Declined, TimedOut, TransientFailure }
public sealed record RefundProviderResponse(RefundProviderOutcome Outcome, string ProviderOperationId);
public interface IRefundPaymentProvider { Task<RefundProviderResponse> RefundAsync(string idempotencyKey, string paymentId, long amountVnd, CancellationToken cancellationToken); Task<RefundProviderOutcome?> QueryRefundAsync(string providerOperationId, CancellationToken cancellationToken); }
public interface IApprovedRefundStore { Task<PaymentRefundLedger?> GetAsync(string trustedTenantId, string paymentId, CancellationToken cancellationToken); Task<bool> SaveAsync(PaymentRefundLedger ledger, CancellationToken cancellationToken); }
public enum ApprovedRefundOutcome { Refunded, Declined, OutcomeUnknown, AlreadyApplied, NeedsAttention, Invalid }

/// <summary>Payments-owned provider execution. An unknown operation is reconciled, never retried as a new unsafe call.</summary>
public sealed class ApprovedRefundService(IApprovedRefundStore store, IRefundPaymentProvider provider)
{
    public async Task<ApprovedRefundOutcome> ApplyAsync(ApprovedRefundPaymentFact fact, long capturedAmountVnd, CancellationToken ct)
    {
        if (!Valid(fact) || capturedAmountVnd <= 0 || fact.AmountVnd > capturedAmountVnd) return ApprovedRefundOutcome.Invalid;
        var ledger = await store.GetAsync(fact.TenantId, fact.PaymentId, ct) ?? new PaymentRefundLedger(fact.TenantId, fact.PaymentId, capturedAmountVnd, []);
        if (ledger.CapturedAmountVnd != capturedAmountVnd || ledger.Operations.Any(x => x.RefundApprovalId == fact.RefundApprovalId)) return ledger.Operations.Any(x => x.RefundApprovalId == fact.RefundApprovalId) ? Already(ledger, fact.RefundApprovalId) : ApprovedRefundOutcome.Invalid;
        if (ledger.Operations.Any(x => x.Status is RefundOperationStatus.OutcomeUnknown)) return ApprovedRefundOutcome.OutcomeUnknown;
        if (ledger.VerifiedRefundedAmountVnd + fact.AmountVnd > ledger.CapturedAmountVnd) return ApprovedRefundOutcome.Invalid;
        var id = $"refund:{fact.PaymentId}:{fact.RefundApprovalId}";
        var response = await provider.RefundAsync(id, fact.PaymentId, fact.AmountVnd, ct);
        var status = response.Outcome is RefundProviderOutcome.Refunded ? RefundOperationStatus.Refunded : response.Outcome is RefundProviderOutcome.Declined ? RefundOperationStatus.Declined : RefundOperationStatus.OutcomeUnknown;
        var updated = ledger with { Operations = [.. ledger.Operations, new(fact.RefundApprovalId, response.ProviderOperationId, fact.AmountVnd, status, 0)] };
        if (!await store.SaveAsync(updated, ct)) return ApprovedRefundOutcome.NeedsAttention;
        return status is RefundOperationStatus.Refunded ? ApprovedRefundOutcome.Refunded : status is RefundOperationStatus.Declined ? ApprovedRefundOutcome.Declined : ApprovedRefundOutcome.OutcomeUnknown;
    }
    public async Task<ApprovedRefundOutcome> ReconcileAsync(string trustedTenantId, string paymentId, string refundApprovalId, CancellationToken ct)
    {
        var ledger = await store.GetAsync(trustedTenantId, paymentId, ct); var operation = ledger?.Operations.SingleOrDefault(x => x.RefundApprovalId == refundApprovalId);
        if (ledger is null || operation is null) return ApprovedRefundOutcome.Invalid;
        if (operation.Status is not RefundOperationStatus.OutcomeUnknown) return Already(ledger, refundApprovalId);
        var response = await provider.QueryRefundAsync(operation.ProviderOperationId, ct);
        var status = response is RefundProviderOutcome.Refunded ? RefundOperationStatus.Refunded : response is RefundProviderOutcome.Declined ? RefundOperationStatus.Declined : RefundOperationStatus.OutcomeUnknown;
        var count = operation.ReconciliationCount + 1;
        var changed = operation with { Status = status, ReconciliationCount = count };
        var updated = ledger with { Operations = ledger.Operations.Select(x => x.RefundApprovalId == refundApprovalId ? changed : x).ToArray() };
        if (!await store.SaveAsync(updated, ct)) return ApprovedRefundOutcome.NeedsAttention;
        return status is RefundOperationStatus.Refunded ? ApprovedRefundOutcome.Refunded : status is RefundOperationStatus.Declined ? ApprovedRefundOutcome.Declined : count >= 3 ? ApprovedRefundOutcome.NeedsAttention : ApprovedRefundOutcome.OutcomeUnknown;
    }
    private static ApprovedRefundOutcome Already(PaymentRefundLedger ledger, string approval) => ledger.Operations.Single(x => x.RefundApprovalId == approval).Status switch { RefundOperationStatus.Refunded => ApprovedRefundOutcome.AlreadyApplied, RefundOperationStatus.Declined => ApprovedRefundOutcome.Declined, _ => ApprovedRefundOutcome.OutcomeUnknown };
    private static bool Valid(ApprovedRefundPaymentFact x) => !string.IsNullOrWhiteSpace(x.EventId) && !string.IsNullOrWhiteSpace(x.TenantId) && !string.IsNullOrWhiteSpace(x.RefundApprovalId) && !string.IsNullOrWhiteSpace(x.PaymentId) && x.AmountVnd > 0 && x.Currency == "VND" && !string.IsNullOrWhiteSpace(x.CorrelationId);
}
