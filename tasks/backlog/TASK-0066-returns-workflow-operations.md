# TASK-0066 — Orchestrate and operate the returns workflow

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 15
Milestone: Milestone D
Depends on: TASK-0040, TASK-0063, TASK-0064, TASK-0065
Execution gate: Step Functions is used only if the accepted orchestration decision justifies it.

## Goal

Merchant staff can operate an observable returns workflow that coordinates refund, inventory, and accounting compensation, retries safely, and reaches a final or actionable exception state.

## Business context

Returns are the first strong Saga/compensation case and must expose partial completion rather than hide it behind one synchronous endpoint.

## In scope

- implement the stateful return workflow using accepted orchestration policy only where justified;
- coordinate validate/approve, provider refund, inventory disposition/return, accounting compensation, finalization, retries, waits, and reconciliation;
- deliver back-office Return list/detail/actions/timeline and operations/DLQ/workflow recovery with end-to-end tests;

## Out of scope

- exchanges, return shipping/logistics, chargebacks, supplier returns, or real payment;
- manual database state edits or compensation without source evidence;

## Acceptance criteria

### AC01 — Successful workflow

Given approved return and eligible effects exist
when workflow runs
then refund, stock disposition, accounting compensation, and final Return state complete once with linked evidence.

### AC02 — Partial failure recovery

Given one step times out/fails after earlier success
when workflow retries/reconciles
then completed effects are not repeated and the Return remains actionable until convergence.

### AC03 — Operator experience

Given return is pending/failed/unknown/completed
when authorized staff inspect/act
then timeline, allowed next action, correlation, and audit are visible; unsafe force-state is absent.

### AC04 — Tenant/IAM safety

Given workflow and operations are inspected
when steps execute
then trusted tenant context and least privilege are preserved throughout.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence/cleanup is recorded.

## Architecture impact

- Owning domain: Sales Return orchestration / Payment / Inventory / Accounting
- Domains touched: Return, Payment, Inventory, Accounting, Back Office, Operations, Audit
- Persistence impact: Domain state remains owned separately; workflow stores references/status only.
- Events/contracts impact: Versioned return workflow/compensation facts with correlation/causation.
- AWS/IaC impact: Accepted workflow mechanism (Step Functions if justified), workers, alarms, existing queues/tables.
- ADR required? No new ADR if following TASK-0039; create/update one if returns require a materially different orchestration mechanism.

## Security and tenant impact

- Authentication: Use established merchant/platform/internal identities.
- Authorization: Return approval/refund/recovery actions have granular permission and audit; workflow roles least-privilege.
- Tenant scoping: Trusted tenant context scopes all records, messages, and operations; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets/real card data; source/customer/business fields are minimized and redacted.
- Abuse/rate-limit considerations: Bound amounts, workflow duration/retries/transitions, manual actions, and failure injection.

## Reliability and idempotency impact

- Retry behavior: Per-step retries only when idempotency/timeout semantics allow; permanent rejection stops with action.
- Timeout semantics: Provider/step timeout remains unknown/pending until query/reconciliation evidence.
- Duplicate-delivery behavior: Duplicate workflow starts/callbacks/events/tasks converge on one Return/effect set.
- Idempotency key/strategy: ReturnRequestId is workflow business key; each step has versioned operation/source key.
- DLQ/recovery/reconciliation: Failed/stuck execution alarms; safe resume/query/compensate, DLQ and manual review are documented.

## Observability impact

- Logs: Structured logs include safe tenant/source/entity/operation/event and correlation data.
- Metrics: Track outcomes, failures, retries, duplicates, lag/age, recovery, and latency.
- Traces/correlation: Preserve correlation/causation across changed domains and providers.
- Operational states/errors: End-to-end timeline and metrics for duration, step failures/retries, stuck age, recovery outcome.

## Cost impact

- Request/compute impact: Bounded transitions/retries per return; count/measure costs.
- Storage impact: Domain state remains owned separately; workflow stores references/status only.
- Network impact: Only approved bounded external/internal traffic.
- New AWS resources/services: Accepted workflow mechanism (Step Functions if justified), workers, alarms, existing queues/tables.
- Free Tier allowance relevant to this task: Use accepted serverless allowances, disabled/low non-prod schedules, and bounded concurrency.
- Expected monthly cost change or `negligible` with rationale: negligible for learning volume; compare transition count to workflow estimate.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Workflow decisions, allowed actions, compensation ordering, UI state mapping.
- Integration: All steps with injected failure/timeout/duplicates and tenant authorization.
- Architecture: Enforce domain ownership, tenant isolation, event/idempotency, and no persistence shortcuts.
- Contract: Workflow input/output/callback/event and Return operations APIs.
- IaC: CDK assertions, synth, diff, and affected policy/routing checks.
- E2E/manual: Full/partial failure return journeys and recovery through UI.
- **Cloud verification required?** Yes — workflow wait/retry/catch/IAM/history and distributed effects require AWS.
- AWS environment/stack(s) required: Return workflow plus MockPayment/Commerce/Async resources
- Preview/staging teardown plan: Stop/remove preview executions and clear synthetic return records/resources.

