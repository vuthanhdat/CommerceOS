# TASK-0052 — Reconcile missing or failed accounting postings

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 12
Milestone: Milestone D
Depends on: TASK-0050, TASK-0051

## Goal

Operators can detect committed source events missing expected Accounting journals, repair transient gaps safely, and disposition permanent failures without duplicating postings.

## Business context

Queues and retries reduce loss but do not prove completeness; financial projections need reconciliation between expected posting facts and Accounting state.

## In scope

- define expected-posting registry/query based on published events/inbox, not producer table reads;
- implement bounded scheduled reconciliation for missing, rejected, stuck, and DLQ accounting work;
- deliver authorized operations view/actions for inspect, replay after correction, acknowledge policy exception, and audit outcome;

## Out of scope

- editing posted journals or source business transactions;
- global exactly-once claim, statutory close, or blind bulk DLQ replay;

## Acceptance criteria

### AC01 — Missing detection

Given a supported source event is committed/published but no expected journal exists after threshold
when reconciliation runs
then a tenant/source-specific missing-posting exception is created.

### AC02 — Safe repair

Given a transient/rule/config issue is corrected
when authorized replay/reconcile runs
then one balanced journal posts or existing result is returned without duplicate.

### AC03 — Permanent disposition

Given event is invalid/unsupported or intentionally non-posting
when operator applies an allowed disposition
then reason, actor, evidence, and audit trail are retained without faking a journal.

### AC04 — No producer shortcut

Given reconciliation implementation is reviewed
when source completeness is determined
then it uses event/public contracts and Accounting records, not direct Sales/Inventory/Procurement tables.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected AWS evidence/teardown is recorded.

## Architecture impact

- Owning domain: Accounting Operations / Platform Events / Audit
- Domains touched: Accounting, Async Operations, Back Office, Audit
- Persistence impact: Add expected-posting/reconciliation exception/action records; posted journals remain immutable.
- Events/contracts impact: Consume publication/inbox evidence; emit bounded operational notification facts.
- AWS/IaC impact: EventBridge Scheduler, reconciliation Lambda, accounting DLQ/CloudWatch, existing DynamoDB.
- ADR required? No — accepted architecture covers this scope.

## Security and tenant impact

- Authentication: Use established merchant/internal worker identity.
- Authorization: Tenant accountants see own exceptions; platform admin cross-tenant access is explicit/audited.
- Tenant scoping: Every record, command, event, and projection is scoped by trusted tenant context; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Bound schedule cadence/batch/replay; require reason and no blind mass action.

## Reliability and idempotency impact

- Retry behavior: Transient reconciliation jobs retry boundedly; repeated repair uses source uniqueness.
- Timeout semantics: Stuck age thresholds and unknown outcomes are explicit; query before retry.
- Duplicate-delivery behavior: Repeated detection/action does not create duplicate exception or posting.
- Idempotency key/strategy: Tenant + expected posting type + logical source/event; action id for audit.
- DLQ/recovery/reconciliation: This is the authoritative missing-posting/DLQ recovery workflow.

## Observability impact

- Logs: Structured logs carry safe tenant, source, entity, event/command, and correlation/causation identifiers.
- Metrics: Measure validation rejects, duplicates, failures, latency, backlog/stuck state, and recovery results.
- Traces/correlation: Preserve correlation/causation end-to-end.
- Operational states/errors: Metrics show expected/missing/stuck/rejected/repaired age/count and resolution time.

## Cost impact

- Request/compute impact: Low-frequency Scheduler and bounded batches; disabled/manual in preview.
- Storage impact: Add expected-posting/reconciliation exception/action records; posted journals remain immutable.
- Network impact: Bounded internal API/event traffic only.
- New AWS resources/services: EventBridge Scheduler, reconciliation Lambda, accounting DLQ/CloudWatch, existing DynamoDB.
- Free Tier allowance relevant to this task: Use documented serverless allowances, short logs, small batches, and disabled/low schedules in non-prod.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; monitor queue/schedule/log growth.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused preview/dev verification.

## Test plan

- Unit: Expected-posting rules, exception lifecycle, replay/disposition permissions.
- Integration: Scheduler, DLQ, duplicate detection, repair to atomic journal, tenant isolation.
- Architecture: Enforce domain ownership, inward dependencies, event conventions, and tenant rules.
- Contract: Reconciliation exception/action APIs and operational notification facts.
- IaC: CDK assertions, synth, diff, and affected resource policy checks.
- E2E/manual: Suppress/fail one posting, detect it, repair once, and audit outcome.
- **Cloud verification required?** Yes — Scheduler, SQS/DLQ, Lambda, alarms, and concurrency need AWS.
- AWS environment/stack(s) required: Accounting/Async/reconciliation resources
- Preview/staging teardown plan: Disable preview schedule, clear synthetic failures, destroy ephemeral resources.

