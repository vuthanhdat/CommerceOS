using CommerceOS.Audit.Domain;
namespace CommerceOS.Audit.Application;

public sealed record TrustedAuditTenantReadContext(string TenantId, string Role, string CorrelationId);
public sealed record TrustedPlatformAuditReadContext(string OperatorId, bool HasSecurityAuditAccess, string CorrelationId);
public interface IAuditStore { Task<bool> AppendAsync(AuditEvidence evidence, CancellationToken ct); Task<IReadOnlyList<AuditEvidence>> ListTenantAsync(string tenantId, DateTimeOffset from, int limit, CancellationToken ct); Task<IReadOnlyList<AuditEvidence>> ListPlatformSecurityAsync(DateTimeOffset from, int limit, CancellationToken ct); }
public sealed class AuditService(IAuditStore store)
{ public Task<bool> AppendAsync(AuditEvidence evidence, CancellationToken ct) => store.AppendAsync(evidence, ct); public Task<IReadOnlyList<AuditEvidence>> ListTenantAsync(TrustedAuditTenantReadContext context, DateTimeOffset from, int limit, CancellationToken ct) => context.Role is "Owner" or "Admin" ? store.ListTenantAsync(context.TenantId, from, Math.Clamp(limit, 1, 100), ct) : Task.FromResult<IReadOnlyList<AuditEvidence>>([]); public Task<IReadOnlyList<AuditEvidence>> ListPlatformSecurityAsync(TrustedPlatformAuditReadContext context, DateTimeOffset from, int limit, CancellationToken ct) => context.HasSecurityAuditAccess ? store.ListPlatformSecurityAsync(from, Math.Clamp(limit, 1, 100), ct) : Task.FromResult<IReadOnlyList<AuditEvidence>>([]); }
