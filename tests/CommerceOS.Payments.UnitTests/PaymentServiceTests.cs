using CommerceOS.MockPaymentProvider.Contracts;
using CommerceOS.Payments.Application;
using CommerceOS.Payments.Contracts;
using CommerceOS.Payments.Domain;

namespace CommerceOS.Payments.UnitTests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task RefundTimeoutBlocksUnsafeDuplicateUntilInquiryFindsVerifiedEvidence()
    {
        var store = new RefundStore(); var provider = new RefundProvider(RefundProviderOutcome.TimedOut, RefundProviderOutcome.Refunded); var service = new ApprovedRefundService(store, provider);
        var fact = new ApprovedRefundPaymentFact("event", "tenant", "approval", "payment", 10, "VND", "c", DateTimeOffset.UtcNow);
        Assert.Equal(ApprovedRefundOutcome.OutcomeUnknown, await service.ApplyAsync(fact, 10, default));
        Assert.Equal(ApprovedRefundOutcome.OutcomeUnknown, await service.ApplyAsync(fact with { EventId = "duplicate" }, 10, default));
        Assert.Equal(ApprovedRefundOutcome.Refunded, await service.ReconcileAsync("tenant", "payment", "approval", default));
    }
    [Fact]
    public async Task TimeoutBlocksANewAttemptUntilReconciliationConverges()
    {
        var service = new PaymentService(new Store(), new Provider(ProviderCallOutcome.TimedOut, ProviderEvidenceStatus.Captured)); var command = Command();
        var unknown = await service.CaptureAsync(command, default);
        Assert.Equal(PaymentCommandOutcome.OutcomeUnknown, unknown.Outcome);
        Assert.Equal(PaymentCommandOutcome.OutcomeUnknown, (await service.CaptureAsync(command with { SourceIdentity = "retry" }, default)).Outcome);
        Assert.Equal(PaymentCommandOutcome.Captured, (await service.ReconcileAsync("tenant", "order", unknown.ProviderOperationId!, "c", default)).Outcome);
    }
    [Fact]
    public async Task DeclineTerminatesOnlyTheAttempt()
    {
        var service = new PaymentService(new Store(), new Provider(ProviderCallOutcome.Declined, ProviderEvidenceStatus.Declined));
        Assert.Equal(PaymentCommandOutcome.Declined, (await service.CaptureAsync(Command(), default)).Outcome);
        Assert.Equal(PaymentCommandOutcome.Declined, (await service.CaptureAsync(Command() with { SourceIdentity = "second" }, default)).Outcome);
    }
    private static CaptureOrderPayment Command() => new("tenant", "order", 10, "VND", "source", "Success", "c");
    private sealed class Store : IPaymentStore { private PaymentObligation? _payment; public Task<PaymentObligation?> GetAsync(TrustedPaymentContext c, string o, CancellationToken ct) => Task.FromResult(_payment); public Task<PaymentStoreOutcome> CreateAsync(TrustedPaymentContext c, PaymentObligation p, CancellationToken ct) { _payment = p; return Task.FromResult(PaymentStoreOutcome.Applied); } public Task<PaymentStoreOutcome> SaveAsync(TrustedPaymentContext c, PaymentObligation before, PaymentObligation after, string evidence, CancellationToken ct) { if (_payment?.Revision != before.Revision) return Task.FromResult(PaymentStoreOutcome.Conflict); _payment = after; return Task.FromResult(PaymentStoreOutcome.Applied); } }
    private sealed class Provider(ProviderCallOutcome capture, ProviderEvidenceStatus query) : IMerchantPaymentProvider { public Task<ProviderCaptureResponse> CaptureAsync(ProviderCaptureRequest request, CancellationToken ct) => Task.FromResult(new ProviderCaptureResponse(capture, new ProviderOperationEvidence(request.IdempotencyKey, request.MerchantReference, request.AmountVnd, query))); public Task<ProviderOperationEvidence?> QueryAsync(string id, CancellationToken ct) => Task.FromResult<ProviderOperationEvidence?>(new(id, "order", 10, query)); public bool VerifyWebhook(SignedProviderWebhook webhook) => true; }
    private sealed class RefundStore : IApprovedRefundStore
    {
        private PaymentRefundLedger? value;
        public Task<PaymentRefundLedger?> GetAsync(string tenant, string payment, CancellationToken ct) => Task.FromResult(value?.TenantId == tenant && value.PaymentId == payment ? value : null);
        public Task<bool> SaveAsync(PaymentRefundLedger ledger, CancellationToken ct) { value = ledger; return Task.FromResult(true); }
    }
    private sealed class RefundProvider(RefundProviderOutcome call, RefundProviderOutcome query) : IRefundPaymentProvider
    {
        public Task<RefundProviderResponse> RefundAsync(string key, string payment, long amount, CancellationToken ct) => Task.FromResult(new RefundProviderResponse(call, key));
        public Task<RefundProviderOutcome?> QueryRefundAsync(string id, CancellationToken ct) => Task.FromResult<RefundProviderOutcome?>(query);
    }
}
