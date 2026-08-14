namespace CommerceOS.MockPaymentProvider.Contracts;

public enum ProviderEvidenceStatus { Captured, Declined, Pending, NoCommit }
public enum ProviderCallOutcome { Captured, Declined, Pending, TimedOut, TransientFailure, IdempotencyConflict }
public sealed record ProviderCaptureRequest(string IdempotencyKey, string MerchantReference, long AmountVnd, string Scenario);
public sealed record ProviderOperationEvidence(string ProviderOperationId, string MerchantReference, long AmountVnd, ProviderEvidenceStatus Status);
public sealed record ProviderCaptureResponse(ProviderCallOutcome Outcome, ProviderOperationEvidence? Evidence);
public sealed record SignedProviderWebhook(string DeliveryId, string ProviderOperationId, ProviderEvidenceStatus Status, int Sequence, string Signature);
public interface IMerchantPaymentProvider
{
    Task<ProviderCaptureResponse> CaptureAsync(ProviderCaptureRequest request, CancellationToken cancellationToken);
    Task<ProviderOperationEvidence?> QueryAsync(string providerOperationId, CancellationToken cancellationToken);
    bool VerifyWebhook(SignedProviderWebhook webhook);
}
