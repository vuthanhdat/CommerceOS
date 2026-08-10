# TASK-0027 — Deliver back-office order operations

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 5
Milestone: Milestone A
Depends on: TASK-0024, TASK-0025

## Goal

Authorized merchant staff can find orders, inspect immutable history, and perform only the Sales transitions currently supported, with honest downstream processing states.

## Business context

Back-office staff need operational visibility before inventory/payment automation exists; the UI must not pretend unfinished effects completed.

## In scope

- build order list/detail/history with tenant-safe filters and pagination;
- add permitted cancellation or administrative transitions defined by the current Sales state machine;
- surface validation, conflict, authorization, and not-yet-integrated processing states with correlation information;

## Out of scope

- inventory fulfillment, payment recovery, refund processing, or accounting views;
- editing immutable order line/price snapshots after placement;

## Acceptance criteria

### AC01 — Order operations

Given authorized staff open order list/detail
when filters and supported transitions are used
then the UI reflects persisted Sales state and append-only history.

### AC02 — Immutable commercial facts

Given staff attempt to alter placed line quantities/prices or skip lifecycle states
when the request is submitted
then the server rejects it and the UI preserves original snapshots.

### AC03 — Role and tenant safety

Given a viewer/unauthorized role or another tenant targets an order
when operation is attempted
then the action is denied without leaking order data.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then all repository checks pass and cloud verification is explicitly N/A unless AWS semantics changed.

## Architecture impact

- Owning domain: Sales & Order Management / Back Office
- Domains touched: Back Office, Sales API, Authorization, Audit hooks
- Persistence impact: No new source of truth; uses Sales queries/commands and status history.
- Events/contracts impact: No new event contract; transitions create Sales domain facts.
- AWS/IaC impact: No new managed service; existing Sales API traffic.
- ADR required? No — follows accepted architecture; create one if a significant new decision emerges.

## Security and tenant impact

- Authentication: Use the established merchant identity or explicit anonymous storefront boundary.
- Authorization: View and transition permissions are explicit; server enforces them independently of UI controls.
- Tenant scoping: Tenant-owned data is scoped from trusted context; public lookup resolves an approved tenant slug and exposes only public projections.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Validate payloads, paginate reads, and bound anonymous or expensive operations.

## Reliability and idempotency impact

- Retry behavior: Synchronous failures are deterministic; retryable writes use explicit concurrency/idempotency controls.
- Timeout semantics: No external ambiguity is introduced unless stated.
- Duplicate-delivery behavior: N/A — no at-least-once consumer introduced.
- Idempotency key/strategy: Optimistic concurrency or command id protects unsafe repeated writes.
- DLQ/recovery/reconciliation: N/A — no asynchronous boundary introduced.

## Observability impact

- Logs: Structured logs contain operation, safe tenant/entity identifiers, and correlation id.
- Metrics: Use built-in metrics and bounded business metrics for meaningful risk.
- Traces/correlation: Preserve correlation across every API/application boundary changed here.
- Operational states/errors: Conflict, forbidden transition, unavailable downstream capability, and request correlation are visible.

## Cost impact

- Request/compute impact: Scales with bounded user traffic.
- Storage impact: No new source of truth; uses Sales queries/commands and status history.
- Network impact: Normal web/API payloads; avoid unbounded responses.
- New AWS resources/services: No new managed service; existing Sales API traffic.
- Free Tier allowance relevant to this task: Use existing pay-per-use services and documented learning-profile limits.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume.
- Estimated one-off cloud-test/load-test cost, if any: None expected.

## Test plan

- Unit: Order-list/detail state, permission-aware actions, transition/error mapping.
- Integration: Frontend adapter contract plus Sales API authorization and concurrency.
- Architecture: Verify domain ownership, inward dependencies, and no cross-domain persistence shortcuts.
- Contract: Consumer tests for order list/detail/transition APIs.
- IaC: N/A unless infrastructure changes.
- E2E/manual: Sales staff finds and transitions an order; viewer and cross-tenant attempts fail.
- **Cloud verification required?** No — UI and existing API behavior are covered locally; no new AWS semantics.
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

