# CommerceOS — Technical Architecture Baseline

_Reconciled by TASK-0088 on 2026-08-09, extended for Subscription & Billing by TASK-0092, and refreshed on 2026-08-10 after the human product-decision and Domain Architect reconciliation._

## 1. Purpose and authority

This document is the canonical implementation-architecture map for CommerceOS. It translates the approved business ownership in [the domain baseline](../02-business-domains.md) into module, contract, persistence, integration, deployment, tenant-security, and AWS boundaries.

Authority order:

1. accepted ADRs for their stated decisions;
2. this technical baseline and the focused architecture documents below;
3. the business/domain baseline and [product-decision register](../domains/product-decisions.md) for business meaning;
4. candidate task wording last.

Detailed architecture documents:

- [Product-decision technical reconciliation](product-decision-technical-reconciliation.md) — authoritative delta produced after the 2026-08-10 product/domain reconciliation;
- [First-frontier contracts and trusted context](first-frontier-contracts.md);
- [Persistence ownership and access patterns](persistence-access-patterns.md);
- [Integration and AWS service matrix](integration-and-aws.md);
- [Subscription & Billing technical architecture extension](subscription-billing-technical-extension.md).

Older detailed documents may still contain historical `PD-*` gate text. Where they conflict with the 2026-08-10 [technical reconciliation](product-decision-technical-reconciliation.md), the reconciliation and this refreshed baseline take precedence. Older mechanics remain valid where they are not contradicted.

Accepted architecture decisions:

- [ADR-003 — First-frontier modular runtime and deployment boundaries](../adr/ADR-003-first-frontier-modular-runtime-and-deployment-boundaries.md)
- [ADR-004 — Trusted tenant authority and authorization boundary](../adr/ADR-004-trusted-tenant-authority-and-authorization-boundary.md)
- [ADR-005 — DynamoDB module ownership and access-pattern strategy](../adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md)
- [ADR-006 — Reliable cross-domain integration and deferred workflow orchestration](../adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md)
- [ADR-007 — Versioned HTTP contract and command-safety conventions](../adr/ADR-007-versioned-http-contract-and-command-safety-conventions.md)
- [ADR-008 — Subscription & Billing module, entitlement decision, and provider boundary](../adr/ADR-008-subscription-billing-module-entitlement-and-provider-boundary.md)
- [ADR-009 — Cross-domain onboarding completion and Trial-bootstrap recovery](../adr/ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md)
- [ADR-010 — Durable order payment/allocation orchestration](../adr/ADR-010-order-payment-allocation-durable-orchestration.md)

A Technical Architect may choose implementation mechanisms but may not manufacture missing product meaning.

## 2. Current decision state

The current domain baseline has no entry still marked `HUMAN PRODUCT DECISION REQUIRED`. Three product-policy areas remain intentionally deferred:

- `PD-004` — exact Suspended-Tenant read/support and Tenant closure/deletion/retention/recovery/privacy semantics;
- `PD-023` — exact refund/return Accounting treatment;
- the exact sellable package/pricing/entitlement matrix portion of `PD-044`.

Architecture also identified domain meaning that is still insufficient for implementation and therefore remains explicitly blocked rather than guessed:

- Storefront Tenant-address ownership/lifecycle/uniqueness;
- Accounting moving-weighted-average cost-pool scope;
- any Category/Brand historical normalized-name reuse rule not stated by the domain baseline.

These are stop conditions for affected tasks, not implementation defaults.

## 3. Implemented baseline versus approved target

### Implemented now

The repository still contains Phase 0 foundation scaffolding only:

- technical `Platform` Domain/Application/Infrastructure projects;
- anonymous `GET /health`;
- Storefront and Back Office frontend foundations;
- CDK `FoundationStack` with bounded CloudWatch logging/tags;
- foundation architecture/IaC/frontend tests;
- no business Lambda packaging, Cognito, API Gateway, DynamoDB business tables, EventBridge, SQS, Step Functions, S3, or CloudFront application resources.

Architecture documents describe the approved target; they do not imply these resources already exist.

### Approved target shape

```text
API Gateway HTTP API
       │ Cognito JWT validation only
       ▼
commerce-api Lambda / shared application runtime
       │
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
       └── later Customer/Pricing/Storefront modules as Ready work requires

module-owned DynamoDB tables introduced only with Ready persistence tasks
worker Lambdas/queues/events/workflows introduced only for named consumers/processes
```

The target remains a **modular serverless monolith**, not a microservice-per-domain system.

## 4. Module and dependency boundaries

Initial implementation modules follow the current domain ownership:

| Module | Owns | Initial deployment rule |
|---|---|---|
| `Platform` | technical readiness/config only | shared composition |
| `Tenancy` | Tenant Management + Merchant Access | shared API runtime; one module table |
| `Catalog` | Catalog | shared API runtime; one module table |
| `SubscriptionBilling` | Plan/PlanVersion, Subscription, EntitlementSet, approved UsageMeter, PlatformCharge | shared API for synchronous surface; workers/provider ingress only when needed |
| `Sales` | SalesOrder and Sales lifecycle | shared API plus order workflow task composition |
| `Inventory` | Warehouse/stock/reservation/movement | shared application module |
| `Payments` | merchant-order Payment/attempt/provider interpretation | shared application module plus provider handlers |
| `Procurement` | Supplier/PO/receipt/invoice/payment evidence | shared application module |
| `Accounting` | chart, valuation/posting policy, journals/ledger | async posting worker when source consumers exist |
| `Reporting` | rebuildable projections | async projection workers |
| `ProductDataIngestion` | source policy/run/snapshot/candidate | crawler dispatcher/workers |
| `Notification` | delivery/read/acknowledgement | async delivery + query module |
| `Audit` | immutable audit evidence | async append + query module |
| `FilesMedia` | merchant-uploaded asset identity/metadata | shared API + object boundary |
| `Storefront`, `Customer`, `Pricing` | matching domain contexts | added only when a Ready task requires them |

Separate external-like applications are used for the merchant-order Mock Payment Provider and the dedicated simulated SaaS Billing Provider. They are not one provider boundary.

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

- Domain has no AWS/framework/persistence/provider dependency.
- Application receives trusted execution context and owns use cases/ports; it does not parse AWS/HTTP/provider records.
- A module may consume only an explicitly approved foreign `*.Contracts` boundary, never foreign Domain/Infrastructure/private table types.
- Infrastructure implements only its module's ports and persistence/adapters.
- Delivery/composition translates external transport to application contracts and contains no business transition logic.
- A Lambda, DynamoDB table, queue, state machine, and CDK stack are operational boundaries, not domain ownership boundaries.

## 5. Trusted tenant authority

Merchant authentication and tenant authorization remain distinct.

```text
Cognito access token
      │ identity evidence only
      ▼
AuthenticatedPrincipal
      + optional requested Tenant selector (untrusted)
      ▼
Merchant Access discovery / ResolveTenantAuthority
      ▼
TrustedTenantContext
      ▼
domain role policy
      ▼
SubscriptionBilling.EvaluateEntitlement when governed
      ▼
owning aggregate invariant + tenant-scoped persistence
```

After `PD-001`/`PD-003`:

- one subject may hold Memberships in multiple Tenants;
- Merchant Access owns a strongly consistent subject-membership discovery representation in the Tenancy table;
- multiple/ambiguous memberships require intentional tenant selection;
- a single discovered candidate may be auto-selected only after current Tenant/Membership validation;
- selected TenantId remains an untrusted target until validated;
- current role is one of Owner/Admin/Staff/Viewer;
- domain applications apply the approved role policy; client/JWT capability claims are never authority;
- Tenant suspension/Membership disable/role change affect the next resolution;
- no cross-request authority cache is authoritative initially.

Conceptual context:

```text
TrustedTenantContext
  tenantId
  subjectId
  membershipId
  role
  tenantRevision?
  membershipRevision?
  correlationId
```

Subscription data is deliberately not copied into this context.

Public storefront, onboarding, background-worker, and platform-admin execution contexts remain separate types with no bypass flag.

## 6. Onboarding consistency boundary

Successful onboarding now spans Tenancy and SubscriptionBilling:

```text
Active Tenant
+ Active initial Owner
+ 30-day Trial Subscription / Trial EntitlementSet
```

It is implemented under ADR-009:

1. Tenancy transaction commits the durable registration operation, Tenant, Owner, authority/discovery records, owner guard, and durable Trial-bootstrap work intent.
2. The coordinator synchronously calls idempotent `SubscriptionBilling.StartTrialSubscription` as the normal fast path.
3. Completed onboarding is returned only after Trial acceptance is proven.
4. If interrupted, return durable `202 Accepted`; a Stream-relayed SQS recovery worker retries the same Trial source command.
5. No cross-module DynamoDB transaction and no destructive Tenant rollback are used.

The process state is technical coordination; it does not become Subscription business state.

## 7. First-frontier contract rules

External JSON/API conventions remain under ADR-007:

- major-versioned JSON API;
- transport DTOs separate from application/domain types;
- RFC 9457-compatible safe problem details;
- opaque identifiers/cursors;
- non-disclosing cross-Tenant not-visible behavior;
- ETag/`If-Match` for revision-sensitive mutations;
- scoped idempotency identities for unsafe retries;
- `202 Accepted` only when a durable operation/status resource exists;
- timeout/Unknown never mapped to definitive business failure.

Current near-term contract consequences are detailed in [product-decision technical reconciliation](product-decision-technical-reconciliation.md), including tenant discovery/selection, verified-email onboarding, invitation semantics, Catalog lifecycle/SKU/slug/media/import contracts, Sales/Inventory/Payments purchase-confirmation contracts, and resolved SubscriptionBilling lifecycle/enforcement contracts.

## 8. Persistence ownership and consistency

ADR-005 remains the default persistence strategy:

- one DynamoDB table per implementation module when introduced;
- every tenant-owned repository/key/query receives trusted Tenant scope;
- no application `Scan`;
- GSI only for an approved eventual query and never as sole authorization/uniqueness/invariant authority;
- conditional writes for one-item invariants/revisions;
- bounded same-module transactions for all-or-nothing local invariants;
- no cross-domain DynamoDB transaction or foreign-table read/write;
- module-owned command/idempotency, outbox, inbox/source, and technical operation records;
- every Ready persistence task maintains an access-pattern ledger with isolation/cost/consistency tests.

Important reconciled additions:

- Tenancy subject-membership discovery uses a strongly consistent subject-partition base-table query, then final current Tenant/Membership validation.
- Catalog uses authoritative tenant-scoped claims for SKU, public slug, Category/Brand normalized-name uniqueness, and source-product mapping; first publication makes SKU immutable and Archive never releases its historical SKU claim.
- Sales stores immutable commercial snapshots, checkout idempotency, and a technical order-process reference separate from Sales business state.
- Inventory mutations preserve `OnHand >= 0`, `Reserved >= 0`, `Available = OnHand - Reserved >= 0` under concurrency; Unknown Payment does not expire a reservation.
- Payments persists one Payment obligation, immutable attempts, provider evidence/reconciliation and stable logical operation identities.
- SubscriptionBilling retains current/historical Subscription/EntitlementSet/PlatformCharge/UsageMeter truth in its own table.
- Accounting atomically applies source-idempotency with its own balanced Journal/valuation effect; it never queries producer tables.

If whole-order reservation cardinality cannot fit a bounded DynamoDB transaction, Architecture must introduce a durable Inventory reservation coordinator/compensation design rather than invent a product max-order-line limit.

## 9. Synchronous and asynchronous integration

### Synchronous contracts

Use synchronous producer-owned application contracts when the caller cannot truthfully complete without an immediate owner result and modules share the runtime. Current examples:

- authority resolution;
- subscription entitlement evaluation;
- Catalog commands/queries;
- checkout Catalog/Pricing validation;
- Inventory reservation task;
- Payments capture/reconciliation task;
- Sales confirm/allocation task;
- Catalog import application;
- read-only SubscriptionBilling platform-admin support query.

### Durable one-worker work

Use SQS for one known slow/retryable worker where fan-out is not needed. When delivery is required after a source commit, pair it with a module-owned transactional work-outbox and idempotent Stream relay.

Approved/conditional examples:

- onboarding Trial-bootstrap recovery;
- crawler acquisition;
- provider reconciliation/backpressure workers.

### Reliable business facts

When a committed owner fact has independent consumers, use ADR-006:

```text
owner state + outbox (atomic)
       ↓ DynamoDB Stream
idempotent relay
       ↓
EventBridge business fact
       ↓
consumer SQS/DLQ when side-effecting/critical
       ↓
inbox/source identity + owned effect (atomic where possible)
```

Named routes now include, when their implementation tasks are Ready:

- `OrderConfirmed` → SubscriptionBilling UsageMeter and Reporting;
- `PaymentCaptured` → Accounting and asynchronous Sales convergence;
- `OrderFulfilled` → Accounting revenue and Reporting;
- `StockIssued` → Accounting COGS;
- `GoodsReceiptRecorded` → Inventory receipt application and Accounting GRNI/inventory posting;
- `SupplierInvoiceRecorded` → Accounting;
- `SupplierPaymentRecorded` → Accounting;
- `StockAdjusted` → Accounting;
- approved privileged action/rejection audit intents → Audit;
- selected facts → Notification/Reporting only when a named recipient/projection exists.

Refund/return Accounting consumers remain absent until `PD-023` is resolved.

## 10. Durable order payment/allocation workflow

ADR-010 selects **Step Functions Standard** for the named cross-domain process from accepted `OrderPlaced` through `OrderAllocated`.

```text
OrderPlaced
   ↓
Inventory reserve all required stock
   ↓
Payments full capture attempt
   ├── Captured → Sales Confirm → Sales Allocate
   ├── definitive decline/no-commit → AwaitingPaymentRetry process state
   └── OutcomeUnknown → durable Payments reconciliation/wait
                          └── unresolved after bounded automation → NeedsAttention
```

Hard rules:

- price reconfirmation happens before Order creation/workflow start;
- the state machine invokes application contracts, never tables/provider internals;
- technical error/timeout/retry exhaustion does not cancel Order or release stock;
- `OutcomeUnknown` never becomes failure from elapsed time and stock remains held;
- decline is attempt-terminal only;
- workflow history is operational evidence, not Sales/Payment truth;
- fulfillment is not automatically invented into this first state machine;
- Accounting remains event-driven and is never a workflow task.

A deterministic Sales-owned process identity plus durable workflow-start outbox prevents duplicate execution/effects.

## 11. Subscription & Billing architecture after product reconciliation

ADR-008 remains valid as the module/trust/provider boundary. The former broad `PD-043`–`PD-053` gate is closed except exact commercial packages under `PD-044`.

Approved technical consequences:

- automatic 30-day no-card Trial through idempotent `StartTrialSubscription`;
- monthly accepted period/anchor history;
- VND whole-đồng PlatformCharge, no tax/statutory-invoice/proration machinery;
- upgrade effectivity only after verified successful PlatformCharge and fresh monthly period;
- downgrade at renewal with owner-authoritative hard-limit revalidation and no destructive remediation;
- definitive renewal failure → PastDue seven-day grace; Unknown stays Unknown;
- Ended removes ordinary operational entitlements but preserves approved authenticated read/history/export/recovery paths;
- hard capabilities, hard counted-resource growth limits, and warning-only Order volume are separate enforcement categories;
- `OrderConfirmed` may feed an idempotent current-billing-period meter; it never blocks shopper checkout or creates overage billing;
- dedicated simulated SaaS billing provider is a separate external-like application/adapter from merchant-order Payments;
- platform-admin Subscription/Billing is read-only support visibility in MVP.

Exact `Starter`/`Growth`/`Business` prices and entitlement/limit packages remain absent until `PD-044` is resolved.

## 12. Accounting architecture after product reconciliation

Accounting remains a separate module/persistence owner and consumes only authoritative integration facts.

Approved posting source routes are:

- `PaymentCaptured` → Cash / Customer Deposits;
- `OrderFulfilled` → Customer Deposits / Sales Revenue;
- `StockIssued` → COGS / Inventory;
- `GoodsReceiptRecorded` → Inventory / GRNI using accepted Procurement cost evidence;
- `SupplierInvoiceRecorded` → GRNI / Accounts Payable plus approved variance handling;
- `SupplierPaymentRecorded` → Accounts Payable / Cash;
- `StockAdjusted` → Inventory Adjustment gain/loss.

Posting + source deduplication is atomic in Accounting. Consumer failure does not roll back the source fact; DLQ/reconciliation exposes the gap.

`PaymentRefunded`/`StockReturned` do not post automatically until `PD-023` resolves the correction model.

The domain has not yet specified the authoritative cost-pool dimension for moving weighted-average valuation, so the valuation-position persistence key remains `DOMAIN DECISION REQUIRED` before implementation.

## 13. Audit, Notification, Ingestion, Reporting, and Files/Media

- **Audit:** successful/rejected privileged actions and security-significant Tenant-isolation denials use durable source-owned audit intents and idempotent Audit append; tenant-visible read is Owner/Admin only and non-disclosing.
- **Notification:** per-recipient read/ack state is module-owned; source event and notification acknowledgement remain independent.
- **Product Data Ingestion:** platform source-policy Current/enabled + Tenant opt-in + Subscription capability when applicable are independent gates; source snapshots/candidates never mutate Catalog directly.
- **Reporting:** projections are rebuildable/display-only and use approved source-fact/timezone formulas; never authorization/entitlement authority.
- **Files/Media:** merchant-uploaded assets use a FilesMedia application boundary and private S3 object storage when introduced; Catalog stores only same-Tenant asset references/associations; arbitrary external copy/hotlink is not supported.

## 14. AWS/CDK mapping

Initial region remains `ap-southeast-1`; CloudFront is global. CDK remains infrastructure source of truth.

```text
FoundationStack
  bounded shared technical observability/config

IdentityStack
  Cognito when protected API starts

CommerceStack
  API Gateway HTTP API
  commerce-api Lambda
  module DynamoDB constructs as Ready tasks introduce them

WebStack
  private S3 origins + CloudFront when web/media delivery is introduced

Integration resources
  module Streams/outbox relays
  EventBridge bus/rules
  consumer SQS/DLQ/workers
  only for named contracts/consumers

OrderWorkflow resources
  Step Functions Standard + task-handler Lambda composition
  only when ADR-010 implementation becomes Ready

CrawlerStack / MockPaymentStack / MockSaaSBillingStack
  only when corresponding Ready tasks introduce distinct runtime/failure boundaries
```

No NAT Gateway, ALB, EC2, RDS/Aurora, Redis/ElastiCache, OpenSearch, MSK/Kafka, EKS, always-on ECS/Fargate, paid WAF, or provisioned Lambda concurrency is approved.

## 15. Cost, reliability, and observability

Architecture remains Free-Tier/credit constrained:

- no speculative queue/bus/stream/state-machine/table/index;
- use built-in CloudWatch metrics first and bounded log retention;
- no high-cardinality custom metrics;
- every async consumer has retry classification, DLQ/redrive/reconciliation, and source identity;
- every provider/external call has idempotency and Unknown/reconciliation semantics;
- Step Functions tasks calculate normal/decline/Unknown transition budgets and avoid high-frequency polling;
- real-AWS dev/preview verification is bounded and ephemeral where possible;
- no new managed service is introduced by this reconciliation.

This documentation pass changes runtime AWS cost by **$0**.

## 16. Verification expectations

Dependent implementation must prove the relevant constraints mechanically:

- architecture/project dependency rules;
- Tenant A/Tenant B isolation and selector/JWT override attacks;
- multi-membership intentional selection/current resolution;
- onboarding partial-failure recovery without false completion or duplicate Trial;
- no cross-module table access/transaction;
- Catalog SKU/slug/name/source claim concurrency/lifecycle behavior;
- Inventory zero-floor/reservation invariants under races;
- duplicate/Unknown Payment behavior and provider evidence non-regression;
- duplicate workflow start/task retry cannot duplicate business effects;
- Accounting one-posting-per-source and balanced immutable journals;
- EventBridge/SQS duplicate/out-of-order/redrive behavior;
- no refund/return Accounting route before `PD-023`;
- no exact commercial package values invented before `PD-044`;
- CDK contains no speculative resource and preserves cost-safe defaults.

Repository-level verification remains:

```bash
python3 scripts/harness_check.py
```

## 17. Backlog Planner handoff

The Backlog Planner may refine tasks using the architecture decisions above and remove obsolete gates for product decisions that have been approved/reconciled. It still owns task maturity and may mark only the first safe dependency frontier Ready.

Keep affected work non-Ready where it would require:

- deferred `PD-004`, `PD-023`, or exact `PD-044` semantics;
- Storefront Tenant-address business semantics;
- Accounting moving-average cost-pool scope;
- any other material business meaning not encoded by the domain baseline;
- a materially new AWS/persistence/deployment decision not covered by an accepted ADR.

## 18. Stop condition

**TECHNICAL BASELINE READY for the reconciled 2026-08-10 domain baseline, subject to the explicit remaining domain/product gates above.**

No Builder should need to invent module ownership, trusted tenant context, onboarding recovery, current Catalog uniqueness mechanics, order payment/allocation orchestration, reliable event topology, SubscriptionBilling trust/provider separation, or the currently approved Accounting source routes.