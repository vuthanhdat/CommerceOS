# TASK-0079 — Extract Reporting when justified

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 18
Milestone: Milestone E
Depends on: TASK-0076
Execution gate: Conditional on an accepted extraction ADR.

## Goal

If and only if its extraction ADR is accepted, Reporting operates as an independently deployable projection/query boundary that can rebuild without affecting transactional domains.

## Business context

Reporting may justify isolation when query/projector load diverges, but extraction must preserve tenant-safe event contracts and projection correctness.

## In scope

- implement accepted Reporting service/stack identity, event inputs, owned projections/checkpoints/query APIs, and dashboard client boundary;
- rebuild/backfill and dual-run projections, compare results/freshness, cut over queries, and retain rollback;
- preserve duplicate/out-of-order handling, DLQ/rebuild, tenant authorization, drill-through contracts, observability, and cost limits;

## Out of scope

- new analytics products/data lake/search engine or extraction if rejected;
- moving transactional source truth into Reporting;

## Acceptance criteria

### AC01 — Gate enforced

Given Reporting extraction ADR is not accepted
when task activation occurs
then no independent boundary is deployed.

### AC02 — Projection equivalence

Given old/new projectors process retained/synthetic events
when dual-run comparison executes
then KPI/financial/notification-facing values and freshness match within explicit semantics.

### AC03 — Failure isolation and rebuild

Given new Reporting fails/backlogs or projection is deleted
when commerce continues and rebuild runs
then source transactions remain unaffected and projection recovers without producer-table scans.

### AC04 — Cutover/rollback

Given query clients switch to new API and divergence/failure is injected
when rollback plan runs
then tenant-safe dashboards restore without lost/duplicated reporting state.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected migration/cloud evidence, cost, rollback, and cleanup are recorded.

## Architecture impact

- Owning domain: Reporting & Analytics
- Domains touched: Reporting, Platform Events, Back Office dashboards, Accounting query/event contracts, IaC
- Persistence impact: Move/rebuild only Reporting-owned projections/checkpoints; transactional domains remain source of truth.
- Events/contracts impact: Preserve versioned projection inputs and compatibility/replay policy.
- AWS/IaC impact: Accepted independent Reporting stack/service using Lambda/SQS/DynamoDB/EventBridge/API; no analytics service unless ADR explicitly approves.
- ADR required? No new decision — implement accepted TASK-0076 Reporting ADR.

## Security and tenant impact

- Authentication: Use existing tenant/platform/service identities; any new service identity is explicitly defined.
- Authorization: Dedicated query/worker identities; tenant authorization and platform aggregates remain separated.
- Tenant scoping: Isolation remains end-to-end through APIs, messages, storage, migrations, and operations; no client-supplied tenant trust.
- Sensitive data/secrets: Secrets and sensitive data are minimized, migrated securely, and never logged/embedded in events.
- Abuse/rate-limit considerations: Bound report ranges/groupings/rebuild/event replay and query rates.

## Reliability and idempotency impact

- Retry behavior: Projection/backfill retries boundedly with checkpoints.
- Timeout semantics: Query timeout returns explicit unavailable/stale result; rebuild/cutover state remains visible.
- Duplicate-delivery behavior: Dual-run/replay does not inflate projections.
- Idempotency key/strategy: Projection + tenant + event/entity version/checkpoint.
- DLQ/recovery/reconciliation: DLQ/rebuild/backfill/rollback are rehearsed and source truth untouched.

## Observability impact

- Logs: Structured, redacted logs preserve tenant/entity/event/operation/correlation across old and new boundaries.
- Metrics: Measure latency, error, retry, queue/lag, divergence, cutover, rollback, and cost.
- Traces/correlation: Cross-boundary correlation/causation must be no worse than before.
- Operational states/errors: Projection lag/divergence/rebuild/query latency/errors/cost/cutover visible.

## Cost impact

- Request/compute impact: One-off dual projection/rebuild plus steady worker/query cost.
- Storage impact: Move/rebuild only Reporting-owned projections/checkpoints; transactional domains remain source of truth.
- Network impact: Cross-boundary traffic and data transfer are estimated/measured; no NAT/ALB introduced by default.
- New AWS resources/services: Accepted independent Reporting stack/service using Lambda/SQS/DynamoDB/EventBridge/API; no analytics service unless ADR explicitly approves.
- Free Tier allowance relevant to this task: Selective extraction must remain pay-per-use and fit the credit envelope unless human-approved otherwise.
- Expected monthly cost change or `negligible` with rationale: measure pre/post event/query/storage; update cost model.
- Estimated one-off cloud-test/load-test cost, if any: Estimate and approve migration/dual-run/staging cost before execution.

## Test plan

- Unit: Projection migration/compatibility/idempotency and query adapters.
- Integration: Event replay/dual-run comparison, rebuild, IAM/API, rollback.
- Architecture: Verify new dependency/deployment/data ownership rules and removal of the old coupling.
- Contract: Independent Reporting query/event contracts and consumer compatibility.
- IaC: CDK assertions/synth/diff plus deployment/replacement/security/cost checks.
- E2E/manual: KPI/financial/dashboard journeys through cutover/failure/rebuild/rollback.
- **Cloud verification required?** Yes — independent queue/API/storage/deployment and rebuild/cutover require AWS.
- AWS environment/stack(s) required: isolated staging plus old/new Reporting resources
- Preview/staging teardown plan: Destroy staging/temporary dual-run resources after evidence and rollback window.

