using CommerceOS.SubscriptionBilling.Application.Catalog;
using CommerceOS.SubscriptionBilling.Application.PlatformCharges;
using CommerceOS.SubscriptionBilling.Application.Trial;
using CommerceOS.SubscriptionBilling.Domain;

namespace CommerceOS.SubscriptionBilling.Application.PaidLifecycle;

public enum PaidLifecycleOperation
{
    Activation,
    Upgrade,
    Renewal,
    Reactivation
}

public enum PaidLifecycleOutcome
{
    Applied,
    AwaitingChargeEvidence,
    DefinitivelyNotSettled,
    AlreadyApplied,
    BlockedByUsage,
    Ended,
    NotDue,
    RevisionConflict
}

public enum DowngradeStatus
{
    Scheduled,
    BlockedByUsage
}

public sealed record PaidEntitlementSnapshot(
    string PlanId,
    string PlanVersionId,
    bool CoreCommerceCapabilities,
    int MaxActiveMemberships,
    int MaxWarehouses,
    bool ScheduledProductIngestion,
    int OrderVolumeWarningThreshold);

public sealed record PendingDowngrade(PaidEntitlementSnapshot Target, DowngradeStatus Status, string OperationId);

public sealed record PaidSubscription(
    string TenantId,
    string SubscriptionId,
    PaidEntitlementSnapshot Entitlements,
    DateTimeOffset BillingAnchor,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveUntil,
    SubscriptionCondition Condition,
    bool CancelRenewalRequested,
    PendingDowngrade? PendingDowngrade,
    long Revision);

public sealed record PaidSubscriptionTransition(
    string OperationId,
    PaidLifecycleOperation Operation,
    PaidSubscription Subscription);

public interface IPaidSubscriptionStore
{
    Task<PaidSubscription?> GetCurrentAsync(string tenantId, CancellationToken cancellationToken);
    Task<PaidLifecycleOutcome> ApplyPeriodAsync(PaidSubscriptionTransition transition, CancellationToken cancellationToken);
    Task<PaidLifecycleOutcome> ScheduleDowngradeAsync(string tenantId, long expectedRevision, PendingDowngrade downgrade, CancellationToken cancellationToken);
    Task<PaidLifecycleOutcome> MarkPastDueAsync(string tenantId, long expectedRevision, DateTimeOffset graceEndsAt, CancellationToken cancellationToken);
    Task<PaidLifecycleOutcome> MarkEndedAsync(string tenantId, long expectedRevision, CancellationToken cancellationToken);
}

/// <summary>
/// Each owner remains authoritative for its current constrained usage. SubscriptionBilling consumes only
/// this producer-owned assessment and never reads a foreign persistence table.
/// </summary>
public interface ISubscriptionUsageAssessor
{
    Task<bool> FitsTargetAsync(string trustedTenantId, PaidEntitlementSnapshot target, CancellationToken cancellationToken);
}

/// <summary>Safe composition default until each constrained owner exposes its approved usage assessment contract.</summary>
public sealed class FailClosedSubscriptionUsageAssessor : ISubscriptionUsageAssessor
{
    public Task<bool> FitsTargetAsync(string trustedTenantId, PaidEntitlementSnapshot target, CancellationToken cancellationToken) => Task.FromResult(false);
}

public sealed class ActivatePaidSubscriptionCommand
{
    public ActivatePaidSubscriptionCommand(string trustedTenantId, string planId, string planVersionId, string operationId, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(trustedTenantId) || string.IsNullOrWhiteSpace(planId) || string.IsNullOrWhiteSpace(planVersionId)
            || string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Paid subscription command values must not be empty.");
        }

        TrustedTenantId = trustedTenantId;
        PlanId = planId;
        PlanVersionId = planVersionId;
        OperationId = operationId;
        CorrelationId = correlationId;
    }

    public string TrustedTenantId { get; }
    public string PlanId { get; }
    public string PlanVersionId { get; }
    public string OperationId { get; }
    public string CorrelationId { get; }
}

public sealed class RequestDowngradeCommand
{
    public RequestDowngradeCommand(string trustedTenantId, string planId, string planVersionId, long expectedRevision, string operationId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedRevision, 1L);
        if (string.IsNullOrWhiteSpace(trustedTenantId) || string.IsNullOrWhiteSpace(planId) || string.IsNullOrWhiteSpace(planVersionId) || string.IsNullOrWhiteSpace(operationId))
        {
            throw new ArgumentException("Downgrade command values must not be empty.");
        }

        TrustedTenantId = trustedTenantId;
        PlanId = planId;
        PlanVersionId = planVersionId;
        ExpectedRevision = expectedRevision;
        OperationId = operationId;
    }

    public string TrustedTenantId { get; }
    public string PlanId { get; }
    public string PlanVersionId { get; }
    public long ExpectedRevision { get; }
    public string OperationId { get; }
}

public sealed record PaidLifecycleResult(PaidLifecycleOutcome Outcome, PaidSubscription? Subscription, PlatformCharge? Charge = null);

public sealed class PaidSubscriptionLifecycleService
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);
    private readonly ISubscriptionCatalogStore _catalog;
    private readonly IPaidSubscriptionStore _subscriptions;
    private readonly IPlatformChargeStore _charges;
    private readonly IPlatformBillingProvider _provider;
    private readonly ISubscriptionUsageAssessor _usage;
    private readonly TimeProvider _clock;

    public PaidSubscriptionLifecycleService(
        ISubscriptionCatalogStore catalog,
        IPaidSubscriptionStore subscriptions,
        IPlatformChargeStore charges,
        IPlatformBillingProvider provider,
        ISubscriptionUsageAssessor usage,
        TimeProvider? clock = null)
    {
        _catalog = catalog;
        _subscriptions = subscriptions;
        _charges = charges;
        _provider = provider;
        _usage = usage;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<PaidLifecycleResult> ActivateOrUpgradeAsync(ActivatePaidSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var target = await GetTargetAsync(command.PlanId, command.PlanVersionId, cancellationToken);
        var current = await _subscriptions.GetCurrentAsync(command.TrustedTenantId, cancellationToken);
        var operation = current is null ? PaidLifecycleOperation.Activation
            : current.Condition is SubscriptionCondition.Ended ? PaidLifecycleOperation.Reactivation
            : PaidLifecycleOperation.Upgrade;
        var charge = await AttemptChargeAsync(command.TrustedTenantId, current?.SubscriptionId ?? $"subscription:{command.TrustedTenantId}", target, command.OperationId, command.CorrelationId, cancellationToken);
        if (charge.Outcome is PlatformChargeOutcome.OutcomeUnknown or PlatformChargeOutcome.Pending)
        {
            return new(PaidLifecycleOutcome.AwaitingChargeEvidence, current, charge);
        }
        if (charge.Outcome is PlatformChargeOutcome.DefinitivelyNotSettled)
        {
            return new(PaidLifecycleOutcome.DefinitivelyNotSettled, current, charge);
        }

        var now = _clock.GetUtcNow();
        var subscription = NewPeriod(command.TrustedTenantId, current, target, now, cancelRenewal: false, pendingDowngrade: null);
        var applied = await _subscriptions.ApplyPeriodAsync(new PaidSubscriptionTransition(command.OperationId, operation, subscription), cancellationToken);
        return new(applied, await _subscriptions.GetCurrentAsync(command.TrustedTenantId, cancellationToken), charge);
    }

    public async Task<PaidLifecycleResult> RequestDowngradeAsync(RequestDowngradeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var current = await _subscriptions.GetCurrentAsync(command.TrustedTenantId, cancellationToken);
        if (current is null || current.Condition is SubscriptionCondition.Ended)
        {
            return new(PaidLifecycleOutcome.RevisionConflict, current);
        }
        var targetPlan = await GetPlanAsync(command.PlanId, command.PlanVersionId, cancellationToken);
        var currentPlan = await GetPlanAsync(current.Entitlements.PlanId, current.Entitlements.PlanVersionId, cancellationToken, false);
        if (targetPlan.MonthlyPrice.Amount >= currentPlan.MonthlyPrice.Amount)
        {
            throw new InvalidOperationException("A scheduled downgrade must select a lower-priced PlanVersion.");
        }
        var target = await GetTargetAsync(command.PlanId, command.PlanVersionId, cancellationToken);
        var scheduled = await _subscriptions.ScheduleDowngradeAsync(command.TrustedTenantId, command.ExpectedRevision, new PendingDowngrade(target, DowngradeStatus.Scheduled, command.OperationId), cancellationToken);
        return new(scheduled, await _subscriptions.GetCurrentAsync(command.TrustedTenantId, cancellationToken));
    }

    public async Task<PaidLifecycleResult> CancelRenewalAsync(string trustedTenantId, long expectedRevision, CancellationToken cancellationToken)
    {
        var current = await _subscriptions.GetCurrentAsync(trustedTenantId, cancellationToken);
        if (current is null)
        {
            return new(PaidLifecycleOutcome.RevisionConflict, current);
        }
        if (current.CancelRenewalRequested && current.Revision == expectedRevision + 1)
        {
            return new(PaidLifecycleOutcome.AlreadyApplied, current);
        }
        if (current.Revision != expectedRevision)
        {
            return new(PaidLifecycleOutcome.RevisionConflict, current);
        }
        var operation = await _subscriptions.ApplyPeriodAsync(new PaidSubscriptionTransition(
            $"cancel-renewal:{trustedTenantId}:{expectedRevision}",
            PaidLifecycleOperation.Renewal,
            current with { CancelRenewalRequested = true, Revision = current.Revision + 1 }), cancellationToken);
        return new(operation, await _subscriptions.GetCurrentAsync(trustedTenantId, cancellationToken));
    }

    public async Task<PaidLifecycleResult> ProcessDueAsync(string trustedTenantId, string operationId, string correlationId, CancellationToken cancellationToken)
    {
        var current = await _subscriptions.GetCurrentAsync(trustedTenantId, cancellationToken);
        if (current is null)
        {
            return new(PaidLifecycleOutcome.NotDue, null);
        }
        var now = _clock.GetUtcNow();
        if (now < current.EffectiveUntil)
        {
            return new(PaidLifecycleOutcome.NotDue, current);
        }
        if (current.Condition is SubscriptionCondition.PastDue || current.CancelRenewalRequested)
        {
            var ended = await _subscriptions.MarkEndedAsync(trustedTenantId, current.Revision, cancellationToken);
            return new(ended is PaidLifecycleOutcome.Applied ? PaidLifecycleOutcome.Ended : ended, await _subscriptions.GetCurrentAsync(trustedTenantId, cancellationToken));
        }

        var target = current.PendingDowngrade?.Target ?? current.Entitlements;
        if (current.PendingDowngrade is not null && !await _usage.FitsTargetAsync(trustedTenantId, target, cancellationToken))
        {
            var blocked = current with { PendingDowngrade = current.PendingDowngrade with { Status = DowngradeStatus.BlockedByUsage }, Revision = current.Revision + 1 };
            var status = await _subscriptions.ApplyPeriodAsync(new PaidSubscriptionTransition($"downgrade-blocked:{current.PendingDowngrade.OperationId}:{current.Revision}", PaidLifecycleOperation.Renewal, blocked), cancellationToken);
            return new(status is PaidLifecycleOutcome.Applied ? PaidLifecycleOutcome.BlockedByUsage : status, await _subscriptions.GetCurrentAsync(trustedTenantId, cancellationToken));
        }

        var charge = await AttemptChargeAsync(trustedTenantId, current.SubscriptionId, target, operationId, correlationId, cancellationToken);
        if (charge.Outcome is PlatformChargeOutcome.OutcomeUnknown or PlatformChargeOutcome.Pending)
        {
            return new(PaidLifecycleOutcome.AwaitingChargeEvidence, current, charge);
        }
        if (charge.Outcome is PlatformChargeOutcome.DefinitivelyNotSettled)
        {
            var pastDue = await _subscriptions.MarkPastDueAsync(trustedTenantId, current.Revision, now.Add(GracePeriod), cancellationToken);
            return new(pastDue, await _subscriptions.GetCurrentAsync(trustedTenantId, cancellationToken), charge);
        }

        var renewed = RenewPeriod(current, target);
        var applied = await _subscriptions.ApplyPeriodAsync(new PaidSubscriptionTransition(operationId, PaidLifecycleOperation.Renewal, renewed), cancellationToken);
        return new(applied, await _subscriptions.GetCurrentAsync(trustedTenantId, cancellationToken), charge);
    }

    private async Task<PaidEntitlementSnapshot> GetTargetAsync(string planId, string planVersionId, CancellationToken cancellationToken)
    {
        var plan = await GetPlanAsync(planId, planVersionId, cancellationToken);
        return new(plan.PlanId.Value, plan.Id.Value, plan.Entitlements.CoreCommerceCapabilities, plan.Entitlements.MaxActiveMemberships,
            plan.Entitlements.MaxWarehouses, plan.Entitlements.ScheduledProductIngestion, plan.Entitlements.OrderVolumeWarningThreshold);
    }

    private async Task<PlanVersion> GetPlanAsync(string planId, string planVersionId, CancellationToken cancellationToken, bool requireAvailableForNewPurchase = true)
    {
        var plan = (await _catalog.GetAsync(CatalogRecordId.For(new PlanId(planId), new PlanVersionId(planVersionId)), cancellationToken))?.PlanVersion;
        return plan is null || (requireAvailableForNewPurchase && !plan.IsAvailableForNewPurchase)
            ? throw new InvalidOperationException("The selected PlanVersion is not available for purchase.")
            : plan;
    }

    private async Task<PlatformCharge> AttemptChargeAsync(string tenantId, string subscriptionReference, PaidEntitlementSnapshot target, string operationId, string correlationId, CancellationToken cancellationToken)
    {
        var existing = await _charges.GetByLogicalIdentityAsync(tenantId, operationId, cancellationToken);
        if (existing is null)
        {
            var charge = new PlatformCharge(
                new PlatformChargeId($"charge-{Guid.NewGuid():N}"), tenantId, subscriptionReference, target.PlanVersionId,
                operationId, (await GetPriceAsync(target, cancellationToken)).MonthlyPrice, $"saas-charge:{tenantId}:{operationId}",
                PlatformChargeOutcome.Pending, 1, _clock.GetUtcNow());
            if (await _charges.CreateIfAbsentAsync(charge, cancellationToken) is PlatformChargeCreateResult.Created)
            {
                try
                {
                    var evidence = await _provider.SubmitAsync(new PlatformBillingRequest(charge.Id, charge.ProviderOperationId, operationId, charge.Amount.Amount, correlationId), cancellationToken);
                    return evidence is null ? await MarkUnknownAsync(charge, cancellationToken) : await ApplyEvidenceAsync(charge, evidence, cancellationToken);
                }
                catch
                {
                    return await MarkUnknownAsync(charge, cancellationToken);
                }
            }
            existing = await _charges.GetByLogicalIdentityAsync(tenantId, operationId, cancellationToken);
        }
        if (existing!.Outcome is PlatformChargeOutcome.OutcomeUnknown or PlatformChargeOutcome.Pending)
        {
            var evidence = await _provider.FindEvidenceAsync(existing.ProviderOperationId, cancellationToken);
            return evidence is null ? existing : await ApplyEvidenceAsync(existing, evidence, cancellationToken);
        }
        return existing;
    }

    private async Task<PlanVersion> GetPriceAsync(PaidEntitlementSnapshot target, CancellationToken cancellationToken) =>
        await GetPlanAsync(target.PlanId, target.PlanVersionId, cancellationToken, false);

    private async Task<PlatformCharge> ApplyEvidenceAsync(PlatformCharge current, PlatformChargeEvidence evidence, CancellationToken cancellationToken)
    {
        var target = evidence.Kind switch
        {
            PlatformChargeEvidenceKind.VerifiedSuccess => PlatformChargeOutcome.Succeeded,
            PlatformChargeEvidenceKind.DefinitiveNoCommit => PlatformChargeOutcome.DefinitivelyNotSettled,
            _ => PlatformChargeOutcome.OutcomeUnknown
        };
        var updated = current.Outcome is PlatformChargeOutcome.Succeeded or PlatformChargeOutcome.DefinitivelyNotSettled
            ? current
            : current with { Outcome = target, Revision = current.Revision + 1 };
        var result = await _charges.ApplyEvidenceAsync(current, evidence, updated, cancellationToken);
        return result is PlatformChargeEvidenceApplyResult.Applied ? updated
            : await _charges.GetByLogicalIdentityAsync(current.TenantId, current.LogicalChargeIdentity, cancellationToken)
                ?? throw new InvalidOperationException("PlatformCharge disappeared while applying evidence.");
    }

    private async Task<PlatformCharge> MarkUnknownAsync(PlatformCharge charge, CancellationToken cancellationToken)
    {
        if (await _charges.MarkOutcomeUnknownAsync(charge, cancellationToken)) return charge with { Outcome = PlatformChargeOutcome.OutcomeUnknown, Revision = charge.Revision + 1 };
        return await _charges.GetByLogicalIdentityAsync(charge.TenantId, charge.LogicalChargeIdentity, cancellationToken)
            ?? throw new InvalidOperationException("PlatformCharge disappeared while recording unknown outcome.");
    }

    private static PaidSubscription NewPeriod(string tenantId, PaidSubscription? current, PaidEntitlementSnapshot target, DateTimeOffset now, bool cancelRenewal, PendingDowngrade? pendingDowngrade)
    {
        var anchor = now;
        return new(tenantId, current?.SubscriptionId ?? $"subscription:{tenantId}", target, anchor, now, AddMonth(anchor), SubscriptionCondition.Active,
            cancelRenewal, pendingDowngrade, (current?.Revision ?? 0) + 1);
    }

    private static PaidSubscription RenewPeriod(PaidSubscription current, PaidEntitlementSnapshot target)
    {
        var elapsedMonths = (current.EffectiveUntil.Year - current.BillingAnchor.Year) * 12 + current.EffectiveUntil.Month - current.BillingAnchor.Month;
        var nextUntil = AddMonthsFromAnchor(current.BillingAnchor, elapsedMonths + 1);
        return current with
        {
            Entitlements = target,
            EffectiveFrom = current.EffectiveUntil,
            EffectiveUntil = nextUntil,
            Condition = SubscriptionCondition.Active,
            CancelRenewalRequested = false,
            PendingDowngrade = null,
            Revision = current.Revision + 1
        };
    }

    public static DateTimeOffset AddMonth(DateTimeOffset anchor)
        => AddMonthsFromAnchor(anchor, 1);

    private static DateTimeOffset AddMonthsFromAnchor(DateTimeOffset anchor, int months)
    {
        var zeroBasedMonth = anchor.Month - 1 + months;
        var targetYear = anchor.Year + zeroBasedMonth / 12;
        var targetMonth = zeroBasedMonth % 12 + 1;
        var targetDay = Math.Min(anchor.Day, DateTime.DaysInMonth(targetYear, targetMonth));
        return new DateTimeOffset(targetYear, targetMonth, targetDay, anchor.Hour, anchor.Minute, anchor.Second, anchor.Offset).AddTicks(anchor.Ticks % TimeSpan.TicksPerSecond);
    }
}
