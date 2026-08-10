# ADR-008 — Subscription & Billing Module, Entitlement Decision, and Provider Boundary

Status: Accepted
Date: 2026-08-10
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

TASK-0091 adds a distinct Subscription & Billing bounded context that owns the merchant's CommerceOS subscription/commercial terms, effective EntitlementSets, approved accumulated UsageMeters, and PlatformCharge evidence. The business model deliberately keeps merchant SaaS billing separate from shopper-order Payments and preserves `PD-043`–`PD-053` as unresolved product policy.

The existing TASK-0088 architecture baseline defines modular-monolith deployment, trusted Tenant authority, one DynamoDB table per implementation module, reliable asynchronous integration, and serverless/cost guardrails. It did not define how Subscription & Billing maps into those rules.

Several architecture risks require one durable decision:

- putting plan/entitlement checks in Tenancy, JWT claims, UI state, or a shared helper would create a second commercial authority;
- letting Merchant Access, Inventory, or Ingestion read Subscription persistence would violate module ownership;
- a synchronous entitlement read alone cannot safely make a more restrictive plan change atomic with concurrent writes in another module;
- making Subscription & Billing part of the existing merchant-order Payments module would conflate two different money flows and provider semantics;
- external SaaS billing may timeout, duplicate, arrive out of order, and reconcile later, so transport failure cannot become a business conclusion;
- prematurely splitting the bounded context into a service/Lambda/provider stack or adding workflow infrastructure would increase cost and complexity before product policy exists.

## Decision

### 1. One SubscriptionBilling implementation module initially

Map the approved bounded context to one `SubscriptionBilling` implementation module with normal Domain/Application/optional Contracts/Infrastructure layers.

It initially hosts Plan/accepted terms, Subscription, EntitlementSet, approved UsageMeter, and PlatformCharge concepts while preserving their domain consistency boundaries.

The module is separate from `Tenancy`, `Payments`, `Sales`, `Inventory`, `Accounting`, and `Reporting`.

When its first Ready synchronous task exists, it runs in the existing shared `commerce-api` Lambda by default. A separate deployment requires measured or explicit security/IAM/runtime/reliability/scale/ownership pressure.

### 2. SubscriptionBilling owns trusted entitlement evaluation

A subscription-governed protected use case first obtains `TrustedTenantContext` from Tenancy/Merchant Access, then calls a producer-owned synchronous SubscriptionBilling entitlement contract when current commercial eligibility is needed before completion.

The contract returns trusted entitlement meaning and provenance, not Plan persistence objects or marketing-plan names.

The following are never entitlement authority:

- caller TenantId/plan/limit/entitlement values;
- JWT custom claims;
- browser/session cache;
- Reporting/platform-admin projections;
- provider-private state.

`TrustedTenantContext` is not expanded into a cross-request subscription snapshot. No entitlement cache is authoritative initially.

### 3. Hard-limit enforcement combines SubscriptionBilling truth with owner-local truth

For an approved hard limit:

- SubscriptionBilling supplies the current trusted entitlement/limit decision;
- the owning domain supplies current authoritative usage/state and enforces its own invariant;
- another module never reads the owner's table and SubscriptionBilling never mutates foreign aggregates to create compliance.

A more restrictive entitlement transition cannot be implemented as “change Subscription first, then let consumers catch up”. When `PD-048`/`PD-050` approve a stricter hard-limit transition, affected owning domains must participate through explicit application contracts and owner-local fencing/constraint revisions so concurrent writes cannot race through stale higher limits while the lower limit is finalized.

The transition is durable, idempotent, observable, and reconcilable. No cross-module DynamoDB transaction is used. If approved product policy cannot tolerate the safe transition behavior required by this protocol, the affected implementation remains blocked for a later architecture decision.

This mechanism does not choose downgrade timing, remediation, grandfathering, overage, or which limits are hard.

### 4. SubscriptionBilling owns one DynamoDB table when persistence is introduced

ADR-005 applies directly:

- one module-owned DynamoDB table when a Ready persistence task needs it;
- tenant-owned Subscription, EntitlementSet, UsageMeter, PlatformCharge, idempotency, provider-evidence, outbox/inbox records are tenant-scoped;
- platform-global Plan records, if approved by `PD-044`, remain SubscriptionBilling-owned behind application contracts;
- no foreign table access or application Scan;
- strong/transactional paths are used where current entitlement/provenance correctness requires them;
- GSIs/reconciliation indexes are added only for documented bounded queries.

A current Subscription and its effective EntitlementSet reference/history are kept coherent inside the module's own consistency boundary. PlatformCharge remains a separate aggregate and does not become the Subscription state machine.

### 5. Provider execution uses a provider-agnostic application port

`SubscriptionBilling.Application` owns a provider-neutral platform-billing port; `SubscriptionBilling.Infrastructure` owns any future provider adapter/protocol mapping.

No SaaS billing provider is selected by this ADR because `PD-052` remains unresolved.

When provider execution is approved:

- one logical PlatformCharge operation has a stable internal idempotency identity;
- provider-supported idempotency is used where available;
- timeout/network/ambiguous responses remain Unknown unless provider semantics conclusively prove the outcome;
- verified callback/query evidence is deduplicated and cannot regress a later known state;
- callback receipt is evidence, not automatic Subscription transition;
- unsafe retry waits for query/reconciliation rather than assuming failure;
- provider references stay inside SubscriptionBilling;
- no raw card data, CVV, provider secret, or signing secret enters domain state/logs/fixtures.

Provider ingress/function isolation and AWS secret/configuration service selection are deferred until a concrete provider task supplies requirements and cost/security analysis.

### 6. Async integration continues to use ADR-006 only for named consumers

SubscriptionBilling does not publish generic CRUD events.

A committed Subscription/Entitlement/PlatformCharge/Usage fact becomes an integration event only when a named consumer exists. Reliable publication uses the existing outbox → DynamoDB Stream → EventBridge → consumer SQS/DLQ pattern where the fact is critical/bursty.

Order-volume metering, Reporting projections, Audit evidence, and provider reconciliation resources are created only when their gated use cases are Ready.

Step Functions remains deferred until an approved business process demonstrates durable waiting/branching/callback/retry/compensation pressure.

### 7. No new always-on infrastructure

Subscription & Billing reuses the existing serverless architecture. This ADR does not introduce NAT Gateway, ALB, EC2, RDS/Aurora, Redis/ElastiCache, OpenSearch, Kafka/MSK, EKS, always-on compute, or provisioned Lambda concurrency.

A module DynamoDB table, workers, queues, event rules, scheduler, callback ingress, and secret facility are conditional resources created only by concrete Ready tasks.

## Alternatives considered

### Option A — Put subscription state/plan claims in Tenancy or `TrustedTenantContext`

Benefits:
- fewer explicit calls for protected commands.

Costs/risks:
- turns identity/tenant authority into commercial authority;
- stale token/context data can outlive plan changes;
- couples every protected request to plan schema and makes commercial changes a Tenancy concern.

Rejected.

### Option B — Let each consuming module copy/cache effective plan data and decide locally

Benefits:
- low latency and fewer synchronous calls.

Costs/risks:
- creates multiple entitlement authorities;
- stale higher limits can violate restrictive changes;
- copied marketing-plan logic spreads across modules;
- reconciliation becomes ambiguous.

Rejected as authority. Rebuildable/display projections remain allowed but non-authoritative.

### Option C — One SubscriptionBilling module with synchronous authoritative decisions and owner-local invariant checks

Benefits:
- preserves business ownership;
- keeps immediate decisions cheap inside the modular-monolith runtime;
- lets each domain keep its own resource truth;
- avoids network/service split and cross-table reads;
- gives a clear future extraction seam.

Costs/risks:
- synchronous dependency can fail closed;
- strict restrictive transitions need an explicit multi-owner fencing/reconciliation protocol rather than one ACID transaction.

Chosen.

### Option D — Put CommerceOS SaaS billing inside merchant-order Payments / Mock Payment Provider

Benefits:
- reuses payment-shaped code and provider simulator concepts.

Costs/risks:
- contradicts the domain boundary;
- conflates shopper-to-merchant payment with merchant-to-CommerceOS commercial charging;
- provider/state semantics and accounting consequences differ.

Rejected.

### Option E — Separate Subscription/Billing microservice/Lambda now

Benefits:
- narrower IAM/runtime isolation and independent scaling.

Costs/risks:
- extra deployment/network/observability complexity before scale/provider needs exist;
- duplicated operational surface and possible cold-start/latency cost;
- encourages service boundaries to follow nouns rather than measured pressure.

Rejected as the initial deployment. Future extraction remains allowed with evidence.

### Option F — Use Step Functions now for every plan change/billing flow

Benefits:
- visible orchestration and durable retry/wait primitives.

Costs/risks:
- would encode unresolved `PD-043`–`PD-053` sequences;
- adds state-transition cost and operational complexity before a durable workflow is justified;
- ordinary entitlement evaluation does not need a state machine.

Rejected for the current scope.

## Consequences

### Positive

- One authoritative commercial/entitlement boundary exists.
- Identity/Membership authorization and subscription eligibility stay distinct.
- Consumers do not need SubscriptionBilling persistence knowledge.
- Hard-limit writes combine current entitlement truth with the resource owner's current invariant.
- Restrictive plan changes have a safe concurrency/recovery direction without destructive foreign writes or distributed ACID.
- SaaS billing uncertainty has an explicit seam and does not contaminate merchant-order Payments.
- No new AWS service or standing cost is introduced merely by adding the domain.

### Negative / trade-offs

- Subscription-governed writes gain a synchronous in-process dependency and fail closed if current entitlement authority cannot be established.
- Strict lower-limit transitions are more complex than updating one record and require owner participation, durable operation state, and reconciliation.
- The shared `commerce-api` Lambda may initially have IAM grants to the SubscriptionBilling table alongside other module tables, so architecture tests and module-private infrastructure remain important.
- Provider execution cannot become Ready until product policy and concrete provider semantics are approved.

## Security and tenant impact

- Tenant scope comes only from trusted execution context for tenant-facing SubscriptionBilling operations.
- Another Tenant's subscription/charge existence is never disclosed through known IDs, provider references, cursors, or callback payloads.
- Platform administration remains a distinct execution context; no bypass flag or direct table access is approved.
- Provider webhook/callback authentication is separate from merchant authentication and must resolve verified evidence to a known module-owned PlatformCharge/Tenant scope.
- Secrets and raw provider payloads are minimized/redacted and never broadcast in integration facts.

## Reliability and idempotency impact

- Plan-change, UsageMeter source, PlatformCharge, and provider-evidence paths use stable logical identities.
- Equivalent replay returns the prior result; incompatible reuse conflicts.
- Timeouts and missing callbacks do not prove external failure.
- Out-of-order evidence cannot regress a later verified provider state.
- Restrictive plan transitions keep durable per-owner progress and reconciliation state; elapsed time never means completion.
- Async consumers follow at-least-once/idempotent ADR-006 rules.

## Cost impact

TASK-0092/ADR-008 has zero runtime cost.

Future impact is conditional:

- one additional low-volume module DynamoDB table when persistence is Ready;
- no Streams/EventBridge/SQS/worker unless a named consumer exists;
- no Scheduler unless provider reconciliation or another approved periodic job requires it;
- no provider/secret-management cost selected here;
- no always-on infrastructure.

Every concrete Ready task must add its request/storage/index/worker/provider assumptions to the existing cost model when material.

## Reversibility / migration

- Splitting Plan Catalog, Usage Metering, or Platform Billing into a separate module/service later requires a new ADR and contract/data migration but does not change the current business owner automatically.
- Extracting SubscriptionBilling into a separate Lambda/service preserves producer-owned Contracts and table ownership, then narrows IAM/network boundaries.
- Replacing DynamoDB requires a persistence ADR/migration while retaining Application contracts.
- Changing the provider replaces the Infrastructure adapter and provider-reference mapping/reconciliation logic; Domain/Application business meaning remains provider-neutral.
- Introducing an entitlement cache requires an explicit staleness/revocation/strict-transition design and cannot silently supersede authoritative evaluation.

## Validation

Dependent implementation must verify, as applicable:

- no foreign Domain/Infrastructure/table dependency;
- two-tenant isolation and no request/JWT plan authority;
- current entitlement failure does not fall back to stale projection/cache;
- duplicate plan-change/meter/charge/provider evidence yields one logical effect;
- restrictive-plan-change concurrency cannot falsely finalize a lower limit while an owner write races on the old rule;
- provider timeout/duplicate/out-of-order callback preserves Unknown/idempotent/reconciliation semantics;
- no provider outcome mutates Subscription/Tenant/Membership without an approved domain rule;
- no speculative AWS integration resource or standing-cost service is synthesized.

## References

- task: `tasks/backlog/TASK-0092-subscription-billing-technical-architecture-reconciliation.md`
- domain baseline: `docs/domains/subscription-billing.md`
- product decisions: `docs/domains/product-decisions.md`
- technical extension: `docs/architecture/subscription-billing-technical-extension.md`
- ADR-004: trusted Tenant authority
- ADR-005: DynamoDB module ownership/access patterns
- ADR-006: reliable cross-domain integration
- ADR-007: HTTP contract/command safety conventions
