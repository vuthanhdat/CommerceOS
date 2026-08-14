namespace CommerceOS.Payments.Domain;

public enum PaymentAttemptStatus { Captured, Declined, OutcomeUnknown }
public sealed record PaymentAttempt(string Id, string SourceIdentity, string ProviderOperationId, PaymentAttemptStatus Status, long AmountVnd, int ReconciliationCount);
public sealed record PaymentObligation(string OrderId, string TenantId, long AmountVnd, string Currency, IReadOnlyList<PaymentAttempt> Attempts, long Revision)
{
    public PaymentAttempt? LatestAttempt => Attempts.Count == 0 ? null : Attempts[^1];
    public static PaymentObligation Create(string orderId, string tenantId, long amountVnd, string currency) => string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(tenantId) || amountVnd <= 0 || currency != "VND" ? throw new PaymentRuleException("PAYMENT_AMOUNT_MISMATCH") : new(orderId, tenantId, amountVnd, currency, [], 1);
    public PaymentObligation StartAttempt(string sourceIdentity, string providerOperationId)
    {
        if (string.IsNullOrWhiteSpace(sourceIdentity) || string.IsNullOrWhiteSpace(providerOperationId)) throw new PaymentRuleException("PAYMENT_OPERATION_CONFLICT");
        if (LatestAttempt is { Status: PaymentAttemptStatus.OutcomeUnknown }) throw new PaymentRuleException("PAYMENT_OUTCOME_UNKNOWN");
        if (Attempts.Any(x => x.SourceIdentity == sourceIdentity)) return this;
        return this with { Attempts = [.. Attempts, new($"attempt-{Attempts.Count + 1}", sourceIdentity, providerOperationId, PaymentAttemptStatus.OutcomeUnknown, AmountVnd, 0)], Revision = Revision + 1 };
    }
    public PaymentObligation ApplyEvidence(string providerOperationId, PaymentAttemptStatus status)
    {
        var attempt = Attempts.LastOrDefault(x => x.ProviderOperationId == providerOperationId) ?? throw new PaymentRuleException("PAYMENT_OPERATION_CONFLICT");
        if (attempt.Status is PaymentAttemptStatus.Captured or PaymentAttemptStatus.Declined) return this;
        var updated = attempt with { Status = status };
        return this with { Attempts = Attempts.Select(x => x.Id == attempt.Id ? updated : x).ToArray(), Revision = Revision + 1 };
    }
    public PaymentObligation RecordReconciliation(string providerOperationId, int maximumAttempts)
    {
        var attempt = Attempts.LastOrDefault(x => x.ProviderOperationId == providerOperationId) ?? throw new PaymentRuleException("PAYMENT_OPERATION_CONFLICT");
        if (attempt.Status is not PaymentAttemptStatus.OutcomeUnknown) return this;
        return this with { Attempts = Attempts.Select(x => x.Id == attempt.Id ? attempt with { ReconciliationCount = Math.Min(maximumAttempts, attempt.ReconciliationCount + 1) } : x).ToArray(), Revision = Revision + 1 };
    }
}
public sealed class PaymentRuleException(string code) : InvalidOperationException(code) { public string Code { get; } = code; }
