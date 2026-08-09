# CommerceOS — Non-Functional Requirements

## 1. Purpose

Although CommerceOS is a learning project, it should be designed with production-minded constraints so architectural choices can be evaluated against explicit quality attributes rather than technology preference.

These are engineering targets, not customer-facing contractual SLAs.

---

## 2. Security

### NFR-SEC-01 — Tenant isolation

A user authenticated for Tenant A must never read, mutate, search, export, or indirectly infer Tenant B's private business data.

Requirements:

- trusted tenant context is derived from authenticated membership/claims;
- do not authorize a request solely from a `tenantId` supplied by the client;
- all tenant-owned data access must require tenant scope;
- asynchronous messages/events must carry tenant context and be validated by consumers;
- platform-admin cross-tenant access must be explicit and audited.

### NFR-SEC-02 — Authentication

Merchant users authenticate through Amazon Cognito.

Initial requirements:

- secure password policy;
- email-based account lifecycle;
- short-lived access tokens;
- refresh-token lifecycle;
- no authentication secrets committed to source control;
- shopper guest checkout is supported so anonymous traffic does not require Cognito MAU.

### NFR-SEC-03 — Authorization

Initial RBAC roles:

- Owner
- Admin
- Sales
- Warehouse
- Accountant
- Viewer

Authorization must evolve toward explicit permissions so domain operations can express rules such as `order.refund`, `inventory.adjust`, and `journal.post`.

### NFR-SEC-04 — Encryption

- TLS for all external traffic;
- encryption at rest using AWS-managed encryption by default;
- secrets stored in AWS-managed configuration/secrets facilities, not code or Lambda environment values when avoidable;
- no real cardholder data is stored because payment is mock-only.

### NFR-SEC-05 — Least privilege

Each Lambda/workflow component receives only the IAM actions and resources required for its responsibilities.

### NFR-SEC-06 — Auditability

Sensitive operations must be auditable, especially:

- tenant administration;
- role changes;
- refunds;
- inventory adjustments;
- manual journals;
- journal posting/reversal;
- crawler-source configuration;
- platform-admin operations.

---

## 3. Reliability & correctness

### NFR-REL-01 — Idempotency

Commands that can be retried by clients, queues, or workflows must be idempotent where duplicate side effects are unsafe.

High-priority examples:

- checkout;
- order creation;
- stock reservation;
- payment create/capture/refund;
- accounting posting triggered by domain events;
- webhook processing.

### NFR-REL-02 — Assume at-least-once delivery

Consumers must not assume a message/event is delivered exactly once. Duplicate event delivery must not create duplicate payment, stock movement, or accounting effects.

### NFR-REL-03 — Retry policy

Retries must distinguish transient from permanent failures.

- bounded retry count;
- exponential backoff where appropriate;
- jitter where appropriate;
- no unbounded retry loops;
- exhausted asynchronous messages go to DLQ or equivalent failure storage.

### NFR-REL-04 — Failure visibility

Every failed asynchronous operation must become observable and actionable.

At minimum expose:

- queue backlog;
- DLQ message count;
- Step Functions failed executions;
- crawler failures;
- payment simulation failures;
- accounting-posting rejection.

### NFR-REL-05 — Transactional invariants

Critical invariants must be protected atomically where possible.

Examples:

- inventory cannot be reserved below permitted availability;
- a posted journal's total debits must equal total credits;
- one business source event cannot create the same accounting posting twice;
- repeated checkout request with the same idempotency key cannot create multiple orders.

DynamoDB transactions/conditional writes should be used for small bounded invariants rather than attempting cross-domain distributed ACID transactions.

---

## 4. Accounting integrity

### NFR-ACC-01 — Double-entry validity

Every posted journal entry must balance:

```text
sum(debit) == sum(credit)
```

### NFR-ACC-02 — Immutability of posted journals

A posted journal is not edited or deleted in normal business operation.

Corrections use:

```text
Original Journal
      ↓
Reversal Journal
      ↓
Correct Journal
```

### NFR-ACC-03 — Traceability

System-generated journal entries must reference their source business transaction/event, for example:

- order id;
- payment id;
- goods receipt id;
- stock movement id;
- event id/correlation id.

### NFR-ACC-04 — No false compliance claim

The platform must not represent the initial accounting module as certified tax/accounting software. Country-specific statutory requirements are explicitly outside the initial scope.

---

## 5. Availability & resilience

### NFR-AVL-01 — Availability target

Engineering target for public storefront and back-office API under normal AWS regional operation:

- target: **99.9% monthly availability** for the learning production environment;
- no contractual SLA is promised.

### NFR-AVL-02 — No single always-on application server

Core runtime must remain serverless and horizontally scalable. Avoid introducing EC2, ALB, RDS instances, or NAT Gateway merely to host application logic.

### NFR-AVL-03 — Graceful degradation

Non-critical downstream failure must not unnecessarily block the core transaction.

Examples:

- analytics failure should not fail a completed sale;
- notification failure should retry asynchronously;
- crawler failure should not affect storefront order processing.

---

## 6. Performance

Targets are measured at the API boundary excluding end-user network latency and deliberately delayed mock-payment scenarios.

### NFR-PERF-01 — API read latency

For normal tenant-scoped reads with warm execution path:

- p50 < 200 ms;
- p95 < 500 ms;
- p99 < 1,000 ms.

### NFR-PERF-02 — API write latency

For normal synchronous commands that do not wait for external workflow completion:

- p95 < 1,000 ms.

Long-running activities should return an accepted/processing state and continue asynchronously.

### NFR-PERF-03 — Storefront delivery

- static frontend assets served from CloudFront;
- product images optimized for web delivery;
- avoid large dynamic API payloads;
- use cache-friendly public catalog endpoints where safe.

---

## 7. Scalability

### NFR-SCALE-01 — Scale-to-zero/near-zero

When there is no traffic, application compute must incur no always-on server cost.

### NFR-SCALE-02 — Horizontal scaling

Traffic growth should primarily increase usage-based charges rather than require manual server provisioning.

### NFR-SCALE-03 — Backpressure

Burst workloads such as crawling, notification, or asynchronous order processing must use queues where necessary so downstream services are protected from sudden concurrency.

### NFR-SCALE-04 — Cost-bounded scaling

Use guardrails where available:

- Lambda reserved concurrency for risky workers;
- DynamoDB maximum on-demand throughput where appropriate;
- crawler concurrency limits;
- bounded queue batch size;
- API throttling;
- AWS Budgets/alerts.

---

## 8. Consistency model

CommerceOS deliberately uses different consistency guarantees by problem type.

### Strong/transactional consistency where correctness is critical

Examples:

- checkout idempotency;
- inventory reservation condition;
- journal balance/posting state;
- unique external-payment idempotency key.

### Eventual consistency where decoupling is more valuable

Examples:

- dashboard projections;
- notifications;
- analytics;
- search/read models;
- accounting projections generated from committed domain events, provided posting is idempotent and traceable.

The UI should display processing states instead of pretending all distributed effects complete synchronously.

---

## 9. Observability

### NFR-OBS-01 — Structured logs

Every backend component uses structured logs containing safe diagnostic fields such as:

- timestamp;
- level;
- service/domain;
- operation;
- request/correlation id;
- tenant id where permitted;
- entity id;
- event id;
- error code.

Never log passwords, tokens, secrets, or mock-payment secret material.

### NFR-OBS-02 — Correlation

A business flow should be traceable across API → event → queue → worker → workflow using correlation identifiers.

### NFR-OBS-03 — Metrics

Core platform metrics:

- API errors and latency;
- Lambda errors/throttles/duration;
- DynamoDB throttles;
- EventBridge failures;
- SQS age/backlog;
- DLQ depth;
- Step Functions failures;
- crawler success/failure/duration;
- payment simulation outcomes;
- journal-post failures.

### NFR-OBS-04 — Log retention

Default learning environment retention should be short and explicit to control cost, e.g. 7–14 days for high-volume application logs. Production-like profile may use longer retention for selected audit/business logs.

---

## 10. Data management

### NFR-DATA-01 — Data ownership by domain

A domain must not reach into another domain's persistence representation as an informal integration mechanism. Cross-domain integration should use explicit application contracts/events.

### NFR-DATA-02 — Source snapshots vs canonical catalog

Crawled source data must be stored separately from merchant-owned canonical products.

### NFR-DATA-03 — Raw crawl retention

Raw downloaded crawl payloads should have bounded retention to avoid unnecessary S3 growth. Proposed default for learning: 7 days; normalized source snapshots can be retained longer.

### NFR-DATA-04 — Backup/recovery profile

Learning-cost profile may use minimal backup features. A production-like profile should enable point-in-time recovery/backup for critical operational and accounting data, with its additional cost tracked explicitly.

Target production-like recovery objectives for the project:

- RPO target: <= 15 minutes for critical data;
- RTO target: <= 60 minutes for a recoverable application-level incident.

These are project goals, not an AWS SLA.

---

## 11. Maintainability

### NFR-MAIN-01 — Modular architecture

Business domains must be recognizable in code and documentation.

Avoid:

- shared `Common` domain dumping ground;
- cross-domain database writes;
- god services;
- Lambda functions containing unrelated business domains.

### NFR-MAIN-02 — Contract versioning

Events and public APIs require explicit backward-compatibility thinking. Breaking event changes should use versioned schemas or migration strategy.

### NFR-MAIN-03 — Infrastructure as Code

AWS infrastructure must be reproducible via AWS CDK. Manual console configuration should be limited to bootstrap/account tasks and documented when unavoidable.

### NFR-MAIN-04 — Automated validation

The repository should evolve to include:

- unit tests for domain rules;
- integration tests for DynamoDB/event/queue interactions;
- contract tests for event schemas;
- end-to-end happy path;
- failure-path tests for timeout/retry/duplicate delivery;
- architecture tests/lint rules where useful.

---

## 12. Cost efficiency

### NFR-COST-01 — Learning environment target

Target steady monthly AWS infrastructure cost: **approximately $0–$2/month** for very small learning usage when free allowances and cost-conscious capacity choices are used.

### NFR-COST-02 — No accidental fixed-cost services

Do not add NAT Gateway, always-on RDS/Aurora capacity, ALB, or EC2 without an explicit architecture decision record explaining why usage-based alternatives are insufficient.

### NFR-COST-03 — Cost observability

- AWS Budget alarm;
- cost tags;
- environment tags;
- log retention limits;
- no uncontrolled crawler loops;
- crawler and worker concurrency caps;
- monthly cost-model document kept current as architecture changes.

See `04-cost-model.md`.

---

## 13. Crawler responsibility & safety

External product ingestion must:

- honor applicable robots/usage policies and source terms;
- prefer official feeds/APIs when suitable;
- not bypass authentication, CAPTCHA, anti-bot controls, or access restrictions;
- use explicit rate limits and identifying configuration where appropriate;
- retain source URL and retrieval timestamp;
- not treat scraped content as owned merchant content;
- separate normalized facts from potentially copyrighted descriptive assets;
- allow a source adapter to be disabled without affecting core commerce operation.

---

## 14. Privacy

- collect only data required for the product workflow;
- do not store real payment card details;
- support deletion/anonymization design for customer-facing personal data later;
- accounting/audit records may have different retention semantics from editable customer profile data, so deletion must be designed rather than implemented as blind cascade deletion.
