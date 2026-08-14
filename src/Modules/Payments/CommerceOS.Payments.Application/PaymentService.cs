using CommerceOS.MockPaymentProvider.Contracts;
using CommerceOS.Payments.Contracts;
using CommerceOS.Payments.Domain;

namespace CommerceOS.Payments.Application;

public sealed record TrustedPaymentContext(string TenantId, string CorrelationId);
public enum PaymentStoreOutcome { Applied, Conflict }
public interface IPaymentStore { Task<PaymentObligation?> GetAsync(TrustedPaymentContext context, string orderId, CancellationToken cancellationToken); Task<PaymentStoreOutcome> CreateAsync(TrustedPaymentContext context, PaymentObligation obligation, CancellationToken cancellationToken); Task<PaymentStoreOutcome> SaveAsync(TrustedPaymentContext context, PaymentObligation before, PaymentObligation after, string evidenceId, CancellationToken cancellationToken); }
public sealed class PaymentService(IPaymentStore store, IMerchantPaymentProvider provider) : IOrderPaymentCapture
{
    public async Task<PaymentCommandResult> HandleWebhookAsync(string trustedTenantId, string orderId, SignedProviderWebhook webhook, string correlationId, CancellationToken cancellationToken)
    {
        if (!provider.VerifyWebhook(webhook)) return new(PaymentCommandOutcome.Invalid, null, null);
        var context = new TrustedPaymentContext(trustedTenantId, correlationId); var obligation = await store.GetAsync(context, orderId, cancellationToken);
        if (obligation is null) return new(PaymentCommandOutcome.Invalid, null, null);
        var evidence = await provider.QueryAsync(webhook.ProviderOperationId, cancellationToken);
        if (evidence is null || evidence.MerchantReference != orderId || evidence.AmountVnd != obligation.AmountVnd) return new(PaymentCommandOutcome.Invalid, null, null);
        return await ApplyAsync(context, obligation, webhook.ProviderOperationId, evidence.Status is ProviderEvidenceStatus.Captured ? PaymentAttemptStatus.Captured : evidence.Status is ProviderEvidenceStatus.Declined or ProviderEvidenceStatus.NoCommit ? PaymentAttemptStatus.Declined : PaymentAttemptStatus.OutcomeUnknown, $"webhook:{webhook.DeliveryId}", cancellationToken);
    }
    public async Task<PaymentCommandResult> CaptureAsync(CaptureOrderPayment command, CancellationToken cancellationToken)
    {
        if (!Valid(command)) return new(PaymentCommandOutcome.Invalid, null, null);
        var context = new TrustedPaymentContext(command.TrustedTenantId, command.CorrelationId); var obligation = await store.GetAsync(context, command.OrderId, cancellationToken);
        if (obligation is null) { obligation = PaymentObligation.Create(command.OrderId, command.TrustedTenantId, command.AmountVnd, command.Currency); if (await store.CreateAsync(context, obligation, cancellationToken) is PaymentStoreOutcome.Conflict) obligation = await store.GetAsync(context, command.OrderId, cancellationToken); }
        if (obligation is null || obligation.AmountVnd != command.AmountVnd || obligation.Currency != command.Currency) return new(PaymentCommandOutcome.Conflict, null, null);
        if (obligation.LatestAttempt is { Status: PaymentAttemptStatus.OutcomeUnknown } latest) return new(PaymentCommandOutcome.OutcomeUnknown, latest.Id, latest.ProviderOperationId);
        var operationId = $"payment:{command.OrderId}:capture:{obligation.Attempts.Count + 1}";
        try { var started = obligation.StartAttempt(command.SourceIdentity, operationId); if (started != obligation && await store.SaveAsync(context, obligation, started, $"start:{command.SourceIdentity}", cancellationToken) is PaymentStoreOutcome.Conflict) return new(PaymentCommandOutcome.Conflict, null, null); obligation = started; }
        catch (PaymentRuleException) { return new(PaymentCommandOutcome.OutcomeUnknown, obligation.LatestAttempt?.Id, obligation.LatestAttempt?.ProviderOperationId); }
        var response = await provider.CaptureAsync(new(operationId, command.OrderId, command.AmountVnd, command.Scenario), cancellationToken);
        return await ApplyAsync(context, obligation, response.Evidence?.ProviderOperationId ?? operationId, response.Outcome switch { ProviderCallOutcome.Captured => PaymentAttemptStatus.Captured, ProviderCallOutcome.Declined => PaymentAttemptStatus.Declined, _ => PaymentAttemptStatus.OutcomeUnknown }, $"provider-response:{operationId}", cancellationToken);
    }
    public async Task<PaymentCommandResult> ReconcileAsync(string trustedTenantId, string orderId, string providerOperationId, string correlationId, CancellationToken cancellationToken)
    {
        var context = new TrustedPaymentContext(trustedTenantId, correlationId); var obligation = await store.GetAsync(context, orderId, cancellationToken); if (obligation is null) return new(PaymentCommandOutcome.Invalid, null, null);
        var evidence = await provider.QueryAsync(providerOperationId, cancellationToken);
        if (evidence is not null && evidence.AmountVnd == obligation.AmountVnd && evidence.MerchantReference == orderId && evidence.Status is ProviderEvidenceStatus.Captured or ProviderEvidenceStatus.Declined or ProviderEvidenceStatus.NoCommit)
            return await ApplyAsync(context, obligation, providerOperationId, evidence.Status is ProviderEvidenceStatus.Captured ? PaymentAttemptStatus.Captured : PaymentAttemptStatus.Declined, $"reconcile:{providerOperationId}:{evidence.Status}", cancellationToken);
        var updated = obligation.RecordReconciliation(providerOperationId, 3); await store.SaveAsync(context, obligation, updated, $"reconcile-wait:{providerOperationId}:{updated.LatestAttempt?.ReconciliationCount}", cancellationToken);
        return updated.LatestAttempt?.ReconciliationCount >= 3 ? new(PaymentCommandOutcome.NeedsAttention, updated.LatestAttempt.Id, providerOperationId) : new(PaymentCommandOutcome.OutcomeUnknown, updated.LatestAttempt?.Id, providerOperationId);
    }
    public async Task<PaymentStatusView?> GetAsync(string trustedTenantId, string orderId, CancellationToken cancellationToken) { var obligation = await store.GetAsync(new(trustedTenantId, "payment-query"), orderId, cancellationToken); var attempt = obligation?.LatestAttempt; return obligation is null ? null : new(orderId, attempt?.Status switch { PaymentAttemptStatus.Captured => PaymentCommandOutcome.Captured, PaymentAttemptStatus.Declined => PaymentCommandOutcome.Declined, _ => PaymentCommandOutcome.OutcomeUnknown }, attempt?.ProviderOperationId); }
    private async Task<PaymentCommandResult> ApplyAsync(TrustedPaymentContext context, PaymentObligation obligation, string operationId, PaymentAttemptStatus status, string evidenceId, CancellationToken ct) { var updated = obligation.ApplyEvidence(operationId, status); if (updated != obligation && await store.SaveAsync(context, obligation, updated, evidenceId, ct) is PaymentStoreOutcome.Conflict) return new(PaymentCommandOutcome.Conflict, null, null); var attempt = updated.LatestAttempt; return new(status is PaymentAttemptStatus.Captured ? PaymentCommandOutcome.Captured : status is PaymentAttemptStatus.Declined ? PaymentCommandOutcome.Declined : PaymentCommandOutcome.OutcomeUnknown, attempt?.Id, operationId); }
    private static bool Valid(CaptureOrderPayment c) => !string.IsNullOrWhiteSpace(c.TrustedTenantId) && !string.IsNullOrWhiteSpace(c.OrderId) && c.AmountVnd > 0 && c.Currency == "VND" && !string.IsNullOrWhiteSpace(c.SourceIdentity) && !string.IsNullOrWhiteSpace(c.CorrelationId);
}
