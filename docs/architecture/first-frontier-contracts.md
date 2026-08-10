# CommerceOS — First-Frontier Contracts and Trusted Context

_Technical contract baseline for Tenant Management, Merchant Access, and Catalog. Reconciled by TASK-0088._

## 1. Contract ownership

Contracts are owned by the module that accepts the request or states the fact.

| Contract family | Owner | Consumers | Rule |
|---|---|---|---|
| Merchant authentication input | API delivery / Cognito adapter | Tenancy authority resolver | Token/claims are identity evidence, not Tenant authority |
| `ResolveTenantAuthority` | Tenancy / Merchant Access | API delivery and protected application composition | Current Membership and Tenant status determine authority |
| `TrustedTenantContext` | Tenancy Contracts | protected module applications | Cannot be constructed from a request TenantId alone |
| onboarding command/result | Tenancy | onboarding delivery | Final fields wait for `PD-002` and `PD-034`; completed result is Tenant + initial Owner outcome |
| Tenant lifecycle commands/results | Tenancy / Tenant Management | separately authorized merchant/platform delivery | Expected revision; reactivation verifies Active Owner without changing Membership; permission/support/Audit details remain gated |
| profile/membership/invitation commands | Tenancy | protected API/back office | Final permissions/recipient schemas wait for their product gates |
| Catalog commands/queries/results | Catalog | API, later Storefront/Sales/Ingestion through explicit contracts | Never expose Catalog persistence models or accept external source authority |
| Storefront address/public-view contracts | Storefront | public delivery/web client | Storefront owns public experience/address binding; owner projections remain non-authoritative and final address schema is product-gated |
| checkout command/result | Sales | Storefront delivery/client | Synchronous idempotent command when approved; browser Tenant/price/cart values are untrusted |
| HTTP DTO/problem/pagination shapes | API delivery | web clients | Versioned transport mapping; not Domain entities |
| integration facts | producing module | named asynchronous consumers | Separate from internal domain facts; governed by ADR-006 |

No consumer imports a producer's Domain or Infrastructure assembly. A contract shape does not transfer fact ownership.

## 2. Authentication and authority flow

### Authentication boundary

API Gateway's Cognito JWT authorizer validates the configured access token's issuer, audience/client, signature, lifetime, and required OAuth scopes. Edge OAuth scopes are coarse API-admission scopes only; they are not merchant roles/capabilities and cannot replace current Membership resolution. The backend treats only the stable external subject identifier and validated token metadata as authentication evidence.

The following are never current tenant authority:

- route, query, header, or body TenantId;
- an email claim;
- a token custom claim naming a Tenant, Membership, role, or capability;
- a previously cached Membership decision;
- knowledge of an aggregate identifier.

### Conceptual input

```text
AuthenticatedPrincipal
  subjectId
  issuer
  audience/client
  authenticationTime where available

RequestedTenantSelection
  value defined only after PD-001

RequestMetadata
  requestId
  correlationId
  occurredAt
```

`RequestedTenantSelection` is an untrusted target. Its exact route/header/UI form is intentionally absent until `PD-001` is approved.

### Authority resolution

Conceptual application contract:

```text
ResolveTenantAuthority(
  AuthenticatedPrincipal,
  RequestedTenantSelection,
  RequestMetadata)
    -> TrustedTenantContext | AuthorityFailure
```

The resolver:

1. resolves the selected Tenant identity under the approved `PD-001` selection contract;
2. reads current Tenant status and the subject's current authority record through a transactionally consistent base-table path;
3. requires an Active Tenant for ordinary work and an Active Membership bound to that Tenant and subject;
4. derives effective capabilities from the approved `PD-003` policy;
5. returns a context whose identifiers originate from the accepted records, not from the requested selector;
6. fails closed on unavailable/invalid authority data without falling back to request data or stale token claims.

Conceptual result:

```text
TrustedTenantContext
  tenantId
  subjectId
  membershipId
  effectiveCapabilities[]
  tenantRevision / membershipRevision where needed diagnostically
  correlationId
```

This is a transport-neutral immutable application contract. Capability values do not exist until the product owner approves their mapping; the contract shape does not grant one.

Resolution occurs for every protected request. There is no cross-request authorization-result cache in the initial architecture. A Membership disable/role change or Tenant suspension therefore affects the next authority resolution. A change does not retroactively cancel a command that already passed one coherent resolution and began its atomic commit; concurrency-sensitive commands still use their own revision/condition checks.

### Application enforcement

For every protected command/query:

1. delivery obtains a `TrustedTenantContext`;
2. the owning application checks the named required capability;
3. aggregate target identifiers remain untrusted inputs;
4. the repository receives TenantId only from trusted context;
5. the base key/query includes that TenantId;
6. missing or mismatched data is returned through a non-disclosing outcome.

Defense in depth requires both authorization and tenant-partitioned persistence. Passing authorization does not allow an unscoped repository call.

## 3. Distinct execution paths

### Onboarding

Onboarding occurs before an ordinary Membership exists. It uses a distinct `TrustedOnboardingContext` produced by the admission/identity mechanism approved under `PD-034`. It contains no caller-supplied TenantId and cannot be converted into a merchant context until the atomic Active Tenant + Active Owner outcome commits.

The final onboarding request schema remains blocked by `PD-002` and `PD-034`. Architecture fixes only these invariants:

- the server assigns TenantId, MembershipId, and aggregate identifiers;
- an accepted intent has a durable idempotency/admission claim and request fingerprint;
- equivalent retry returns the prior logical result;
- incompatible reuse is a conflict;
- completed success means both Tenant and initial Owner Membership exist and are Active.

### Public storefront

A public request uses a separate `PublicTenantContext` derived from an approved storefront address mapping. It never creates Merchant authority and cannot call protected commands.

Storefront is a future implementation module, not merely the static web delivery project. It owns Storefront configuration/address binding, public experience composition, tenant-bound transient cart behavior, checkout intent at the interaction boundary, and any Storefront-owned derived view/cache. The web application is its delivery adapter. Storefront does not own Catalog Product, Sales Order, Inventory, or Payment truth and never writes those modules' tables.

Initial public browse composition is synchronous inside the shared commerce runtime: resolve `PublicTenantContext` through Storefront, then invoke Catalog's producer-owned public Product query. Catalog owns public-eligibility truth and its public Product contract; a later Storefront materialized view requires a named versioned fact consumer and ADR-006 delivery. Cache/projection lag never authorizes checkout.

Checkout intent crosses synchronously into a Sales-owned idempotent command when its product decisions are approved. Storefront never converts browser Tenant, price, availability, or cart values into authority; Sales re-resolves the trusted public Tenant and owner facts. The exact checkout sequence remains gated by `PD-011`–`PD-014`, `PD-018`, and `PD-042`.

The domain owner/lifecycle/uniqueness for tenant storefront addressing is currently missing; see the domain-decision record in the technical baseline. Product addressing is separately gated by `PD-008`. Until both are resolved, no public route, address index, Storefront persistence schema, or cache key contract is final. Whether transient cart state is browser-local or Storefront-persisted also remains a later Ready-task decision; it never becomes canonical Sales state.

### Background consumer

A background handler uses an immutable `MessageExecutionContext` built from a verified versioned envelope. It contains producer-supplied TenantId for routing plus consumer validation; it is not a merchant actor context and cannot exercise interactive capabilities.

The consumer:

- validates the known event/command type and version;
- validates TenantId and aggregate/source identifiers before persistence;
- scopes all consumer-owned state by the envelope TenantId;
- deduplicates through the consumer's inbox/source key;
- never uses a message to read or write the producer's table.

### Platform administration

Platform administration is a separate authenticated, authorized, audited path with its own application contracts. It is not `TrustedTenantContext` plus a bypass flag and is not modeled as an Owner Membership in every Tenant. Its detailed contract is technically deferred until the corresponding product/security task.

## 4. HTTP contract conventions

These external contract decisions are governed by [ADR-007](../adr/ADR-007-versioned-http-contract-and-command-safety-conventions.md).

### Versioning and representation

- External JSON APIs use a consistent major version prefix, initially `/api/v1` unless a refined task documents an equivalent gateway mapping.
- Transport DTOs are distinct from application commands/results and Domain types.
- Additive compatible fields may appear within a major version; consumers ignore unknown response fields.
- A breaking semantic or required-field change uses a new major version or a documented migration window.
- Dates/times are UTC ISO-8601 strings; business-effective dates remain explicit domain values when introduced.
- Monetary values are structured amount + ISO currency; no bare number implies a currency.
- Identifiers are opaque strings. Their encoding is not a business contract and clients do not parse them.
- Requests and responses do not repeat TenantId merely as authorization. A task may include a target/selector only when its non-authoritative semantics are explicit.

### Success status baseline

| Operation result | HTTP behavior |
|---|---|
| resource created synchronously | `201 Created`, stable body, and `Location` when a read route exists |
| command completed with result/no new resource | `200 OK` or `204 No Content` as fixed by that route contract |
| query succeeded | `200 OK` |
| accepted durable operation still progressing | `202 Accepted` only when a durable operation identity/state and status query exist |
| equivalent idempotent replay | same logical status/result contract as the accepted operation, with optional replay metadata |

`202` is never used to hide a best-effort background call. A command must first persist the operation/intention durably.

### Problem details

Errors use `application/problem+json` compatible with RFC 9457 and include only safe fields:

```json
{
  "type": "https://commerceos.example/problems/catalog-revision-stale",
  "title": "The resource changed before this update was applied.",
  "status": 412,
  "code": "CATALOG_REVISION_STALE",
  "correlationId": "corr_...",
  "errors": {
    "field": ["safe validation message"]
  }
}
```

The example URI is a contract shape, not an allocated public domain. A refined API task defines the real stable problem-type namespace.

Never return stack traces, table keys, AWS request payloads, token/claim contents, invitation secrets, or facts that reveal another Tenant's aggregate.

### Stable transport mapping

| Outcome | Status | Notes |
|---|---:|---|
| malformed JSON, unsupported value encoding, or contract-shape failure | 400 | No domain command is attempted |
| unauthenticated/invalid/expired token | 401 | Normally rejected at API Gateway |
| authenticated but Membership/capability/ordinary Tenant status denies work | 403 | Response does not confirm another Tenant's existence |
| aggregate absent or not visible in trusted Tenant context | 404 | Same public shape for cross-tenant target |
| business validation rejected | 422 | Stable context-owned code + optional field errors |
| uniqueness, invalid state transition, or incompatible idempotency reuse | 409 | No accepted state is overwritten |
| required optimistic-concurrency precondition omitted | 428 | Revision-sensitive routes only |
| expected revision/ETag is stale | 412 | Context-specific stale-revision code |
| request rate/concurrency guard exceeded | 429 | Include safe `Retry-After` when known |
| trusted authority or required dependency unavailable | 503 | Fail closed; do not convert to no-membership or use stale authority |
| unexpected server failure | 500 | Generic code and correlation only |

`AlreadyApplied` returns the prior accepted result/reference rather than an error. `OutcomeUnknown` is represented as a durable owned state; when exposed as a still-progressing operation it returns `202` plus its status resource. It is never mapped to a definitive failure merely because a caller timed out.

## 5. Optimistic concurrency

Every aggregate mutation whose lost update would be unsafe carries an expected revision in its application contract.

HTTP behavior:

1. GET returns an opaque ETag derived from the aggregate revision, not a DynamoDB key or item hash.
2. Revision-sensitive mutation requires `If-Match`.
3. Delivery parses the ETag into the application expected revision.
4. Infrastructure applies a conditional write/transaction.
5. Missing precondition returns 428; a losing concurrent write returns 412 and the context-specific stale code.

The server never automatically retries a stale business mutation against the new state. The caller reloads and deliberately reapplies intent.

Commands already made inherently idempotent by a stable business operation identity still use the applicable aggregate condition; idempotency and optimistic concurrency solve different problems.

## 6. Idempotency contract

`Idempotency-Key` is required only for a documented externally retryable command where duplicate side effects or duplicate server-assigned resources are unsafe. Candidate first-frontier uses include onboarding and any create/accept command whose domain identity alone cannot safely resolve a retry.

An idempotency record is scoped by:

```text
operation name
+ trusted Tenant/actor scope, or approved pre-Tenant onboarding principal scope
+ Idempotency-Key
```

It stores:

- a normalized semantic request fingerprint, not a hash of raw JSON formatting;
- command processing/accepted state as needed;
- result identity and stable response fields needed for replay;
- creation/expiry metadata when bounded retention is safe.

Behavior:

| Existing record | Request fingerprint | Result |
|---|---|---|
| none | any valid | conditionally claim and execute within the owning consistency boundary |
| accepted | equivalent | return/reference the original result; no second effect |
| accepted or processing | incompatible | `409` idempotency conflict; no effect |
| processing/unknown after interruption | equivalent | resume/reconcile according to the command contract; do not start a second logical effect |

TTL is storage cleanup, not a uniqueness or business-state rule. Each Ready task specifies its retry window and any permanent logical source claim. Onboarding's no-duplicate invariant must not disappear merely because a replay-cache TTL expires; its durable claim follows the admission policy approved under `PD-034`.

Correlation ID is never used as the idempotency key.

## 7. Correlation and causation

At ingress:

- accept a client correlation ID only when it meets a bounded character/length policy; otherwise generate one;
- return it in a consistent response header and problem body;
- generate a separate server request ID;
- preserve both through logs and application metadata.

Identifiers have distinct meanings:

| Identifier | Meaning |
|---|---|
| requestId | one transport delivery/attempt |
| commandId / idempotency identity | one logical requested effect |
| correlationId | related end-to-end business/diagnostic flow |
| eventId | one immutable published integration fact |
| causationId | command/event that directly caused this fact |
| aggregateId | owning aggregate related to the fact |

A retry normally has a new requestId but preserves its command/idempotency identity and correlation where the client supplies it safely. Redrive preserves eventId and causation; it does not manufacture a new fact.

## 8. Pagination, filtering, and query safety

List APIs use bounded page size and an opaque versioned cursor.

The cursor is bound to:

- trusted Tenant scope;
- route/query contract version;
- approved filter/sort shape;
- the last stable access key needed by the owning repository.

The API never exposes a raw DynamoDB `LastEvaluatedKey`, never trusts TenantId embedded in a cursor, and rejects a cursor reused with a different filter/tenant/query version. Repository code always reconstructs the tenant partition from `TrustedTenantContext`.

No route promises total count unless a maintained count access pattern exists. No arbitrary sort/filter/search parameter is added without an access pattern; a DynamoDB `FilterExpression` or table scan is not an implementation of search.

## 9. First-frontier contract gates

Technical shapes that are intentionally incomplete:

| Contract | Blocking business decision(s) |
|---|---|
| tenant selector and membership discovery | `PD-001` |
| onboarding request/profile/Owner proof | `PD-002`, `PD-034` |
| capability names and role mapping | `PD-003` |
| suspended read/support result details | `PD-004` |
| invitation issue/accept request and lookup | `PD-036` |
| Product create fields/SKU fingerprint | `PD-002`, `PD-005`, `PD-037` |
| Category/Brand commands | `PD-009` |
| Product publication/public projection/routes/media | `PD-006`–`PD-010`, `PD-037`, plus the missing storefront-Tenant-address decision |
| import-candidate apply request/result | `PD-040` |

These rows are not permission for a Builder to select a placeholder default. They are inputs to Domain Architecture and TASK-0089 refinement.

## 10. Required contract verification when implemented

- contract serialization and compatibility fixtures for every public HTTP/event version;
- Tenant A/Tenant B/anonymous/inactive-Membership authorization cases;
- body/route/query/header/cursor attempts to override trusted Tenant scope;
- expired/invalid token versus no/inactive Membership versus dependency unavailable;
- safe 404 behavior for known cross-tenant identifiers;
- ETag success, missing precondition, and concurrent stale update;
- idempotency replay, incompatible reuse, concurrent claim, and interrupted-processing recovery;
- the same idempotency header under two trusted actors/principals cannot claim, conflict with, or replay each other's record;
- bounded/tenant-bound pagination cursor validation;
- correlation propagation without token, secret, or sensitive-payload logging.
