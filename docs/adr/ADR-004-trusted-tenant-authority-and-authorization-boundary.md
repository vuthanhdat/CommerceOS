# ADR-004 — Trusted Tenant Authority and Authorization Boundary

Status: Accepted
Date: 2026-08-09
Last reconciled: 2026-08-10
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS must deny cross-Tenant access even when a caller knows another Tenant or aggregate identifier. Cognito proves external identity, but identity alone does not prove current authority for any Tenant.

The original ADR intentionally left Tenant membership cardinality/selection (`PD-001`) and role cardinality/mapping (`PD-003`) open. Those product decisions are now resolved:

- one authenticated identity may hold Memberships in multiple Tenants;
- when exactly one eligible Tenant exists CommerceOS may auto-select it;
- when more than one eligible Tenant exists the user must intentionally select the active Tenant;
- Membership has exactly one current MVP role: Owner, Admin, Staff, or Viewer;
- current Membership/Tenant state remains server-authoritative;
- Subscription entitlements remain a separate authority from Membership/Tenant authorization.

The trust boundary must therefore support safe tenant discovery/selection without turning a selector, JWT claim, browser cache, or eventual index into authority.

## Decision

### Authentication

- Use API Gateway HTTP API's Cognito JWT authorizer on protected merchant routes.
- Validate issuer, audience/client, signature, lifetime, and required access-token scopes at the edge.
- Treat the stable Cognito subject identifier as external identity evidence only.
- Do not treat email, Tenant, Membership, role, capability, plan, entitlement, or limit token claims as current business authority.

### Subject membership discovery

Merchant Access owns a module-private, strongly consistent subject-to-Membership discovery representation in the Tenancy DynamoDB table.

Conceptual shape:

```text
PK = SUBJECT#<SubjectId>
SK = MEMBERSHIP#<MembershipId>#TENANT#<TenantId>
```

It is a technical authorization lookup, not a second Membership aggregate. It contains only the minimum references/current Membership status/revision needed for discovery and is updated atomically with the owning Membership change.

Rules:

- no Subject GSI is authorization authority;
- discovery uses a strongly consistent base-table Query;
- a discovered Tenant/Membership reference is only a candidate until current Tenant + Membership validation succeeds;
- if no selector is supplied and discovery yields one candidate, CommerceOS may auto-select only after current validation;
- if discovery is multiple/ambiguous, return `TENANT_SELECTION_REQUIRED` and require intentional Tenant selection;
- requiring explicit selection in an ambiguous case is always safer than inferring Tenant from SubjectId;
- suspended Tenant/inactive Membership is rejected by final current authority resolution.

### Authority resolution

Treat any Tenant identifier/reference from route, header, query, body, browser state, or selection UI as `RequestedTenantSelection`, an untrusted target.

For every protected request:

1. establish the authenticated Subject;
2. discover/select a target Tenant under the rules above;
3. call the Tenancy/Merchant Access `ResolveTenantAuthority` application contract;
4. transactionally/currently read the selected Tenant and `(TenantId, SubjectId)` Membership authority representation from the authoritative base-table paths;
5. require Active Tenant for ordinary merchant work and Active Membership bound to that Tenant/Subject;
6. return an immutable trusted context populated from authoritative records, not caller data;
7. fail closed when current authority cannot be established; never fall back to request/JWT/cache data.

Conceptual result:

```text
TrustedTenantContext
  tenantId
  subjectId
  membershipId
  role                 # Owner | Admin | Staff | Viewer
  tenantRevision?
  membershipRevision?
  correlationId
```

The owning domain application applies the approved role policy for its operation. This ADR does not create a global mutable permission engine or let the client name an authoritative capability.

### Subscription entitlement remains separate

`TrustedTenantContext` intentionally does not contain cached plan/entitlement/limit authority.

A subscription-governed mutation separately calls the authoritative `SubscriptionBilling.EvaluateEntitlement` contract after Tenant/Membership authorization. Missing/unavailable entitlement authority fails the governed mutation closed; it never becomes `Unlimited` and does not rewrite Tenant/Membership state.

### Enforcement and non-disclosure

- Protected use cases receive trusted context, apply their approved role/domain policy, and derive repository Tenant scope only from that context.
- Target aggregate IDs remain untrusted and cannot change the context Tenant.
- Tenant-owned repository contracts have no unscoped overload and include TenantId in the base key/query.
- A missing/cross-Tenant aggregate uses the same safe not-visible behavior.
- Authentication failure, inactive Membership, suspended Tenant, role denial, entitlement denial, target not visible, and dependency unavailable remain internally distinguishable without revealing another Tenant's data.

### Freshness

Initial architecture performs current authority resolution for every protected request.

- no cross-request Membership/Tenant authorization cache is authoritative;
- no eventual GSI is the sole current-authority path;
- Membership disablement/role change and Tenant suspension affect the next coherent resolution;
- an already-started command still relies on its own aggregate revision/transaction conditions for concurrency safety.

A later cache requires an explicit staleness/revocation/outage contract and architecture decision.

### Separate trust paths

- Onboarding uses `TrustedOnboardingContext` from authenticated verified identity and has no caller-selected Tenant authority. Cross-domain completion/recovery is defined by ADR-009.
- Public storefront uses a separate `PublicTenantContext` only after Storefront Tenant-address business semantics are approved; it cannot invoke merchant-protected commands.
- Queue/event/workflow handlers use validated service/message execution context, not merchant actor context.
- Platform administrators use a separate authenticated/authorized/audited path; there is no `bypassTenant` flag and no Owner Membership in every Tenant.

## Alternatives considered

### Option A — Put Tenant and role/entitlement authority in Cognito custom claims

Benefits:
- fewer data reads.

Costs/risks:
- stale disablement/role/tenant/subscription state;
- revocation complexity;
- multi-Tenant ambiguity;
- identity provider becomes accidental business authority.

Rejected.

### Option B — Trust client-selected TenantId after token validation

Benefits:
- minimal implementation.

Costs/risks:
- direct confused-deputy/cross-Tenant vulnerability.

Rejected.

### Option C — Use an eventually consistent Subject GSI as authority

Benefits:
- convenient cross-Tenant discovery.

Costs/risks:
- stale membership cardinality can defeat intentional selection semantics;
- stale disablement can appear active;
- an index becomes authorization authority.

Rejected as authority.

### Option D — Strong subject discovery + current selected-Tenant validation + tenant-scoped repositories

Benefits:
- supports many-to-many Membership cleanly;
- explicit user selection when ambiguous;
- next-resolution status/role freshness;
- defense in depth at application and persistence boundaries;
- preserves future module extraction.

Costs/risks:
- additional DynamoDB reads/transactions per protected request;
- authority-store outage fails protected work closed;
- subject discovery representation must be transactionally maintained with Membership.

Chosen.

## Consequences

### Positive

- Token + known TenantId cannot create Tenant authority.
- Multi-Tenant users have an explicit safe selection model.
- Current role/status changes affect the next request without token regeneration.
- SubscriptionBilling remains the only entitlement authority.
- All merchant modules consume one transport-neutral tenant context.
- Public/onboarding/background/admin paths cannot silently acquire merchant permissions.

### Negative / trade-offs

- Protected requests pay current-read latency/capacity cost.
- Membership changes require maintaining both tenant authority lookup and subject discovery representation transactionally inside Tenancy.
- Authority-store incidents deny protected work even while Cognito tokens remain valid.
- Storefront public Tenant addressing remains a separate unresolved domain contract.

## Security and tenant impact

- Tenant isolation uses both authorization and tenant-key persistence; neither substitutes for the other.
- Route/body/query/header/cursor/JWT/browser values may identify a target but never grant authority.
- Subject discovery is accessible only for the authenticated subject/application authorization path and contains minimal data.
- Tenant-visible failures and Audit records remain non-disclosing about foreign Tenant/entity identity.
- Tokens, full claims, invitation credentials, and raw selectors are not logged/persisted as authority evidence.

## Reliability and operability impact

- Authority read failures return a safe dependency-unavailable result rather than no-Membership or stale fallback.
- SDK retries are bounded and technical only.
- Current authority resolution and aggregate optimistic concurrency remain separate mechanisms.
- Logs preserve safe module/outcome/correlation data with no token/email/cross-Tenant target detail.
- High-cardinality SubjectId/TenantId are not custom metric dimensions.

## Cost impact

This ADR/reconciliation deploys nothing. Later protected requests add bounded strong/transactional DynamoDB reads and a subject-discovery query. At the learning volume this remains within the existing DynamoDB cost envelope but must be measured before considering caching.

No new managed service is introduced.

## Reversibility / migration

- A later authority cache requires a revocation/staleness/outage ADR and cannot silently supersede current validation.
- Changing Tenant selection UX changes delivery contracts but not the rule that selection is untrusted until Membership/Tenant validation.
- Moving authority resolution into a separate deployment preserves the application contract and requires internal authentication/IAM/latency/cost analysis.
- Changing identity provider changes authentication adaptation, not Merchant Access ownership.

## Validation

Dependent implementation must verify:

- one subject with Tenant A + Tenant B Memberships requires intentional selection when ambiguous;
- one candidate may be auto-selected only after current validation;
- route/header/query/body/JWT/cursor attempts cannot override trusted Tenant scope;
- still-valid tokens lose authority on the next resolution after Membership disable/role change or Tenant suspension;
- Subject discovery cannot authorize without final Tenant/Membership validation;
- Tenant A cannot read/mutate Tenant B by known aggregate/Membership IDs;
- Subscription entitlement never falls back to JWT/client/cache values;
- authority-store failure fails closed;
- architecture tests enforce trusted context/repository scope and prohibit foreign table access;
- logs/problems contain safe codes/correlation only.

## References

- domain baseline: `docs/domains/tenant-identity.md`
- technical baseline: `docs/architecture/technical-baseline.md`
- product-decision technical reconciliation: `docs/architecture/product-decision-technical-reconciliation.md`
- ADR-005: DynamoDB module ownership/access patterns
- ADR-008: SubscriptionBilling authority boundary
- ADR-009: onboarding completion/recovery