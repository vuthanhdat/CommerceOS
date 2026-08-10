# CommerceOS — Technical Architecture Baseline

_Reconciled by TASK-0088 on 2026-08-09, extended by TASK-0092, and refreshed on 2026-08-10 after final resolution of `PD-004`, `PD-023`, and `PD-044`._

## 1. Purpose and authority

This document is the canonical implementation-architecture map for CommerceOS. It translates approved business/domain ownership into module, contract, persistence, integration, deployment, tenant-security, and AWS boundaries.

Authority order:

1. accepted ADRs for their stated decisions;
2. this technical baseline and focused architecture documents;
3. business/domain baselines and `docs/domains/product-decisions.md` for business meaning;
4. candidate task wording last.

Focused architecture documents:

- [Product-decision technical reconciliation](product-decision-technical-reconciliation.md)
- [First-frontier contracts and trusted context](first-frontier-contracts.md)
- [Persistence ownership and access patterns](persistence-access-patterns.md)
- [Integration and AWS service matrix](integration-and-aws.md)
- [Subscription & Billing technical extension](subscription-billing-technical-extension.md)

Accepted architecture decisions:

- [ADR-003](../adr/ADR-003-first-frontier-modular-runtime-and-deployment-boundaries.md) — modular runtime/deployment boundaries
- [ADR-004](../adr/ADR-004-trusted-tenant-authority-and-authorization-boundary.md) — trusted Tenant authority, Suspended read/mutation split, platform trust paths
- [ADR-005](../adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md) — DynamoDB module ownership/access patterns
- [ADR-006](../adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md) — reliable integration pattern
- [ADR-007](../adr/ADR-007-versioned-http-contract-and-command-safety-conventions.md) — HTTP/idempotency conventions
- [ADR-008](../adr/ADR-008-subscription-billing-module-entitlement-and-provider-boundary.md) — SubscriptionBilling authority/catalog/provider boundary
- [ADR-009](../adr/ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md) — onboarding completion/recovery
- [ADR-010](../adr/ADR-010-order-payment-allocation-durable-orchestration.md) — durable order payment/allocation orchestration
- [ADR-011](../adr/ADR-011-refund-approval-propagation-and-accounting-correction-integration.md) — refund approval propagation/accounting correction integration

A Technical Architect may choose implementation mechanisms but may not manufacture missing business meaning.

## 2. Current decision state

The current product-decision register contains **no unresolved/deferred `PD-*` gate for approved MVP scope**.

Resolved final inputs now include:

- `PD-004`: Active/Suspended Tenant lifecycle, platform-only reasoned suspend/reactivate, controlled Suspended read-only merchant access, privileged platform support read-only path, no MVP closure/deletion/retention timer/privacy erasure;
- `PD-023`: explicit refund request/approval/rejection, restockable return, append-only revenue/COGS/Cash correction rules, verified provider refund truth;
- `PD-044`: Trial + Starter/Growth/Business terms/prices/limits/automation/warning thresholds.

Independent domain gaps still block only affected work:

- Storefront Tenant-address ownership/lifecycle/uniqueness;
- Accounting moving-weighted-average cost-pool scope;
- Category/Brand historical normalized-name reuse if implementation requires it;
- refund approval role-to-capability mapping if not supplied during task refinement;
- any future non-restock refund semantics.

These are explicit stop conditions, not implementation defaults.

## 3. Implemented baseline versus approved target

### Implemented now

The repository still contains Phase 0 foundation scaffolding only:

- technical `Platform` Domain/Application/Infrastructure projects;
- anonymous `GET /health`;
- Storefront and Back Office frontend foundations;
- CDK `FoundationStack` with bounded CloudWatch logging/tags;
- foundation architecture/IaC/frontend tests;
- no business Lambda packaging, Cognito, API Gateway, business DynamoDB tables, EventBridge, SQS, Step Functions, S3, or CloudFront application resources.

Architecture documents describe the approved target; they do not imply those resources already exist.

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
       └── later Storefront / Customer / Pricing modules as Ready work requires

module-owned DynamoDB tables introduced only with Ready persistence tasks
workers/queues/events/workflows introduced only for named consumers/processes
```

The target remains a **modular serverless monolith**, not a microservice-per-domain system.

## 4. Module and dependency boundaries

| Module | Owns | Initial deployment rule |
|---|---|---|
| `Platform` | technical readiness/composition only | shared composition |
| `Tenancy` | Tenant Management + Merchant Access | shared API runtime; module table |
| `Catalog` | Catalog | shared API runtime; module table |
| `SubscriptionBilling` | Plan/PlanVersion, Trial terms, Subscription, EntitlementSet, UsageMeter, PlatformCharge | shared API; named workers/provider ingress only when needed |
| `Sales` | SalesOrder, cancellation/refund-review truth | shared API + order workflow task composition |
| `Inventory` | Warehouse/stock/reservation/movement/return truth | shared application module + named async consumers |
| `Payments` | merchant-order Payment/capture/refund/provider interpretation | shared app + provider handlers/reconciliation |
| `Procurement` | Supplier/PO/receipt/invoice/payment evidence | shared application module |
| `Accounting` | chart, valuation/posting policy, immutable journals/ledger | fact-consumer worker + module table |
| `Reporting` | rebuildable projections | async projection workers |
| `ProductDataIngestion` | source policy/run/snapshot/candidate/schedule | crawler dispatcher/workers |
| `Notification` | delivery/read/acknowledgement | async delivery + query module |
| `Audit` | immutable audit evidence | async append + query module |
| `FilesMedia` | merchant-uploaded asset identity/metadata | shared API + object storage boundary |
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

- Domain has no AWS/framework/persistence/provider dependency.
- Application receives trusted execution context and owns use cases/ports.
- A module consumes only approved producer-owned application/contracts, never foreign Domain/Infrastructure/private table models.
- Infrastructure implements only its module's ports.
- Delivery/composition translates transport to application contracts and contains no business-transition authority.
- Lambda/table/queue/state machine/CDK stack are operational boundaries, not domain ownership boundaries.

## 5. Trusted Tenant authority after `PD-004`

Merchant authentication and Tenant authorization remain distinct.

```text
Cognito access token
      │ identity evidence only
      ▼
AuthenticatedPrincipal
      + optional requested Tenant selector (untrusted)
      ▼
Merchant Access discovery/current authority
      ├── ResolveTenantReadAuthority
      └── ResolveTenantMutationAuthority
```

One identity may hold Memberships in multiple Tenants. Merchant Access uses a strongly consistent subject-membership discovery representation; no JWT claim or eventual Subject GSI is authority.

### Merchant read context

`TrustedTenantReadContext` requires an Active Membership and permits Tenant `Active` or `Suspended`. The owning domain then applies normal role-specific read visibility.

### Merchant mutation context

`TrustedTenantMutationContext` requires Tenant `Active`, Active Membership, role/domain authorization, and—where applicable—current Subscription entitlement plus the owning aggregate invariant.

This split prevents Suspended read authority from reaching mutation use cases.

### Platform context

- platform suspend/reactivate uses `TrustedPlatformAdminContext`, explicit reason, expected revision, and durable Audit evidence;
- platform investigation uses `TrustedPlatformSupportReadContext` plus producer-owned read-only application queries;
- no bypass flag, no Owner Membership in every Tenant, no direct foreign-table support reads.

### Public context

Public storefront uses a separate `PublicTenantContext`. Once Tenant addressing is resolved, current Tenant status must gate public storefront/checkout; Suspended denies public commerce regardless of cached Catalog data.

## 6. Onboarding consistency boundary

Successful onboarding spans Tenancy and SubscriptionBilling:

```text
Active Tenant
+ Active initial Owner
+ 30-day Trial Subscription / Trial EntitlementSet
```

ADR-009 remains authoritative:

1. Tenancy commits durable registration operation, Tenant, Owner, authority/discovery records, owner guard, and Trial-bootstrap work intent.
2. Coordinator synchronously calls idempotent `SubscriptionBilling.StartTrialSubscription` as the fast path.
3. Completed success is returned only after Trial acceptance is proven.
4. Interrupted completion returns durable `202 Accepted`; SQS recovery retries the same Trial source command.
5. No cross-module DynamoDB transaction and no destructive Tenant rollback.

Trial EntitlementSet is now concrete:

```text
CoreCommerceCapabilities = enabled
MaxActiveMemberships = 3
MaxWarehouses = 1
ScheduledProductIngestion = true
OrderVolumeWarningThreshold = 500
```

Trial is not a Starter alias and does not auto-convert on expiry.

## 7. HTTP and contract rules

ADR-007 remains authoritative:

- major-versioned JSON API;
- DTOs separated from application/domain types;
- RFC 9457-compatible safe problem details;
- opaque identifiers/cursors;
- non-disclosing cross-Tenant not-visible behavior;
- ETag/`If-Match` for revision-sensitive mutations;
- scoped idempotency identities for unsafe retries;
- `202 Accepted` only when a durable operation/status resource exists;
- timeout/Unknown never mapped to definitive business failure.

Important current contract families:

- `DiscoverMerchantTenants`;
- `ResolveTenantReadAuthority` / `ResolveTenantMutationAuthority`;
- platform `SuspendTenant(reason)` / `ReactivateTenant(reason)`;
- `StartTrialSubscription`;
- `EvaluateEntitlement` and sellable PlanVersion queries;
- Catalog lifecycle/SKU/slug/media/import contracts;
- order placement/reservation/payment/reconciliation contracts;
- Sales refund request/approve/reject contracts;
- Inventory `ApplyApprovedReturn`;
- Payments `StartApprovedRefund`/reconciliation;
- versioned integration facts under ADR-006/ADR-011.

## 8. SubscriptionBilling catalog and entitlement architecture

Resolved initial terms:

| Terms | Price/month | MaxActiveMemberships | MaxWarehouses | ScheduledProductIngestion | OrderVolumeWarningThreshold |
|---|---:|---:|---:|---|---:|
| Trial | n/a | 3 | 1 | true | 500 |
| Starter | 199,000 VND | 3 | 1 | false | 500 |
| Growth | 499,000 VND | 10 | 3 | true | 2,000 |
| Business | 999,000 VND | 30 | 10 | true | 10,000 |

Architecture under ADR-008:

- Plan/immutable PlanVersion + Trial terms live in SubscriptionBilling DynamoDB;
- a version-controlled seed artifact is applied by an idempotent bootstrap/migration command;
- no AppConfig/SSM/frontend constants are runtime commercial authority;
- accepted EntitlementSet snapshot is runtime authority, not Plan name;
- future price/limit changes create new PlanVersions and preserve accepted history.

Hard-limit enforcement combines SubscriptionBilling's current limit with owner-local authoritative counts:

- Tenancy owns Active Membership count/guard; every Active role counts;
- Inventory owns active-Warehouse count/guard;
- owner writes conditionally preserve current limit under concurrency.

PDI checks `ScheduledProductIngestion` at schedule enable/create and scheduled dispatch, plus independent source-policy permission.

`OrderConfirmed` feeds the SubscriptionBilling meter idempotently; warning thresholds never block checkout or create overage charges.

## 9. Persistence ownership and consistency

ADR-005 remains the default:

- one DynamoDB table per implementation module when introduced;
- every tenant-owned repository/key/query receives trusted Tenant scope;
- no application `Scan`;
- GSI only for approved eventual query, never sole authority/uniqueness/invariant enforcement;
- conditional writes for one-item invariants/revisions;
- bounded same-module transactions for local all-or-nothing invariants;
- no cross-domain transaction or foreign-table read/write;
- module-owned idempotency/outbox/inbox/process records;
- access-pattern ledger + isolation/cost/consistency tests for Ready persistence tasks.

Current important patterns:

- Tenancy: subject discovery, last-owner guard, Active Membership count/guard, Active/Suspended status, no business-record TTL for Suspended data;
- Catalog: permanent post-publication SKU claim, current slug claim, Category/Brand normalized-name claims, source-product mapping;
- SubscriptionBilling: immutable catalog versions, Trial terms, EntitlementSets, PlatformCharges, UsageMeter, downgrade operation;
- Sales: immutable Order snapshot, checkout idempotency, order-process state, RefundRequest/review state/outbox;
- Inventory: concurrency-safe stock/reservation/return and active-Warehouse count/guard;
- Payments: Payment + immutable attempts + refund operation/provider evidence/reconciliation;
- Accounting: source-idempotency + balanced immutable Journal + Accounting-owned provenance/valuation state.

If whole-order reservation cardinality cannot fit one bounded DynamoDB transaction, Architecture must solve it with a durable Inventory process rather than inventing a product max-order-line limit.

## 10. Synchronous and asynchronous integration

### Synchronous contracts

Use synchronous producer-owned application contracts when the caller cannot truthfully complete without an immediate owner result and modules share the runtime.

Current examples:

- merchant read/mutation authority resolution;
- SubscriptionBilling entitlement/catalog query;
- Catalog commands/checkout validation;
- Membership/Warehouse hard-limit guarded writes;
- PDI scheduled-ingestion entitlement checks;
- ADR-010 Inventory/Payments/Sales order tasks;
- platform support read-only module queries.

### Durable one-worker work

Use SQS for one known retryable worker where fan-out is not needed. If required work must survive a source commit, pair with a transactional work-outbox + Stream relay.

Examples:

- onboarding Trial-bootstrap recovery;
- crawler acquisition;
- provider reconciliation/backpressure work.

### Reliable business facts

Use ADR-006:

```text
owner state + outbox (atomic)
       ↓ DynamoDB Stream
idempotent relay
       ↓
EventBridge
       ↓
consumer-specific SQS/DLQ
       ↓
inbox/source id + owned effect
```

Named routes include:

- `OrderConfirmed` → SubscriptionBilling UsageMeter / Reporting;
- `PaymentCaptured` → Accounting / Sales convergence;
- `OrderFulfilled` → Accounting revenue / Reporting;
- `StockIssued` → Accounting COGS;
- `RefundApproved` → Inventory return / Payments refund execution / Accounting revenue compensation;
- `StockReturned` → Accounting Inventory/COGS reversal;
- `PaymentRefunded` → Accounting Customer Deposits/Cash settlement;
- Procurement source facts → Inventory/Accounting as approved;
- covered privileged actions/rejections → Audit.

Every side-effect consumer is at-least-once/idempotent and never reads producer persistence.

## 11. Durable order payment/allocation workflow

ADR-010 selects **Step Functions Standard** only for accepted `OrderPlaced` through `OrderAllocated`:

```text
OrderPlaced
   ↓
Inventory reserve all required stock
   ↓
Payments full capture attempt
   ├── Captured → Sales Confirm → Sales Allocate
   ├── definitive decline/no-commit → AwaitingPaymentRetry process state
   └── OutcomeUnknown → durable Payments reconciliation/wait
                          └── unresolved automation → NeedsAttention
```

Technical timeout/retry exhaustion never becomes Payment failure, Order cancellation, or stock release. Workflow invokes application contracts only and never posts Accounting.

## 12. Refund integration after `PD-023`

ADR-011 selects reliable event choreography after Sales approval rather than a new Step Functions workflow.

```text
RefundApproved
   ├── Inventory -> StockReturned
   ├── Payments -> provider refund/reconciliation -> PaymentRefunded
   └── Accounting -> revenue compensation

StockReturned  -> Accounting COGS/inventory reversal
PaymentRefunded -> Accounting Customer Deposits/Cash clearing
```

Rules:

- `RefundRequested`/`RefundRejected` have no stock/payment/accounting effect;
- Sales commits approval + outbox atomically;
- every consumer owns idempotency/effect truth;
- Payments timeout remains OutcomeUnknown;
- Accounting uses its own original issue/journal provenance and never reads producer tables;
- no global `RefundCompleted` state is invented;
- queue/DLQ/retry state is operational only.

## 13. Accounting architecture

Accounting remains a separate module/persistence owner and consumes authoritative integration facts only.

Approved posting routes:

- `PaymentCaptured` → Dr Cash / Cr Customer Deposits;
- `OrderFulfilled` → Dr Customer Deposits / Cr Sales Revenue;
- `StockIssued` → Dr COGS / Cr Inventory;
- `RefundApproved` → Dr Sales Revenue / Cr Customer Deposits for recognized sale;
- `StockReturned` → Dr Inventory / Cr COGS using original issue-cost provenance;
- `PaymentRefunded` → Dr Customer Deposits / Cr Cash;
- `GoodsReceiptRecorded` → Dr Inventory / Cr GRNI;
- `SupplierInvoiceRecorded` → Dr GRNI / Cr Accounts Payable plus approved variance treatment;
- `SupplierPaymentRecorded` → Dr Accounts Payable / Cr Cash;
- `StockAdjusted` → approved Inventory Adjustment gain/loss posting.

Posting + source dedup is atomic in Accounting. Posted journals are immutable.

Moving-weighted-average valuation is approved business policy, but the authoritative cost-pool dimension is still a domain gap before valuation persistence keys are finalized.

## 14. AWS/CDK mapping

No new managed service is required by the final three PD resolutions.

```text
IdentityStack
  Cognito User Pool / client

ApiStack
  API Gateway HTTP API
  commerce-api Lambda

per-module persistence stacks/resources
  DynamoDB table when Ready

IntegrationStack capabilities only when named work is Ready
  DynamoDB Streams relay
  EventBridge custom bus
  SQS/DLQ consumer queues

OrderWorkflowStack capability
  Step Functions Standard for ADR-010 only

CrawlerStack / MockPaymentStack / MockSaaSBillingStack
  only when corresponding Ready tasks introduce distinct runtime/failure boundaries

FilesMedia capability
  private S3 + controlled CloudFront when Ready
```

No NAT Gateway, ALB, EC2, RDS/Aurora, Redis/ElastiCache, OpenSearch, MSK/Kafka, EKS, always-on service, provisioned Lambda concurrency, or AppConfig/SSM Plan-catalog authority is introduced by default.

## 15. Security/reliability/cost invariants

- client TenantId never authorizes;
- Suspended read context cannot authorize mutation;
- platform support/admin paths remain explicit and audited;
- Subscription EntitlementSet remains sole commercial runtime authority;
- foreign modules never read/write another module's table;
- provider timeout/Unknown never becomes failure by time passage;
- event consumers assume duplicates/out-of-order delivery;
- Accounting source postings are idempotent, balanced, immutable, and traceable;
- queues/workflows/alarms are operational state, not business truth;
- add AWS services only for named problems with pay-per-use/cost rationale;
- no speculative resources ahead of Ready tasks.

## 16. Backlog handoff

Backlog Planner may now remove obsolete `PD-004`, `PD-023`, and `PD-044` gates from affected tasks after reconciling this architecture.

It still must keep tasks non-Ready where independent domain gaps above would force a Builder to invent business meaning.

## 17. Stop condition

**TECHNICAL BASELINE READY FOR BACKLOG RECONCILIATION.**

The current approved MVP no longer requires Builders to choose Tenant suspension mechanics, refund cross-domain integration, or initial Plan catalog/entitlement infrastructure semantics.
