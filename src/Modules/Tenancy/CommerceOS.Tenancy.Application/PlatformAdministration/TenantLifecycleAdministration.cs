using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.Application.PlatformAdministration;

public enum TenantLifecycleAction
{
    Suspend,
    Reactivate
}

public enum TenantLifecycleOutcome
{
    Applied,
    AlreadyApplied,
    RevisionConflict,
    InvalidTransition,
    NotFound
}

public sealed class TenantLifecycleCommand
{
    public TenantLifecycleCommand(
        TenantId tenantId,
        TenantLifecycleAction action,
        long expectedRevision,
        string operationId,
        string reason)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedRevision, 1L);

        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw new ArgumentException("OperationId must not be empty.", nameof(operationId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason must not be empty.", nameof(reason));
        }

        TenantId = tenantId;
        Action = action;
        ExpectedRevision = expectedRevision;
        OperationId = operationId;
        Reason = reason;
    }

    public TenantId TenantId { get; }
    public TenantLifecycleAction Action { get; }
    public long ExpectedRevision { get; }
    public string OperationId { get; }
    public string Reason { get; }
}

public sealed record TenantLifecycleResult(TenantLifecycleOutcome Outcome, Tenant? Tenant);

public sealed record TenantLifecycleAuditIntent(
    string OperationId,
    TenantId TenantId,
    TenantLifecycleAction Action,
    SubjectId PlatformSubjectId,
    string CorrelationId,
    string Reason,
    TenantLifecycleOutcome Outcome,
    DateTimeOffset OccurredAt);

/// <summary>Module-owned platform-only persistence surface; it is not a merchant repository bypass.</summary>
public interface IPlatformTenantAdministrationStore
{
    Task<TenantLifecycleResult> TransitionAsync(
        TenantLifecycleCommand command,
        TenantLifecycleAuditIntent auditIntent,
        CancellationToken cancellationToken);

    Task<Tenant?> GetForPlatformSupportAsync(TenantId tenantId, CancellationToken cancellationToken);
}

public sealed class TenantLifecycleAdministrationService
{
    private readonly IPlatformTenantAdministrationStore _store;
    private readonly TimeProvider _timeProvider;

    public TenantLifecycleAdministrationService(IPlatformTenantAdministrationStore store, TimeProvider? timeProvider = null)
    {
        _store = store;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<TenantLifecycleResult> ExecuteAsync(
        TrustedPlatformAdminContext context,
        TenantLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        return _store.TransitionAsync(command, new TenantLifecycleAuditIntent(
            command.OperationId,
            command.TenantId,
            command.Action,
            context.PlatformSubjectId,
            context.CorrelationId,
            command.Reason,
            TenantLifecycleOutcome.Applied,
            _timeProvider.GetUtcNow()), cancellationToken);
    }
}

public sealed class TenantPlatformSupportQueryService
{
    private readonly IPlatformTenantAdministrationStore _store;

    public TenantPlatformSupportQueryService(IPlatformTenantAdministrationStore store) => _store = store;

    public Task<Tenant?> GetTenantAsync(
        TrustedPlatformSupportReadContext context,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _store.GetForPlatformSupportAsync(tenantId, cancellationToken);
    }
}
