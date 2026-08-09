# CommerceOS — Serverless Architecture

## 1. Architecture principles

CommerceOS is built as a **multi-tenant modular serverless SaaS** on AWS.

Initial principles:

1. Business domains first, AWS services second.
2. No always-on application server.
3. Avoid premature microservices.
4. Explicit tenant isolation.
5. Event-driven integration across domain boundaries where asynchronous consistency is acceptable.
6. Queue bursty work.
7. Use orchestration only for workflows that truly need state, waiting, branching, retry, or compensation.
8. Critical commands are idempotent.
9. Posted accounting journals are immutable.
10. Infrastructure is defined using AWS CDK.
11. Cost guardrails are part of architecture, not an afterthought.

Primary deployment region for the project is proposed as **Asia Pacific (Singapore), `ap-southeast-1`**, with CloudFront serving public content globally.

---

## 2. Why not microservices initially?

The domain model contains many boundaries, but a boundary does not imply an independent deployed service on day one.

Prematurely splitting every domain would create:

- many Lambda packages/stacks;
- duplicated operational boilerplate;
- distributed debugging overhead;
- more event/API contracts before domain understanding is mature;
- harder refactoring;
- risk of a distributed monolith.

Initial architecture therefore follows a **modular serverless** approach:

```text
Repository
  │
  ├── Storefront frontend
  ├── Back-office frontend
  ├── API/application layer
  │     ├── Tenant module
  │     ├── Catalog module
  │     ├── Sales module
  │     ├── Inventory module
  │     ├── Procurement module
  │     ├── Accounting module
  │     └── Reporting module
  │
  ├── Async workers
  ├── Mock payment provider
  ├── Crawlers
  └── Infrastructure
```

A domain may later become independently deployed when justified by:

- independent scale;
- reliability isolation;
- security/isolation needs;
- very different runtime characteristics;
- team ownership;
- deployment velocity;
- external integration boundary.

The Mock Payment Provider and Product Data Ingestion workers are good early candidates for independent deployment because their failure/runtime behavior differs from the main transactional API.

---

# 3. High-level AWS architecture

```text
                                INTERNET
                                    │
                                    ▼
                         ┌────────────────────┐
                         │    CloudFront      │
                         │ Storefront/Admin   │
                         └─────────┬──────────┘
                                   │
                          static assets/images
                                   │
                                   ▼
                                  S3

 Browser / Storefront / Admin
              │
              ▼
      ┌──────────────────┐
      │ API Gateway HTTP │
      └────────┬─────────┘
               │
        Cognito JWT Authorizer
               │
               ▼
      ┌──────────────────┐
      │ Lambda API Layer │
      └────────┬─────────┘
               │
      ┌────────┼──────────────────────────────┐
      │        │                              │
      ▼        ▼                              ▼
  DynamoDB    S3                         EventBridge
 transactional/files                    Domain Event Bus
                                             │
                        ┌────────────────────┼───────────────────┐
                        │                    │                   │
                        ▼                    ▼                   ▼
                       SQS                  SQS             direct Lambda
                 order/accounting        crawler             projection
                    workers              workers
                        │                    │
                        ▼                    ▼
                     Lambda               Lambda
                        │                    │
                        ▼                    ▼
                  Step Functions        External sites
                  where required        (policy-limited)

 External-like integration boundary:

 CommerceOS Order/Payment Adapter
              │ HTTPS
              ▼
      API Gateway / Function URL
              │
              ▼
       Mock Payment Lambda
              │
          DynamoDB state
              │
              └──────── webhook simulation ───────► CommerceOS callback

 Observability: CloudWatch logs / metrics / alarms
 Infrastructure: AWS CDK
```

---

# 4. Frontend

Two primary web applications are recommended.

## 4.1 Storefront

Public tenant storefront.

Responsibilities:

- product catalog;
- search/filter;
- product detail;
- cart;
- checkout;
- order confirmation/status.

Deployment:

```text
React/TypeScript SPA or static-capable frontend
        ↓
S3
        ↓
CloudFront
```

Public catalog responses should be cache-friendly where tenant-specific business rules allow.

## 4.2 Back office

Merchant employee application.

Modules:

- Dashboard
- Products
- Orders
- Customers
- Inventory
- Purchasing
- Accounting
- Reports
- Staff/Settings
- Import/Crawler review
- Operational failures

Authentication uses Cognito.

---

# 5. API boundary

Use **Amazon API Gateway HTTP API** rather than REST API initially because the project requires a conventional authenticated JSON API without the full REST API feature set.

Responsibilities:

- HTTPS endpoint;
- routing;
- JWT authorization;
- throttling/limits as the project grows;
- standardized request boundary.

The API layer is not allowed to derive trusted tenant authorization from arbitrary request-body `tenantId` values.

Suggested request context:

```text
Authenticated user
      │
      ├── subject/user id
      ├── membership
      ├── tenant context
      └── permissions
            │
            ▼
      application command
```

---

# 6. Lambda organization

Avoid "one Lambda per trivial method" and avoid "one Lambda for the whole world".

Initial deployment can use a small number of functions grouped by runtime behavior, for example:

```text
commerce-api
  - Tenant
  - Catalog
  - Sales
  - Inventory
  - Procurement
  - Accounting query/command endpoints

accounting-event-worker
crawler-dispatcher
crawler-worker
report-projection-worker
mock-payment-api
mock-payment-webhook-dispatcher
```

Later, hot or independently owned domains can be split without first changing the conceptual domain boundaries.

---

# 7. Persistence

## 7.1 DynamoDB

DynamoDB is the default transactional persistence choice because the project goal is serverless architecture with usage-based scale.

Do not begin with one giant unstructured table merely to practice single-table design.

Initial recommendation:

- tables/read models aligned with clear access patterns;
- explicit tenant keying;
- transactions/conditional writes for bounded invariants;
- GSIs added only for known query patterns;
- domain ownership documented.

Examples of logical persistence areas:

```text
Platform/Tenant
Catalog
Sales
Inventory
Procurement
Accounting
ExternalSourceSnapshots
MockPayment
ReadModel/Reporting
```

Physical table consolidation can be evaluated later.

## 7.2 Critical consistency examples

### Stock reservation

Use conditional write/transaction so two concurrent orders cannot both reserve the same last unit.

### Journal posting

Use transactional validation/write so:

- debit == credit;
- posting key is unique;
- posted state and lines become committed together within the chosen persistence model.

### Idempotency

Maintain idempotency records with bounded TTL where appropriate for commands such as checkout and mock payment calls.

---

# 8. S3

S3 use cases:

- frontend static assets;
- merchant product images where allowed;
- raw crawler payloads with short lifecycle;
- exports/reports;
- temporary batch artifacts;
- future analytics snapshots.

Raw crawler content should use lifecycle expiration to control storage growth.

---

# 9. EventBridge — domain event bus

EventBridge is used for cross-domain business events when asynchronous processing is acceptable.

Examples:

```text
OrderFulfilled
      │
      ├────────► Accounting projection/posting
      ├────────► Analytics
      ├────────► Notification
      └────────► Audit/read models where appropriate
```

Other examples:

- PaymentCaptured
- GoodsReceived
- StockAdjusted
- JournalPosted
- ProductSourceChanged

Rules:

1. Event name describes a business fact.
2. Event payload contains IDs and stable facts, not a database row dump.
3. Event has unique `eventId`.
4. Event includes `occurredAt`, `tenantId`, `correlationId`, and schema version where appropriate.
5. Consumers are idempotent.

Example envelope:

```json
{
  "eventId": "evt_...",
  "eventType": "OrderFulfilled",
  "eventVersion": 1,
  "occurredAt": "2026-08-09T10:00:00Z",
  "tenantId": "tenant_...",
  "correlationId": "corr_...",
  "data": {
    "orderId": "ord_..."
  }
}
```

---

# 10. SQS — asynchronous work and backpressure

SQS is used when work should be buffered/retried independently of the producer.

Initial queues:

```text
crawler-jobs
crawler-jobs-dlq

accounting-events
accounting-events-dlq

notification-jobs (later)
notification-jobs-dlq
```

Possible later queues:

- order background tasks;
- report generation;
- bulk imports;
- catalog-image processing.

Rules:

- bounded retry;
- DLQ after exhaustion;
- alarm on age/depth;
- consumers assume duplicate delivery;
- message should reference business objects rather than contain huge documents.

---

# 11. Step Functions — orchestration

Step Functions should not wrap every CRUD call.

Use when a flow needs one or more of:

- multiple dependent steps;
- branching;
- waiting;
- retry policy;
- timeout;
- compensation;
- long-running state;
- human/manual continuation later.

Good candidate workflows:

## Checkout/payment confirmation

```text
Create Order
    ↓
Reserve Stock
    ↓
Create Mock Payment
    ↓
Payment result?
 ┌───────┬───────────┐
 │       │           │
Success Pending     Failed
 │       │           │
 ▼       ▼           ▼
Confirm Wait       Release Stock
Order  callback     + Fail Order
```

Whether checkout starts with Step Functions in the MVP is an implementation decision. A simple synchronous version may be built first, then refactored once the architectural pain becomes visible.

## Refund

```text
Validate Refund
      ↓
Mock Payment Refund
      ↓
Return/Release Inventory where applicable
      ↓
Accounting Reversal
      ↓
Finalize Refund
```

## Procurement later

Longer-lived procurement can demonstrate wait states and human approval.

---

# 12. Mock Payment Provider boundary

The mock payment system should be independently deployed enough to behave like an external provider.

Desired shape:

```text
CommerceOS
   │
 HTTPS + idempotency key
   ▼
Mock Payment API
   │
   ├── immediate result
   ├── timeout/no response
   └── later signed webhook
```

This lets the project learn:

- external API failure;
- ambiguous timeout;
- webhook verification;
- duplicate webhook;
- retry;
- idempotency;
- eventual consistency;
- refund workflow.

See `06-mock-payment-provider.md`.

---

# 13. Product-data ingestion architecture

```text
EventBridge Scheduler
        │
        ▼
Crawler Dispatcher
        │
        ▼
       SQS
        │
        ▼
Crawler Lambda Worker
        │
        ├────► Amazon adapter
        ├────► The Gioi Di Dong adapter
        ├────► Dien May Xanh adapter
        └────► CellphoneS adapter
        │
        ▼
Raw payload → S3 (short retention)
        │
        ▼
Normalize
        │
        ▼
External Source Snapshot (DynamoDB)
        │
        ▼
Import Candidate / Product Mapping
```

Crawler is intentionally isolated from order-processing concurrency and failure paths.

See `05-product-data-ingestion.md`.

---

# 14. Accounting event flow

Accounting receives explicit business events; it does not query and mutate operational tables behind those domains' backs.

Example:

```text
Sales commits OrderFulfilled
        │
        ▼
     EventBridge
        │
        ▼
accounting-events SQS
        │
        ▼
Accounting Worker
        │
        ├── find posting rule
        ├── verify idempotency
        ├── create balanced journal
        └── post journal
```

If accounting processing temporarily fails, the sale remains a committed operational fact while the accounting event retries and becomes visible operationally. This deliberately teaches eventual consistency and reconciliation.

A reconciliation job should later detect committed source transactions missing an expected journal.

---

# 15. Reporting architecture

Do not build dashboards by scanning transactional tables on every page load.

Initial small-scale implementation may query efficient indexes, but the target architecture uses event-driven projections:

```text
Domain Event
     │
     ▼
Projection Worker
     │
     ▼
Read Model
     │
     ▼
Dashboard API
```

Potential read models:

- TenantDailySales
- ProductSalesSummary
- InventorySummary
- ReceivableSummary
- PayableSummary
- PlatformUsageSummary

Future analytics can export snapshots/events to S3 for Athena or other analytical processing without forcing that complexity into the MVP.

---

# 16. Observability

CloudWatch is the initial observability platform.

Every flow should support:

```text
requestId
correlationId
commandId/eventId
workflowExecutionId where applicable
tenantId (safe diagnostic use)
entityId
```

Alarms should eventually include:

- Lambda error/throttle;
- API 5xx;
- DynamoDB throttle;
- SQS old-message age;
- DLQ > 0;
- Step Functions failed execution;
- crawler failure rate;
- accounting-posting failure;
- mock-payment callback failure.

Keep log retention bounded.

---

# 17. Infrastructure as Code

Use AWS CDK.

Suggested stacks:

```text
FoundationStack
  - shared configuration
  - event bus
  - common observability

WebStack
  - S3
  - CloudFront

IdentityStack
  - Cognito

CommerceStack
  - API Gateway
  - primary Lambdas
  - DynamoDB

AsyncStack
  - SQS/DLQ
  - async workers
  - Step Functions

CrawlerStack
  - scheduler
  - crawler queues
  - crawler workers
  - raw S3 lifecycle

MockPaymentStack
  - mock provider endpoint
  - Lambda
  - state storage
  - callback dispatcher
```

These stacks are operational/IaC groupings, not necessarily domain boundaries.

---

# 18. Environments

Initial environments:

- `local` — unit/domain testing and selected local emulation;
- `dev` — personal AWS learning environment;
- `prod-like` — optional deployed environment exercising realistic policies.

Avoid duplicating expensive always-on infrastructure between environments.

---

# 19. Cost guardrails in architecture

- CloudFront flat-rate plan considered for predictable public delivery;
- no NAT Gateway in initial architecture;
- no ALB;
- no EC2;
- no always-on relational database;
- DynamoDB capacity/profile chosen intentionally;
- Lambda reserved concurrency for crawler/risky workers;
- EventBridge Scheduler cadence bounded;
- SQS retries bounded;
- S3 lifecycle for crawler raw content;
- CloudWatch log retention explicitly configured;
- AWS Budget alarm from the first deployment;
- cost tags on stacks/resources.

See `04-cost-model.md`.

---

# 20. Architecture evolution checkpoints

The architecture is expected to evolve deliberately.

### Checkpoint A — simple transactional MVP

- API Gateway
- Lambda
- DynamoDB
- Cognito
- S3/CloudFront

### Checkpoint B — async domain integration

Introduce EventBridge and SQS when operational side effects need decoupling.

### Checkpoint C — workflow orchestration

Introduce Step Functions for payment/refund/procurement flows where stateful orchestration provides clear value.

### Checkpoint D — scale/failure engineering

Add:

- DLQ recovery tooling;
- idempotency hardening;
- reconciliation;
- throttling/backpressure;
- tracing/correlation;
- cost dashboards.

### Checkpoint E — selective service extraction

Only after metrics/business pressure demonstrate a benefit, split independently scaled/deployed domains.

---

# 21. Initial technology recommendation

- Backend: C#/.NET on AWS Lambda
- Frontend: React + TypeScript
- IaC: AWS CDK
- API: API Gateway HTTP API
- Auth: Cognito
- Transactional data: DynamoDB
- Objects: S3
- CDN: CloudFront
- Events: EventBridge
- Queues: SQS
- Workflow: Step Functions
- Observability: CloudWatch

The exact framework/library versions should be chosen at implementation time rather than frozen in this product-level architecture document.

---

# 22. References

AWS service behavior/pricing changes over time. Validate current documentation before implementation.

- AWS Lambda: https://aws.amazon.com/lambda/
- API Gateway: https://aws.amazon.com/api-gateway/
- DynamoDB: https://aws.amazon.com/dynamodb/
- EventBridge: https://aws.amazon.com/eventbridge/
- SQS: https://aws.amazon.com/sqs/
- Step Functions: https://aws.amazon.com/step-functions/
- Cognito: https://aws.amazon.com/cognito/
- CloudFront: https://aws.amazon.com/cloudfront/
- AWS Serverless Lens: https://docs.aws.amazon.com/wellarchitected/latest/serverless-applications-lens/
- AWS SaaS Lens: https://docs.aws.amazon.com/wellarchitected/latest/saas-lens/
