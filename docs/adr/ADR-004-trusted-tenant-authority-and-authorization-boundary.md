# ADR-004 — Trusted Tenant Authority and Authorization Boundary

Status: Accepted
Date: 2026-08-09
Last reconciled: 2026-08-10 after resolved PD-004
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS must deny cross-Tenant access even when a caller knows another Tenant or aggregate identifier. Cognito proves external identity, but identity alone does not prove current authority for any Tenant.

Approved product/domain policy now establishes:

- one authenticated identity may hold Memberships in multiple Tenants;
- Membership has exactly one current MVP role: Owner, Admin, Staff, or Viewer;
- Tenant lifecycle is `Active` or `Suspended` only in MVP;
- suspension/reactivation is platform-administration only, requires a reason, and is auditable;
- Suspended blocks public storefront/checkout and all ordinary merchant mutations;
- Active Memberships remain intact while a Tenant is Suspended and retain controlled read-only access according to normal role visibility;
- authorized platform support may investigate through a separate privileged read-only path and never becomes a Tenant Membership;
- Subscription entitlement remains a separate authority from Tenant/Membership authorization.

The previous version of this ADR treated a Suspended Tenant as a generic final authority failure. That is now too coarse: merchant mutation authority must fail, but approved merchant read-only recovery/history access must remain possible.

## Decision

### Authentication

- Use API Gateway HTTP API's Cognito JWT authorizer on protected merchant routes.
- Treat the stable Cognito subject identifier as external identity evidence only.
- Never treat email, Tenant, Membership, role, plan, entitlement, or limit token/client claims as current business authority.

### Subject membership discovery

Merchant Access owns a module-private strongly consistent subject-to-Membership discovery representation in the Tenancy DynamoDB table.

Conceptual shape:

```text
PK = SUBJECT#<SubjectId>
SK = MEMBERSHIP#<MembershipId>#TENANT#<TenantId>
```

Rules:

- discovery uses a strongly consistent base-table `Query`;
- no Subject GSI/JWT/browser cache is authorization authority;
- a discovered Tenant/Membership is only a candidate until current Tenant + Membership validation succeeds;
- one candidate may be auto-selected only after current validation;
- multiple/ambiguous candidates require intentional Tenant selection;
- Suspended Tenants are not discarded merely because mutation is unavailable: they may still be selected for approved authenticated read-only access;
- Disabled Memberships do not gain ordinary merchant read/mutation authority from discovery.

### Split read authority from mutation authority

The Tenancy application boundary exposes separate resolved authority results so a Suspended read context cannot accidentally be reused for a mutation.

Conceptually:

```text
ResolveTenantReadAuthority(
  AuthenticatedPrincipal,
  RequestedTenantSelection?,
  RequestMetadata)
    -> TrustedTenantReadContext | AuthorityFailure

ResolveTenantMutationAuthority(
  AuthenticatedPrincipal,
  RequestedTenantSelection?,
  RequestMetadata)
    -> TrustedTenantMutationContext | AuthorityFailure
```

Both validate the current selected Tenant and `(TenantId, SubjectId)` Membership using authoritative Tenancy records.

`TrustedTenantReadContext` requires:

- an Active Membership bound to the authenticated Subject and selected Tenant;
- Tenant status of `Active` or `Suspended`;
- current role from authoritative Membership state.

`TrustedTenantMutationContext` requires:

- everything required for read authority;
- Tenant status `Active`;
- the owning domain's role/capability rule;
- Subscription entitlement when the operation is subscription-governed.

Conceptual fields remain transport-neutral:

```text
TrustedTenantReadContext
  tenantId
  subjectId
  membershipId
  role
  tenantStatus          # Active | Suspended
  tenantRevision?
  membershipRevision?
  correlationId

TrustedTenantMutationContext
  tenantId
  subjectId
  membershipId
  role
  tenantRevision?
  membershipRevision?
  correlationId
```

The mutation context intentionally does not carry cached Subscription entitlement authority.

### Suspended-Tenant behavior

Delivery/application composition must make the access class explicit.

- ordinary merchant commands always require `TrustedTenantMutationContext` and therefore fail while Suspended;
- merchant queries use `TrustedTenantReadContext` and then apply the normal role-specific read visibility of the owning domain;
- Owner/Admin may reach the approved history/operations/billing/support/recovery/Audit views they are otherwise authorized to read;
- Staff/Viewer remain limited to normal-role read visibility;
- no query path may smuggle a state-changing side effect into a Suspended read-only request;
- Reactivate restores Tenant eligibility only; it does not rewrite Membership or Subscription lifecycle.

### Platform administration and support

Platform actors use separate trust types established by platform authentication/authorization, never merchant Memberships or a `bypassTenant` flag.

Conceptually:

```text
TrustedPlatformAdminContext
TrustedPlatformSupportReadContext
```

- `SuspendTenant` and `ReactivateTenant` are Tenancy-owned platform-administration commands requiring explicit reason, expected Tenant revision, and durable Audit intent/evidence.
- platform support investigation uses producer-owned read-only application queries in each module, scoped to an explicit target Tenant and privileged reason/correlation metadata;
- platform support never reads foreign module tables directly and never receives merchant mutation authority by implication.

### Public storefront

Public storefront uses a separate `PublicTenantContext`. PD-052 defines a globally unique Tenant-owned `/{storefrontSlug}` binding; resolving it must verify current Tenant status. `Suspended` returns storefront/checkout unavailable and cannot be bypassed by cached public Product data.

### Subscription entitlement remains separate

A subscription-governed merchant mutation asks `SubscriptionBilling.EvaluateEntitlement` only after active-Tenant Membership authority has been established.

Missing/unavailable entitlement authority fails the governed mutation closed. Entitlement failure does not become Tenant suspension or Membership disablement.

### Freshness and non-disclosure

- current Tenant/Membership authority is resolved per protected request initially;
- no cross-request authorization cache is authoritative;
- Tenant suspension/reactivation, Membership disable/reactivation, and role change affect the next resolution;
- target aggregate IDs remain untrusted and repository Tenant scope comes only from trusted context;
- missing/cross-Tenant targets use safe non-disclosing not-visible behavior;
- dependency failures never fall back to JWT/client/cache authority.

## Alternatives considered

### One generic `TrustedTenantContext` with a boolean `isSuspended`

Rejected. Every consumer would need to remember whether its operation is read or mutation, making accidental suspended mutations easier to introduce.

### Treat Suspended as no merchant authority at all

Rejected because resolved `PD-004` explicitly preserves controlled authenticated read-only access.

### Put Tenant status/role in Cognito claims

Rejected because suspension/role changes must affect the next authority decision without waiting for token refresh/revocation.

### Give platform support an Owner Membership in every Tenant or a bypass flag

Rejected because it collapses platform and merchant trust boundaries and creates cross-Tenant confused-deputy risk.

### Separate read and mutation authority contexts with explicit platform trust paths

Chosen because it makes the resolved lifecycle semantics mechanically visible at application boundaries while preserving current authority checks.

## Consequences

Positive consequences:

- Suspended merchant read-only access is possible without weakening mutation denial.
- A Builder cannot accidentally treat `TenantStatus` as a single allow/deny bit for all protected requests.
- Platform support stays explicit, privileged, read-only, and non-membership-based.
- Subscription entitlement remains independently authoritative.
- Current suspension/reactivation applies on the next authority resolution without token changes.

Trade-offs:

- delivery/application composition must classify protected operations as read or mutation;
- Tenancy exposes two resolved authority contracts instead of one broad result;
- protected requests continue to incur current Tenancy reads;
- Storefront public Tenant route/index keys must implement PD-052's separate Tenant-owned `/{storefrontSlug}` contract.

## Security and tenant impact

- Tenant isolation still requires both current authorization and tenant-scoped persistence.
- Route/header/query/body/JWT/browser Tenant references never grant authority.
- Suspended read contexts cannot be passed to mutation use cases by type/contract design.
- Platform support/admin contexts are independently authenticated/authorized/audited and never become Tenant Memberships.
- Privileged support queries stay producer-owned and cannot directly access another module's table.
- Tenant-visible failures/logs/Audit output remain non-disclosing about foreign Tenant/entity identity.

## Reliability and operability impact

- authority-store failure returns a dependency-unavailable result; there is no stale fallback.
- platform suspend/reactivate commands use expected Tenant revision and idempotent command/Audit delivery identity.
- a failed Audit consumer does not roll back the accepted Tenancy transition; durable source-owned Audit intent supports recovery.
- cached storefront/catalog data cannot override current Tenant suspension at the public request/checkout gate.
- logs preserve safe operation class, outcome, and correlation identifiers without tokens or cross-Tenant target details.

## Cost impact

No new managed service is introduced.

The resolved design adds only bounded current DynamoDB reads already within the Tenancy authority model. Platform support/admin queries reuse module application surfaces and existing serverless runtimes. Audit delivery uses the already-approved durable integration pattern when its implementation task is Ready.

## Reversibility / migration

- Existing generic authority call sites can migrate by classifying each route/use case as read or mutation and adapting to the corresponding trusted context.
- No stored business data requires destructive migration; Tenant already owns status/revision.
- A future Tenant closure/privacy lifecycle requires a new product/privacy decision and architecture reconciliation; it must not be inferred from Suspended records.
- A future authority cache requires a separate staleness/revocation/outage architecture decision.

## Validation

Dependent implementation must verify:

- Suspended Tenant + Active Owner/Admin can use approved read-only queries but cannot execute ordinary merchant mutations;
- Suspended Tenant + Active Staff/Viewer cannot exceed their normal role read visibility;
- Reactivate restores Tenant eligibility but does not reactivate Disabled Membership or Ended Subscription;
- only a trusted platform-admin path may suspend/reactivate and reason is mandatory;
- platform support read does not create a Tenant Membership and cannot mutate business state;
- public storefront/checkout is unavailable for Suspended Tenant even when public Product data is cached;
- one subject with multiple Memberships still requires intentional Tenant selection when ambiguous;
- known foreign Tenant/aggregate identifiers cannot override trusted Tenant scope;
- authority dependency failure fails closed;
- architecture tests prevent mutation handlers from accepting read-only authority context and prohibit foreign-table access.

## References

- `docs/domains/tenant-identity.md`
- `docs/domains/product-decisions.md` (`PD-004`)
- `docs/architecture/technical-baseline.md`
- `docs/architecture/product-decision-technical-reconciliation.md`
- ADR-005 — DynamoDB module ownership/access patterns
- ADR-006 — reliable integration/Audit delivery
- ADR-008 — SubscriptionBilling entitlement boundary
