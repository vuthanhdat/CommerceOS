# CommerceOS — Serverless Architecture

_Technical baseline reconciled by TASK-0088 on 2026-08-09._

## 1. Authority and detailed baseline

CommerceOS is a multi-tenant modular serverless SaaS. This document is the high-level entry point; the implementation-useful baseline is:

- [Technical architecture baseline](architecture/technical-baseline.md)
- [First-frontier contracts and trusted context](architecture/first-frontier-contracts.md)
- [Persistence ownership and access patterns](architecture/persistence-access-patterns.md)
- [Integration and AWS service matrix](architecture/integration-and-aws.md)

Accepted decisions:

- [ADR-001 — AWS CDK as Infrastructure as Code](adr/ADR-001-aws-cdk-infrastructure-as-code.md)
- [ADR-002 — Phase 0 toolchain and repository structure](adr/ADR-002-phase-0-toolchain-and-repository-structure.md)
- [ADR-003 — First-frontier modular runtime and deployment boundaries](adr/ADR-003-first-frontier-modular-runtime-and-deployment-boundaries.md)
- [ADR-004 — Trusted tenant authority and authorization boundary](adr/ADR-004-trusted-tenant-authority-and-authorization-boundary.md)
- [ADR-005 — DynamoDB module ownership and access patterns](adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md)
- [ADR-006 — Reliable cross-domain integration and deferred workflows](adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md)

The [business-domain baseline](02-business-domains.md) remains authoritative for ownership, invariants, states, and fact meaning. The [product-decision register](domains/product-decisions.md) remains authoritative for unresolved business policy. Architecture diagrams do not resolve a `PD-*` entry.

## 2. Principles

1. Business domains and approved contracts precede AWS services.
2. Start as a modular monolith, not one deployed service per bounded context.
3. A module, table, Lambda, queue, and CDK stack are different kinds of boundary.
4. Cognito authenticates; Merchant Access resolves current Tenant authority.
5. Tenant authorization and tenant-partitioned persistence are both mandatory.
6. Keep immediate local owner decisions synchronous.
7. Publish cross-domain facts durably only for named consumers.
8. Queue bursty, slow, independently retryable work.
9. Use Step Functions only for approved durable orchestration pressure.
10. Critical commands, producers, relays, and consumers are idempotent at their own boundaries.
11. Domain code has no AWS/framework/persistence dependency.
12. AWS CDK is the source of truth; Free Tier/credit constraints are architecture constraints.

## 3. Implemented architecture

The repository currently implements foundation scaffolding only:

```text
ASP.NET Core local/composition host
  GET /health
  Platform readiness module

React/Vite Storefront foundation
React/Vite Back Office foundation

CDK FoundationStack
  one bounded CloudWatch log group
  environment/cost tags
```

No business module, Lambda package, Cognito, API Gateway, DynamoDB, EventBridge, SQS, Step Functions, S3, or CloudFront application resource exists yet. The target architecture below is conditional and introduced slice by slice through Ready tasks.

## 4. First-frontier target

```text
Merchant browser
      │ Cognito access token
      ▼
API Gateway HTTP API
      │ verifies token only
      ▼
commerce-api Lambda
      │
      ├── Tenancy.ResolveTenantAuthority
      │       ├── current Tenant
      │       └── current Membership/capabilities
      │
      ├── Tenancy application use cases
      └── Catalog application use cases
              │
              ▼
      module-owned DynamoDB tables
```

### Modules

- `Tenancy` hosts Tenant Management and Merchant Access as distinct model areas so the Active Tenant + initial Active Owner onboarding result can commit atomically.
- `Catalog` is a separate module and persistence owner.
- `Platform` remains technical foundation only.
- Audit, Sales, Inventory, Payments, Procurement, Accounting, Reporting, Product Data Ingestion, and supporting contexts receive modules only when a Ready task introduces them.
- The Mock Payment Provider remains a separate external-like application/deployment when introduced.

The first runtime is one `commerce-api` Lambda. This is an operational choice, not permission for cross-module table or Domain access. Split functions/services only for measured scale, IAM/security isolation, runtime, reliability, ownership, or deployment pressure.

## 5. Trusted Tenant context

Protected requests use this trust chain:

```text
verified token subject
      +
requested Tenant selection (untrusted)
      ▼
Merchant Access current authority resolution
      ▼
TrustedTenantContext
      ▼
application capability check
      ▼
tenant-scoped repository/key
```

JWT Tenant/role claims, email, route/body/query/header TenantId, and cached Membership results are not current authority. Resolution occurs on every protected request initially so disablement, role change, and Tenant suspension affect the next resolution.

The exact Tenant-selection experience/transport is intentionally blocked by `PD-001`; the role-to-capability mapping is blocked by `PD-003`. Public storefront, onboarding, background worker, and platform-administration contexts are separate paths rather than merchant-context bypasses.

See [ADR-004](adr/ADR-004-trusted-tenant-authority-and-authorization-boundary.md).

## 6. API and application boundary

API Gateway HTTP API provides HTTPS routing, JWT validation, and throttling controls. `CommerceOS.Api` maps transport to application contracts and composes Infrastructure; it does not contain business rules.

Baseline conventions:

- consistent major-versioned JSON API;
- transport DTOs separated from commands/results/Domain entities;
- RFC 9457-compatible safe problem details with stable context codes and correlation ID;
- non-disclosing 404 for absent/cross-Tenant aggregates;
- ETag/`If-Match` preconditions for revision-sensitive resources;
- operation-scoped `Idempotency-Key` only where unsafe retry is documented;
- opaque tenant/query-bound pagination cursor; no raw DynamoDB key;
- `202 Accepted` only for a durable operation with status identity;
- timeout/unknown outcome never becomes definitive failure by transport mapping.

See [First-frontier contracts](architecture/first-frontier-contracts.md).

## 7. Persistence

DynamoDB is the initial transactional store. Use one table per implementation module, not one giant platform table or a table per entity.

Rules:

- every tenant-owned base key/query includes trusted TenantId;
- repository contracts have no unscoped tenant overload;
- documented access patterns use Get/Query/condition/bounded transaction; no Scan;
- GSIs support only approved eventual queries and never authorize or enforce uniqueness;
- conditional writes protect revisions/single-item invariants;
- bounded module transactions protect onboarding, last-owner/invitation, Product revision/reference, and SKU-claim invariants;
- another module never reads/writes the table, index, stream, or item model;
- outbox/inbox/idempotency records are module-owned technical records.

First-frontier access patterns and product gates are enumerated in [Persistence ownership and access patterns](architecture/persistence-access-patterns.md) and [ADR-005](adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md).

## 8. Synchronous and asynchronous integration

Near-term Tenancy/Catalog commands and queries are synchronous in-process application calls. EventBridge/SQS/Step Functions are not required for their CRUD/transactional behavior.

When a named cross-domain consumer exists:

```text
owner transaction
  state + outbox
        ▼
DynamoDB Stream + relay Lambda
        ▼
EventBridge business fact
        ▼
consumer-specific SQS/DLQ when critical/bursty
        ▼
consumer inbox/source key + owned effect
```

Event contracts carry `eventId`, `eventType`, `eventVersion`, tenant scope when applicable, `aggregateId`, `occurredAt`, `correlationId`, `causationId` when applicable, producer, and stable fact data. They do not serialize a database row.

Direct EventBridge-to-Lambda is limited to explicitly rebuildable low-risk projections. A database write followed by best-effort publication is not allowed for critical effects. Standard SQS is at least once; consumers remain idempotent and protect against out-of-order regression.

See [Integration and AWS matrix](architecture/integration-and-aws.md) and [ADR-006](adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md).

## 9. High-risk flows remain product-gated

The architecture deliberately does not select:

- checkout repricing/discount/quantity policy;
- reserve/pay/confirm/cancel/complete ordering;
- partial allocation/fulfillment;
- Payment cardinality/capture/retry/no-commit/unknown-stock-hold behavior;
- negative stock/backorder/adjustment floor;
- Accounting revenue/COGS/procurement/refund/adjustment triggers and account/date policy;
- Procurement correction/invoice/payment evidence semantics;
- public Tenant/Product addressing and public Catalog fields/media policy;
- Reporting formulas/date or Notification/Audit reader semantics.

The applicable `PD-*` register entry must be resolved before a route, event, state machine, index, or worker encodes one of these choices.

In particular:

- elapsed time, HTTP timeout, retry exhaustion, queue age, or DLQ placement never proves Payment failure;
- a generic “Failed” branch never authorizes stock release or Order failure;
- Accounting does not subscribe to `OrderFulfilled`, `PaymentCaptured`, `GoodsReceiptRecorded`, `StockReceived`, or another candidate until the approved policy chooses exactly one logical trigger per effect;
- `GoodsReceiptRecorded` and `StockReceived` remain different owner facts;
- external source change never mutates a canonical Product without explicit Catalog acceptance.

## 10. AWS service staging

| Service | Purpose | Decision status |
|---|---|---|
| API Gateway HTTP API | external JSON/JWT edge | accepted with first API deployment |
| Lambda | scale-to-zero API/workers | accepted; one commerce API, workers split by runtime need |
| Cognito | merchant authentication | accepted; not Membership/Tenant authority |
| DynamoDB | module persistence/conditions/transactions | accepted per module |
| DynamoDB Streams | outbox change capture | conditional with first reliable integration consumer |
| EventBridge custom bus | versioned fact routing/fan-out | conditional; no empty foundation bus |
| EventBridge Scheduler | bounded crawl/reconciliation/cleanup | conditional; dev schedules disabled/manual by default |
| SQS + DLQ | work buffering/backpressure/retry isolation | conditional per named worker/consumer |
| Step Functions Standard | durable wait/branch/callback/compensation | deferred until approved workflow ADR |
| S3 | private static origins/policy-safe objects/raw ingestion | conditional by Web/Ingestion/Files task |
| CloudFront | global static delivery/cache | conditional by Web task; never transaction authority |
| CloudWatch | bounded logs/built-in metrics/alarms | accepted; short retention, low cardinality |
| AWS CDK | infrastructure source of truth | accepted by ADR-001 |

Initial writes are single-region in `ap-southeast-1`; CloudFront is global. No global tables, NAT Gateway, ALB, EC2, RDS/Aurora, OpenSearch, ElastiCache, MSK, EKS, always-on ECS/Fargate, paid WAF, or provisioned Lambda concurrency is approved.

## 11. Stack map

```text
FoundationStack
  bounded shared technical configuration/observability only

IdentityStack
  Cognito resources when protected APIs start

CommerceStack
  API Gateway HTTP API
  commerce-api Lambda
  Tenancy/Catalog module table constructs as introduced

WebStack
  private S3 origins + CloudFront when static deployment is in scope

Integration resources
  streams/relay/event bus/queues/DLQs/workers only with a named consumer

CrawlerStack / MockPaymentStack
  only with Ready tasks for their distinct runtime boundaries
```

Stacks are CDK deployment/update groupings, not business contexts.

## 12. Frontends and objects

Storefront and Back Office remain independent React/TypeScript static applications. When deployed, private S3 origins sit behind CloudFront.

- Back Office uses Cognito and protected API contracts.
- Storefront uses a public Tenant context only after the Tenant-addressing business model is approved.
- Catalog owns the public Product projection; Storefront does not become Product authority.
- Product images/raw source payloads/exports enter S3 only through a policy-approved task and lifecycle.
- A reachable external URL is not permission to copy or republish media.
- Cached public data never authorizes checkout, price, publication, or stock reservation.

The current domain baseline does not define Tenant storefront slug/subdomain/custom-domain ownership/lifecycle. This is explicitly `DOMAIN DECISION REQUIRED` before public routes become Ready.

## 13. Product Data Ingestion

When its policy/product gates are resolved, acquisition uses a bounded dispatcher → SQS/DLQ → Lambda worker pattern. Product Data Ingestion owns acquisition, immutable source snapshots, normalization, and ImportCandidate state. Technical worker states such as queued/fetching/retry/DLQ remain telemetry.

Applying approved fields uses an explicit Catalog application contract. Ingestion never writes Catalog persistence; a candidate is Applied only after Catalog accepts the canonical effect. `PD-026` and `PD-040` still define authority/lifecycle/cardinality.

## 14. Mock Payment Provider

The provider is independently callable over HTTPS and uses idempotency, durable provider state, deterministic failures, signed callback evidence, query, and callback retry/DLQ when introduced.

CommerceOS Payments verifies/deduplicates provider evidence and owns its known/unknown outcome. Sales and Accounting never consume provider-private state or raw callbacks directly. Timeout is a caller observation; inquiry/reconciliation precedes unsafe retry or a terminal conclusion.

The final Payment model/orchestration waits for `PD-014`, `PD-016`–`PD-018`, and `PD-042` plus a dedicated ADR.

## 15. Accounting and Reporting

Accounting consumes only an approved authoritative business fact through the reliable integration pattern. Posting + logical source dedup is atomic in Accounting. A failed consumer does not roll back the committed operational source; retry/DLQ/reconciliation exposes the gap.

No accounting route exists until `PD-020`–`PD-024`, `PD-038`, and `PD-039` select recognition, valuation, account, and date policy. General Ledger/Trial Balance remain Accounting derivations.

Reporting projections are rebuildable/idempotent, expose freshness where relevant, and never authorize source transactions. Metric/date semantics wait for `PD-030` and `PD-031`.

## 16. Observability, security, and cost

Structured logs preserve safe `module`, `operation`, `outcomeCode`, request/correlation/event/causation identity, and permitted Tenant/aggregate identity. Tokens, secrets, invitation credentials, raw personal/payment/source data, and public stack traces are prohibited. Logs are not Audit records.

Use built-in metrics first. Never use TenantId/SubjectId/ProductId or another unbounded value as a custom metric dimension. Non-production logs remain short-lived; queues/workers/retries/schedules are bounded and alarmed.

Use AWS-managed/default encryption when suitable. A task needing a secret selects an AWS-managed configuration/secret facility with IAM/cost analysis; the initial public SPA client has no client secret and Cognito SMS/advanced paid features are disabled by default.

CloudFront normal Free Tier/pay-as-you-go behavior is the current assumption. Do not rely on flat-rate plans while the AWS account plan is ineligible. The [cost model](04-cost-model.md) and [Free Tier guardrails](development/13-free-tier-and-credit-guardrails.md) govern deployment.

TASK-0088 deploys no resource and changes monthly/one-off AWS cost by zero.

## 17. Evolution checkpoints

### A — First protected modular slice

- approved product gates for the selected slice;
- Tenancy/Catalog projects and expanded architecture tests;
- Cognito/API Gateway/Lambda/module table through CDK;
- trusted context/two-Tenant/idempotency/concurrency verification.

### B — First real asynchronous consumer

- producer-owned versioned contract;
- outbox/stream relay/EventBridge and consumer queue where required;
- inbox/idempotency/DLQ/redrive/reconciliation;
- bounded real-AWS verification and cost evidence.

### C — Approved durable workflow

- resolved business sequence;
- workflow ADR and transition-cost estimate;
- timeout/ambiguity/retry/compensation/operator recovery tests.

### D — Selective extraction/scale

- measured runtime/IAM/reliability/ownership pressure;
- contract/data/IAM/deployment migration ADR;
- no extraction solely because a domain boundary exists.

## 18. Technology baseline

- Backend/runtime/CDK: C# and .NET 10 under ADR-002
- Frontend: React 19 + TypeScript + Vite under ADR-002
- API: API Gateway HTTP API
- Authentication: Cognito
- Transactional data: DynamoDB
- Objects/static origins: S3
- CDN: CloudFront
- Fact routing: EventBridge when justified
- Work/backpressure: SQS + DLQ when justified
- Workflow: Step Functions only by later workflow ADR
- Observability: CloudWatch
- IaC: AWS CDK

Exact libraries/packages are selected by a refined implementation/foundation task under these boundaries; they do not change the business model.
