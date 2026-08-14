using CommerceOS.Audit.Domain;

namespace CommerceOS.Audit.Application;

/// <summary>Versioned, source-owned audit intent delivered at-least-once. It deliberately excludes foreign persistence pointers and secrets.</summary>
public sealed record AuditDeliveryFact(string EventId, int EventVersion, string SourceIdentity, string? TenantId, AuditAudience Audience, string ActorId, string Action, string Outcome, string SafeReason, string CorrelationId, DateTimeOffset OccurredAt);
public enum AuditDeliveryOutcome { Appended, AlreadyRecorded, Invalid }
public sealed class AuditDeliveryConsumer(AuditService audit)
{
    public async Task<AuditDeliveryOutcome> ConsumeAsync(AuditDeliveryFact fact, CancellationToken cancellationToken)
    {
        if (fact.EventVersion != 1 || string.IsNullOrWhiteSpace(fact.EventId) || string.IsNullOrWhiteSpace(fact.SourceIdentity) || string.IsNullOrWhiteSpace(fact.ActorId) || string.IsNullOrWhiteSpace(fact.Action) || string.IsNullOrWhiteSpace(fact.Outcome) || (fact.Audience is AuditAudience.Tenant && string.IsNullOrWhiteSpace(fact.TenantId))) return AuditDeliveryOutcome.Invalid;
        return await audit.AppendAsync(new AuditEvidence(fact.EventId, fact.SourceIdentity, fact.TenantId, fact.Audience, fact.ActorId, fact.Action, fact.Outcome, fact.SafeReason, fact.CorrelationId, fact.OccurredAt), cancellationToken)
            ? AuditDeliveryOutcome.Appended : AuditDeliveryOutcome.AlreadyRecorded;
    }
}
