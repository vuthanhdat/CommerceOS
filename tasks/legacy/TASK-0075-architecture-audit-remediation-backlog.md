# TASK-0075 — Audit the architecture and generate remediation work

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 17
Milestone: Milestone E
Depends on: TASK-0074
Execution gate: Architecture changes are outputs as new tasks/ADRs, not silently implemented here.

## Goal

CommerceOS has an evidence-backed architecture audit covering domain boundaries, coupling, contracts, retries, idempotency, workflow value, cost, latency, and operations, with every finding converted into an owned remediation task or explicit no-action decision.

## Business context

Selective extraction is prohibited until the modular architecture is measured and its real pain is understood.

## In scope

- audit bounded contexts, code/dependency graphs, persistence ownership/leaks, shared code, APIs/events/queues/workflows, Lambda responsibilities, IAM, tenant isolation, retries/idempotency/reconciliation;
- analyze measured cost, latency, failures, deployment frequency/ownership, scaling and operational burden using TASK-0074 evidence;
- produce prioritized findings with severity/evidence/root cause, proposed harness improvement, ADR trigger, task owner/dependencies, and explicit decisions for no-action items;

## Out of scope

- implementing broad remediation or extracting services;
- splitting domains to claim microservices without evidence;

## Acceptance criteria

### AC01 — Boundary audit

Given all modules/contracts/data stores/deployments are mapped
when review completes
then wrong-direction dependencies, cross-domain persistence, god functions/shared models, and ownership ambiguity are evidenced or explicitly absent.

### AC02 — Distributed audit

Given events/queues/workflows/external calls are reviewed
when failure matrix is applied
then unsafe retries, missing idempotency/reconciliation, unstable contracts, useless queues/workflows, and observability gaps are recorded.

### AC03 — Measured priorities

Given cost/latency/failure/deployment/scale data is analyzed
when findings are ranked
then each priority cites evidence, consequence, remediation type, and expected validation.

### AC04 — Actionable backlog

Given audit closes
when findings are dispositioned
then each accepted remediation has a scoped task/ADR/harness action and no code is silently changed under this audit.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and cloud verification is N/A unless audit measurement explicitly requires it.

## Architecture impact

- Owning domain: Architecture / Engineering Harness
- Domains touched: All domains, contracts, infrastructure, operations, cost/security
- Persistence impact: Read-only audit by default; findings may propose migrations in later tasks.
- Events/contracts impact: Inventory/schema/producer/consumer/version/usage review only.
- AWS/IaC impact: Read-only CDK/deployed metric/cost review; no new resources by default.
- ADR required? No for the audit itself; its accepted material findings trigger new ADRs.

## Security and tenant impact

- Authentication: Use existing tenant/platform/service identities; any new service identity is explicitly defined.
- Authorization: Audit explicit trusted-tenant paths, IAM, platform admin, secrets, public surfaces, and sensitive contracts.
- Tenant scoping: Isolation remains end-to-end through APIs, messages, storage, migrations, and operations; no client-supplied tenant trust.
- Sensitive data/secrets: Evidence is redacted; do not copy production secrets/data into reports.
- Abuse/rate-limit considerations: Review throttling/amplification/cost-abuse protections and missing limits.

## Reliability and idempotency impact

- Retry behavior: Audit every retry policy and transient/permanent classification.
- Timeout semantics: Audit ambiguous/partial timeout semantics for every distributed boundary.
- Duplicate-delivery behavior: Audit every side-effecting consumer/command under replay.
- Idempotency key/strategy: Map key/source/retention/tenant binding and flag gaps.
- DLQ/recovery/reconciliation: Audit DLQ, reconciliation, restore, workflow/manual recovery and runbook evidence.

## Observability impact

- Logs: Structured, redacted logs preserve tenant/entity/event/operation/correlation across old and new boundaries.
- Metrics: Measure latency, error, retry, queue/lag, divergence, cutover, rollback, and cost.
- Traces/correlation: Cross-boundary correlation/causation must be no worse than before.
- Operational states/errors: Audit whether important failures are diagnosable without debugger/manual table inspection.

## Cost impact

- Request/compute impact: Documentation/analysis only; use existing measurements.
- Storage impact: Read-only audit by default; findings may propose migrations in later tasks.
- Network impact: Cross-boundary traffic and data transfer are estimated/measured; no NAT/ALB introduced by default.
- New AWS resources/services: Read-only CDK/deployed metric/cost review; no new resources by default.
- Free Tier allowance relevant to this task: Selective extraction must remain pay-per-use and fit the credit envelope unless human-approved otherwise.
- Expected monthly cost change or `negligible` with rationale: negligible; no new runtime resources.
- Estimated one-off cloud-test/load-test cost, if any: None expected.

## Test plan

- Unit: N/A; architecture/static reports may be generated from existing code/tests.
- Integration: Re-run representative architecture/security/failure evidence cited by findings.
- Architecture: Verify new dependency/deployment/data ownership rules and removal of the old coupling.
- Contract: Audit all public/event/provider/adapter/workflow contracts and compatibility policy.
- IaC: N/A unless audit measurement changes infrastructure.
- E2E/manual: Review critical sell/failure/procure/account/refund/recovery journeys against evidence.
- **Cloud verification required?** No — uses existing artifacts/measurements; any new measurement must be separately bounded.
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

