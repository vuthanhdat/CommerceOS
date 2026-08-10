# TASK-0071 — Exercise DLQ recovery and failure injection

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 16
Milestone: Milestone E
Depends on: TASK-0019, TASK-0038, TASK-0052, TASK-0066

## Goal

Every asynchronous boundary has tested DLQ recovery and deterministic failure injection proving retries, duplicates, poison work, partial completion, and reconciliation remain safe.

## Business context

Documented recovery is not enough; platform hardening must exercise it across crawler, webhook, accounting, reporting, notification, and workflows.

## In scope

- inventory every queue/DLQ/event/workflow/reconciliation boundary and define failure/duplicate/out-of-order/poison/stuck scenarios;
- implement restricted deterministic failure injection and audited operations for inspect, replay, discard, pause, resume, and reconcile;
- run a bounded resilience campaign, fix defects, and improve reusable harness/runbooks/alarms;

## Out of scope

- random uncontrolled chaos, production fault injection, large load test, or direct database repair;
- weakening retry/DLQ guardrails to make tests pass;

## Acceptance criteria

### AC01 — Boundary coverage

Given async inventory is complete
when campaign plan is reviewed
then each boundary has owner, retry/timeout/idempotency/DLQ/recovery/reconciliation scenario and expected evidence.

### AC02 — Safe failure campaign

Given deterministic duplicates, poison messages, timeouts, out-of-order and partial failures are injected
when systems recover
then business effects occur at most once and committed source facts remain intact.

### AC03 — Operable recovery

Given authorized operator replays/discards/pauses/resumes/reconciles selected failures
when actions run
then outcomes are bounded, audited, correlated, and require no persistence edits.

### AC04 — Harness improvement

Given campaign reveals a defect or ambiguity
when root cause is fixed
then regression and reusable fixture/check/runbook improvement are added.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and cloud evidence, cost, and teardown/retention decisions are recorded.

## Architecture impact

- Owning domain: Platform Reliability / Operations / Engineering Harness
- Domains touched: All async consumers/producers/workflows and affected business domains
- Persistence impact: Only failure-injection config/evidence and operational actions; business stores change through normal idempotent commands.
- Events/contracts impact: Validate all async schemas/envelopes/version handling/causation under duplicates and failures.
- AWS/IaC impact: SQS/DLQ, EventBridge, Lambda, Step Functions, Scheduler, CloudWatch, test-control IAM.
- ADR required? No unless campaign demonstrates a material reliability topology change.

## Security and tenant impact

- Authentication: Failure injection/recovery controls are non-production, least-privilege, and audited.
- Authorization: No raw secrets/payload leakage in failure views; tenant-scoped recovery and explicit platform authority.
- Tenant scoping: Tenant-owned data and async context remain scoped by trusted identity; explicit audited platform access is the only cross-tenant path.
- Sensitive data/secrets: Secrets/PII/payment/audit data are minimized, protected, and redacted from logs/tests.
- Abuse/rate-limit considerations: Strict scenario, volume, duration, concurrency, and environment allowlists; global kill switch.

## Reliability and idempotency impact

- Retry behavior: Primary scope: verify bounded backoff/jitter and permanent failure classification.
- Timeout semantics: Primary scope: verify before/after commit and stuck/visibility/workflow timeouts.
- Duplicate-delivery behavior: Primary scope: prove every side-effecting consumer/command is duplicate-safe.
- Idempotency key/strategy: Audit and test keys/retention/source binding at every boundary.
- DLQ/recovery/reconciliation: Primary scope: DLQ, replay, discard, reconciliation, pause/resume and runbooks.

## Observability impact

- Logs: Structured, redacted logs retain safe tenant/entity/event/operation/correlation context.
- Metrics: Use built-in metrics first; measure security, saturation, errors, latency, failures, recovery, and cost at bounded cardinality.
- Traces/correlation: Verify end-to-end correlation/causation through affected journeys.
- Operational states/errors: Campaign evidence includes metrics/logs/correlation, detection time, recovery time, residual exceptions.

## Cost impact

- Request/compute impact: Small deterministic messages/executions only; no volume test.
- Storage impact: Only failure-injection config/evidence and operational actions; business stores change through normal idempotent commands.
- Network impact: Measured and bounded; no unapproved fixed-cost network component.
- New AWS resources/services: SQS/DLQ, EventBridge, Lambda, Step Functions, Scheduler, CloudWatch, test-control IAM.
- Free Tier allowance relevant to this task: Respect the approximately USD 100 credit envelope and normal $0–$5/month target.
- Expected monthly cost change or `negligible` with rationale: pre-estimate transitions/requests/logs and keep within a small approved experiment budget.
- Estimated one-off cloud-test/load-test cost, if any: Estimate and approve before execution; record actual spend/request volume afterward.

## Test plan

- Unit: Failure classifiers, scenario configs, recovery eligibility, redaction.
- Integration: Real AWS duplicates/redrive/timeouts/out-of-order/partial failures for each boundary.
- Architecture: Re-run and extend tenant/domain/event/IaC/security guardrails for discovered recurring risks.
- Contract: Failure/control/recovery APIs and async schema compatibility.
- IaC: CDK assertions/synth/diff plus real AWS policy/resource verification.
- E2E/manual: Run representative commerce, crawler, accounting, reporting and return failures to recovery.
- **Cloud verification required?** Yes — real SQS/EventBridge/Step Functions/Lambda retry/DLQ behavior is the point of the task.
- AWS environment/stack(s) required: ephemeral staging across selected stacks
- Preview/staging teardown plan: Disable failure injection, drain/delete synthetic queues/executions, destroy staging, verify no leaked schedule.

