# TASK-0077 — Extract Product Data Ingestion when justified

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 18
Milestone: Milestone E
Depends on: TASK-0076
Execution gate: Conditional on an accepted extraction ADR.

## Goal

If and only if its extraction ADR is accepted, Product Data Ingestion operates behind an independently deployable/scalable boundary without changing Catalog ownership or merchant import behavior.

## Business context

Crawler scale/failure/policy characteristics may justify isolation, but extraction must preserve source safety and end-to-end operability.

## In scope

- implement the exact accepted deployment/service boundary, identity, public/internal contracts, and owned source/snapshot/job data;
- migrate/backfill/cut over queued/manual/scheduled ingestion with dual-run/divergence safeguards and rollback;
- preserve source registry policy, rates, kill switches, idempotency, DLQ/recovery, import-review contract, metrics, and cost limits;

## Out of scope

- new crawler capabilities, new sources, policy bypass, or canonical Catalog ownership change;
- execution if TASK-0076 rejects/defers this extraction;

## Acceptance criteria

### AC01 — Gate enforced

Given no accepted Product Data Ingestion extraction ADR exists
when task activation/deployment is attempted
then work does not proceed and backlog status remains conditional.

### AC02 — Contract-preserving cutover

Given accepted migration plan and synthetic data/jobs exist
when cutover runs
then manual/scheduled ingestion and merchant import produce equivalent outcomes through the new boundary.

### AC03 — Failure isolation

Given new ingestion service backlogs/fails/pauses
when core commerce runs
then Storefront/order/payment remain available and crawler work is recoverable without cross-domain storage access.

### AC04 — Rollback/data integrity

Given migration/cutover failure or divergence occurs
when rollback/reconciliation runs
then no job/snapshot/import effect is lost/duplicated and old path can be restored per ADR.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected migration/cloud evidence, cost, rollback, and cleanup are recorded.

## Architecture impact

- Owning domain: Product Data Ingestion
- Domains touched: Ingestion, Catalog import contract, Platform Events/Operations, IaC
- Persistence impact: Move only Ingestion-owned registry/job/snapshot/history state per ADR with verified backfill/cutover; Catalog stays owned separately.
- Events/contracts impact: Versioned ingestion API/job/source-change/import contracts remain compatible or use explicit migration versions.
- AWS/IaC impact: Accepted independent Crawler/Ingestion stacks/services using pay-per-use Lambda/SQS/S3/DynamoDB/EventBridge; no default fixed-cost network.
- ADR required? No new decision — strictly implement accepted TASK-0076 ADR; update if migration must diverge.

## Security and tenant impact

- Authentication: Use existing tenant/platform/service identities; any new service identity is explicitly defined.
- Authorization: Dedicated least-privilege service/deployment identity, approved outbound sources, secret isolation, authenticated tenant/internal calls.
- Tenant scoping: Isolation remains end-to-end through APIs, messages, storage, migrations, and operations; no client-supplied tenant trust.
- Sensitive data/secrets: Secrets and sensitive data are minimized, migrated securely, and never logged/embedded in events.
- Abuse/rate-limit considerations: Preserve per-source/tenant rate/concurrency/budget/kill controls during dual-run and after cutover.

## Reliability and idempotency impact

- Retry behavior: Migration and new consumers retry boundedly with original job/event keys.
- Timeout semantics: Cross-boundary command timeout returns accepted/unknown/queryable job state.
- Duplicate-delivery behavior: Dual-run/replay cannot duplicate fetch/snapshot/change/import effects.
- Idempotency key/strategy: Preserve crawlJob/sourceTarget/window/importCandidate/event identities.
- DLQ/recovery/reconciliation: DLQ/reconciliation/rollback and paused-source behavior are rehearsed.

## Observability impact

- Logs: Structured, redacted logs preserve tenant/entity/event/operation/correlation across old and new boundaries.
- Metrics: Measure latency, error, retry, queue/lag, divergence, cutover, rollback, and cost.
- Traces/correlation: Cross-boundary correlation/causation must be no worse than before.
- Operational states/errors: Old/new path metrics, divergence, backlog, latency, errors, cost, cutover and rollback visible.

## Cost impact

- Request/compute impact: One-off migration/dual-run plus steady crawler workload; concurrency remains bounded.
- Storage impact: Move only Ingestion-owned registry/job/snapshot/history state per ADR with verified backfill/cutover; Catalog stays owned separately.
- Network impact: Cross-boundary traffic and data transfer are estimated/measured; no NAT/ALB introduced by default.
- New AWS resources/services: Accepted independent Crawler/Ingestion stacks/services using pay-per-use Lambda/SQS/S3/DynamoDB/EventBridge; no default fixed-cost network.
- Free Tier allowance relevant to this task: Selective extraction must remain pay-per-use and fit the credit envelope unless human-approved otherwise.
- Expected monthly cost change or `negligible` with rationale: must not exceed accepted ADR baseline without new approval; measure before/after.
- Estimated one-off cloud-test/load-test cost, if any: Estimate and approve migration/dual-run/staging cost before execution.

## Test plan

- Unit: Contract adapters, migration mappings, idempotency, policy/kill rules.
- Integration: Old/new compatibility, data backfill, queue replay, IAM/network, rollback.
- Architecture: Verify new dependency/deployment/data ownership rules and removal of the old coupling.
- Contract: Independent ingestion API/job/event/import schemas and compatibility tests.
- IaC: CDK assertions/synth/diff plus deployment/replacement/security/cost checks.
- E2E/manual: Manual and scheduled source journeys through cutover/failure/rollback.
- **Cloud verification required?** Yes — independent deployment, IAM, queues/storage, migration/cutover and failure isolation require AWS.
- AWS environment/stack(s) required: isolated staging plus old/new ingestion resources
- Preview/staging teardown plan: Remove old/temporary dual-run resources only after rollback window/evidence; destroy staging copies.

