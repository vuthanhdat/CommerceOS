# TASK-0084 — Support partial goods receipts

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Later product capability
Milestone: Unscheduled
Depends on: TASK-0042

## Goal

Procurement can record multiple partial goods receipts without exceeding ordered quantities, and each accepted receipt updates Inventory exactly once until the purchase order is fully received.

## Business context

Real procurement often arrives in parts; the initial full receipt intentionally deferred cumulative quantity and concurrent receipt complexity.

## In scope

- extend PO/line lifecycle with ordered/received/remaining quantities, partial/full receipt status, close/cancel policy, and concurrent receipt validation;
- create multiple GoodsReceipts and idempotent Inventory ReceiveStock line effects with immutable source/cost snapshots;
- update Back Office history/remaining quantities and Accounting event inputs according to existing policy;

## Out of scope

- over-receipt approval, quality inspection, supplier returns, landed-cost allocation, or changing submitted PO quantities;
- editing prior goods receipts or Inventory movements;

## Acceptance criteria

### AC01 — Partial receipt

Given submitted PO has remaining quantity
when a valid partial receipt is confirmed
then cumulative received/remaining and Inventory movement update exactly once and PO stays partially received.

### AC02 — Final receipt

Given final remaining quantities are received
when receipt commits
then PO reaches GoodsReceived according to lifecycle and totals never exceed ordered amount.

### AC03 — Concurrent protection

Given two receipts race for the same remaining quantity
when both execute
then conditional validation permits at most ordered quantity and loser receives explicit conflict.

### AC04 — Accounting/source traceability

Given multiple receipt events are posted
when Accounting processes them
then each receipt has unique source/cost evidence and no duplicate/missing logical posting.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected cloud evidence, cost, and cleanup are recorded.

## Architecture impact

- Owning domain: Procurement / Inventory
- Domains touched: Procurement, Inventory, Accounting events, Back Office
- Persistence impact: Extend cumulative PO line quantities/status/version and multiple immutable GoodsReceipt/source identities.
- Events/contracts impact: GoodsReceived v2 or compatible event includes receipt/line/cumulative/source cost data.
- AWS/IaC impact: Existing DynamoDB/API/event resources; transactional/conditional updates.
- ADR required? Yes if event compatibility/accounting recognition policy requires a breaking change; otherwise document version migration.

## Security and tenant impact

- Authentication: Use the established merchant/shopper/internal identity boundary.
- Authorization: Receipt permissions/tenant scope/audit remain enforced.
- Tenant scoping: Trusted tenant context scopes all data/actions; public/shopper identities can access only explicitly authorized tenant resources.
- Sensitive data/secrets: Minimize/redact PII, secrets, and provider data; no real card data.
- Abuse/rate-limit considerations: Bound receipt count/lines/quantities and reject excessive fragmentation/overflow.

## Reliability and idempotency impact

- Retry behavior: Receipt/Inventory/Accounting steps retry with receipt/line source keys.
- Timeout semantics: Unknown receipt/Inventory outcome queried by source before retry.
- Duplicate-delivery behavior: Duplicate receipt/event cannot increase stock or post twice.
- Idempotency key/strategy: Tenant + goodsReceiptId + lineId; PO line version/remaining conditional.
- DLQ/recovery/reconciliation: Partial cross-domain failure is reconciled through existing receipt/inventory/accounting operations.

## Observability impact

- Logs: Structured/redacted logs include safe tenant/entity/operation/event and correlation data.
- Metrics: Measure success/failure, duplicates, latency, backlog/stuck state, and relevant usage/cost.
- Traces/correlation: Preserve correlation/causation through all changed boundaries.
- Operational states/errors: Ordered/received/remaining, receipt status, conflict, stock/posting links and correlation visible.

## Cost impact

- Request/compute impact: Additional bounded receipt transactions/events.
- Storage impact: Extend cumulative PO line quantities/status/version and multiple immutable GoodsReceipt/source identities.
- Network impact: Bounded API/CDN/provider traffic only.
- New AWS resources/services: Existing DynamoDB/API/event resources; transactional/conditional updates.
- Free Tier allowance relevant to this task: Validate current pricing/allowances at scheduling time and keep non-production usage bounded.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume.
- Estimated one-off cloud-test/load-test cost, if any: Estimate before execution and record actual bounded test usage.

## Test plan

- Unit: Cumulative/remaining/lifecycle/concurrent validation and event mapping.
- Integration: DynamoDB concurrent partial receipts, Inventory idempotency, Accounting duplicate posting.
- Architecture: Enforce domain ownership, tenant isolation, inward dependencies, and event/idempotency rules.
- Contract: GoodsReceipt event/API compatibility and remaining quantity DTOs.
- IaC: CDK assertions/synth/diff and affected real-AWS policy/resource tests.
- E2E/manual: Two partial receipts complete a PO; race over-receipt; inspect stock/accounting/history.
- **Cloud verification required?** Yes — DynamoDB concurrency and event/Inventory/Accounting integration require AWS.
- AWS environment/stack(s) required: Procurement/Inventory/Accounting resources
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic data; document retained configuration.

