# TASK-0051 — Post inventory and procurement events automatically

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 12
Milestone: Milestone C / D
Depends on: TASK-0031, TASK-0043, TASK-0049

## Goal

Order fulfillment, goods receipt/invoice, supplier payment, and selected stock adjustments automatically create one policy-correct balanced Accounting journal per logical source.

## Business context

Operations become a closed business loop only when inventory/procurement facts generate COGS, inventory, payable, cash, and adjustment postings consistently.

## In scope

- implement fulfillment COGS/inventory posting using captured cost policy and stable OrderFulfilled/StockIssued facts;
- implement GoodsReceived/SupplierInvoiceRecorded/SupplierPaid inventory/payable/cash rules according to TASK-0044 policy;
- implement only approved StockAdjusted gain/expense cases with rule versions, source uniqueness, rejection, and drill-through;

## Out of scope

- refund postings, tax, landed cost, partial receipt allocation, depreciation, or direct producer-table reads;
- inventing source amounts absent from versioned events;

## Acceptance criteria

### AC01 — Fulfillment posting

Given a valid fulfillment/issue fact contains policy-required cost snapshot
when rule executes
then one balanced COGS/Inventory journal posts with event/order/movement traceability.

### AC02 — Procurement postings

Given valid receipt/invoice/payment facts arrive in accepted order
when rules execute
then balanced Inventory/AP/Cash journals reflect policy exactly once per logical source.

### AC03 — Adjustment policy

Given an approved or unsupported stock adjustment event arrives
when worker processes it
then approved case posts balanced gain/expense and unsupported case is explicitly rejected/reviewed.

### AC04 — Duplicate/out-of-order safety

Given events duplicate or arrive before prerequisites
when worker handles them
then no duplicate/guessed posting occurs and deferred/rejected state is recoverable.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected AWS evidence/teardown is recorded.

## Architecture impact

- Owning domain: Accounting
- Domains touched: Accounting, Inventory, Procurement event contracts
- Persistence impact: Extend posting rules/version/source uniqueness; journals remain Accounting-owned.
- Events/contracts impact: Consume OrderFulfilled/StockIssued/GoodsReceived/SupplierInvoiceRecorded/SupplierPaid/selected StockAdjusted v1.
- AWS/IaC impact: Existing accounting queue/worker/DynamoDB.
- ADR required? No — accepted architecture covers this scope.

## Security and tenant impact

- Authentication: Use established merchant/internal worker identity.
- Authorization: Worker validates tenant/source and financial report access remains protected.
- Tenant scoping: Every record, command, event, and projection is scoped by trusted tenant context; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Validate values/payloads and bound queries, retries, batches, and privileged actions.

## Reliability and idempotency impact

- Retry behavior: Retry transient persistence/prerequisite availability; deterministic unsupported policy does not loop.
- Timeout semantics: Unknown post result resolves by source-event uniqueness.
- Duplicate-delivery behavior: Duplicate event/source cannot duplicate journal or COGS/inventory/AP effect.
- Idempotency key/strategy: Tenant + rule type/version + logical source/event.
- DLQ/recovery/reconciliation: Out-of-order/missing prerequisite and rejects feed TASK-0052.

## Observability impact

- Logs: Structured logs carry safe tenant, source, entity, event/command, and correlation/causation identifiers.
- Metrics: Measure validation rejects, duplicates, failures, latency, backlog/stuck state, and recovery results.
- Traces/correlation: Preserve correlation/causation end-to-end.
- Operational states/errors: Posting type/rule/source/latency/reject reason and correlation are visible.

## Cost impact

- Request/compute impact: Scales with bounded transactional/event/report activity.
- Storage impact: Extend posting rules/version/source uniqueness; journals remain Accounting-owned.
- Network impact: Bounded internal API/event traffic only.
- New AWS resources/services: Existing accounting queue/worker/DynamoDB.
- Free Tier allowance relevant to this task: Use documented serverless allowances, short logs, small batches, and disabled/low schedules in non-prod.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measured event/workflow/storage impact is material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused preview/dev verification.

## Test plan

- Unit: Each debit/credit rule, balance, cost/amount validation, ordering and duplicate decisions.
- Integration: Event worker with duplicates/out-of-order and atomic posting for each source family.
- Architecture: Enforce domain ownership, inward dependencies, event conventions, and tenant rules.
- Contract: Operational event payload requirements and Journal outcomes.
- IaC: CDK assertions, synth, diff, and affected resource policy checks.
- E2E/manual: Fulfill sale and complete procurement lifecycle; inspect one correct posting per accepted trigger.
- **Cloud verification required?** Yes — event routing/queue delivery and DynamoDB posting idempotency require AWS.
- AWS environment/stack(s) required: Accounting/Async plus Sales/Inventory/Procurement resources
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic records after evidence.

