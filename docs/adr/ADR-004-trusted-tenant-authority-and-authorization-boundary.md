# ADR-004 — Trusted Tenant Authority and Authorization Boundary

Status: Accepted
Date: 2026-08-09
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS must deny cross-tenant access even when a caller knows another Tenant or aggregate identifier. TASK-0087 separates authentication identity from active Membership authority and requires Membership disablement/role changes to affect later authorization even while an older token remains cryptographically valid.

The high-level architecture previously showed subject, Membership, Tenant, and permissions as one request-context flow without deciding which evidence is authoritative. Cognito tokens can prove an external identity, but placing Tenant/role authority in long-lived claims or accepting a route/body TenantId would become stale and create confused-deputy/cross-tenant risk.

The product decision about single-versus-multiple Tenant membership and the intentional Tenant-selection experience (`PD-001`) is not approved. The architecture must support a future approved selection while preventing the selector from becoming authority.

## Decision

### Authentication

- Use API Gateway HTTP API's Cognito JWT authorizer on protected merchant routes.
- Validate issuer, audience/client, signature, lifetime, and required access-token scopes at the edge.
- Treat the stable Cognito subject identifier as external identity evidence only.
- Do not treat email, Tenant, Membership, role, or capability token claims as current business authority.

### Authority resolution

- Treat any Tenant identifier/reference selected through route, header, query, body, or future UI as `RequestedTenantSelection`, an untrusted target.
- For every protected request, call the Tenancy/Merchant Access `ResolveTenantAuthority` application contract.
- Resolve one coherent current snapshot of the selected Tenant status and `(TenantId, SubjectId)` Membership authority through the authoritative base-table path defined by ADR-005.
- Require Active Tenant status for ordinary merchant work and an Active Membership bound to that Tenant and subject.
- Derive current effective capabilities only from the approved `PD-003` policy and current Membership state.
- Return an immutable `TrustedTenantContext` containing accepted Tenant, Subject, Membership, capabilities, and correlation identity.
- Do not cache the authorization result across requests initially and do not use an eventually consistent GSI as the sole resolution path.
- Fail closed with a safe dependency-unavailable result when authority storage cannot be read. Never fall back to request data or stale claims.

`PD-001` still decides how a user intentionally chooses/discovers a Tenant and the final selector contract. Whatever is approved, resolution verifies Membership before producing authority.

### Enforcement and non-disclosure

- Protected application use cases accept trusted context, check the named capability, and derive repository Tenant scope only from it.
- Target aggregate IDs remain untrusted and cannot change the context Tenant.
- Tenant-owned repository contracts have no unscoped overload and include TenantId in the base key/query.
- A missing/cross-tenant aggregate returns the same non-disclosing not-visible result.
- Authentication failure, authenticated-no/inactive-Membership, forbidden capability, target not visible, and authority dependency unavailable remain internally distinguishable without exposing another Tenant's data.

### Separate trust paths

- Pre-Membership onboarding uses a distinct `TrustedOnboardingContext` produced by the admission mechanism approved under `PD-034`; it contains no Tenant authority.
- Public storefront access uses a distinct `PublicTenantContext` only after storefront Tenant addressing is defined; it cannot invoke protected commands.
- Queue/event workers use a validated `MessageExecutionContext`, not a merchant actor context.
- Platform administrators use a separately modeled authenticated/authorized/audited path; there is no `bypassTenant` flag and no Owner Membership in every Tenant.

## Alternatives considered

### Option A — Put Tenant and roles/capabilities in Cognito custom claims

- Benefits: no per-request Membership read; simple handler context.
- Costs/risks: stale disablement/role/tenant status, token refresh/revocation complexity, multi-Tenant ambiguity, and identity provider becoming accidental Membership authority.

### Option B — Trust client-selected TenantId after token validation

- Benefits: minimal implementation and read cost.
- Costs/risks: direct cross-tenant vulnerability; authentication proves person identity, not authority for an arbitrary Tenant.

### Option C — Cache a custom authorizer/Membership decision

- Benefits: lower repeated reads and centralized route denial.
- Costs/risks: a cache TTL becomes an unapproved authorization-revocation SLA; disabled Membership may remain usable; API Gateway/custom-authorizer behavior becomes more complex.

### Option D — Resolve current authority on each request and scope every repository

- Benefits: clear trust boundary, next-resolution disablement, supports either future membership cardinality, defense in depth, non-disclosing persistence access.
- Costs/risks: additional DynamoDB reads/latency per protected request; shared runtime initially needs Tenancy access; unavailability fails protected requests closed.

Chosen: Option D, with API Gateway JWT validation from the authentication portion of Option A but no business authority in claims.

## Consequences

### Positive

- A token and target identifier cannot combine into cross-tenant authority.
- Membership disablement, role change, and Tenant suspension affect the next coherent resolution.
- The product owner can later choose single- or multi-Tenant membership without replacing the trust rule.
- All modules receive one stable transport-neutral trusted context and cannot independently reinterpret JWT claims.
- Public, onboarding, background, and platform-admin execution cannot silently acquire merchant permissions.

### Negative / trade-offs

- Protected requests pay the latency/capacity cost of current authority reads.
- Authority-store incidents deny protected merchant work even when tokens remain valid; this is the secure failure mode.
- The exact Tenant-selection API cannot be finalized until `PD-001`.
- The capability set cannot be populated until `PD-003`.
- Cross-request caching requires a later ADR or explicit security decision with a revocation/staleness SLA.

## Security and tenant impact

- Tenant isolation: authorization and tenant-key persistence are both mandatory; neither substitutes for the other.
- Authentication/authorization: API Gateway/Cognito authenticates; Merchant Access authorizes current Tenant membership/capabilities; the owning application enforces operation capability.
- Non-disclosure: target mismatch uses not-visible behavior; denial/log/audit output never confirms another Tenant's entity.
- Sensitive data/secrets: tokens and full claims are not logged or persisted in request context; only minimum stable subject/issuer metadata is used. Invitation and onboarding proof remain separately protected.
- Platform administration: explicitly excluded from merchant context until its own security/audit contract exists.

## Reliability and operability impact

- Failure modes: invalid token, no Membership, inactive Membership, suspended Tenant, missing capability, target not visible, and dependency unavailable are separate internal outcomes with safe external mapping.
- Retry/recovery: authority reads may use bounded SDK retries for transient AWS errors; there is no fallback to cached/request authority. Callers may retry a 503 safely when the application command has not begun.
- Concurrency: one transactionally consistent authority snapshot gates the request. Commands still use aggregate revision/transaction conditions so authorization does not become a stale-write guard.
- Observability: safe module/outcome/correlation fields identify denial classes; no token, email, raw selector, or cross-Tenant target detail is logged. High-cardinality Tenant/Subject values are not custom metric dimensions.

## Cost impact

- Learning profile: TASK-0088 has zero cost. Later protected requests add bounded DynamoDB transactional/strong reads; at the repository's tiny learning volume this remains within the existing DynamoDB cost envelope but must be measured.
- Beta profile: authority reads may become a visible fraction of DynamoDB request units. Optimize item size/access before considering a cache that weakens freshness.
- Larger-scale implication: a dedicated authorization service/cache may become justified only with measured latency/cost plus an explicit revocation SLA and failure/security ADR.
- Cost-model update required? No now. The existing DynamoDB/API/Lambda model covers requests; implementation telemetry should refine it.

## Reversibility / migration

- Adding a safe cache later requires versioned authority entries, invalidation/revocation behavior, maximum staleness, outage behavior, and cross-tenant tests.
- Changing Tenant-selection UX/transport after `PD-001` affects delivery contracts but not the core verify-selected-Membership rule.
- Moving authority resolution to a separate Lambda/service requires an authenticated internal contract, timeout/availability policy, IAM isolation, and latency/cost evidence.
- Changing identity provider affects token validation/adaptation but does not transfer Membership ownership.

## Validation

- Real Cognito/API Gateway tests verify valid/expired/invalid tokens and access-token scope behavior.
- Tenant A and Tenant B fixtures attempt route/body/query/header/cursor overrides and known cross-Tenant aggregate IDs.
- A still-valid token is denied on the first authority resolution after Membership disablement or Tenant suspension.
- Role/capability changes take effect on the next resolution after `PD-003` is approved.
- Authority-store failure produces safe 503/fail-closed behavior and never fallback authority.
- Architecture tests ensure protected module applications receive trusted context and repositories require Tenant scope.
- Logs/problems contain correlation and safe codes but no token/claim/sensitive/cross-Tenant details.

## References

- relevant task: [TASK-0088](../../tasks/completed/TASK-0088-technical-architecture-baseline-reconciliation.md)
- domain baseline: [Tenant Management & Merchant Access](../domains/tenant-identity.md)
- architecture docs: [First-frontier contracts](../architecture/first-frontier-contracts.md), [Persistence access patterns](../architecture/persistence-access-patterns.md)
- AWS: [API Gateway HTTP API JWT authorizers](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-jwt-authorizer.html)
