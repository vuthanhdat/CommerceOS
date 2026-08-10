# TASK-0031 — Allocate and fulfill orders with low-stock visibility

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 6
Milestone: Milestone A
Depends on: TASK-0024, TASK-0030

## Goal

Confirmed orders can be allocated, issued, and fulfilled through explicit Sales–Inventory contracts, while staff see low-stock state without either domain sharing persistence.

## Business context

Order fulfillment turns reservations into physical stock movement and exposes independent Sales and Inventory lifecycle responsibilities.

## In scope

- orchestrate allocation from Sales to idempotent Inventory reservation and record honest order state/history;
- implement IssueStock against an active reservation and transition Sales to Fulfilled/Completed only after successful inventory effect;
- build fulfillment/stock views plus a tenant-scoped low-stock read model with configurable threshold;

## Out of scope

- payment initiation, procurement receipt, returns, accounting postings, or multi-warehouse picking;
- direct Sales writes to Inventory records or Inventory writes to Sales records;

## Acceptance criteria

### AC01 — Allocate order

Given a valid order needs allocation and stock is available
when the Sales application invokes Inventory
then one reservation is created and the order reaches Allocated only after success.

### AC02 — Fulfill once

Given an allocated order is fulfilled or retried
when Inventory issues the reservation and Sales records fulfillment
then OnHand/Reserved decrease exactly once, an Issue movement exists, and order history reaches Fulfilled.

### AC03 — Failure and low stock

Given stock is insufficient or issue fails
when the operation completes
then order state remains honest, partial effects are repairable, and low-stock projection reflects committed Inventory state.

### AC04 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and selected cloud verification plus teardown evidence is recorded.

## Architecture impact

- Owning domain: Sales & Order Management / Inventory
- Domains touched: Sales, Inventory, Back Office, Reporting projection
- Persistence impact: Each domain changes only its own state; low-stock is an Inventory-owned projection/read model.
- Events/contracts impact: OrderAllocated/OrderFulfilled and StockReserved/StockIssued facts; reliable cross-domain publication waits for TASK-0048.
- AWS/IaC impact: Existing DynamoDB/API resources; no new service yet.
- ADR required? No — initial in-process application-contract orchestration is intentional before Step Functions.

## Security and tenant impact

- Authentication: Use established merchant/Storefront identity boundaries.
- Authorization: Sales/warehouse fulfillment permissions are explicit and all commands validate tenant/source ownership.
- Tenant scoping: All tenant-owned commands/queries use trusted tenant context; client tenant ids are rejected/ignored and cross-tenant access is tested.
- Sensitive data/secrets: No secrets or real payment/card data are stored or logged.
- Abuse/rate-limit considerations: Validate and bound quantities, payloads, concurrency, and externally callable operations.

## Reliability and idempotency impact

- Retry behavior: Each cross-domain command is idempotent; failures stop before unsafe later transitions.
- Timeout semantics: Unknown Inventory command outcome is queried by source id before Sales retries or transitions.
- Duplicate-delivery behavior: Repeated allocation/fulfillment cannot duplicate reservation, issue movement, or status transition.
- Idempotency key/strategy: OrderId + operation version is the cross-domain source key.
- DLQ/recovery/reconciliation: Expose allocation/fulfillment failure state and safe retry; no hidden partial success.

## Observability impact

- Logs: Structured logs include safe tenant, entity, command/event, and correlation identifiers.
- Metrics: Bounded metrics cover conflicts, failures, and latency for the changed capability.
- Traces/correlation: Preserve correlation across all application/external boundaries.
- Operational states/errors: Correlation links order, reservation, movement, and projection status; low-stock lag/failure is visible.

## Cost impact

- Request/compute impact: Usage scales with bounded business requests.
- Storage impact: Each domain changes only its own state; low-stock is an Inventory-owned projection/read model.
- Network impact: Normal API traffic only.
- New AWS resources/services: Existing DynamoDB/API resources; no new service yet.
- Free Tier allowance relevant to this task: Use documented Lambda, DynamoDB, API Gateway, SQS, and CloudWatch limits where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at the learning profile.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview tests.

## Test plan

- Unit: Allocation/fulfillment decision logic, state transitions, source keys, and low-stock threshold.
- Integration: Cross-domain contracts, duplicate/timeouts, atomic Inventory effects, and tenant isolation.
- Architecture: Enforce domain ownership, inward dependencies, and no cross-domain persistence access.
- Contract: Sales–Inventory command/query/result contracts and low-stock DTO.
- IaC: CDK assertions, synth, and reviewed diff.
- E2E/manual: Allocate and fulfill an order, verify movements/order history, and surface low stock.
- **Cloud verification required?** Yes — DynamoDB concurrency plus deployed cross-module API behavior require AWS verification.
- AWS environment/stack(s) required: Sales/Inventory resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic data.

