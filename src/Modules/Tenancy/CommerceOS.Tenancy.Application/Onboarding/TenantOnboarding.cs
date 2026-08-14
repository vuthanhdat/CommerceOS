using System.Security.Cryptography;
using System.Text;
using CommerceOS.SubscriptionBilling.Contracts;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.Application.Onboarding;

/// <summary>
/// Identity evidence supplied by the authentication edge after verification.
/// It deliberately contains no caller-selected Tenant identifier or role.
/// </summary>
public sealed record TrustedOnboardingContext(SubjectId SubjectId, string VerifiedEmail)
{
    public static TrustedOnboardingContext FromVerifiedIdentity(SubjectId subjectId, string verifiedEmail)
    {
        if (string.IsNullOrWhiteSpace(verifiedEmail))
        {
            throw new ArgumentException("A verified email is required for merchant onboarding.", nameof(verifiedEmail));
        }

        return new TrustedOnboardingContext(subjectId, verifiedEmail.Trim());
    }
}

public enum OnboardingStatus
{
    PendingTrial,
    Completed,
    NeedsAttention
}

public sealed record OnboardingOperation(
    string Id,
    SubjectId SubjectId,
    string IdempotencyKey,
    string RequestFingerprint,
    Tenant Tenant,
    Membership InitialOwner,
    OnboardingStatus Status,
    string CorrelationId);

public sealed record TrialBootstrapWorkItem(
    string WorkId,
    string OnboardingOperationId,
    string TenantId,
    string SourceIdentity,
    string CorrelationId);

public enum LocalOnboardingRegistrationOutcome
{
    Created,
    Replayed,
    Conflict
}

public sealed record LocalOnboardingRegistrationResult(
    LocalOnboardingRegistrationOutcome Outcome,
    OnboardingOperation? Operation,
    TrialBootstrapWorkItem? WorkItem);

/// <summary>
/// Tenancy-owned transactional port. Its implementation commits the operation,
/// Tenant, initial Owner, authority/discovery guards, and work outbox together.
/// </summary>
public interface ITenantOnboardingStore
{
    Task<LocalOnboardingRegistrationResult> RegisterAsync(
        OnboardingOperation operation,
        TrialBootstrapWorkItem workItem,
        CancellationToken cancellationToken);

    Task<OnboardingOperation?> GetAsync(
        TrustedOnboardingContext context,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<OnboardingOperation?> GetByOperationIdAsync(string operationId, CancellationToken cancellationToken);

    Task<bool> MarkCompletedAsync(string operationId, CancellationToken cancellationToken);
}

/// <summary>
/// Queue consumers use only the stable Tenancy work source. Duplicate delivery
/// is safe because SubscriptionBilling receives the same source identity and
/// completion is conditional.
/// </summary>
public sealed class OnboardingTrialRecoveryWorker
{
    private readonly ITenantOnboardingStore _store;
    private readonly ITrialSubscriptionStarter _trialStarter;

    public OnboardingTrialRecoveryWorker(ITenantOnboardingStore store, ITrialSubscriptionStarter trialStarter)
    {
        _store = store;
        _trialStarter = trialStarter;
    }

    public async Task<bool> ProcessAsync(TrialBootstrapWorkItem workItem, CancellationToken cancellationToken)
    {
        var operation = await _store.GetByOperationIdAsync(workItem.OnboardingOperationId, cancellationToken);
        if (operation is null || operation.Status is OnboardingStatus.Completed)
        {
            return false;
        }

        var trial = await _trialStarter.StartTrialSubscriptionAsync(
            new StartTrialSubscriptionCommand(
                workItem.TenantId,
                workItem.OnboardingOperationId,
                workItem.SourceIdentity,
                workItem.CorrelationId),
            cancellationToken);
        if (trial.Outcome is TrialSubscriptionStartOutcome.SourceConflict)
        {
            throw new InvalidOperationException("Trial source identity conflicts with existing SubscriptionBilling state.");
        }

        return await _store.MarkCompletedAsync(operation.Id, cancellationToken);
    }
}

public enum MerchantOnboardingOutcome
{
    Completed,
    PendingTrial,
    Conflict
}

public sealed record MerchantOnboardingResult(
    MerchantOnboardingOutcome Outcome,
    string? OperationId = null,
    string? TenantId = null);

public sealed class TenantOnboardingCoordinator
{
    private readonly ITenantOnboardingStore _store;
    private readonly ITrialSubscriptionStarter _trialStarter;

    public TenantOnboardingCoordinator(ITenantOnboardingStore store, ITrialSubscriptionStarter trialStarter)
    {
        _store = store;
        _trialStarter = trialStarter;
    }

    public async Task<MerchantOnboardingResult> RegisterAsync(
        TrustedOnboardingContext context,
        string idempotencyKey,
        BusinessProfile profile,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateRequest(idempotencyKey, profile, correlationId);
        var fingerprint = Fingerprint(profile);
        var operation = BuildOperation(context, idempotencyKey, fingerprint, profile, correlationId);
        var workItem = new TrialBootstrapWorkItem(
            $"trial-work-{operation.Id}",
            operation.Id,
            operation.Tenant.Id.Value,
            $"merchant-onboarding:{operation.Id}",
            correlationId);
        var local = await _store.RegisterAsync(operation, workItem, cancellationToken);
        if (local.Outcome == LocalOnboardingRegistrationOutcome.Conflict)
        {
            return new MerchantOnboardingResult(MerchantOnboardingOutcome.Conflict);
        }

        var accepted = local.Operation!;
        if (accepted.Status == OnboardingStatus.Completed)
        {
            return Completed(accepted);
        }

        try
        {
            var trial = await _trialStarter.StartTrialSubscriptionAsync(
                new StartTrialSubscriptionCommand(
                    accepted.Tenant.Id.Value,
                    accepted.Id,
                    workItem.SourceIdentity,
                    accepted.CorrelationId),
                cancellationToken);
            if (trial.Outcome is TrialSubscriptionStartOutcome.Accepted or TrialSubscriptionStartOutcome.AlreadyApplied)
            {
                await _store.MarkCompletedAsync(accepted.Id, cancellationToken);
                return Completed(accepted);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A timeout/abandoned remote attempt is deliberately pending; the durable work item retries it.
        }
        catch (Exception)
        {
            // Trial ownership remains SubscriptionBilling. Do not roll back the accepted Tenancy outcome.
        }

        return new MerchantOnboardingResult(MerchantOnboardingOutcome.PendingTrial, accepted.Id, accepted.Tenant.Id.Value);
    }

    public async Task<MerchantOnboardingResult?> GetStatusAsync(
        TrustedOnboardingContext context,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var operation = await _store.GetAsync(context, idempotencyKey, cancellationToken);
        return operation is null
            ? null
            : operation.Status == OnboardingStatus.Completed
                ? Completed(operation)
                : new MerchantOnboardingResult(MerchantOnboardingOutcome.PendingTrial, operation.Id, operation.Tenant.Id.Value);
    }

    private static MerchantOnboardingResult Completed(OnboardingOperation operation) =>
        new(MerchantOnboardingOutcome.Completed, operation.Id, operation.Tenant.Id.Value);

    private static OnboardingOperation BuildOperation(
        TrustedOnboardingContext context,
        string idempotencyKey,
        string fingerprint,
        BusinessProfile profile,
        string correlationId)
    {
        var source = $"{context.SubjectId.Value}\n{idempotencyKey}";
        var operationId = $"onb-{StableToken(source, "operation")}";
        var tenantId = new TenantId($"ten-{StableToken(source, "tenant")}");
        var membershipId = new MembershipId($"mem-{StableToken(source, "owner")}");
        return new OnboardingOperation(
            operationId,
            context.SubjectId,
            idempotencyKey,
            fingerprint,
            new Tenant(tenantId, TenantStatus.Active, profile, 1, $"store-{StableToken(source, "storefront")}"),
            new Membership(membershipId, tenantId, context.SubjectId, MerchantRole.Owner, MembershipStatus.Active, 1),
            OnboardingStatus.PendingTrial,
            correlationId);
    }

    private static void ValidateRequest(string idempotencyKey, BusinessProfile profile, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
        {
            throw new ArgumentException("A bounded idempotency key is required.", nameof(idempotencyKey));
        }
        if (string.IsNullOrWhiteSpace(profile.DisplayName) || string.IsNullOrWhiteSpace(profile.TimeZoneIana))
        {
            throw new ArgumentException("Business display name and IANA timezone are required.", nameof(profile));
        }
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("A correlation identifier is required.", nameof(correlationId));
        }
    }

    private static string Fingerprint(BusinessProfile profile) =>
        $"{profile.DisplayName.Trim()}\n{profile.TimeZoneIana.Trim()}";

    private static string StableToken(string source, string purpose) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}\n{source}"))).ToLowerInvariant()[..24];
}
