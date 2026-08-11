# CommerceOS — Delivery Roadmap

> **Planning reconciliation note:** This roadmap is directional. The domain baseline, product-decision register, technical architecture baseline, accepted ADRs, canonical V2 backlog, and individually refined task maturity take precedence. A phase number or named technology here never makes a task Ready or resolves a product/architecture decision.

## 1. Roadmap principle

CommerceOS is implemented as **business-capability slices**, not as a checklist of cloud services.

Each phase asks:

1. What business capability becomes usable?
2. What architectural pressure appears?
3. Which capability/pattern addresses it?
4. Which LocalStack-supported AWS-style service mapping, if any, helps teach that capability?

Under ADR-012, LocalStack is the only infrastructure/runtime target. No roadmap phase requires a real AWS account, AWS IAM/OIDC deployment, AWS Budget/Free Tier controls, real-cloud staging, or cloud validation.

---

## 2. Phase 0 — Repository & LocalStack foundation

### Goal

Create a safe, reproducible project foundation before business code.

### Directional deliverables

- solution/repository structure;
- coding conventions and ADRs;
- AWS CDK source-of-truth infrastructure;
- LocalStack runtime profiles;
- deterministic start/readiness/bootstrap/reset/redeploy flow;
- task/worktree resource isolation;
- CI/harness skeleton;
- architecture/documentation checks;
- explicit LocalStack limitations policy.

### Exit intent

A clean checkout can synthesize the foundation and deterministically bootstrap, inspect, reset, and redeploy it against LocalStack without hidden manual resources.

Canonical near-frontier tasks: `TASK-0093`–`TASK-0095`.

---

## 3. Phase 1 — Tenant & merchant identity

### Business capability

A business can join CommerceOS and merchant staff can operate with trusted Tenant-scoped authority.

### Architectural lesson

Multi-tenancy is an authorization and data-isolation problem, not merely a `tenantId` field. Identity transport proves subject identity only; Merchant Access resolves current Tenant/Membership authority.

Identity-edge mapping may use LocalStack Cognito where sufficiently supported or a test identity adapter behind the same project-owned contract.

---

## 4. Phase 2 — Canonical product catalog

### Business capability

Merchant can create, manage, and publish Products according to the approved Catalog domain baseline.

### Architectural lesson

Model ownership, lifecycle, uniqueness, and access patterns before optimizing physical persistence. DynamoDB remains the preferred LocalStack persistence mapping under ADR-005.

---

## 5. Phase 3 — First external product source

### Business capability

Merchant can import reviewed structured product information from an approved external source into its own canonical Catalog.

### Architectural lesson

Introduce durable queued crawler work because source latency/failure/backpressure requires it, not because the project wants an SQS icon. LocalStack SQS/S3 mappings are used where supported; source policy and parser fixtures remain product/engineering constraints independent of the emulator.

---

## 6. Phase 4 — Public storefront

### Business capability

Each eligible Tenant can expose a public storefront once Tenant-address semantics are resolved.

### Architectural lesson

Separate public read delivery/projections from transactional merchant operations. Object/static delivery mappings may use LocalStack S3 and other supported local delivery mechanisms; no real CDN deployment is required by the learning runtime.

---

## 7. Phase 5 — Cart & simple checkout

### Business capability

Customer can build a cart and place an Order using authoritative current Catalog/Pricing validation and idempotent checkout semantics.

### Architectural lesson

Start with the simplest truthful synchronous coordination and expose where distributed pressure actually appears.

---

## 8. Phase 6 — Inventory

### Business capability

Merchant has authoritative stock state and concurrent Orders cannot incorrectly sell the same final stock.

### Architectural lesson

Learn invariant protection through conditional writes/bounded transactions and owner-local persistence, using LocalStack DynamoDB where sufficiently supported.

---

## 9. Phase 7 — Mock Payment Provider v1

### Business capability

Checkout can simulate merchant-order payment without real money/card data.

### Architectural lesson

Treat provider integration as an external boundary even when both applications are owned by CommerceOS. Stable operation identity and idempotency precede retries.

---

## 10. Phase 8 — Payment failure engineering

### Business capability

Orders remain correct when the Mock Payment Provider is slow, fails transiently, duplicates callbacks, or leaves outcome ambiguous.

### Architectural lesson

Timeout is not failure. `OutcomeUnknown` requires inquiry/reconciliation; unsafe retries are prohibited until prior outcome is resolved.

---

## 11. Phase 9 — Order payment/allocation orchestration

### Business capability

The approved `OrderPlaced -> reservation -> payment/reconciliation -> OrderConfirmed -> OrderAllocated` process survives interruptions without inventing Sales/Inventory/Payment facts.

### Architectural lesson

ADR-010 selects durable workflow orchestration for this named process. Preferred learning mapping is LocalStack Step Functions Standard semantics where supported. Missing emulator behavior is documented/tested at the nearest reliable layer rather than validated in real AWS.

---

## 12. Phase 10 — Procurement

### Business capability

Merchant can replenish stock from Suppliers/Purchase Orders/receipts according to the approved domain baseline.

### Architectural lesson

Model a second major operational flow that independently produces Inventory and Accounting facts without foreign-persistence shortcuts.

---

## 13. Phase 11 — Accounting foundation

### Business capability

Merchant has an internal double-entry ledger with immutable posted Journals and traceable corrections.

### Architectural lesson

Financial records require append/correction semantics, source idempotency, and stronger integrity rules than ordinary CRUD data.

---

## 14. Phase 12 — Event-driven automatic accounting

### Business capability

Committed operational facts automatically produce idempotent Accounting effects.

### Architectural lesson

ADR-006 reliable fact publication uses owner state + outbox -> change-feed relay -> fact routing -> consumer-specific queue/DLQ. Preferred LocalStack mapping is DynamoDB Streams -> EventBridge -> SQS where supported.

---

## 15. Phase 13 — Basic finance/back-office reports

### Business capability

Merchant can understand revenue, inventory, finance projections, and operational exceptions from owned/rebuildable read models.

### Architectural lesson

Use projections/read models rather than scanning transactional ownership stores or coupling Reporting to foreign persistence.

---

## 16. Phase 14 — Product-source intelligence

### Business capability

Merchant can refresh approved external-source references and inspect changes over time.

### Architectural lesson

Use scheduled dispatch, queue backpressure, source-specific concurrency, parser versioning, and observability only when named ProductDataIngestion tasks justify them. Scheduler/event mappings target LocalStack where supported.

---

## 17. Phase 15 — Returns & refunds

### Business capability

Merchant can request, review, approve/reject, and propagate approved restockable refund effects across Sales, Inventory, Payments, and Accounting according to `PD-023`.

### Architectural lesson

ADR-011 explicitly chooses **reliable event choreography**, not a global Step Functions refund workflow:

```text
RefundApproved
   ├── Inventory -> StockReturned
   ├── Payments -> provider refund/reconciliation -> PaymentRefunded
   └── Accounting -> revenue compensation

StockReturned   -> Accounting COGS/inventory reversal
PaymentRefunded -> Accounting Deposits/Cash settlement
```

No global `RefundCompleted` authority is invented.

---

## 18. Phase 16 — Platform hardening

### Directional deliverables

- authorization/tenant-isolation hardening;
- throttling/abuse controls at project-owned boundaries;
- DLQ/recovery tooling;
- deterministic load/failure campaigns sized for local/CI resources;
- backup/reset/recovery exercises where the selected LocalStack setup supports them;
- observability and operator-diagnostic improvements;
- explicit emulator limitation register.

### Architectural lesson

Hardening validates CommerceOS contracts and failure handling against the chosen learning runtime. It does not claim exact AWS control-plane, quota, IAM, performance, or managed-service behavior.

---

## 19. Phase 17 — Architecture audit

Before any extraction, ask:

- Are bounded contexts still correct?
- Are there cross-domain persistence leaks?
- Which queues/events/workflows provide real value?
- Which contracts are unstable?
- Where are retries/idempotency unsafe?
- Which runtime components have become god components?
- What dominates local latency/resource use/operational failures?
- Which LocalStack limitations materially reduce learning confidence?
- Is any proposed extraction justified by measured runtime, reliability, ownership, or deployment pressure?

---

## 20. Phase 18 — Selective extraction, only if justified

Possible future candidates remain ProductDataIngestion, Mock Payment, Accounting, or Reporting when measured boundaries justify independent runtime/deployment treatment.

Do **not** split Sales, Catalog, Inventory, or other contexts merely to claim microservices.

Changing the infrastructure target from LocalStack to a hosted cloud is not an extraction step; it requires an explicit human architecture decision superseding ADR-012.

---

## 21. Milestone intent

- **Milestone A — Sell something:** onboarding/catalog/storefront/checkout/inventory/mock payment.
- **Milestone B — Survive failure:** payment ambiguity and durable order orchestration.
- **Milestone C — Run the business:** procurement/accounting/reporting.
- **Milestone D — Event-driven effects:** reliable cross-domain facts, projections, refunds, ingestion automation.
- **Milestone E — Production-minded engineering:** hardening, architecture audit, selective extraction only when justified.

These are learning/product milestones, not cloud-environment promotion stages.

## 22. Current execution authority

The canonical V2 backlog controls actual sequencing and Ready status. As of the LocalStack reconciliation, the first Ready frontier remains `TASK-0093`; `TASK-0094` is the LocalStack foundation lifecycle remediation that follows it.
