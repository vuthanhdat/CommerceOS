# TASK-0076 — Decide whether selective extraction is justified

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 18
Milestone: Milestone E
Depends on: TASK-0075
Execution gate: Produces accepted/rejected ADR decisions for each candidate.

## Goal

Each candidate domain has an accepted or rejected evidence-based ADR deciding whether selective extraction improves the system enough to justify migration and distributed cost.

## Business context

Domain boundaries are not deployment boundaries. Product Data Ingestion, Accounting, and Reporting may be extracted only when independent scale, reliability, security, ownership, or deployment pressure is real.

## In scope

- evaluate Product Data Ingestion, Accounting, Reporting, and confirm Mock Payment's existing independent boundary against TASK-0075 findings;
- for each candidate compare remain-modular, internal re-boundary, separate stack/function, and independent service options;
- write accepted/rejected ADR decisions with contract/data ownership, migration/rollback, tenant/security, reliability, observability, cost, and measurable success criteria;

## Out of scope

- implementation/migration of any extraction;
- a single blanket decision that assumes every candidate should be split;

## Acceptance criteria

### AC01 — Per-candidate evidence

Given audit metrics/findings exist
when candidate review runs
then scale/failure/security/ownership/deploy/cost pressures are quantified rather than asserted.

### AC02 — Independent decisions

Given alternatives are compared for each candidate
when ADRs are accepted/rejected
then each candidate has its own decision/rationale and remaining modular is a valid result.

### AC03 — Migration readiness

Given a candidate extraction is accepted
when its implementation task is activated
then API/event/data ownership, backfill/cutover/rollback/idempotency/IAM/cost/validation are sufficiently specified.

### AC04 — No unjustified split

Given candidate lacks threshold evidence
when decision closes
then extraction is rejected/deferred and no distributed boundary is added.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and cloud verification is N/A unless audit measurement explicitly requires it.

## Architecture impact

- Owning domain: Architecture
- Domains touched: Product Data Ingestion, Accounting, Reporting, Mock Payment, Platform
- Persistence impact: Decision defines ownership/migration for each accepted candidate; no data moves here.
- Events/contracts impact: Decision defines stable public contracts and compatibility/migration strategy.
- AWS/IaC impact: No runtime change; compare stack/service/topology costs and Free Tier impact.
- ADR required? Yes — separate accepted/rejected ADR outcome per candidate or one ADR with independently reversible decisions.

## Security and tenant impact

- Authentication: Use existing tenant/platform/service identities; any new service identity is explicitly defined.
- Authorization: Compare service identities, tenant propagation/validation, data exposure, secrets, and blast radius.
- Tenant scoping: Every accepted boundary has trusted tenant propagation and consumer validation design.
- Sensitive data/secrets: Define accounting/source/report data migration and secret handling without broadening exposure.
- Abuse/rate-limit considerations: Compare throttling/backpressure/concurrency/cost amplification for each topology.

## Reliability and idempotency impact

- Retry behavior: Define cross-service retry policy and idempotent commands/events.
- Timeout semantics: Define unknown/partial cutover/call semantics.
- Duplicate-delivery behavior: Define at-least-once/replay and dual-run divergence protection.
- Idempotency key/strategy: Preserve source/business/event keys across migration.
- DLQ/recovery/reconciliation: Define backfill/cutover/rollback/reconciliation/DLQ and disaster recovery.

## Observability impact

- Logs: Structured, redacted logs preserve tenant/entity/event/operation/correlation across old and new boundaries.
- Metrics: Measure latency, error, retry, queue/lag, divergence, cutover, rollback, and cost.
- Traces/correlation: Cross-boundary correlation/causation must be no worse than before.
- Operational states/errors: Define SLOs/metrics/correlation/alerts/divergence/cutover evidence.

## Cost impact

- Request/compute impact: Decision only; estimate one-off migration and monthly steady-state cost.
- Storage impact: Decision defines ownership/migration for each accepted candidate; no data moves here.
- Network impact: Cross-boundary traffic and data transfer are estimated/measured; no NAT/ALB introduced by default.
- New AWS resources/services: No runtime change; compare stack/service/topology costs and Free Tier impact.
- Free Tier allowance relevant to this task: Selective extraction must remain pay-per-use and fit the credit envelope unless human-approved otherwise.
- Expected monthly cost change or `negligible` with rationale: no runtime change; material accepted extraction must update cost model before implementation.
- Estimated one-off cloud-test/load-test cost, if any: None expected.

## Test plan

- Unit: N/A; use existing architecture/performance/failure evidence.
- Integration: N/A; optional proof-of-concept only if separately scoped and disposable.
- Architecture: Verify new dependency/deployment/data ownership rules and removal of the old coupling.
- Contract: Proposed independent API/event schemas and compatibility plan.
- IaC: N/A unless audit measurement changes infrastructure.
- E2E/manual: Review each candidate decision and execution gates for TASK-0077–0079.
- **Cloud verification required?** No — decision task uses existing evidence and does not deploy.
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

