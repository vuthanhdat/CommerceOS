# TASK-0080 — Validate architecture, reliability, and cost after extraction

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 18
Milestone: Milestone E
Depends on: TASK-0077, TASK-0078, TASK-0079
Execution gate: Run against only the extraction work actually approved and completed.

## Goal

After all approved extractions—and explicit rejections for the rest—CommerceOS proves end-to-end correctness, tenant isolation, failure recovery, latency, cost, and operational clarity against the pre-extraction baseline.

## Business context

A successful deployment is not proof that decomposition improved the system; benefits and regressions must be measured after the migration decisions settle.

## In scope

- resolve TASK-0077–0079 dependencies as completed extraction or accepted not-applicable/rejected decision;
- run architecture/security/contract/data-integrity/failure/performance/cost comparisons across approved new boundaries;
- update ADR validation, architecture/domain/deployment docs, cost model, runbooks, and create remediation/rollback decisions for unmet criteria;

## Out of scope

- new extraction or unrelated feature work;
- declaring success from service count or green unit tests alone;

## Acceptance criteria

### AC01 — Decision closure

Given each extraction candidate is reviewed
when validation begins
then every candidate is either completed under accepted ADR or explicitly rejected/deferred with no orphan task/resource.

### AC02 — Behavior/integrity

Given critical onboarding/sell/failure/procure/account/report/refund journeys run
when old baseline and new system are compared
then business/financial/inventory/tenant outcomes remain correct and traceable.

### AC03 — Benefit evidence

Given accepted extraction success metrics are measured
when results are reviewed
then reliability/scale/security/deploy/operations benefit is demonstrated or rollback/remediation decision is triggered.

### AC04 — Cost/complexity evidence

Given request/event/latency/failure/operational/monthly cost is compared
when audit closes
then trade-offs and cost-model/ADR validation are updated with no hidden infrastructure.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected migration/cloud evidence, cost, rollback, and cleanup are recorded.

## Architecture impact

- Owning domain: Architecture / Verification / Operations
- Domains touched: All approved extracted and remaining modular domains
- Persistence impact: Read/validate/migrate only through approved extraction plans; no new source of truth.
- Events/contracts impact: Contract/version/correlation/replay compatibility validated end-to-end.
- AWS/IaC impact: Existing post-extraction resources plus isolated validation staging; no new permanent service by default.
- ADR required? Update validation/consequences of accepted/rejected extraction ADRs; new ADR only for remediation decision.

## Security and tenant impact

- Authentication: Use existing tenant/platform/service identities; any new service identity is explicitly defined.
- Authorization: Re-run platform/merchant/service IAM and tenant isolation across every new boundary.
- Tenant scoping: Cross-tenant attack suite includes API, events, queues, migrations, reports, admin and failure recovery.
- Sensitive data/secrets: Validate logs/events/migration artifacts/backup cleanup/redaction.
- Abuse/rate-limit considerations: Retest throttling/backpressure/cost amplification across new boundaries.

## Reliability and idempotency impact

- Retry behavior: Exercise transient retries across new network/event boundaries.
- Timeout semantics: Exercise ambiguous timeouts and partial outages at each extracted service.
- Duplicate-delivery behavior: Replay/dual delivery across new boundaries must preserve one logical effect.
- Idempotency key/strategy: Verify keys survived migration and service hops.
- DLQ/recovery/reconciliation: Run DLQ/reconciliation/rebuild/rollback/restore paths and confirm runbooks.

## Observability impact

- Logs: Structured, redacted logs preserve tenant/entity/event/operation/correlation across old and new boundaries.
- Metrics: Measure latency, error, retry, queue/lag, divergence, cutover, rollback, and cost.
- Traces/correlation: Cross-boundary correlation/causation must be no worse than before.
- Operational states/errors: Compare correlation, dashboards, alarms, recovery time and ownership to baseline.

## Cost impact

- Request/compute impact: Bounded validation campaign and post-extraction steady-state measurement.
- Storage impact: Read/validate/migrate only through approved extraction plans; no new source of truth.
- Network impact: Cross-boundary traffic and data transfer are estimated/measured; no NAT/ALB introduced by default.
- New AWS resources/services: Existing post-extraction resources plus isolated validation staging; no new permanent service by default.
- Free Tier allowance relevant to this task: Selective extraction must remain pay-per-use and fit the credit envelope unless human-approved otherwise.
- Expected monthly cost change or `negligible` with rationale: update actual/estimated monthly and one-off migration cost; benefits must justify increase.
- Estimated one-off cloud-test/load-test cost, if any: Estimate and approve migration/dual-run/staging cost before execution.

## Test plan

- Unit: Regression/invariant suites across migrated domain code.
- Integration: All new APIs/events/queues/storage/IAM plus cross-tenant and failure tests.
- Architecture: Verify new dependency/deployment/data ownership rules and removal of the old coupling.
- Contract: Producer/consumer compatibility for every extracted boundary.
- IaC: CDK assertions/synth/diff plus deployment/replacement/security/cost checks.
- E2E/manual: Full critical journeys, service outage/recovery, performance/cost comparison.
- **Cloud verification required?** Yes — post-extraction deployment/network/IAM/queue/storage/failure/cost behavior requires AWS.
- AWS environment/stack(s) required: isolated ephemeral staging mirroring approved final topology
- Preview/staging teardown plan: Destroy validation staging/temporary migration resources and confirm final inventory matches CDK.

