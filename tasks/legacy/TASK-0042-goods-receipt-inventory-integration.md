# TASK-0042 — Receive purchased goods into inventory

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 10
Milestone: Milestone C
Depends on: TASK-0029, TASK-0041

## Goal

Authorized warehouse staff can record a goods receipt against a submitted purchase order and increase Inventory exactly once through an explicit cross-domain command.

## Business context

Receipt belongs to Procurement, while physical quantity belongs to Inventory; the boundary must remain explicit and recoverable.

## In scope

- implement GoodsReceipt validation and lifecycle for full initial receipt;
- invoke idempotent Inventory ReceiveStock for each accepted line using stable goods-receipt source keys;
- record honest Procurement/Inventory states, correlation, failure/retry behavior, and back-office receipt flow;

## Out of scope

- partial/over receipt, supplier invoice, accounting posting, returns to supplier, or direct Inventory writes;
- automatic PO payment/close;

## Acceptance criteria

### AC01 — Goods received

Given a submitted PO is eligible and quantities match
when authorized staff confirm receipt
then one GoodsReceipt is recorded and matching Inventory Receive movements increase stock once.

### AC02 — Duplicate safety

Given the receipt command or Inventory call is repeated
when processing runs
then no duplicate receipt, stock movement, or quantity increase occurs.

### AC03 — Partial failure recovery

Given Procurement records or invokes receipt and a downstream step fails/times out
when the flow is retried/reconciled
then states remain explicit and converge without direct cross-domain persistence repair.

### AC04 — Invalid quantity/state

Given PO is ineligible or receipt exceeds initial full-receipt rules
when confirmation is attempted
then the operation is rejected before stock mutation.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Procurement / Inventory
- Domains touched: Procurement, Inventory, Back Office
- Persistence impact: Procurement owns GoodsReceipt; Inventory owns stock/movement/idempotency records; no shared transaction/table.
- Events/contracts impact: GoodsReceived and StockReceived facts with stable source references; reliable publication waits for TASK-0048.
- AWS/IaC impact: Existing DynamoDB/API resources; no new service.
- ADR required? No — initial application-contract integration follows accepted modular architecture.

## Security and tenant impact

- Authentication: Use established merchant/internal/provider identities.
- Authorization: Receipt requires warehouse/purchasing permission and same-tenant PO/warehouse/product checks.
- Tenant scoping: Trusted tenant context scopes every merchant record and message; client tenant ids cannot override it.
- Sensitive data/secrets: No real card data or secrets are stored/logged; personal/business data is minimized and redacted.
- Abuse/rate-limit considerations: Bound payloads, retries, concurrency, and operational controls.

## Reliability and idempotency impact

- Retry behavior: Cross-domain ReceiveStock uses goodsReceiptId/line as source key; retry only after outcome lookup.
- Timeout semantics: Unknown Inventory response is queried by source key before retry/finalizing Procurement state.
- Duplicate-delivery behavior: Duplicate receipt/line calls cannot repeat stock or status effects.
- Idempotency key/strategy: Tenant + goodsReceiptId + lineId is the Inventory receipt source key.
- DLQ/recovery/reconciliation: Expose processing/failed receipt state and safe retry/reconciliation; no manual table edits.

## Observability impact

- Logs: Structured logs carry safe tenant/entity/operation/event and correlation identifiers.
- Metrics: Measure outcomes, failures, retries, duplicates, latency, and stuck states at bounded cardinality.
- Traces/correlation: Preserve correlation/causation across all changed boundaries.
- Operational states/errors: Correlation links PO, receipt, Inventory movement, actor, and failed step.

## Cost impact

- Request/compute impact: Scales with bounded business activity and retry policy.
- Storage impact: Procurement owns GoodsReceipt; Inventory owns stock/movement/idempotency records; no shared transaction/table.
- Network impact: Only bounded API/event traffic.
- New AWS resources/services: Existing DynamoDB/API resources; no new service.
- Free Tier allowance relevant to this task: Use accepted serverless allowances and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; measure workflows/retries where relevant.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for targeted failure/cloud tests.

## Test plan

- Unit: Receipt eligibility/quantity rules, state transitions, source-key mapping.
- Integration: Procurement–Inventory contract, duplicate/timeouts/partial failures, tenant isolation.
- Architecture: Check domain boundaries, tenant context, and no persistence shortcuts.
- Contract: GoodsReceipt and ReceiveStock integration contracts.
- IaC: CDK assertions, synth, diff, and selected deployment checks.
- E2E/manual: Receive a submitted PO, verify stock/movement once, then replay safely.
- **Cloud verification required?** Yes — DynamoDB conditional cross-module effects and API behavior need AWS verification.
- AWS environment/stack(s) required: Procurement/Inventory resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral stacks and synthetic data after evidence collection.

