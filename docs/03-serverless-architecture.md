# CommerceOS — Serverless Architecture

_Technical architecture entry point refreshed on 2026-08-10 after final resolution of `PD-004`, `PD-023`, and `PD-044`._

## 1. Authority and detailed baseline

CommerceOS is a multi-tenant modular serverless SaaS.

Implementation-useful authority:

- [Technical architecture baseline](architecture/technical-baseline.md)
- [Product-decision technical reconciliation](architecture/product-decision-technical-reconciliation.md)
- [First-frontier contracts and trusted context](architecture/first-frontier-contracts.md)
- [Persistence ownership and access patterns](architecture/persistence-access-patterns.md)
- [Integration and AWS service matrix](architecture/integration-and-aws.md)
- [Subscription & Billing technical extension](architecture/subscription-billing-technical-extension.md)

Accepted ADRs include:

- [ADR-001 — AWS CDK](adr/ADR-001-aws-cdk-infrastructure-as-code.md)
- [ADR-002 — Phase 0 toolchain/repository](adr/ADR-002-phase-0-toolchain-and-repository-structure.md)
- [ADR-003 — modular runtime/deployment boundaries](adr/ADR-003-first-frontier-modular-runtime-and-deployment-boundaries.md)
- [ADR-004 — trusted Tenant authority and authorization](adr/ADR-004-trusted-tenant-authority-and-authorization-boundary.md)
- [ADR-005 — DynamoDB module ownership/access patterns](adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md)
- [ADR-006 — reliable cross-domain integration](adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md)
- [ADR-007 — HTTP/idempotency conventions](adr/ADR-007-versioned-http-contract-and-command-safety-conventions.md)
- [ADR-008 — SubscriptionBilling boundary/catalog/entitlements/provider](adr/ADR-008-subscription-billing-module-entitlement-and-provider-boundary.md)
- [ADR-009 — onboarding Trial completion/recovery](adr/ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md)
- [ADR-010 — order payment/allocation orchestration](adr/ADR-010-order-payment-allocation-durable-orchestration.md)
- [ADR-011 — refund approval propagation/accounting corrections](adr/ADR-011-refund-approval-propagation-and-accounting-correction-integration.md)

Business/domain baseline and product-decision register remain authoritative for business meaning.

## 2. Current decision boundary

The approved MVP product-decision register currently has **no unresolved/deferred `PD-*` gate**.

Final resolved inputs include:

- `PD-004`: Active/Suspended Tenant lifecycle; platform-only reasoned suspension/reactivation; merchant read-only access while Suspended; public commerce/mutations disabled; privileged platform support read-only path; no MVP destructive lifecycle;
- `PD-023`: explicit refund request/approval/rejection; approved restockable return; append-only revenue/COGS/Cash corrections; provider-verified refund truth;
- `PD-044`: exact Trial/Starter/Growth/Business pricing/limits/automation/warning thresholds.

Independent domain gaps remain explicit rather than guessed:

- Storefront Tenant-address ownership/lifecycle/uniqueness;
- Accounting moving-weighted-average cost-pool scope;
- Category/Brand historical normalized-name reuse if needed;
- refund approval role-to-capability mapping if not supplied by refined task/domain contract;
- non-restock refunds.

## 3. Core principles

1. Business ownership/contracts precede AWS services.
2. Start as a modular monolith, not a deployed microservice per domain.
3. Module, Lambda, table, queue, workflow, and stack are different boundaries.
4. Cognito authenticates; Merchant Access resolves current Tenant authority.
5. Tenant authorization and tenant-partitioned persistence are both mandatory.
6. Suspended read-only authority is distinct from Active mutation authority.
7. Subscription EntitlementSet is separate commercial authority from Tenant/Membership.
8. Keep immediate owner decisions synchronous.
9. Use durable work queues for one-worker retry/backpressure and reliable facts for independent consumers.
10. Step Functions is only for a named process with real durable orchestration pressure.
11. Commands/producers/relays/workflows/consumers are idempotent at their own boundaries.
12. Domain code has no AWS/framework/persistence/provider dependency.
13. AWS CDK is source of truth; Free Tier/pay-per-use is an architecture constraint.

## 4. Implemented architecture

Repository currently implements foundation scaffolding only:

```text
ASP.NET Core local/composition host
  GET /health
  Platform readiness module

React/Vite Storefront foundation
React/Vite Back Office foundation

CDK FoundationStack
  bounded CloudWatch log group
  environment/cost tags
```

No business module runtime, Cognito, API Gateway, business DynamoDB table, EventBridge bus, application SQS queue, Step Functions state machine, S3 application bucket, or CloudFront distribution is implemented yet.

## 5. Target runtime and modules

```text
merchant/public clients
        │
        ▼
API Gateway HTTP API
        │ Cognito validates merchant identity where protected
        ▼
shared Lambda application runtime (`commerce-api`)
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
        └── supporting modules when Ready

module-owned DynamoDB tables
conditional workers/queues/events/workflows
```

Merchant-order Mock Payment Provider and simulated SaaS Billing Provider are separate external-like applications when introduced.

## 6. Trusted Tenant contexts

Protected merchant trust chain:

```text
verified Cognito subject
      + optional Tenant selector (untrusted)
      ▼
Merchant Access discovery/current validation
      ├── merchant read -> TrustedTenantReadContext
      └── merchant mutation -> TrustedTenantMutationContext
```

One Subject may hold Memberships in multiple Tenants. Discovery uses a strongly consistent subject-membership representation and final current Tenant/Membership validation.

### Read

Active Membership + Tenant Active/Suspended produces merchant read context. Owning domain still applies normal role visibility.

### Mutation

Active Membership + Tenant Active produces mutation context. Owning domain role policy, current Subscription entitlement where applicable, and aggregate invariant must also accept the command.

### Platform

Suspend/reactivate and privileged support use separate platform contexts. There is no bypass flag and no Owner Membership in every Tenant.

### Public

Public storefront uses separate `PublicTenantContext`. Once Tenant-address binding exists, current Tenant status must be checked; Suspended makes storefront/checkout unavailable even if Catalog projection is cached.

## 7. Onboarding and Trial

Successful onboarding spans two modules:

```text
Tenancy                 SubscriptionBilling
Active Tenant           30-day Trial Subscription
Active initial Owner    Trial EntitlementSet
```

ADR-009:

- Tenancy atomically commits local registration + durable onboarding operation/work intent;
- coordinator calls idempotent `StartTrialSubscription` synchronously;
- completed success only after Trial acceptance;
- interruption returns durable `202 Accepted` and SQS recovery retries same logical Trial command;
- no cross-domain DynamoDB transaction/destructive rollback.

Trial terms:

```text
core capabilities enabled
MaxActiveMemberships = 3
MaxWarehouses = 1
ScheduledProductIngestion = true
OrderVolumeWarningThreshold = 500
```

Trial does not auto-convert to Starter.

## 8. Subscription catalog and entitlements

Approved catalog:

| Terms | Price/month | Memberships | Warehouses | Scheduled ingestion | Order warning |
|---|---:|---:|---:|---|---:|
| Trial | n/a | 3 | 1 | Yes | 500 |
| Starter | 199,000 VND | 3 | 1 | No | 500 |
| Growth | 499,000 VND | 10 | 3 | Yes | 2,000 |
| Business | 999,000 VND | 30 | 10 | Yes | 10,000 |

ADR-008:

- SubscriptionBilling DynamoDB owns stable Plan + immutable PlanVersion + Trial terms;
- initial values are loaded by version-controlled idempotent bootstrap/migration;
- EntitlementSet is runtime authority, never Plan name/JWT/frontend constants;
- future commercial changes create new PlanVersions;
- Tenancy owns Active Membership count/guard;
- Inventory owns active-Warehouse count/guard;
- PDI checks scheduled-ingestion entitlement plus independent source policy at enable and dispatch;
- `OrderConfirmed` feeds warning meter idempotently; threshold never blocks checkout/charges overage.

No AppConfig/SSM/separate config database is introduced merely for the catalog.

## 9. Persistence

DynamoDB remains the initial transactional store with one table per implementation module when Ready.

Rules:

- trusted Tenant scope on tenant-owned keys/queries;
- no unscoped merchant repository overload;
- no cross-module table read/write/transaction;
- no application `Scan`;
- GSI only for approved eventual queries, never authority/uniqueness/invariant sole source;
- conditional writes and bounded transactions protect local invariants;
- module-owned idempotency/outbox/inbox/process records support retries/recovery.

Current notable patterns:

- Tenancy: subject discovery, last-owner guard, active Membership count guard, Active/Suspended status with no suspension TTL;
- Catalog: permanent SKU claims, current slug/name/source claims;
- SubscriptionBilling: immutable PlanVersion/Trial terms, EntitlementSets, UsageMeter, PlatformCharge;
- Sales: immutable Order + technical order process + RefundRequest/review/outbox;
- Inventory: stock/reservations/StockReturned + active-Warehouse count guard;
- Payments: capture/refund provider operations/evidence/reconciliation;
- Accounting: source claims + immutable balanced journals + own provenance/valuation state.

## 10. Synchronous integration

Use producer-owned synchronous contracts when an immediate owner answer is required.

Examples:

- merchant read/mutation authority;
- SubscriptionBilling catalog/entitlement;
- Catalog commands/checkout validation;
- Membership/Warehouse hard-limit enforcement;
- PDI scheduled-ingestion entitlement check;
- ADR-010 Inventory/Payments/Sales workflow tasks;
- platform support read-only queries.

No synchronous contract allows foreign persistence access.

## 11. Reliable asynchronous integration

For committed facts with independent consumers:

```text
owner state + outbox
      ↓ DynamoDB Stream
relay Lambda
      ↓
EventBridge
      ↓
consumer-specific SQS/DLQ
      ↓
inbox/source identity + owned effect
```

Named routes include:

- `OrderConfirmed` → SubscriptionBilling meter / Reporting;
- `PaymentCaptured` → Accounting / Sales convergence;
- `OrderFulfilled` → Accounting revenue / Reporting;
- `StockIssued` → Accounting COGS;
- `RefundApproved` → Inventory / Payments / Accounting revenue compensation;
- `StockReturned` → Accounting COGS/inventory reversal;
- `PaymentRefunded` → Accounting Customer Deposits/Cash settlement;
- Procurement source facts → Inventory/Accounting;
- covered privileged actions/rejections → Audit;
- named facts → Notification/Reporting where appropriate.

For one known retryable worker, use direct SQS with work-outbox when reliability from source commit is required.

## 12. Order payment/allocation workflow

ADR-010 selects Step Functions Standard for accepted `OrderPlaced` through `OrderAllocated`:

```text
OrderPlaced
   ↓
all-line Inventory reservation
   ↓
full Payment capture
   ├── Captured -> OrderConfirmed -> OrderAllocated
   ├── definitive no-commit/decline -> payment retry needed
   └── OutcomeUnknown -> durable Payments reconciliation/wait
                          -> NeedsAttention if automation stops unresolved
```

Technical timeout/retry exhaustion does not imply Payment failure, Order cancellation, or stock release.

State machine invokes application contracts only and never owns provider persistence or Accounting.

## 13. Refund propagation

ADR-011 selects reliable event choreography after Sales `RefundApproved`:

```text
RefundRequested
   -> merchant review
   -> RefundApproved / RefundRejected

RefundApproved
   ├── Inventory -> StockReturned
   ├── Payments -> provider refund/reconcile -> PaymentRefunded
   └── Accounting -> Dr Sales Revenue / Cr Customer Deposits

StockReturned
   -> Accounting -> Dr Inventory / Cr COGS

PaymentRefunded
   -> Accounting -> Dr Customer Deposits / Cr Cash
```

Rules:

- request/rejection causes no stock/payment/accounting effect;
- consumers are source-idempotent;
- provider timeout remains OutcomeUnknown;
- Accounting never reads producer tables and uses own original issue/journal provenance;
- no global `RefundCompleted` state is invented;
- refund does not use Step Functions under current MVP architecture.

## 14. Accounting

Accounting consumes authoritative facts and persists one immutable balanced logical posting per source.

Current routes:

- PaymentCaptured → Cash / Customer Deposits;
- OrderFulfilled → Customer Deposits / Sales Revenue;
- StockIssued → COGS / Inventory;
- RefundApproved → Sales Revenue / Customer Deposits compensation;
- StockReturned → Inventory / COGS reversal using original issue-cost provenance;
- PaymentRefunded → Customer Deposits / Cash settlement;
- GoodsReceiptRecorded → Inventory / GRNI;
- SupplierInvoiceRecorded → GRNI / AP plus approved variance;
- SupplierPaymentRecorded → AP / Cash;
- StockAdjusted → approved Inventory Adjustment gain/loss.

Posting + source dedup is atomic. Journals are immutable.

Moving-weighted-average cost-pool scope remains a domain decision before valuation keys harden.

## 15. Suspension architecture

Suspension is a Tenant eligibility state, not a mass mutation of every domain.

- Tenancy stores Active/Suspended.
- Suspended merchant mutation fails at authority resolution.
- approved merchant queries may continue under role-scoped read context.
- public commerce resolves Tenant status and fails closed while Suspended.
- platform suspend/reactivate is reasoned, revision-protected, audited.
- Membership/Subscription/Order/Accounting data is not rewritten/deleted.
- no suspended-data TTL/cleanup workflow exists in MVP.

## 16. AWS target

Use only when named Ready work introduces the need:

```text
IdentityStack
  Cognito

ApiStack
  API Gateway HTTP API
  commerce-api Lambda

Module persistence
  DynamoDB per module

Reliable integration
  DynamoDB Streams
  EventBridge custom bus
  consumer SQS/DLQ + Lambda

Order workflow
  Step Functions Standard for ADR-010 only

MockPaymentStack
  merchant-order provider

MockSaaSBillingStack
  SaaS billing provider

Crawler / FilesMedia capabilities
  SQS/Lambda/Scheduler and S3/CloudFront when Ready
```

No refund-specific workflow stack is approved.

No NAT Gateway, ALB, EC2, RDS/Aurora, Redis, OpenSearch, MSK/Kafka, EKS, provisioned Lambda concurrency, or AppConfig/SSM plan authority by default.

## 17. Cost and reliability

- serverless/pay-per-use remains default;
- no speculative resource before named Ready producer/consumer/process;
- event consumers assume at-least-once/out-of-order delivery;
- SQS/DLQ/workflow retry states are operational, never business truth;
- Step Functions transition budget required for ADR-010 implementation;
- refund reuses ADR-006 event infrastructure and adds no state-machine transition cost;
- current authority/entitlement reads are measured before considering caches that weaken freshness;
- CloudWatch logging/alarms use bounded retention and low-cardinality metrics.

## 18. Stop condition

**SERVERLESS TECHNICAL ARCHITECTURE RECONCILED WITH THE FINAL MVP PRODUCT DECISIONS.**
