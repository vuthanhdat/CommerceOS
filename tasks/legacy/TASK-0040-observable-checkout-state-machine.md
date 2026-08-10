# TASK-0040 — Implement an observable checkout state machine

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 9
Milestone: Milestone B
Depends on: TASK-0039
Execution gate: Run only if TASK-0039 accepts Step Functions or another explicit orchestration mechanism.

## Goal

Checkout executes through the orchestration mechanism accepted by TASK-0039 with observable waiting, retry, failure, and compensation while domain state remains authoritative.

## Business context

If orchestration is justified, migration must improve diagnosability without moving business invariants into workflow JSON or duplicating effects.

## In scope

- implement the accepted workflow for create order, reserve inventory, request payment, wait/query/reconcile, confirm, fail, and release;
- preserve domain/application commands as idempotent task boundaries and propagate tenant/correlation/execution identifiers;
- migrate safely from v1, expose execution status/history, alarms, recovery, and rollback path, and test happy/failure branches;

## Out of scope

- refund/returns orchestration, procurement workflows, or unrelated CRUD;
- placing core Sales, Inventory, or Payment rules directly in Step Functions definitions;

## Acceptance criteria

### AC01 — Workflow outcomes

Given success, decline, pending, timeout, and retry scenarios run
when the accepted workflow executes
then order/payment/inventory outcomes match existing domain rules exactly once.

### AC02 — Observable waiting and failure

Given a workflow waits, retries, fails, or compensates
when operators inspect it
then execution/step/status/correlation and next recovery action are visible.

### AC03 — Migration safety

Given old/in-flight and new checkout paths coexist during rollout
when requests and callbacks arrive
then each checkout is owned by one path and rollback does not orphan or duplicate effects.

### AC04 — Tenant/IAM isolation

Given a workflow execution/task runs
when context and permissions are inspected
then trusted tenant identity is preserved and each role can access only required resources/actions.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Sales / Payment orchestration / Inventory
- Domains touched: Sales, Payment, Inventory, Step Functions/accepted mechanism, Operations
- Persistence impact: Domain state remains in domain stores; workflow execution references ids and may add bounded orchestration/idempotency metadata.
- Events/contracts impact: Callback/task continuation contracts and workflow status facts are versioned.
- AWS/IaC impact: Step Functions Standard and task Lambdas/alarms if ADR accepts it; otherwise the exact accepted mechanism.
- ADR required? No new ADR — implement the accepted TASK-0039 ADR without expanding it.

## Security and tenant impact

- Authentication: Use established merchant/internal/provider identities.
- Authorization: Only authorized checkout entry starts a tenant-scoped execution; workflow roles are least-privilege and inputs/logs are redacted.
- Tenant scoping: Trusted tenant context scopes every merchant record and message; client tenant ids cannot override it.
- Sensitive data/secrets: No real card data or secrets are stored/logged; personal/business data is minimized and redacted.
- Abuse/rate-limit considerations: Bound execution duration, retries, state transitions, payload size, concurrency, and test scenarios.

## Reliability and idempotency impact

- Retry behavior: Use ADR-defined per-state retry/catch; all task handlers are independently idempotent.
- Timeout semantics: Wait/timeout/PaymentUnknown paths follow the accepted payment policy and never infer failure from silence.
- Duplicate-delivery behavior: Duplicate starts/callbacks/task retries converge through execution/business idempotency keys.
- Idempotency key/strategy: Checkout key maps to one execution; domain step keys remain order/payment/operation based.
- DLQ/recovery/reconciliation: Failed/stuck executions alarm and use documented resume/reconcile/compensation; no manual database edit.

## Observability impact

- Logs: Structured logs carry safe tenant/entity/operation/event and correlation identifiers.
- Metrics: Measure outcomes, failures, retries, duplicates, latency, and stuck states at bounded cardinality.
- Traces/correlation: Preserve correlation/causation across all changed boundaries.
- Operational states/errors: Execution history links to payment/order/reservation and appears in TASK-0038 operations.

## Cost impact

- Request/compute impact: State transitions and retries are counted/capped; no CRUD-only workflows.
- Storage impact: Domain state remains in domain stores; workflow execution references ids and may add bounded orchestration/idempotency metadata.
- Network impact: Only bounded API/event traffic.
- New AWS resources/services: Step Functions Standard and task Lambdas/alarms if ADR accepts it; otherwise the exact accepted mechanism.
- Free Tier allowance relevant to this task: Use accepted serverless allowances and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: bounded by learning transition budget; compare measured cost with TASK-0039 estimate.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for targeted failure/cloud tests.

## Test plan

- Unit: Workflow decision logic and every task handler/domain command.
- Integration: Definition assertions plus retries/callbacks/duplicates and domain convergence.
- Architecture: Check domain boundaries, tenant context, and no persistence shortcuts.
- Contract: Workflow input/output/task/event schemas.
- IaC: CDK assertions, synth, diff, and selected deployment checks.
- E2E/manual: All payment scenario journeys through deployed workflow, including rollback drill.
- **Cloud verification required?** Yes — Step Functions retry/catch/wait/history/IAM semantics require real AWS.
- AWS environment/stack(s) required: Async/CommerceStack workflow plus MockPaymentStack
- Preview/staging teardown plan: Stop/remove preview executions/stacks; retain only documented dev workflow and synthetic records.

