namespace CommerceOS.Audit.Domain;

public enum AuditAudience { Tenant, PlatformSecurity }
public sealed record AuditEvidence(string Id, string SourceIdentity, string? TenantId, AuditAudience Audience, string ActorId, string Action, string Outcome, string SafeReason, string CorrelationId, DateTimeOffset OccurredAt);
