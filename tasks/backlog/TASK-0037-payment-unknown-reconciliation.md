# TASK-0037 — Resolve PaymentUnknown through retry and reconciliation

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 8
Milestone: Milestone B
Depends on: TASK-0035, TASK-0036

## Goal

Orders in PaymentUnknown are resolved exactly once through safe retry, provider query, webhook convergence, and scheduled reconciliation.

## Business context

Timeout is not failure. Without explicit ambiguous state and reconciliation, retries can double-charge or release paid inventory.

## In scope

- add internal PaymentUnknown state and decision rules for before/after-commit timeout, pending, 500, webhook, and query results;
- implement bounded retry/backoff only where provider idempotency makes it safe and a query-first reconciliation application service;
- add low-frequency EventBridge Scheduler reconciliation for stuck orders plus single-resolution concurrency/idempotency;

## Out of scope

- Step Functions migration, refunds, manual operations UI, or real payment service;
- assuming missing webhook means failure or retrying stable declines;

## Acceptance criteria

### AC01 — Unknown state

Given CommerceOS times out without a definitive provider outcome
when the payment call returns
then order/payment enter diagnosable PaymentUnknown and reserved stock is not prematurely released.

### AC02 — Query resolution

Given provider query reports Captured, Failed, or Pending
when reconciliation runs
then Captured confirms once, Failed releases/fails once, and Pending remains waiting with bounded escalation.

### AC03 — Webhook/reconciler race

Given webhook and scheduled reconciliation resolve the same payment concurrently
when both handlers execute
then one terminal transition wins and no payment/order/inventory effect is duplicated.

### AC04 — Stuck visibility

Given an order remains unknown beyond thresholds
when scheduled checks are exhausted
then the item becomes an operational exception without unsafe automatic failure.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Sales / Payment integration / Inventory
- Domains touched: Payment, Sales, Inventory, Scheduler, Operations
- Persistence impact: Extend internal payment/order resolution state, attempts, next-check time, provider query result, and source-version guards.
- Events/contracts impact: PaymentResolutionRequested/Resolved/StillUnknown and terminal Payment facts with correlation/causation.
- AWS/IaC impact: EventBridge Scheduler plus reconciliation Lambda and existing provider/API/DynamoDB resources.
- ADR required? Yes — record the payment ambiguity/reconciliation consistency policy if not already accepted as a dedicated ADR.

## Security and tenant impact

- Authentication: Use established merchant/internal/provider identities.
- Authorization: Reconciliation is internal/least-privilege; provider references are tenant-bound and test controls are protected.
- Tenant scoping: Trusted tenant context scopes every merchant record and message; client tenant ids cannot override it.
- Sensitive data/secrets: No real card data or secrets are stored/logged; personal/business data is minimized and redacted.
- Abuse/rate-limit considerations: Bound retry count/cadence/batch size and disable/slow schedules in preview/dev by default.

## Reliability and idempotency impact

- Retry behavior: Query first; retry state-changing provider calls only with the original key and documented safe scenario.
- Timeout semantics: Timeout always remains unknown until a verified provider query/webhook establishes outcome.
- Duplicate-delivery behavior: Webhook, scheduler, and manual retry may race but terminal state/effects commit once.
- Idempotency key/strategy: Payment intent/order + operation version + provider event/query observation.
- DLQ/recovery/reconciliation: After bounded automatic checks, surface manual review; never silently discard ambiguous payments.

## Observability impact

- Logs: Structured logs carry safe tenant/entity/operation/event and correlation identifiers.
- Metrics: Measure outcomes, failures, retries, duplicates, latency, and stuck states at bounded cardinality.
- Traces/correlation: Preserve correlation/causation across all changed boundaries.
- Operational states/errors: Unknown age/count, next attempt, last provider observation, resolution source, and correlated stock/order effects are visible.

## Cost impact

- Request/compute impact: Low-frequency Scheduler invocations and bounded batches/queries.
- Storage impact: Extend internal payment/order resolution state, attempts, next-check time, provider query result, and source-version guards.
- Network impact: Only bounded API/event traffic.
- New AWS resources/services: EventBridge Scheduler plus reconciliation Lambda and existing provider/API/DynamoDB resources.
- Free Tier allowance relevant to this task: Use accepted serverless allowances and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible at learning scale; schedule disabled/manual in previews and cadence documented.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for targeted failure/cloud tests.

## Test plan

- Unit: Resolution decision table, safe retry policy, terminal convergence, and stock release rules.
- Integration: Provider timeouts/query, webhook race, scheduler batch, DynamoDB conditional resolution.
- Architecture: Check domain boundaries, tenant context, and no persistence shortcuts.
- Contract: Provider query and internal payment-resolution contracts/events.
- IaC: CDK assertions, synth, diff, and selected deployment checks.
- E2E/manual: Exercise timeout-before/after-commit, delayed success, and webhook/reconcile races.
- **Cloud verification required?** Yes — Scheduler, Lambda concurrency, real timeout/query, and conditional convergence need AWS tests.
- AWS environment/stack(s) required: Payment reconciliation resources, CommerceStack, MockPaymentStack
- Preview/staging teardown plan: Disable/remove preview schedule and clear synthetic unknown payments.

