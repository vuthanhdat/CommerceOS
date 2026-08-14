using CommerceOS.SubscriptionBilling.Contracts;

namespace CommerceOS.SubscriptionBilling.Application.Trial;

public sealed record TrialEntitlementSnapshot(
    string TrialTermsVersionId,
    int DurationDays,
    bool CoreCommerceCapabilities,
    int MaxActiveMemberships,
    int MaxWarehouses,
    bool ScheduledProductIngestion,
    int OrderVolumeWarningThreshold);

public sealed record TrialSubscription(
    string TenantId,
    string OnboardingOperationId,
    string SourceIdentity,
    TrialEntitlementSnapshot Entitlements,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveUntil,
    SubscriptionCondition Condition = SubscriptionCondition.Trial);

public enum SubscriptionCondition { Trial, Active, PastDue, Ended }

public interface ITrialSubscriptionStore
{
    Task<TrialSubscription?> GetForOnboardingAsync(
        string tenantId,
        string onboardingOperationId,
        CancellationToken cancellationToken);

    Task<TrialSubscription?> GetCurrentForTenantAsync(string tenantId, CancellationToken cancellationToken);

    Task<bool> CreateIfAbsentAsync(TrialSubscription subscription, CancellationToken cancellationToken);
}

/// <summary>
/// SubscriptionBilling owns both the Trial subscription and its immutable
/// entitlement snapshot. A repeated onboarding source can only observe the
/// same accepted Trial; it cannot select a different terms version.
/// </summary>
public sealed class TrialSubscriptionService : ITrialSubscriptionStarter
{
    public const string CurrentTrialTermsVersionId = "trial-v1";

    private readonly ISubscriptionCatalogQuery _catalog;
    private readonly ITrialSubscriptionStore _store;
    private readonly TimeProvider _clock;

    public TrialSubscriptionService(ISubscriptionCatalogQuery catalog, ITrialSubscriptionStore store, TimeProvider? clock = null)
    {
        _catalog = catalog;
        _store = store;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<TrialSubscriptionStartResult> StartTrialSubscriptionAsync(
        StartTrialSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await _store.GetForOnboardingAsync(
            command.TenantId,
            command.OnboardingOperationId,
            cancellationToken);
        if (existing is not null)
        {
            return SameSource(existing, command)
                ? new TrialSubscriptionStartResult(TrialSubscriptionStartOutcome.AlreadyApplied, existing.Entitlements.TrialTermsVersionId)
                : new TrialSubscriptionStartResult(TrialSubscriptionStartOutcome.SourceConflict);
        }

        var terms = await _catalog.GetTrialTermsVersionAsync(CurrentTrialTermsVersionId, cancellationToken)
            ?? throw new InvalidOperationException("The required Trial terms are not bootstrapped.");
        var effectiveFrom = _clock.GetUtcNow();
        var subscription = new TrialSubscription(
            command.TenantId,
            command.OnboardingOperationId,
            command.SourceIdentity,
            new TrialEntitlementSnapshot(
                terms.TrialTermsVersionId,
                terms.DurationDays,
                terms.CoreCommerceCapabilities,
                terms.MaxActiveMemberships,
                terms.MaxWarehouses,
                terms.ScheduledProductIngestion,
                terms.OrderVolumeWarningThreshold),
            effectiveFrom,
            effectiveFrom.AddDays(terms.DurationDays));

        if (await _store.CreateIfAbsentAsync(subscription, cancellationToken))
        {
            return new TrialSubscriptionStartResult(TrialSubscriptionStartOutcome.Accepted, terms.TrialTermsVersionId);
        }

        existing = await _store.GetForOnboardingAsync(command.TenantId, command.OnboardingOperationId, cancellationToken);
        return existing is not null && SameSource(existing, command)
            ? new TrialSubscriptionStartResult(TrialSubscriptionStartOutcome.AlreadyApplied, existing.Entitlements.TrialTermsVersionId)
            : new TrialSubscriptionStartResult(TrialSubscriptionStartOutcome.SourceConflict);
    }

    private static bool SameSource(TrialSubscription subscription, StartTrialSubscriptionCommand command) =>
        subscription.TenantId == command.TenantId &&
        subscription.OnboardingOperationId == command.OnboardingOperationId &&
        subscription.SourceIdentity == command.SourceIdentity;
}
