namespace CommerceOS.SubscriptionBilling.Contracts;

/// <summary>
/// Producer-owned command used only to start the Trial required by an accepted
/// merchant-onboarding operation. The identifiers are server-issued stable
/// source references, never a caller-selected entitlement or plan.
/// </summary>
public sealed record StartTrialSubscriptionCommand(
    string TenantId,
    string OnboardingOperationId,
    string SourceIdentity,
    string CorrelationId);

public enum TrialSubscriptionStartOutcome
{
    Accepted,
    AlreadyApplied,
    SourceConflict
}

public sealed record TrialSubscriptionStartResult(
    TrialSubscriptionStartOutcome Outcome,
    string? TrialTermsVersionId = null);

public interface ITrialSubscriptionStarter
{
    Task<TrialSubscriptionStartResult> StartTrialSubscriptionAsync(
        StartTrialSubscriptionCommand command,
        CancellationToken cancellationToken);
}
