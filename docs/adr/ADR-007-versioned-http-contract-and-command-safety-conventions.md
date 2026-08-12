# ADR-007 — Versioned HTTP Contract and Command Safety Conventions

Status: Accepted
Date: 2026-08-10
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

TASK-0088 must make the first Tenant/Merchant Access and Catalog implementation slices safe to refine without Builders inventing external API conventions, error behavior, optimistic-concurrency semantics, idempotency scope, or correlation rules route by route.

TASK-0087 deliberately defines business outcomes rather than HTTP status codes. It also requires non-disclosing tenant isolation, idempotent onboarding, stale-revision protection, honest unknown outcomes, and explicit separation between authentication identity and current tenant authority. Those meanings need a stable transport strategy without turning transport choices into new business semantics.

The first-frontier contract baseline already defines the intended conventions, but a material externally consumed API/command strategy requires an ADR under the repository ADR process. Without one, later tasks could drift into incompatible endpoint versioning, generic exceptions, blind retries, unsafe idempotency caches, raw DynamoDB cursors, or inconsistent concurrency behavior.

## Decision

### External HTTP versioning and representation

- External JSON APIs use one consistent major-version boundary, initially `/api/v1` unless a refined delivery task documents an equivalent gateway mapping with the same compatibility guarantees.
- HTTP DTOs are delivery contracts. They are distinct from Domain entities, Infrastructure persistence models, and application implementation types.
- Compatible additive response fields may be introduced within a major version when consumers are required to tolerate unknown response fields.
- Breaking semantic changes or newly required fields use a new major version or an explicitly documented migration window.
- Identifiers are opaque strings and clients do not parse their encoding.
- Monetary values are structured amount + ISO currency. No bare numeric amount implies a currency.
- UTC timestamps use ISO-8601 representation; business-effective dates remain explicit domain values when introduced.
- A Tenant identifier appearing in a route, query, header, body, or cursor identifies a target/selection only when the owning contract defines that purpose. It never becomes authorization authority.

### Error contract and non-disclosure

- Failed HTTP operations use an `application/problem+json` representation compatible with RFC 9457.
- Problem responses expose a stable machine code, safe title/status information, and correlation identity. Validation errors may include safe field-level detail.
- Stack traces, AWS payloads, table/key details, tokens/claims, invitation secrets, and cross-tenant existence details are never returned.
- Baseline transport mapping is:
  - malformed/shape failure → `400`;
  - unauthenticated/invalid token → `401`;
  - authenticated but current Tenant/Membership/capability denies work → `403`;
  - aggregate absent or not visible in trusted Tenant context → `404` with the same non-disclosing shape;
  - business validation rejection → `422`;
  - uniqueness, invalid transition, or incompatible idempotency reuse → `409`;
  - missing required optimistic-concurrency precondition → `428`;
  - stale expected revision/ETag → `412`;
  - bounded request/concurrency guard exceeded → `429`;
  - trusted authority/required dependency unavailable before a safe result can be produced → `503`;
  - unexpected server failure → `500` with generic safe detail.
- `AlreadyApplied` returns the prior accepted result/reference rather than becoming an error.
- `OutcomeUnknown` is never converted to definitive failure merely because the caller timed out. If exposed as a still-progressing durable operation, it uses `202 Accepted` plus a stable operation/status resource.
- `202 Accepted` is allowed only when the operation/intention and its status are durably recorded; it is not a wrapper for a best-effort background call.

### Optimistic concurrency

- Every mutation whose lost update would violate a documented invariant or overwrite a newer merchant decision carries an expected aggregate revision.
- HTTP GET may expose an opaque ETag derived from the aggregate revision, never from a DynamoDB key/item hash contract.
- Revision-sensitive mutation requires `If-Match`; missing precondition returns `428`.
- Delivery maps the ETag to the application expected revision and Infrastructure enforces it through a conditional write/transaction.
- A losing concurrent update returns `412` plus the context-owned stale-revision code.
- The server does not automatically re-read and replay stale business intent against newer state. The caller must deliberately reload and reapply intent.

### Idempotency

- `Idempotency-Key` is required only for a documented externally retryable command where duplicate side effects or duplicate server-assigned resources are unsafe.
- A command idempotency identity is scoped by operation name plus trusted Tenant/actor scope, or the approved pre-Tenant onboarding principal scope, plus the bounded idempotency key.
- The owning module stores a normalized semantic request fingerprint, processing/accepted state when needed, stable result/reference fields, and bounded retention metadata where safe.
- Equivalent replay returns/references the original logical result and performs no second effect.
- Incompatible reuse returns `409` and performs no effect.
- Equivalent retry of an interrupted/unknown processing record resumes or reconciles according to the owning command contract; it does not start a second logical effect.
- TTL is storage cleanup only. It never removes a permanent business uniqueness/source claim such as the accepted onboarding identity required by the approved admission policy.
- Correlation ID is not an idempotency key.

### Correlation, causation, and attempt identity

The following identifiers remain distinct:

- `requestId` — one transport delivery/attempt;
- command/idempotency identity — one logical requested effect;
- `correlationId` — one related end-to-end diagnostic/business flow;
- `eventId` — one immutable published integration fact;
- `causationId` — the direct command/event that caused a fact;
- `aggregateId` — the owning aggregate related to that fact.

Every ingress generates or validates a bounded correlation identifier and returns it where safe. A retry normally receives a new requestId while preserving its logical command/idempotency identity. Event redrive preserves eventId and causation identity; it does not manufacture a new business fact.

### Pagination and query safety

- List APIs use bounded page sizes and opaque versioned cursors.
- A cursor is bound to trusted Tenant scope, route/query contract version, approved filter/sort shape, and the owner repository's stable continuation position.
- Raw DynamoDB `LastEvaluatedKey` is never an external contract.
- A cursor cannot override trusted Tenant scope and is rejected when reused under another tenant, query version, or incompatible filter/sort shape.
- No endpoint promises arbitrary filtering, sorting, search, or total count without an approved persistence/projection access pattern.

## Alternatives considered

### Option A — Let each endpoint choose its own HTTP/error/retry conventions

- Benefits: minimal up-front design and maximum local flexibility.
- Costs/risks: inconsistent clients, duplicated retry logic, accidental information disclosure, conflicting stale-write behavior, and Builders becoming API architects task by task.

### Option B — Use transport/request identity as the universal idempotency mechanism

- Benefits: fewer persistence concepts and headers.
- Costs/risks: retries can receive new request IDs; correlation spans multiple effects; neither proves semantic request equivalence or actor/Tenant scope. Duplicate resources/effects remain possible.

### Option C — Hide optimistic concurrency and automatically retry business mutations

- Benefits: fewer client-visible preconditions and simpler happy-path UX.
- Costs/risks: stale merchant intent can silently overwrite newer accepted state; server-side replay may reinterpret a business command against different facts.

### Option D — Shared versioned contract/error/concurrency/idempotency conventions with context-owned business codes

- Benefits: stable client behavior, explicit lost-update protection, safe retry semantics, non-disclosing tenant behavior, and reusable contract verification without merging domain error meaning.
- Costs/risks: additional command records/conditions, ETag handling, problem-contract fixtures, and stricter endpoint design discipline.

Chosen: Option D.

## Consequences

### Positive

- Builders do not invent API versioning, status mappings, retry semantics, correlation meanings, or cursor safety per feature.
- Domain/business outcome codes remain owned by their contexts while HTTP mapping remains consistent.
- Lost updates are explicit and testable rather than hidden by automatic retries.
- Duplicate externally retryable commands are scoped to the correct trusted actor/Tenant and can replay safely.
- Cross-tenant not-visible behavior is compatible with both authorization and persistence isolation.
- Durable asynchronous/unknown outcomes remain honest instead of being collapsed into generic success/failure.

### Negative / trade-offs

- Revision-sensitive clients must handle ETags and deliberate conflict recovery.
- Idempotent commands add bounded persistence records and semantic fingerprinting rules.
- API compatibility fixtures and problem-code registries require maintenance.
- A later public API style change may require a new major version/ADR rather than ad hoc endpoint drift.

## Security and tenant impact

- Tenant isolation: no HTTP selector, cursor, idempotency key, correlation ID, or aggregate identifier can replace trusted Tenant scope.
- Authentication/authorization: `401`, `403`, and non-disclosing `404` remain distinct without exposing another Tenant's records; ADR-004 remains authority.
- Sensitive data/secrets: problem/log/correlation fields exclude tokens, claims, invitation secrets, raw personal/payment/source payloads, and persistence internals.
- Abuse controls: page size, request size, idempotency-key length, correlation-id length, retry/concurrency behavior, and route throttling are bounded by the introducing task.

## Reliability and operability impact

- Failure modes: malformed requests, domain rejection, stale revision, duplicate/incompatible replay, dependency unavailability, and unexpected failure have stable classes instead of generic exceptions.
- Retry/recovery: callers can distinguish safe technical retry from deliberate business reapplication; ambiguous external outcomes remain durable/reconcilable.
- Observability: request, command, correlation, event, and causation identities remain separable in structured telemetry.
- Operational burden: contract fixtures, idempotency recovery tests, stale-write tests, and cursor validation become part of Ready-task verification.

## Cost impact

- Learning profile: TASK-0088 adds no AWS resource and no runtime cost. Later idempotent commands add small DynamoDB records/conditional transactions already covered by the existing persistence cost model.
- Beta profile: additional command-record writes/storage and occasional strong/transactional reads remain request-driven; measure them with the owning access-pattern ledger.
- Larger-scale implication: high-volume idempotency retention and authority/concurrency reads may require storage/throughput tuning, but no new managed service is justified by this ADR.
- Cost-model update required? No.

## Reversibility / migration

- A new external major version can coexist while clients migrate; old problem/status/idempotency behavior remains supported for its stated compatibility window.
- Changing optimistic-concurrency representation requires preserving application revisions while versioning delivery contracts.
- Changing idempotency storage layout remains module-private if the logical scope, semantic fingerprint, and replay contract are preserved.
- Pagination cursor encoding can change behind a versioned opaque token; old cursors may be rejected only under the documented compatibility/expiry contract.

## Validation

- Contract serialization/compatibility fixtures cover every externally consumed major version.
- Tenant A/Tenant B/anonymous/inactive-Membership tests cover `401`/`403`/non-disclosing `404` behavior and all selector/cursor override attempts.
- ETag tests cover success, missing `If-Match`, concurrent stale mutation, and no automatic stale replay.
- Idempotency tests cover equivalent replay, incompatible reuse, concurrent claim, interrupted processing, and the same header under two trusted actors/Tenants.
- Pagination tests prove opaque cursor tenant/query binding and no raw DynamoDB continuation leakage.
- Correlation tests prove propagation without confusing request/command/event identities or logging secrets.
- `202` tests, when such an operation is introduced, prove a durable operation/status record exists before the response is emitted.

## References

- relevant task: [TASK-0088](../../tasks/commerceos/completed/TASK-0088-technical-architecture-baseline-reconciliation.md)
- architecture docs: [First-frontier contracts and trusted context](../architecture/first-frontier-contracts.md), [Technical baseline](../architecture/technical-baseline.md), [Persistence access patterns](../architecture/persistence-access-patterns.md)
- related ADRs: [ADR-004](ADR-004-trusted-tenant-authority-and-authorization-boundary.md), [ADR-005](ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md), [ADR-006](ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md)
