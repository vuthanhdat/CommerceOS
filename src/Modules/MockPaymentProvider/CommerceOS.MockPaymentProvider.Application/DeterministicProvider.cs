using CommerceOS.MockPaymentProvider.Domain;
using CommerceOS.MockPaymentProvider.Contracts;
using System.Security.Cryptography;
using System.Text;
namespace CommerceOS.MockPaymentProvider.Application;

public enum ProviderOutcome { Captured, Declined, Pending, TimedOut, TransientFailure, IdempotencyConflict }
public sealed record ProviderResult(ProviderOutcome Outcome, ProviderPaymentIntent? Intent, bool ShouldDispatchWebhook, int DuplicateWebhookCount);
public sealed record ProviderWebhook(string DeliveryId, string IntentId, ProviderPaymentStatus Status, int Sequence, string Signature);
public interface IProviderOperationStore
{
    Task<ProviderOperation?> GetAsync(string idempotencyKey, CancellationToken ct);
    Task<ProviderPaymentIntent?> GetIntentAsync(string intentId, CancellationToken ct);
    Task<bool> PutAsync(ProviderOperation operation, CancellationToken ct);
}
public sealed class DeterministicProvider(IProviderOperationStore store, string signingSecret = "commerceos-test-webhook-secret") : IMerchantPaymentProvider
{
    public async Task<ProviderCaptureResponse> CaptureAsync(ProviderCaptureRequest request, CancellationToken cancellationToken)
    {
        var scenario = Enum.TryParse<ProviderScenario>(request.Scenario, true, out var parsed) ? parsed : ProviderScenario.Success;
        var result = await CaptureAsync(request.IdempotencyKey, $"{request.MerchantReference}|{request.AmountVnd}", request.MerchantReference, request.AmountVnd, scenario, cancellationToken);
        return new(result.Outcome switch { ProviderOutcome.Captured => ProviderCallOutcome.Captured, ProviderOutcome.Declined => ProviderCallOutcome.Declined, ProviderOutcome.Pending => ProviderCallOutcome.Pending, ProviderOutcome.TimedOut => ProviderCallOutcome.TimedOut, ProviderOutcome.IdempotencyConflict => ProviderCallOutcome.IdempotencyConflict, _ => ProviderCallOutcome.TransientFailure }, result.Intent is null ? null : Evidence(result.Intent));
    }
    async Task<ProviderOperationEvidence?> IMerchantPaymentProvider.QueryAsync(string providerOperationId, CancellationToken cancellationToken)
    { var intent = await QueryAsync(providerOperationId, cancellationToken); return intent is null ? null : Evidence(intent); }
    bool IMerchantPaymentProvider.VerifyWebhook(SignedProviderWebhook webhook) => VerifyWebhook(new ProviderWebhook(webhook.DeliveryId, webhook.ProviderOperationId, ToDomain(webhook.Status), webhook.Sequence, webhook.Signature), signingSecret);
    public async Task<ProviderResult> CaptureAsync(string idempotencyKey, string fingerprint, string merchantReference, long amountVnd, ProviderScenario scenario, CancellationToken ct)
    {
        if (amountVnd <= 0 || string.IsNullOrWhiteSpace(idempotencyKey) || string.IsNullOrWhiteSpace(fingerprint)) return new(ProviderOutcome.TransientFailure, null, false, 0);
        var replay = await store.GetAsync(idempotencyKey, ct);
        if (replay is not null) return replay.RequestFingerprint == fingerprint ? ResultFor(replay.Intent, replay.CallerTimedOut) : new(ProviderOutcome.IdempotencyConflict, null, false, 0);
        if (scenario is ProviderScenario.Provider500) return new(ProviderOutcome.TransientFailure, null, false, 0);
        var status = scenario switch { ProviderScenario.Declined => ProviderPaymentStatus.Declined, ProviderScenario.DelayedSuccess => ProviderPaymentStatus.Pending, ProviderScenario.TimeoutBeforeCommit => ProviderPaymentStatus.Created, _ => ProviderPaymentStatus.Captured };
        var intent = new ProviderPaymentIntent($"pi_{Guid.NewGuid():N}", merchantReference, amountVnd, status, scenario, 1);
        var timeout = scenario is ProviderScenario.TimeoutAfterCommit or ProviderScenario.TimeoutBeforeCommit;
        if (!await store.PutAsync(new(idempotencyKey, fingerprint, intent, timeout), ct)) return new(ProviderOutcome.TransientFailure, null, false, 0);
        return ResultFor(intent, timeout);
    }
    public Task<ProviderPaymentIntent?> QueryAsync(string intentId, CancellationToken ct) => store.GetIntentAsync(intentId, ct);
    public static IReadOnlyList<ProviderWebhook> BuildWebhooks(ProviderResult result, string signingSecret)
    {
        if (!result.ShouldDispatchWebhook || result.Intent is null) return [];
        var count = Math.Max(1, result.DuplicateWebhookCount);
        var sequence = result.Intent.Scenario is ProviderScenario.WebhookBeforeResponse ? 0 : 1;
        return Enumerable.Range(0, count).Select(index => CreateWebhook(result.Intent, sequence + index, signingSecret)).ToArray();
    }
    public static bool VerifyWebhook(ProviderWebhook webhook, string signingSecret) => FixedTimeEquals(webhook.Signature, Sign(webhook.IntentId, webhook.Status, webhook.Sequence, signingSecret));
    private static ProviderWebhook CreateWebhook(ProviderPaymentIntent intent, int sequence, string secret) => new($"wh_{intent.Id}_{sequence}", intent.Id, intent.Status, sequence, Sign(intent.Id, intent.Status, sequence, secret));
    private static string Sign(string intentId, ProviderPaymentStatus status, int sequence, string secret) => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{intentId}|{status}|{sequence}")));
    private static bool FixedTimeEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static ProviderResult ResultFor(ProviderPaymentIntent intent, bool timeout) => timeout ? new(ProviderOutcome.TimedOut, intent, intent.Status is ProviderPaymentStatus.Captured, intent.Scenario is ProviderScenario.DuplicateWebhook ? 2 : 1) : intent.Status switch { ProviderPaymentStatus.Captured => new(ProviderOutcome.Captured, intent, true, intent.Scenario is ProviderScenario.DuplicateWebhook ? 2 : 1), ProviderPaymentStatus.Declined => new(ProviderOutcome.Declined, intent, true, 1), _ => new(ProviderOutcome.Pending, intent, false, 0) };
    private static ProviderOperationEvidence Evidence(ProviderPaymentIntent intent) => new(intent.Id, intent.MerchantReference, intent.AmountVnd, intent.Status switch { ProviderPaymentStatus.Captured => ProviderEvidenceStatus.Captured, ProviderPaymentStatus.Declined => ProviderEvidenceStatus.Declined, ProviderPaymentStatus.Created => ProviderEvidenceStatus.NoCommit, _ => ProviderEvidenceStatus.Pending });
    private static ProviderPaymentStatus ToDomain(ProviderEvidenceStatus status) => status switch { ProviderEvidenceStatus.Captured => ProviderPaymentStatus.Captured, ProviderEvidenceStatus.Declined => ProviderPaymentStatus.Declined, ProviderEvidenceStatus.NoCommit => ProviderPaymentStatus.Created, _ => ProviderPaymentStatus.Pending };
}
