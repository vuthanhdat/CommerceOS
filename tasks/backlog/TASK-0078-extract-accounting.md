# TASK-0078 — Extract Accounting when justified

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 18
Milestone: Milestone E
Depends on: TASK-0076
Execution gate: Conditional on an accepted extraction ADR.

## Goal

If and only if its extraction ADR is accepted, Accounting operates behind a stricter independently deployable boundary while preserving immutable balanced journals and exact source-event traceability.

## Business context

Financial integrity or deployment control may justify isolation, but migration introduces high risk and cannot weaken Accounting invariants.

## In scope

- implement the accepted Accounting service/deployment identity, API/event contracts, owned ledger/inbox/posting/reconciliation data, and access controls;
- backfill/migrate/cut over chart/journal/projection/event consumers with balance/source uniqueness/divergence verification and rollback;
- preserve manual/post/reverse/report/reconciliation behavior, tenant isolation, audit, idempotency, and operational controls;

## Out of scope

- changing accounting policy, rewriting historical posted journals, adding statutory features, or extracting if ADR rejects;
- producer-domain table access or best-effort dual write;

## Acceptance criteria

### AC01 — Gate enforced

Given no accepted Accounting extraction ADR exists
when task is activated
then implementation does not proceed.

### AC02 — Ledger integrity migration

Given source ledger contains posted/draft/reversal journals
when migration/backfill validates
then counts/totals/balances/source uniqueness/links are equivalent and posted records remain immutable.

### AC03 — Event convergence

Given old/new consumers receive duplicates/replay during cutover
when processing runs
then one logical posting per source exists and divergence is detected before authority switches.

### AC04 — Rollback and security

Given cutover fails or access is tested
when rollback/authorization runs
then ledger remains usable/traceable, tenant boundaries hold, and no broad producer access is introduced.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected migration/cloud evidence, cost, rollback, and cleanup are recorded.

## Architecture impact

- Owning domain: Accounting
- Domains touched: Accounting, Platform Events, Sales/Payment/Inventory/Procurement contracts, Reporting, IaC
- Persistence impact: Move only Accounting-owned chart/journal/inbox/rules/reconciliation/projection state with immutable migration ledger.
- Events/contracts impact: Preserve versioned posting inputs and Journal events; compatibility/replay window follows ADR.
- AWS/IaC impact: Accepted independent Accounting stack/service with Lambda/SQS/DynamoDB/EventBridge/API and strict IAM; no fixed-cost database.
- ADR required? No new decision — implement accepted TASK-0076 Accounting ADR exactly.

## Security and tenant impact

- Authentication: Dedicated service/user access, strong accountant/platform separation, least-privilege producer/consumer identities.
- Authorization: Financial data/migration access is restricted/audited; encryption/redaction and tenant validation maintained.
- Tenant scoping: Isolation remains end-to-end through APIs, messages, storage, migrations, and operations; no client-supplied tenant trust.
- Sensitive data/secrets: Secrets and sensitive data are minimized, migrated securely, and never logged/embedded in events.
- Abuse/rate-limit considerations: Bound posting/query/backfill/replay and protect financial APIs from enumeration/overload.

## Reliability and idempotency impact

- Retry behavior: Backfill/events/API commands retry idempotently with original source/journal keys.
- Timeout semantics: Unknown post/migration outcome queried by source/journal/migration record.
- Duplicate-delivery behavior: Dual-run/replay cannot duplicate journal or reversal.
- Idempotency key/strategy: Preserve tenant + source/rule/event/journal/reversal keys.
- DLQ/recovery/reconciliation: Reconciliation, DLQ, migration checkpoint, rollback and restore are rehearsed.

## Observability impact

- Logs: Structured, redacted logs preserve tenant/entity/event/operation/correlation across old and new boundaries.
- Metrics: Measure latency, error, retry, queue/lag, divergence, cutover, rollback, and cost.
- Traces/correlation: Cross-boundary correlation/causation must be no worse than before.
- Operational states/errors: Ledger integrity/divergence, posting lag/failure, migration progress, API latency and cost visible.

## Cost impact

- Request/compute impact: One-off backfill/dual-run plus steady posting/query workload.
- Storage impact: Move only Accounting-owned chart/journal/inbox/rules/reconciliation/projection state with immutable migration ledger.
- Network impact: Cross-boundary traffic and data transfer are estimated/measured; no NAT/ALB introduced by default.
- New AWS resources/services: Accepted independent Accounting stack/service with Lambda/SQS/DynamoDB/EventBridge/API and strict IAM; no fixed-cost database.
- Free Tier allowance relevant to this task: Selective extraction must remain pay-per-use and fit the credit envelope unless human-approved otherwise.
- Expected monthly cost change or `negligible` with rationale: compare pre/post and accepted ADR estimate; update cost model.
- Estimated one-off cloud-test/load-test cost, if any: Estimate and approve migration/dual-run/staging cost before execution.

## Test plan

- Unit: Migration transforms, balance/immutability/source uniqueness, contract adapters.
- Integration: Backfill, duplicate event dual-run, API/IAM, reconciliation/rollback.
- Architecture: Verify new dependency/deployment/data ownership rules and removal of the old coupling.
- Contract: Independent Accounting APIs and event schemas with compatibility tests.
- IaC: CDK assertions/synth/diff plus deployment/replacement/security/cost checks.
- E2E/manual: Sale/procure/refund/manual/reversal/reports through cutover and rollback drill.
- **Cloud verification required?** Yes — financial data migration, queues/IAM/deployment/cutover require real AWS.
- AWS environment/stack(s) required: isolated staging plus old/new Accounting resources
- Preview/staging teardown plan: Retain migration evidence/backup per policy; destroy staging and temporary dual-run resources after rollback window.

