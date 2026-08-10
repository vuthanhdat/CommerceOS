# TASK-0024 — Establish sales-order lifecycle and persistence

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 5
Milestone: Milestone A
Depends on: TASK-0009, TASK-0011

## Goal

Sales can create and retrieve tenant-owned orders with immutable line/price placeholders, valid lifecycle transitions, and auditable status history.

## Business context

Sales owns the commercial agreement and order lifecycle; Catalog, Inventory, and Payment must integrate through explicit contracts instead of sharing storage.

## In scope

- introduce Sales Domain/Application/Infrastructure projects and SalesOrder, line, price snapshot, status, and status-history model;
- persist tenant-scoped orders with documented access patterns for id, tenant list, customer reference, and status;
- implement create/query and valid state-transition primitives without yet calling inventory or payment;

## Out of scope

- Storefront checkout orchestration, stock reservation, payment, fulfillment, refunds, or accounting;
- retroactive updates from later Catalog changes;

## Acceptance criteria

### AC01 — Order snapshot

Given a valid create-order command contains resolved catalog facts
when Sales creates the order
then lines and captured commercial values become immutable historical snapshots.

### AC02 — Lifecycle integrity

Given a valid or invalid order transition is requested
when the command runs
then allowed transitions append status history and invalid transitions leave the order unchanged.

### AC03 — Tenant isolation

Given Tenant B knows Tenant A's order id
when read or transition is attempted
then the operation is denied/non-disclosing and no history is changed.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then all repository checks pass and real-AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Sales & Order Management
- Domains touched: Sales, Catalog application contract, API, Tenant authorization
- Persistence impact: Add Sales order/status-history items and tenant/status/customer access patterns with optimistic concurrency.
- Events/contracts impact: Define OrderPlaced/status domain facts locally; reliable external publication waits for TASK-0048.
- AWS/IaC impact: DynamoDB and Sales API/Lambda routes in CommerceStack.
- ADR required? No — follows accepted architecture; create one if a significant new decision emerges.

## Security and tenant impact

- Authentication: Use the established merchant identity or explicit anonymous storefront boundary.
- Authorization: Sales create/read/transition permissions are explicit by role; shopper access is deferred.
- Tenant scoping: Tenant-owned data is scoped from trusted context; public lookup resolves an approved tenant slug and exposes only public projections.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Validate payloads, paginate reads, and bound anonymous or expensive operations.

## Reliability and idempotency impact

- Retry behavior: Synchronous failures are deterministic; retryable writes use explicit concurrency/idempotency controls.
- Timeout semantics: No external ambiguity is introduced unless stated.
- Duplicate-delivery behavior: N/A — no at-least-once consumer introduced.
- Idempotency key/strategy: Internal create command accepts a stable business command id; public checkout semantics are TASK-0025.
- DLQ/recovery/reconciliation: N/A — no asynchronous boundary introduced.

## Observability impact

- Logs: Structured logs contain operation, safe tenant/entity identifiers, and correlation id.
- Metrics: Use built-in metrics and bounded business metrics for meaningful risk.
- Traces/correlation: Preserve correlation across every API/application boundary changed here.
- Operational states/errors: Invalid transition, stale version, missing catalog reference, and persistence conflict are diagnosable.

## Cost impact

- Request/compute impact: Scales with bounded user traffic.
- Storage impact: Add Sales order/status-history items and tenant/status/customer access patterns with optimistic concurrency.
- Network impact: Normal web/API payloads; avoid unbounded responses.
- New AWS resources/services: DynamoDB and Sales API/Lambda routes in CommerceStack.
- Free Tier allowance relevant to this task: Use existing pay-per-use services and documented learning-profile limits.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded preview/dev checks.

## Test plan

- Unit: Order state machine, immutable snapshots, totals arithmetic, and transition history.
- Integration: DynamoDB order create/query/concurrency and tenant-isolation API tests.
- Architecture: Verify domain ownership, inward dependencies, and no cross-domain persistence shortcuts.
- Contract: Sales application create/query/transition contracts.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Create an order from resolved test catalog facts and exercise allowed/denied transitions.
- **Cloud verification required?** Yes — DynamoDB access/concurrency, API integration, Lambda packaging, and IAM require AWS evidence.
- AWS environment/stack(s) required: Sales resources in CommerceStack
- Preview/staging teardown plan: Destroy preview resources; document retained dev state.

