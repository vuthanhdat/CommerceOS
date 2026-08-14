# CommerceOS — Technical Architecture Baseline

_Reconciled by TASK-0088 on 2026-08-09, extended by TASK-0092, refreshed after final product-decision resolution on 2026-08-10, and reconciled to a LocalStack-only infrastructure strategy on 2026-08-11._

## 1. Purpose and authority

This document is the canonical implementation-architecture map for CommerceOS. It translates approved business/domain ownership into module, contract, persistence, integration, runtime, tenant-security, and infrastructure boundaries.

Authority order:

1. accepted ADRs for their stated decisions;
2. this technical baseline and focused architecture documents;
3. business/domain baselines and `docs/domains/product-decisions.md` for business meaning;
4. candidate task wording last.

Focused architecture documents:

- [Product-decision technical reconciliation](product-decision-technical-reconciliation.md)
- [First-frontier contracts and trusted context](first-frontier-contracts.md)
- [Persistence ownership and access patterns](persistence-access-patterns.md)
- [Integration and AWS-style service matrix](integration-and-aws.md)
- [Subscription & Billing technical extension](subscription-billing-technical-extension.md)
- [LocalStack runtime and infrastructure lifecycle](localstack-runtime-and-lifecycle.md)

Accepted architecture decisions:

- ADR-001 — AWS CDK remains the IaC source of truth, amended by ADR-012;
- ADR-002 — Phase 0 toolchain/repository structure;
- ADR-003 — modular runtime/deployment boundaries;
- ADR-004 — trusted Tenant authority and authorization boundary;
- ADR-005 — DynamoDB module ownership/access patterns;
- ADR-006 — reliable cross-domain integration;
- ADR-007 — versioned HTTP/idempotency conventions;
- ADR-008 — SubscriptionBilling authority/catalog/provider boundary;
- ADR-009 — onboarding completion/recovery;
- ADR-010 — order payment/allocation durable orchestration;
- ADR-011 — refund propagation/accounting correction integration;
- ADR-012 — LocalStack-only infrastructure/runtime target.

A Technical Architect may choose implementation mechanisms but may not manufacture missing business meaning.

## 2. Infrastructure target decision

CommerceOS no longer uses a real AWS account for development, staging, validation, or deployment.

**LocalStack is the default and only infrastructure/runtime target** under ADR-012.

AWS-style service names in this baseline describe capability mappings for learning purposes. They do not imply AWS-hosted execution.

Consequences:

- no architecture decision may require provisioning resources in an AWS account;
- no Ready/Refined task may require AWS account/region selection, AWS IAM/OIDC federation, AWS Budget/credit controls, cloud deployment authorization, or real-cloud teardown evidence;
- application and domain code must not depend directly on LocalStack-specific APIs/configuration;
- endpoints, synthetic credentials, region, account-id placeholders, ports, feature flags, and resource prefixes are infrastructure/runtime configuration concerns;
- unsupported/partial/edition-dependent/different LocalStack behavior is documented explicitly instead of being treated as AWS-equivalent evidence;
- a real AWS fallback is not part of normal verification.

## 3. Current business/domain decision state

The current product-decision register contains no unresolved/deferred `PD-*` gate for approved MVP scope.

Resolved final inputs include the accepted Tenant Active/Suspended lifecycle, refund semantics/integration policy, and initial Trial/Starter/Growth/Business terms.

Independent domain gaps still block only affected work:

- Storefront Tenant-address implementation, using the approved Tenant-owned `/{storefrontSlug}` binding from PD-052;
- Accounting moving-weighted-average cost-pool scope;
- Category/Brand historical normalized-name reuse if implementation requires it;
- refund approval role-to-capability mapping if not supplied during task refinement;
- any future non-restock refund semantics.

Infrastructure target changes do not alter these business semantics.

## 4. Implemented baseline versus approved target

### Implemented now

The repository still contains Phase 0 foundation scaffolding only:

- technical `Platform` Domain/Application/Infrastructure projects;
- anonymous `GET /health`;
- Storefront and Back Office frontend foundations;
- CDK `FoundationStack` with bounded logging/tags;
- foundation architecture/IaC/frontend tests;
- no business modules/resources listed below are implied to exist merely because the target architecture names them.

### Approved target shape

```text
HTTP/serverless delivery capability
  -> API Gateway + Lambda mapping in LocalStack where supported
  -> shared commerce-api application runtime
      ├── Tenancy
      ├── Catalog
      ├── SubscriptionBilling
      ├── Sales
      ├── Inventory
      ├── Payments
      ├── Procurement
      ├── Accounting
      ├── Reporting
      ├── ProductDataIngestion
      ├── Notification / Audit / FilesMedia
      └── later Storefront / Customer / Pricing modules as Ready work requires

module-owned persistence
  -> DynamoDB mapping when Ready

workers/queues/events/workflows
  -> SQS/EventBridge/Step Functions mappings only for named consumers/processes
```

The target remains a **modular serverless monolith**, not microservice-per-domain.

## 5. Module and dependency boundaries

| Module | Owns | Initial runtime rule |
|---|---|---|
| `Platform` | technical readiness/composition only | shared composition |
| `Tenancy` | Tenant Management + Merchant Access | shared API runtime; owned persistence |
| `Catalog` | Catalog | shared API runtime; owned persistence |
| `SubscriptionBilling` | Plan/PlanVersion, Trial terms, Subscription, EntitlementSet, UsageMeter, PlatformCharge | shared API; named workers/provider ingress only when needed |
| `Sales` | SalesOrder, cancellation/refund-review truth | shared API + order-process composition |
| `Inventory` | Warehouse/stock/reservation/movement/return truth | shared application module + named async consumers |
| `Payments` | merchant-order Payment/capture/refund/provider interpretation | shared app + provider handlers/reconciliation |
| `Procurement` | Supplier/PO/receipt/invoice/payment evidence | shared application module |
| `Accounting` | chart, valuation/posting policy, immutable journals/ledger | fact-consumer worker + owned persistence |
| `Reporting` | rebuildable projections | async projection workers |
| `ProductDataIngestion` | source policy/run/snapshot/candidate/schedule | dispatcher/workers |
| `Notification` | delivery/read/acknowledgement | async delivery + query module |
| `Audit` | immutable audit evidence | async append + query module |
| `FilesMedia` | merchant-uploaded asset identity/metadata | shared API + object-storage boundary |
| `Storefront`, `Customer`, `Pricing` | matching domain contexts | added only when Ready |

Merchant-order Mock Payment Provider and simulated SaaS Billing Provider are separate external-like applications when introduced.

Dependency direction:

```text
Domain
  ↑
Application  ← producer-owned Contracts
  ↑
Infrastructure
  ↑
API / queue handler / workflow task / provider ingress composition
```

Rules:

- Domain has no AWS SDK, LocalStack, framework, persistence, or provider dependency.
- Application receives trusted execution context and owns use cases/ports.
- A module consumes only approved producer-owned application/contracts, never foreign Domain/Infrastructure/private table models.
- Infrastructure implements only its module ports and may use AWS SDKs configured for LocalStack.
- Delivery/composition translates transport into application contracts and contains no business-transition authority.
- Lambda/table/queue/state machine/CDK stack/LocalStack container are operational boundaries, not domain ownership boundaries.

## 6. Trusted Tenant authority

Authentication and Tenant authorization remain distinct.

```text
identity evidence
  (LocalStack Cognito where supported, otherwise test identity adapter)
      ↓
AuthenticatedPrincipal
      + optional requested Tenant selector (untrusted)
      ↓
Merchant Access current authority
      ├── ResolveTenantReadAuthority
      └── ResolveTenantMutationAuthority
```

One identity may hold Memberships in multiple Tenants. No JWT claim, emulator identity object, or client-supplied `tenantId` is Tenant authority.

`TrustedTenantReadContext` requires an Active Membership and allows Active or Suspended Tenant according to approved role visibility.

`TrustedTenantMutationContext` requires Active Tenant + Active Membership + owning-domain authorization and current entitlement where applicable.

Platform support/admin contexts remain explicit, privileged, audited, and separate from Tenant Membership.

Public storefront uses a separate `PublicTenantContext`; Suspended denies public commerce regardless of cached Catalog data.

## 7. Onboarding consistency boundary

Successful onboarding spans Tenancy and SubscriptionBilling:

```text
Active Tenant
+ Active initial Owner
+ 30-day Trial Subscription / Trial EntitlementSet
```

ADR-009 remains authoritative:

1. Tenancy commits registration state/authority plus durable Trial-bootstrap work intent.
2. Coordinator synchronously invokes idempotent `StartTrialSubscription` as fast path.
3. Completed success is returned only after Trial acceptance is proven.
4. Interrupted completion returns durable accepted/pending status and queue recovery retries the same source command.
5. No cross-module persistence transaction and no destructive Tenant rollback.

The SQS/DynamoDB mappings used to realize this run against LocalStack where supported; the consistency semantics are infrastructure-independent.

## 8. HTTP and contract rules

ADR-007 remains authoritative:

- major-versioned JSON API;
- DTOs separated from application/domain types;
- safe problem details;
- opaque identifiers/cursors;
- non-disclosing cross-Tenant not-visible behavior;
- ETag/`If-Match` for revision-sensitive mutations;
- scoped idempotency identities for unsafe retries;
- `202 Accepted` only when durable operation/status state exists;
- timeout/Unknown never mapped to definitive business failure.

Transport mappings may use API Gateway/Lambda in LocalStack or a direct local host for fast tests. The application contract remains the same.

## 9. SubscriptionBilling architecture

Plan/immutable PlanVersion + Trial terms remain SubscriptionBilling-owned. Accepted EntitlementSet snapshots remain runtime commercial authority, not Plan names or infrastructure configuration.

Hard-limit enforcement combines SubscriptionBilling current limits with owner-local authoritative counts in Tenancy/Inventory.

PDI checks `ScheduledProductIngestion` at enable/create and scheduled dispatch plus independent source-policy permission.

`OrderConfirmed` feeds UsageMeter idempotently; warning thresholds never block checkout or create overage charges.

## 10. Persistence ownership and consistency

ADR-005 remains the default:

- one DynamoDB table per implementation module when introduced, mapped to LocalStack DynamoDB for infrastructure testing;
- every tenant-owned repository/key/query receives trusted Tenant scope;
- no application `Scan`;
- indexes are never sole authority for uniqueness/invariants;
- conditional writes protect single-item invariants/revisions;
- bounded same-module transactions protect local all-or-nothing invariants;
- no cross-domain transaction or foreign-table read/write;
- module-owned idempotency/outbox/inbox/process records;
- access-pattern ledger + isolation/consistency tests for Ready persistence tasks.

If LocalStack DynamoDB differs from AWS for an edge behavior, record the limitation; do not change ownership or domain semantics to match the emulator.

## 11. Synchronous and asynchronous integration

### Synchronous contracts

Use producer-owned application contracts when the caller cannot truthfully complete without an immediate owner result and modules share the runtime.

### Durable one-worker work

Use a durable queue capability for one known retryable worker with no fan-out. Default mapping is SQS in LocalStack. Required work that must survive a source commit uses a transactional work-outbox plus relay.

### Reliable business facts

ADR-006 remains authoritative:

```text
owner state + outbox (atomic)
      ↓ owned persistence change feed
idempotent relay
      ↓ fact-routing capability
consumer-specific queue/DLQ
      ↓
inbox/source identity + owned effect
```

Preferred LocalStack mapping remains DynamoDB Streams -> EventBridge -> SQS/DLQ where supported.

Named routes include `OrderConfirmed`, `PaymentCaptured`, `OrderFulfilled`, `StockIssued`, `RefundApproved`, `StockReturned`, `PaymentRefunded`, Procurement facts, and privileged Audit evidence according to accepted ADRs/contracts.

Every side-effect consumer is at-least-once/idempotent and never reads producer persistence.

## 12. Durable order payment/allocation workflow

ADR-010 remains scoped to `OrderPlaced -> reservation -> payment/reconciliation -> OrderConfirmed -> OrderAllocated`.

Preferred durable-orchestration mapping remains Step Functions Standard semantics in LocalStack where supported.

Technical timeout/retry exhaustion never becomes Payment failure, Order cancellation, or stock release. Workflow tasks call application contracts only and never post Accounting.

If LocalStack cannot reproduce a required orchestration behavior, the task records the gap and tests the workflow/application contract at the nearest reliable layer; it does not silently claim AWS equivalence.

## 13. Refund integration

ADR-011 remains choreography after Sales approval:

```text
RefundApproved
   ├── Inventory -> StockReturned
   ├── Payments -> provider refund/reconciliation -> PaymentRefunded
   └── Accounting -> revenue compensation

StockReturned   -> Accounting COGS/inventory reversal
PaymentRefunded -> Accounting Customer Deposits/Cash clearing
```

No global `RefundCompleted` state is invented. Queue/DLQ/retry state is operational only.

## 14. Accounting architecture

Accounting remains a separate persistence owner and consumes authoritative integration facts only.

Posting + source dedup is atomic in Accounting; posted journals are immutable. Approved posting routes and correction semantics remain unchanged by the infrastructure target.

Moving-weighted-average valuation is approved business policy, but its authoritative cost-pool dimension remains a domain gap before persistence keys are finalized.

## 15. Capability-first LocalStack mapping

| Required capability | Preferred LocalStack/AWS-style mapping | Architectural rule |
|---|---|---|
| HTTP/serverless delivery | API Gateway + Lambda | transport only; direct local host allowed for fast loop |
| identity evidence | Cognito where supported, otherwise test identity adapter | never Tenant authority |
| module persistence | DynamoDB | ADR-005 ownership/conditional/transaction semantics |
| work queue / DLQ | SQS | at-least-once/idempotent consumers |
| fact routing | EventBridge | only named producer/consumer routes |
| durable workflow | Step Functions | only ADR-approved workflow scopes |
| object storage | S3 | FilesMedia domain rules unchanged |
| observability | CloudWatch-style APIs where useful | operational evidence only |
| IaC | AWS CDK + LocalStack-compatible deployment flow | repository source of truth |

No capability is selected merely because LocalStack exposes a service.

## 16. Local infrastructure lifecycle

Canonical flow:

```text
start LocalStack
   ↓
wait for readiness
   ↓
CDK synth/deploy/bootstrap
   ↓
seed idempotent technical data only as required
   ↓
smoke/integration/E2E/failure verification
   ↓
collect diagnostics
   ↓
logical reset or clean reset
   ↓
re-bootstrap/redeploy when repeatability is under test
```

No required resource may exist only through manual LocalStack setup.

Parallel worktrees derive ports/resource prefixes from task instance identity where possible.

## 17. Integration-testing strategy

Infrastructure-sensitive tests use `localstack-test` for supported capabilities.

Required scenarios include as applicable:

- tenant isolation;
- conditional/concurrent persistence behavior;
- bounded transactions;
- duplicate/out-of-order queue/event delivery;
- retry/DLQ behavior;
- event version/routing behavior;
- workflow branch/retry/wait/Unknown handling;
- object-storage integration;
- bootstrap/reset/redeploy reproducibility;
- concurrent task-instance isolation.

Unsupported, partial, behaviorally different, or edition-dependent LocalStack features must be explicitly documented. No real AWS verification fallback is required.

## 18. Configuration boundaries

Infrastructure/delivery configuration owns:

- LocalStack service endpoint/base URL;
- SDK endpoint overrides;
- synthetic access/secret values;
- region;
- account placeholder;
- resource/task-instance prefix;
- ports;
- LocalStack feature flags/edition switches;
- state/reset policy;
- diagnostics/log verbosity.

Domain/Application code may not branch on these settings.

## 19. Security/reliability invariants

- client TenantId never authorizes;
- Suspended read context cannot authorize mutation;
- platform support/admin paths remain explicit and audited;
- Subscription EntitlementSet remains sole commercial runtime authority;
- foreign modules never read/write another module persistence;
- provider timeout/Unknown never becomes failure by time passage;
- event consumers assume duplicates/out-of-order delivery;
- Accounting postings are idempotent, balanced, immutable, and traceable;
- queues/workflows/emulator logs are operational state, not business truth;
- no speculative infrastructure ahead of Ready tasks;
- LocalStack differences never justify weakening domain/application contracts.

## 20. ADR impact and supersession

- ADR-001 remains accepted for CDK but its real-AWS account/OIDC/bootstrap/cost/deployment assumptions are superseded by ADR-012.
- ADR-002 remains valid for toolchain/repository structure.
- ADR-003 through ADR-011 remain valid for module, tenant, persistence, contract, integration, entitlement, onboarding, orchestration, and refund semantics.
- Any clause in prior documentation that requires real AWS validation/account resources is superseded by ADR-012.
- A future return to real AWS requires an explicit human architecture decision and new/superseding ADR.

## 21. Backlog handoff

Backlog Planner must remove obsolete AWS account, cloud execution authorization, AWS Budget/credit, OIDC/IAM, real-cloud preview/staging, and cost-validation gates.

Current foundation remediation is:

- `TASK-0094` — deterministic LocalStack foundation lifecycle and verification;
- `TASK-0095` — CI LocalStack infrastructure verification.

Later tasks describe infrastructure capabilities first and use LocalStack service mappings only where supported.

Independent unresolved business/domain gaps remain blockers only for their affected work.

## 22. Stop condition

**TECHNICAL BASELINE READY FOR LOCALSTACK-ONLY BACKLOG RECONCILIATION.**

The infrastructure target change does not require redesign of module boundaries, trusted tenant context, persistence ownership, cross-domain contracts, accounting semantics, or approved business workflows.
