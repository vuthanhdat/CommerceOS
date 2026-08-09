# TASK-0039 — Decide checkout orchestration from measured complexity

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 9
Milestone: Milestone B
Depends on: TASK-0034, TASK-0035, TASK-0036, TASK-0037
Execution gate: Produces an ADR; implementation depends on its accepted decision.

## Goal

CommerceOS has an evidence-based accepted ADR deciding whether checkout/payment complexity now justifies Step Functions, continued application-code orchestration, or another bounded mechanism.

## Business context

The roadmap deliberately postpones orchestration until timeout, retry, waiting, branching, and compensation pain can be measured.

## In scope

- measure current flow states, failure branches, retries, waiting duration, observability gaps, operational burden, and transition/request cost;
- compare application-code orchestration, Step Functions Standard, and credible alternatives against security, reliability, reversibility, testing, and Free Tier constraints;
- write an ADR with decision, migration boundary, state ownership, compensation semantics, validation metrics, and rollback plan;

## Out of scope

- implementing the selected workflow;
- rewriting unrelated Sales/Inventory/Payment domain rules or adding Step Functions merely for demonstration;

## Acceptance criteria

### AC01 — Evidence captured

Given Phase 5–8 flows and failures exist
when the decision analysis runs
then actual complexity, recovery gaps, measured/estimated transitions, and operational needs are documented.

### AC02 — Decision recorded

Given credible orchestration alternatives are compared
when the ADR is reviewed
then one option is accepted/rejected with explicit consequences, cost, security, failure, and migration rationale.

### AC03 — Implementation contract

Given an orchestration mechanism is accepted
when TASK-0040 is activated
then domain/application contracts, workflow states, idempotency boundaries, rollout, rollback, and verification are sufficiently specified.

### AC04 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and cloud verification is explicitly N/A unless AWS semantics changed.

## Architecture impact

- Owning domain: Architecture / Sales / Payment / Inventory
- Domains touched: Sales, Payment, Inventory, Platform workflow/observability
- Persistence impact: Decision only; document ownership/migration of workflow execution and existing domain state.
- Events/contracts impact: Decision defines callback/event continuation contracts if selected.
- AWS/IaC impact: No resource in this decision task; Step Functions cost/transition estimates are evaluated.
- ADR required? Yes — required output using ADR template.

## Security and tenant impact

- Authentication: Analyze workflow IAM/entry authorization and test-control isolation.
- Authorization: Decision must preserve tenant context at every workflow step and least-privilege service roles.
- Tenant scoping: No workflow input may treat caller tenantId as trusted.
- Sensitive data/secrets: No real card data or secrets are stored/logged; personal/business data is minimized and redacted.
- Abuse/rate-limit considerations: Bound payloads, retries, concurrency, and operational controls.

## Reliability and idempotency impact

- Retry behavior: Document per-step retry classes, max attempts, backoff, and compensation safety.
- Timeout semantics: Define timeout/wait/unknown-state semantics and source of truth.
- Duplicate-delivery behavior: Define how repeated starts/callbacks/tasks converge.
- Idempotency key/strategy: Map keys at checkout, reservation, payment, status transition, and compensation boundaries.
- DLQ/recovery/reconciliation: Define failed execution, manual intervention, redrive/reconciliation, and rollback behavior.

## Observability impact

- Logs: Structured logs carry safe tenant/entity/operation/event and correlation identifiers.
- Metrics: Measure outcomes, failures, retries, duplicates, latency, and stuck states at bounded cardinality.
- Traces/correlation: Preserve correlation/causation across all changed boundaries.
- Operational states/errors: ADR specifies required execution history, correlation, metrics, alarms, and operational UI integration.

## Cost impact

- Request/compute impact: Decision includes learning/beta transition/request estimates and retry sensitivity.
- Storage impact: Decision only; document ownership/migration of workflow execution and existing domain state.
- Network impact: Only bounded API/event traffic.
- New AWS resources/services: No resource in this decision task; Step Functions cost/transition estimates are evaluated.
- Free Tier allowance relevant to this task: Use accepted serverless allowances and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: documentation only; chosen mechanism's monthly/experiment cost is estimated and cost model updated if material.
- Estimated one-off cloud-test/load-test cost, if any: None expected.

## Test plan

- Unit: N/A — decision evidence may use existing test measurements.
- Integration: N/A beyond collecting existing failure/operational evidence.
- Architecture: Check domain boundaries, tenant context, and no persistence shortcuts.
- Contract: Workflow/application/event contracts are specified, not implemented.
- IaC: N/A unless infrastructure changes.
- E2E/manual: Review existing Phase 5–8 failure journeys and perform a small cost/transition model.
- **Cloud verification required?** No — the task produces a decision; any optional measurement uses existing dev resources and is documented.
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

