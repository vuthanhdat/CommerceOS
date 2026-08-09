# CommerceOS — Delivery Roadmap

## 1. Roadmap principle

CommerceOS should be implemented as a sequence of **business-capability slices**, not as a checklist of AWS services.

Each phase should answer four questions:

1. What business capability becomes usable?
2. What architectural pressure appears?
3. Which design pattern addresses it?
4. Which AWS service is justified by that pattern?

The project should deliberately start simpler than the final target architecture and refactor when the limitation becomes visible.

---

# 2. Phase 0 — Repository & AWS foundation

## Goal

Create a safe, reproducible project foundation before business code.

## Deliverables

- solution/repository structure;
- coding conventions;
- ADR folder;
- AWS CDK bootstrap;
- `dev` environment;
- tagging standard;
- AWS Budget alert;
- bounded CloudWatch retention;
- CI skeleton;
- architecture/documentation checks.

## AWS concepts

- IAM;
- CDK;
- CloudFormation;
- budgets/tags;
- CloudWatch basics.

## Exit criteria

A clean checkout can deploy and destroy the empty/skeleton environment reproducibly.

---

# 3. Phase 1 — Tenant & merchant identity

## Business capability

A business can join CommerceOS and its staff can log in with tenant-scoped permissions.

## Deliverables

- Tenant;
- BusinessProfile;
- Cognito authentication;
- merchant membership;
- Owner/Admin/Sales/Warehouse/Accountant/Viewer roles;
- trusted tenant context;
- tenant-isolation integration tests;
- basic audit records.

## Architectural lesson

Multi-tenancy is an authorization/data-isolation problem, not merely adding a `tenantId` column.

---

# 4. Phase 2 — Canonical product catalog

## Business capability

Merchant can create and publish products.

## Deliverables

- Product;
- Category;
- Brand;
- SKU;
- price;
- specifications;
- product image references;
- publish/unpublish/archive;
- merchant back-office product screens;
- DynamoDB access patterns documented.

## Architectural lesson

Model domain ownership and query patterns before trying advanced DynamoDB single-table optimization.

---

# 5. Phase 3 — First external product source

## Business capability

Merchant can paste a supported external product URL and import selected structured fields into its catalog.

## Deliverables

- Source Registry;
- choose one Vietnamese electronics source after current policy/robots review;
- manual URL import;
- crawler queue;
- crawler worker;
- DLQ;
- S3 raw snapshot with 7-day lifecycle;
- normalized external product snapshot;
- merchant import-review UI;
- parser fixtures/tests.

## Architectural lesson

Introduce SQS because crawler work is bursty/unreliable and requires backpressure, not because "the roadmap says learn SQS".

---

# 6. Phase 4 — Public storefront

## Business capability

Each tenant has a public storefront.

## Deliverables

- storefront route/tenant slug;
- product listing;
- product detail;
- category/filter basics;
- CloudFront + S3 deployment;
- public catalog caching strategy;
- image delivery strategy.

## Architectural lesson

Separate public read optimization from transactional back-office APIs.

---

# 7. Phase 5 — Cart & simple checkout

## Business capability

Customer can build a cart and place an order.

## Deliverables

- cart state;
- checkout command;
- order price snapshot;
- SalesOrder;
- SalesOrderLine;
- order status;
- checkout idempotency key;
- initial synchronous implementation.

## Architectural lesson

Start simple enough to expose where synchronous coupling becomes painful.

---

# 8. Phase 6 — Inventory

## Business capability

Merchant has real stock state and an order cannot sell the same last item twice.

## Deliverables

- Warehouse (single warehouse active initially);
- OnHand/Reserved/Available;
- stock movements;
- reserve/release/issue;
- conditional write/transaction for concurrency safety;
- low-stock read model.

## Architectural lesson

Learn concurrency and invariants with DynamoDB conditional writes/transactions.

---

# 9. Phase 7 — Mock Payment Provider v1

## Business capability

Checkout can simulate payment without real money.

## Deliverables

- independently deployed MockPaymentStack;
- create/capture/query;
- `pm_success`;
- `pm_declined`;
- payment idempotency;
- CommerceOS payment adapter;
- order confirmation/failure handling.

## Architectural lesson

Treat external integrations as boundaries even when both sides are code we own.

---

# 10. Phase 8 — Payment failure engineering

## Business capability

Orders remain correct when the payment provider is slow or ambiguous.

## Add scenarios

- HTTP 500 then success;
- timeout before commit;
- timeout after commit;
- delayed success;
- duplicate webhook;
- webhook retry;
- webhook DLQ.

## Deliverables

- signed webhook simulation;
- webhook deduplication;
- PaymentUnknown state;
- retry/backoff;
- reconciliation job;
- operational failure UI.

## Architectural lesson

Learn why retry without idempotency is dangerous and why timeout does not equal business failure.

---

# 11. Phase 9 — Order workflow orchestration

## Business capability

Checkout/payment processing has an explicit observable state machine.

## Deliverables

Evaluate and implement Step Functions for the parts that now justify orchestration:

```text
Create Order
  ↓
Reserve Inventory
  ↓
Request Payment
  ↓
Choice
 ├ success → Confirm
 ├ pending → Wait/reconcile
 └ fail    → Release Inventory
```

## Architectural lesson

Compare application-code orchestration with Step Functions based on actual complexity encountered in Phases 5–8.

---

# 12. Phase 10 — Procurement

## Business capability

Merchant can replenish stock from suppliers.

## Deliverables

- Supplier;
- PurchaseOrder;
- PO lines;
- GoodsReceipt;
- ReceiveStock integration;
- supplier invoice reference;
- purchase status flow.

## Architectural lesson

Model a second major business flow that produces inventory effects independently of sales.

---

# 13. Phase 11 — Accounting foundation

## Business capability

Merchant has a basic internal ledger.

## Deliverables

- chart of accounts;
- JournalEntry;
- JournalLine;
- debit/credit validation;
- draft/post/reverse;
- immutable posted journal;
- source transaction reference;
- General Ledger;
- Trial Balance.

## Architectural lesson

Immutable financial records require different mutation rules from CRUD product data.

---

# 14. Phase 12 — Event-driven automatic accounting

## Business capability

Committed operational activity automatically generates bookkeeping records.

## Initial posting events

- PaymentCaptured / sale recognition;
- OrderFulfilled / COGS + inventory movement according to chosen accounting policy;
- GoodsReceived / inventory + payable where policy applies;
- SupplierPaid;
- PaymentRefunded;
- selected StockAdjusted cases.

## Deliverables

- EventBridge domain event bus;
- accounting-events SQS;
- idempotent accounting worker;
- posting-rule engine/table;
- journal source-event uniqueness;
- accounting DLQ;
- reconciliation for missing expected postings.

## Architectural lesson

Experience eventual consistency between operational truth and accounting projection while preserving traceability and correctness.

---

# 15. Phase 13 — Basic finance/back-office reports

## Business capability

Merchant can understand the business from generated data.

## Deliverables

- daily revenue;
- gross-profit projection;
- inventory value;
- cash projection;
- receivable projection;
- payable projection;
- top products;
- operational exceptions;
- basic P&L projection later in the phase.

## Architectural lesson

Create read models/projections rather than repeatedly scanning transactional data.

---

# 16. Phase 14 — Crawler v2 / product-source intelligence

## Business capability

Merchant can refresh external product references and see changes over time.

## Deliverables

- second Vietnamese source;
- EventBridge Scheduler refresh;
- price snapshots;
- normalized change hashes;
- source-change events;
- parser-version metrics;
- source operational dashboard;
- kill switch per source.

Optional after account/license review:

- Amazon Creators API adapter.

## Architectural lesson

Use scheduled serverless jobs, queue backpressure, source-specific concurrency, observability, and parser versioning.

---

# 17. Phase 15 — Returns & refund workflow

## Business capability

Merchant can process return/refund consistently across sales, payment, inventory, and accounting.

## Deliverables

- return request;
- refund validation;
- mock payment refund;
- inventory return;
- accounting reversal/contra posting;
- Step Functions workflow where justified;
- retry/reconciliation.

## Architectural lesson

This is the first strong candidate for Saga/compensation thinking across multiple domains.

---

# 18. Phase 16 — Platform hardening

## Deliverables

- permission-granular authorization;
- tenant isolation audit/tests;
- API throttling;
- WAF/CDN review;
- DynamoDB max-throughput guardrails;
- reserved concurrency;
- DLQ recovery tooling;
- load test;
- failure injection;
- backup/PITR production-like profile;
- cost model updated from real CloudWatch/Cost Explorer data.

---

# 19. Phase 17 — Architecture audit

Before any microservice split, perform an architecture audit.

Questions:

- Are bounded contexts still correct?
- Which domains share code for the wrong reason?
- Are there cross-domain persistence leaks?
- Which queues/events are actually useful?
- Which event contracts are unstable?
- Where are retries unsafe?
- Where is idempotency missing?
- Which Lambda functions have become god functions?
- Are Step Functions used because they help, or because they exist?
- What dominates AWS cost?
- What dominates latency?
- What dominates operational failures?

---

# 20. Phase 18 — Selective extraction, only if justified

Possible candidates:

### Product Data Ingestion

Already independently scalable and externally constrained.

### Mock Payment

Already behaves as an external service.

### Accounting

Could be isolated later if financial integrity/deployment controls justify it.

### Reporting

Could move to separate analytics pipeline if transactional load and query load diverge.

Do **not** split Sales, Catalog, Inventory, etc. merely to claim the project uses microservices.

---

# 21. Suggested milestone grouping

## Milestone A — Sell something

Phases 0–7.

Outcome:

> A tenant can import/create a product, publish it, receive an order, reserve stock, and simulate payment.

## Milestone B — Survive failure

Phases 8–9.

Outcome:

> Checkout remains correct under retry, timeout, duplicate callback, and ambiguous payment state.

## Milestone C — Run the business

Phases 10–13.

Outcome:

> Purchase, inventory, sales, payment, and accounting form one business loop with useful reports.

## Milestone D — Become event-driven

Phases 12–15 overlap here intentionally.

Outcome:

> Cross-domain side effects use events/queues/workflows with idempotency, reconciliation, and failure handling.

## Milestone E — Production-minded SaaS

Phases 16–18.

Outcome:

> Architecture is measured, hardened, audited, costed, and only then selectively decomposed.

---

# 22. First implementation target

The first coding target should **not** be accounting, crawler discovery, or Step Functions.

Recommended first vertical slice:

```text
Tenant login
    ↓
Create product manually
    ↓
Publish product
    ↓
Public storefront displays it
```

Then add one manual external URL import.

This gives a working product surface quickly while preserving a path to the deeper architecture.
