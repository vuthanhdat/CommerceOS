# Audit access patterns

Append uses immutable source-identity deduplication. Tenant query requires Owner/Admin trusted read context and bounded time/limit. Platform-security evidence is a separate audience and is never returned by Tenant queries.
