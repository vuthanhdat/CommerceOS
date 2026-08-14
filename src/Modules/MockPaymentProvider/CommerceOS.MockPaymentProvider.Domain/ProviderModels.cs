namespace CommerceOS.MockPaymentProvider.Domain;

public enum ProviderScenario { Success, Declined, TimeoutBeforeCommit, TimeoutAfterCommit, DelayedSuccess, Provider500, DuplicateWebhook, WebhookBeforeResponse }
public enum ProviderPaymentStatus { Created, Captured, Declined, Pending, Refunded }
public sealed record ProviderPaymentIntent(string Id, string MerchantReference, long AmountVnd, ProviderPaymentStatus Status, ProviderScenario Scenario, long Revision);
public sealed record ProviderOperation(string IdempotencyKey, string RequestFingerprint, ProviderPaymentIntent Intent, bool CallerTimedOut);
