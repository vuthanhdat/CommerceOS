using CommerceOS.SubscriptionBilling.Domain;

namespace CommerceOS.SubscriptionBilling.Application.PlatformCharges;

public sealed class RecordPlatformChargeAttemptCommand
{
    public RecordPlatformChargeAttemptCommand(
        string tenantId,
        string subscriptionReference,
        string termsReference,
        string logicalChargeIdentity,
        VndMoney amount,
        string correlationId)
    {
        if (string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(subscriptionReference)
            || string.IsNullOrWhiteSpace(termsReference)
            || string.IsNullOrWhiteSpace(logicalChargeIdentity)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Platform charge references and correlation must not be empty.");
        }

        TenantId = tenantId;
        SubscriptionReference = subscriptionReference;
        TermsReference = termsReference;
        LogicalChargeIdentity = logicalChargeIdentity;
        Amount = amount;
        CorrelationId = correlationId;
    }

    public string TenantId { get; }
    public string SubscriptionReference { get; }
    public string TermsReference { get; }
    public string LogicalChargeIdentity { get; }
    public VndMoney Amount { get; }
    public string CorrelationId { get; }
}

public sealed record PlatformBillingRequest(
    PlatformChargeId ChargeId,
    string ProviderOperationId,
    string IdempotencyKey,
    long AmountVnd,
    string CorrelationId);

/// <summary>
/// This port represents only CommerceOS SaaS charges. It is deliberately not the merchant Payments provider port.
/// </summary>
public interface IPlatformBillingProvider
{
    Task<PlatformChargeEvidence?> SubmitAsync(PlatformBillingRequest request, CancellationToken cancellationToken);

    Task<PlatformChargeEvidence?> FindEvidenceAsync(string providerOperationId, CancellationToken cancellationToken);
}

public enum PlatformChargeCreateResult
{
    Created,
    AlreadyExists
}

public enum PlatformChargeEvidenceApplyResult
{
    Applied,
    Duplicate,
    RevisionConflict
}

public interface IPlatformChargeStore
{
    Task<PlatformCharge?> GetByLogicalIdentityAsync(string tenantId, string logicalChargeIdentity, CancellationToken cancellationToken);

    Task<PlatformChargeCreateResult> CreateIfAbsentAsync(PlatformCharge charge, CancellationToken cancellationToken);

    Task<PlatformChargeEvidenceApplyResult> ApplyEvidenceAsync(
        PlatformCharge current,
        PlatformChargeEvidence evidence,
        PlatformCharge updated,
        CancellationToken cancellationToken);

    Task<bool> MarkOutcomeUnknownAsync(PlatformCharge current, CancellationToken cancellationToken);
}

public sealed record PlatformChargeAttemptResult(PlatformCharge Charge, bool Created);

public sealed class PlatformChargeService
{
    private readonly IPlatformChargeStore _store;
    private readonly IPlatformBillingProvider _provider;
    private readonly TimeProvider _clock;

    public PlatformChargeService(IPlatformChargeStore store, IPlatformBillingProvider provider, TimeProvider? clock = null)
    {
        _store = store;
        _provider = provider;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<PlatformChargeAttemptResult> RecordAttemptAsync(
        RecordPlatformChargeAttemptCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var existing = await _store.GetByLogicalIdentityAsync(command.TenantId, command.LogicalChargeIdentity, cancellationToken);
        if (existing is not null)
        {
            EnsureEquivalent(existing, command);
            return new PlatformChargeAttemptResult(existing, false);
        }

        var now = _clock.GetUtcNow();
        var charge = new PlatformCharge(
            new PlatformChargeId($"charge-{Guid.NewGuid():N}"),
            command.TenantId,
            command.SubscriptionReference,
            command.TermsReference,
            command.LogicalChargeIdentity,
            command.Amount,
            $"saas-charge:{command.TenantId}:{command.LogicalChargeIdentity}",
            PlatformChargeOutcome.Pending,
            1,
            now);
        if (await _store.CreateIfAbsentAsync(charge, cancellationToken) is PlatformChargeCreateResult.AlreadyExists)
        {
            existing = await _store.GetByLogicalIdentityAsync(command.TenantId, command.LogicalChargeIdentity, cancellationToken)
                ?? throw new InvalidOperationException("Platform charge creation conflicted without a persisted charge.");
            EnsureEquivalent(existing, command);
            return new PlatformChargeAttemptResult(existing, false);
        }

        try
        {
            var evidence = await _provider.SubmitAsync(new PlatformBillingRequest(
                charge.Id,
                charge.ProviderOperationId,
                charge.LogicalChargeIdentity,
                charge.Amount.Amount,
                command.CorrelationId), cancellationToken);
            var final = evidence is null
                ? await MarkUnknownAsync(charge, cancellationToken)
                : await RecordEvidenceAsync(charge, evidence, cancellationToken);
            return new PlatformChargeAttemptResult(final, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new PlatformChargeAttemptResult(await MarkUnknownAsync(charge, cancellationToken), true);
        }
    }

    public async Task<PlatformCharge> ReconcileAsync(
        string tenantId,
        string logicalChargeIdentity,
        CancellationToken cancellationToken)
    {
        var charge = await _store.GetByLogicalIdentityAsync(tenantId, logicalChargeIdentity, cancellationToken)
            ?? throw new InvalidOperationException("Platform charge was not found.");
        if (charge.Outcome is PlatformChargeOutcome.Succeeded or PlatformChargeOutcome.DefinitivelyNotSettled)
        {
            return charge;
        }

        var evidence = await _provider.FindEvidenceAsync(charge.ProviderOperationId, cancellationToken);
        return evidence is null
            ? await MarkUnknownAsync(charge, cancellationToken)
            : await RecordEvidenceAsync(charge, evidence, cancellationToken);
    }

    public async Task<PlatformCharge> RecordProviderEvidenceAsync(
        string tenantId,
        string logicalChargeIdentity,
        PlatformChargeEvidence evidence,
        CancellationToken cancellationToken)
    {
        var charge = await _store.GetByLogicalIdentityAsync(tenantId, logicalChargeIdentity, cancellationToken)
            ?? throw new InvalidOperationException("Platform charge was not found.");
        return await RecordEvidenceAsync(charge, evidence, cancellationToken);
    }

    private async Task<PlatformCharge> RecordEvidenceAsync(
        PlatformCharge charge,
        PlatformChargeEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (evidence.ChargeId != charge.Id || evidence.ProviderOperationId != charge.ProviderOperationId)
        {
            throw new ArgumentException("Provider evidence does not belong to this PlatformCharge.", nameof(evidence));
        }

        var targetOutcome = ResolveOutcome(charge.Outcome, evidence.Kind);
        var updated = targetOutcome == charge.Outcome
            ? charge
            : charge with { Outcome = targetOutcome, Revision = charge.Revision + 1 };
        var result = await _store.ApplyEvidenceAsync(charge, evidence, updated, cancellationToken);
        if (result is PlatformChargeEvidenceApplyResult.Applied)
        {
            return updated;
        }

        var current = await _store.GetByLogicalIdentityAsync(charge.TenantId, charge.LogicalChargeIdentity, cancellationToken)
            ?? throw new InvalidOperationException("Platform charge disappeared during evidence processing.");
        return current;
    }

    private async Task<PlatformCharge> MarkUnknownAsync(PlatformCharge charge, CancellationToken cancellationToken)
    {
        if (charge.Outcome is PlatformChargeOutcome.Succeeded or PlatformChargeOutcome.DefinitivelyNotSettled or PlatformChargeOutcome.OutcomeUnknown)
        {
            return charge;
        }

        if (await _store.MarkOutcomeUnknownAsync(charge, cancellationToken))
        {
            return charge with { Outcome = PlatformChargeOutcome.OutcomeUnknown, Revision = charge.Revision + 1 };
        }

        return await _store.GetByLogicalIdentityAsync(charge.TenantId, charge.LogicalChargeIdentity, cancellationToken)
            ?? throw new InvalidOperationException("Platform charge disappeared while recording OutcomeUnknown.");
    }

    private static PlatformChargeOutcome ResolveOutcome(PlatformChargeOutcome current, PlatformChargeEvidenceKind evidence) =>
        current is PlatformChargeOutcome.Succeeded or PlatformChargeOutcome.DefinitivelyNotSettled
            ? current
            : evidence switch
            {
                PlatformChargeEvidenceKind.VerifiedSuccess => PlatformChargeOutcome.Succeeded,
                PlatformChargeEvidenceKind.DefinitiveNoCommit => PlatformChargeOutcome.DefinitivelyNotSettled,
                _ => PlatformChargeOutcome.OutcomeUnknown
            };

    private static void EnsureEquivalent(PlatformCharge existing, RecordPlatformChargeAttemptCommand command)
    {
        if (existing.SubscriptionReference != command.SubscriptionReference
            || existing.TermsReference != command.TermsReference
            || existing.Amount != command.Amount)
        {
            throw new InvalidOperationException("Logical PlatformCharge identity was reused with incompatible commercial content.");
        }
    }
}
