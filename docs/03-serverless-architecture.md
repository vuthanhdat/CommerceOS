# CommerceOS — Serverless Architecture

_Technical baseline originally reconciled by TASK-0088 on 2026-08-09 and refreshed on 2026-08-10 after the product/domain decision reconciliation._

## 1. Authority and detailed baseline

CommerceOS is a multi-tenant modular serverless SaaS. This document is the high-level architecture entry point.

Implementation-useful authority is:

- [Technical architecture baseline](architecture/technical-baseline.md)
- [Product-decision technical reconciliation](architecture/product-decision-technical-reconciliation.md)
- [First-frontier contracts and trusted context](architecture/first-frontier-contracts.md)
- [Persistence ownership and access patterns](architecture/persistence-access-patterns.md)
- [Integration and AWS service matrix](architecture/integration-and-aws.md)
- [Subscription & Billing technical extension](architecture/subscription-billing-technical-extension.md)

Accepted ADRs include:

- [ADR-001 — AWS CDK as Infrastructure as Code](adr/ADR-001-aws-cdk-infrastructure-as-code.md)
- [ADR-002 — Phase 0 toolchain and repository structure](adr/ADR-002-phase-0-toolchain-and-repository-structure.md)
- [ADR-003 — First-frontier modular runtime and deployment boundaries](adr/ADR-003-first-frontier-modular-runtime-and-deployment-boundaries.md)
- [ADR-004 — Trusted tenant authority and authorization boundary](adr/ADR-004-trusted-tenant-authority-and-authorization-boundary.md)
- [ADR-005 — DynamoDB module ownership and access-pattern strategy](adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md)
- [ADR-006 — Reliable cross-domain integration and deferred workflow orchestration](adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md)
- [ADR-007 — Versioned HTTP contract and command-safety conventions](adr/ADR-007-versioned-http-contract-and-command-safety-conventions.md)
- [ADR-008 — Subscription & Billing module, entitlement decision, and provider boundary](adr/ADR-008-subscription-billing-module-entitlement-and-provider-boundary.md)
- [ADR-009 — Cross-domain onboarding completion and Trial-bootstrap recovery](adr/ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md)
- [ADR-010 — Durable order payment/allocation orchestration](adr/ADR-010-order-payment-allocation-durable-orchestration.md)

The [business-domain baseline](02-business-domains.md) and [product-decision register](domains/product-decisions.md) remain authoritative for business meaning. Architecture does not resolve missing business semantics by choosing an API, database key, AWS service, workflow state, or deployment topology.

## 2. Current decision boundary

The 2026-08-10 product/domain pass resolved the former broad first-frontier and commerce gates. Architecture now treats the approved role model, Catalog lifecycle, order/payment sequence, Accounting triggers, and Subscription/Billing lifecycle as business inputs.

Only these product-policy areas remain intentionally deferred in the current register:

- `PD-004` — exact Suspended-Tenant read/support and closure/deletion/retention/recovery/privacy semantics;
- `PD-023` — exact refund/return Accounting treatment;
- exact sellable plan prices/entitlement packages under `PD-044`.

Additional domain meaning still missing for implementation is recorded explicitly in the refreshed technical baseline, notably Storefront Tenant addressing and Accounting moving-average cost-pool scope. Builders must not guess them.

## 3. Core principles

1. Business-domain ownership and contracts precede AWS services.
2. Start as a modular monolith, not one deployed service per bounded context.
3. Module, Lambda, table, queue, state machine, and CDK stack are different boundaries.
4. Cognito authenticates; Merchant Access resolves current Tenant authority.
5. Tenant authorization and tenant-partitioned persistence are both mandatory.
6. Subscription entitlement is a separate authority from Membership/Tenant authorization.
7. Keep immediate owner decisions synchronous when the caller needs the accepted/rejected result.
8. Use durable work queues for one-worker retry/backpressure; use reliable business facts for independent consumers.
9. Use Step Functions only for a named approved process with real durable orchestration pressure.
10. Critical commands, producers, relays, workflows, and consumers are idempotent at their own boundaries.
11. Domain code has no AWS/framework/persistence/provider dependency.
12. AWS CDK is source of truth; Free Tier/credit constraints are architecture constraints.

## 4. Implemented architecture

The repository currently implements foundation scaffolding only:

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

No business module runtime, Cognito, API Gateway, business DynamoDB table, EventBridge bus, SQS application queue, Step Functions state machine, S3 application bucket, or CloudFront application distribution is implemented yet.

## 5. Target runtime and modules

```text
merchant/public clients
        │
        ▼
API Gateway HTTP API
        │ Cognito validates merchant identity where protected
        ▼
shared Lambda application runtime
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

`Tenancy` hosts Tenant Management and Merchant Access as distinct model areas because they need module-local atomic invariants such as initial Owner and last-Owner protection. `SubscriptionBilling` remains a separate module and business/persistence authority.

Merchant-order Mock Payment Provider and simulated SaaS Billing Provider are separate external-like applications when introduced.

## 6. Trusted Tenant context

Protected merchant requests use this trust chain:

```text
verified Cognito subject
      +
optional requested Tenant selector (untrusted)
      ▼
Merchant Access discovery/current authority resolution
      ▼
TrustedTenantContext
  tenantId / subjectId / membershipId / role
      ▼
domain role policy
      ▼
SubscriptionBilling.EvaluateEntitlement when governed
      ▼
owner-local invariant + tenant-scoped repository
```

One identity may hold Memberships in multiple Tenants. Merchant Access uses a strongly consistent subject-membership discovery representation, never JWT claims or an eventual Subject GSI as authority. Multiple/ambiguous memberships require intentional selection; any selected Tenant is revalidated against current Tenant + Membership state.

Public storefront, onboarding, background-worker, and platform-admin paths use distinct contexts. No bypass flag exists.

## 7. Onboarding

Successful onboarding now spans two modules:

```text
Tenancy             SubscriptionBilling
Active Tenant       30-day Trial Subscription
Active Owner        Trial EntitlementSet
```

ADR-009 preserves ownership without a cross-domain transaction:

- Tenancy atomically commits Tenant/Owner plus a durable onboarding operation and Trial-bootstrap work intent;
- the coordinator calls idempotent `StartTrialSubscription` synchronously;
- completed success is returned only after Trial acceptance;
- interruption returns durable `202 Accepted` and SQS recovery retries the same logical Trial command;
- committed Tenant/Owner is not destructively deleted as compensation.

## 8. Persistence

DynamoDB remains the initial transactional store with **one table per implementation module** when the module is introduced.

Rules:

- every tenant-owned base key/query receives trusted Tenant scope;
- no unscoped tenant repository overload;
- no cross-module table read/write or cross-domain DynamoDB transaction;
- no application `Scan`;
- GSI only for approved eventual queries, never sole authority/uniqueness/invariant enforcement;
- conditions protect revisions/single-item invariants;
- bounded same-module transactions protect local all-or-nothing invariants;
- module-owned command/idempotency/outbox/inbox/technical-operation records support retries and recovery.

The refreshed access-pattern consequences include strongly consistent membership discovery, permanent post-publication SKU claims, slug/name/source mapping claims, Inventory zero-floor conditions, Payment provider-evidence records, SubscriptionBilling entitlement/charge history, and Accounting source-posting deduplication.

## 9. Synchronous integration

Use synchronous producer-owned application contracts when the caller cannot truthfully complete without an immediate owner result and the modules share the runtime.

Examples:

- Merchant Access authority resolution;
- SubscriptionBilling entitlement evaluation;
- Catalog commands/queries and checkout validation;
- Inventory reserve command from the order process;
- Payments capture/reconciliation command/query;
- Sales confirmation/allocation command;
- Catalog import application;
- read-only platform-admin Subscription/Billing support query.

No synchronous contract authorizes foreign persistence access.

## 10. Reliable asynchronous integration

For one known retryable worker with no fan-out, use SQS. If work must survive a source commit, pair it with a transactional work-outbox and idempotent Stream relay.

For committed business facts with independent consumers, use ADR-006:

```text
owner state + outbox
      ↓ DynamoDB Stream
relay Lambda
      ↓
EventBridge
      ↓
consumer-specific SQS/DLQ
      ↓
inbox/source id + owned effect
```

Named approved routes now include:

- `OrderConfirmed` → SubscriptionBilling order-volume meter / Reporting;
- `PaymentCaptured` → Accounting and asynchronous Sales convergence;
- `OrderFulfilled` → Accounting revenue / Reporting;
- `StockIssued` → Accounting COGS;
- `GoodsReceiptRecorded` → Inventory receipt application and Accounting;
- `SupplierInvoiceRecorded` / `SupplierPaymentRecorded` / `StockAdjusted` → Accounting;
- approved privileged audit intents → Audit;
- selected facts → Notification/Reporting when a named recipient/projection exists.

`PaymentRefunded`/`StockReturned` do not route to Accounting until `PD-023` is resolved.

## 11. Order payment/allocation workflow

The approved order sequence now demonstrates a real Step Functions need.

ADR-010 selects **Step Functions Standard** for the process from accepted `OrderPlaced` through `OrderAllocated`:

```text
OrderPlaced
   ↓
all-line Inventory reservation
   ↓
full Payment capture attempt
   ├── Captured → OrderConfirmed → OrderAllocated
   ├── definitive decline/no-commit → payment retry needed
   └── OutcomeUnknown → durable reconciliation/wait
                          └── unresolved automation → NeedsAttention
```

Critical semantic guards:

- price changes require shopper reconfirmation before Order creation;
- decline is attempt-terminal only;
- Unknown blocks another attempt and keeps stock held;
- workflow timeout/retry exhaustion/DLQ/elapsed time never means Payment failure, Order cancellation, or stock release;
- state machine invokes application contracts only;
- provider-private state and Accounting are outside the state machine;
- the initial workflow does not invent automatic fulfillment/shipping behavior.

## 12. Subscription & Billing

`SubscriptionBilling` remains the sole commercial/entitlement authority for CommerceOS SaaS.

Approved technical consequences now include:

- automatic 30-day no-card Trial;
- monthly accepted billing periods/anchors;
- VND whole-đồng SaaS Money with no tax/statutory invoice/proration machinery;
- upgrade only after verified successful PlatformCharge and a fresh monthly period;
- downgrade at renewal with owner-authoritative hard-limit revalidation and no destructive remediation;
- definitive renewal failure → PastDue with seven-day grace; Unknown stays Unknown;
- Ended removes ordinary operational entitlements while approved authenticated history/export/recovery access remains;
- separate hard capability, hard counted-resource, and warning-only order-volume categories;
- dedicated simulated SaaS billing provider separate from merchant-order Payments;
- read-only platform-admin Subscription/Billing support surface.

Exact sellable package prices/entitlements remain `PD-044`-gated.

## 13. Accounting

Accounting consumes approved authoritative business facts and persists one immutable balanced logical posting per source.

Current routes:

- `PaymentCaptured` → Cash / Customer Deposits;
- `OrderFulfilled` → Customer Deposits / Sales Revenue;
- `StockIssued` → COGS / Inventory;
- `GoodsReceiptRecorded` → Inventory / GRNI;
- `SupplierInvoiceRecorded` → GRNI / Accounts Payable plus approved variance;
- `SupplierPaymentRecorded` → Accounts Payable / Cash;
- `StockAdjusted` → Inventory Adjustment gain/loss.

Posting + source dedup is atomic in Accounting. Consumer failure is recovered asynchronously and never rolls back the source domain.

Refund/return posting remains blocked by `PD-023`. Moving-weighted-average cost-pool scope also requires domain clarification before valuation persistence keys are finalized.

## 14. Files, ingestion, reporting, notification, and audit

- Merchant Product media uses CommerceOS-managed merchant uploads through `FilesMedia`; S3/CloudFront are technical delivery choices when that task is Ready; arbitrary external copy/hotlink is not supported.
- Product Data Ingestion requires platform source-policy approval + Tenant opt-in + Subscription capability when applicable; source evidence never writes Catalog persistence.
- Reporting is rebuildable/display-only and never transaction/entitlement authority.
- Notification owns per-recipient delivery/read/acknowledgement state; acknowledgement never resolves the source exception.
- Audit owns append-oriented privileged/security evidence. Successful/rejected covered actions use durable source-owned audit intents; Tenant-visible Audit is Owner/Admin only and non-disclosing.

## 15. AWS service map

| Capability | AWS mapping | Status |
|---|---|---|
| HTTPS/JWT edge | API Gateway HTTP API | accepted with first protected/public API |
| application/workers | Lambda | accepted pay-per-use runtime |
| authentication | Cognito | accepted for merchant identity only |
| transactional persistence | DynamoDB | accepted per module |
| outbox wake-up/relay | DynamoDB Streams | conditional with named durable integration/work recovery |
| business fact routing | EventBridge | conditional with named consumer |
| work/backpressure/critical consumer | SQS + DLQ | conditional with named worker/consumer |
| order durable orchestration | Step Functions Standard | accepted for ADR-010 process when Ready |
| schedules | EventBridge Scheduler | conditional for approved crawl/reconciliation jobs |
| object/static storage | S3 | conditional for Web/FilesMedia/Ingestion/export |
| CDN/static delivery | CloudFront | conditional for Web/FilesMedia |
| observability | CloudWatch | accepted with bounded retention/metrics |
| IaC | AWS CDK/CloudFormation | accepted |

Initial application writes remain single-region in `ap-southeast-1`; CloudFront is global.

No NAT Gateway, ALB, EC2, RDS/Aurora, Redis/ElastiCache, OpenSearch, MSK/Kafka, EKS, always-on ECS/Fargate, paid WAF, or provisioned Lambda concurrency is approved.

## 16. Stack direction

```text
FoundationStack
  shared bounded technical configuration/observability

IdentityStack
  Cognito when protected API begins

CommerceStack
  API Gateway HTTP API
  commerce-api Lambda
  module DynamoDB constructs as Ready tasks introduce them

WebStack
  private S3 origins + CloudFront

OrderWorkflow resources
  Step Functions Standard + task handler composition when ADR-010 task is Ready

Integration resources
  Streams/relays/EventBridge/SQS/DLQ/workers only for named contracts

CrawlerStack / MockPaymentStack / MockSaaSBillingStack
  introduced only by their Ready runtime tasks
```

Stacks are deployment/update units, never business ownership.

## 17. Security, reliability, and cost

- no client/JWT Tenant, role, plan, entitlement, price, provider, or cursor value becomes authority;
- fail closed when current Tenant/entitlement authority cannot be established;
- every external/async boundary has stable idempotency/source identity;
- provider timeout is an explicit Unknown observation until verified evidence resolves it;
- EventBridge/SQS delivery assumes duplicates/out-of-order and supports redrive/reconciliation;
- Step Functions operational failure never becomes a business conclusion;
- structured logs omit tokens, secrets, card-like data, invitation credentials, raw provider payloads, and cross-Tenant disclosure;
- use built-in metrics first and bounded log retention;
- no speculative AWS resource is provisioned;
- normal development remains near the Free Tier / low-credit envelope.

This architecture documentation update deploys nothing and changes AWS runtime cost by **$0**.

## 18. Remaining gates

Architecture intentionally leaves these non-final:

- `PD-004` suspension-detail/closure/retention/privacy lifecycle;
- `PD-023` refund/return Accounting;
- exact commercial packages/prices/entitlements under `PD-044`;
- Storefront Tenant-address business semantics;
- Accounting moving-weighted-average cost-pool scope;
- any other domain meaning not explicitly recorded by the canonical domain baseline.

The Backlog Planner must keep affected work non-Ready rather than letting a Builder select a convenient default.

## 19. Evolution checkpoints

### A — First protected modular slice

- Tenancy/Catalog projects and architecture tests;
- Cognito/API Gateway/Lambda/module tables;
- trusted tenant discovery/selection/current authority;
- two-Tenant/idempotency/concurrency tests.

### B — Cross-domain onboarding

- Tenancy registration transaction + operation/outbox;
- SubscriptionBilling Trial contract;
- SQS recovery worker under ADR-009;
- partial-failure/idempotency verification.

### C — First durable commerce workflow

- Sales/Inventory/Payments contracts;
- provider uncertainty/reconciliation;
- Step Functions Standard under ADR-010;
- transition-cost/failure-injection verification.

### D — Reliable financial/operational consumers

- producer-owned versioned facts;
- outbox/EventBridge/SQS/DLQ;
- Accounting/Reporting/SubscriptionBilling/Audit consumers;
- replay/reconciliation verification.

### E — Selective extraction

Split a module/deployment only for measured IAM/security/runtime/reliability/scale/ownership pressure. A domain boundary alone is not a reason to create a microservice.

## 20. Technology baseline

- Backend/runtime/CDK: C# / .NET 10
- Frontend: React 19 + TypeScript + Vite
- API: API Gateway HTTP API
- Authentication: Cognito
- Transactional data: DynamoDB
- Objects/static origins: S3
- CDN: CloudFront
- Fact routing: EventBridge when justified
- Work/backpressure: SQS + DLQ when justified
- Durable order workflow: Step Functions Standard for ADR-010 process
- Observability: CloudWatch
- IaC: AWS CDK

Exact libraries/packages remain implementation-task choices inside these boundaries.