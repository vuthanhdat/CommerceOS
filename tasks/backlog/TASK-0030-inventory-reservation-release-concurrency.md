# TASK-0030 — Reserve and release stock safely under concurrency

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 6
Milestone: Milestone A
Depends on: TASK-0028, TASK-0029

## Goal

Inventory can reserve and release stock atomically so concurrent orders cannot both claim the same final available units.

## Business context

Reservation is the roadmap's core concurrency lesson and must never use unprotected read-then-write logic.

## In scope

- implement idempotent ReserveStock and ReleaseReservation with reservation lifecycle/source reference;
- use DynamoDB conditional writes/transactions to protect Available and append auditable movements;
- prove concurrent reservation, duplicate command, release, stale command, and insufficient-stock behavior;

## Out of scope

- Sales orchestration, physical issue/fulfillment, backorder/negative availability, or multi-warehouse allocation;
- event-driven reservation requests;

## Acceptance criteria

### AC01 — Sufficient reservation

Given available stock is sufficient
when a unique reservation command commits
then Reserved increases, Available decreases, and one reservation/movement is recorded atomically.

### AC02 — Final-unit concurrency

Given two different orders concurrently request more than the remaining availability permits
when both commands race
then at most the permitted quantity is reserved and at least one request fails explicitly.

### AC03 — Release and replay

Given a reservation is released or the same reserve/release command is repeated
when commands execute
then balances change exactly once and released reservations cannot be issued later.

### AC04 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and selected cloud verification plus teardown evidence is recorded.

## Architecture impact

- Owning domain: Inventory
- Domains touched: Inventory; Sales uses only the application contract later
- Persistence impact: Conditional/transactional StockItem, StockReservation, StockMovement, and idempotency records.
- Events/contracts impact: StockReserved/ReservationFailed/StockReleased facts for later publication.
- AWS/IaC impact: DynamoDB transactions/conditions in Inventory infrastructure.
- ADR required? No — accepted domain/serverless rules cover this task.

## Security and tenant impact

- Authentication: Use established merchant/Storefront identity boundaries.
- Authorization: Only authorized internal Sales flow or warehouse operation can reserve/release; tenant/source identity is validated.
- Tenant scoping: All tenant-owned commands/queries use trusted tenant context; client tenant ids are rejected/ignored and cross-tenant access is tested.
- Sensitive data/secrets: No secrets or real payment/card data are stored or logged.
- Abuse/rate-limit considerations: Cap quantities and concurrent attempts; protect hot keys with measured throughput/concurrency settings.

## Reliability and idempotency impact

- Retry behavior: Conditional conflicts may retry with a small bounded policy; insufficient stock is permanent for that state.
- Timeout semantics: A caller timeout requires querying reservation by source/idempotency key before retry.
- Duplicate-delivery behavior: Duplicate reserve/release cannot create extra quantity or movements.
- Idempotency key/strategy: Tenant + order/source + operation + version identifies each reservation effect.
- DLQ/recovery/reconciliation: N/A — no queue/event consumer.

## Observability impact

- Logs: Structured logs include safe tenant, entity, command/event, and correlation identifiers.
- Metrics: Bounded metrics cover conflicts, failures, and latency for the changed capability.
- Traces/correlation: Preserve correlation across all application/external boundaries.
- Operational states/errors: Insufficient stock, conditional conflict, duplicate, already released, and unknown commit are distinct.

## Cost impact

- Request/compute impact: Small DynamoDB transactions per unique reservation/release.
- Storage impact: Conditional/transactional StockItem, StockReservation, StockMovement, and idempotency records.
- Network impact: Normal API traffic only.
- New AWS resources/services: DynamoDB transactions/conditions in Inventory infrastructure.
- Free Tier allowance relevant to this task: Use documented Lambda, DynamoDB, API Gateway, SQS, and CloudWatch limits where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at the learning profile.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview tests.

## Test plan

- Unit: Reservation lifecycle, quantity invariants, release rules, and idempotency decisions.
- Integration: High-contention final-unit tests against DynamoDB Local and selected real DynamoDB.
- Architecture: Enforce domain ownership, inward dependencies, and no cross-domain persistence access.
- Contract: ReserveStock/ReleaseReservation application contracts and result codes.
- IaC: CDK assertions, synth, and reviewed diff.
- E2E/manual: Race two reservations for the final unit, then release the winner exactly once.
- **Cloud verification required?** Yes — real DynamoDB conditional/transaction concurrency behavior must be verified.
- AWS environment/stack(s) required: Inventory table/resources in dev or ephemeral CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic data.

