# TASK-0019 — Make crawler failures observable and recoverable

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 3
Milestone: Milestone A
Depends on: TASK-0015, TASK-0016, TASK-0017

## Goal

Operators can detect, diagnose, safely replay, or discard crawler failures and poison jobs without affecting storefront or order processing.

## Business context

An asynchronous crawler is only operable when backlog, parser failures, source failures, and DLQ recovery are visible and bounded.

## In scope

- add per-source/queue metrics, alarms, structured failure records, and an operations view;
- implement authorized DLQ inspection, redrive/replay, discard, and source-pause workflows with audit evidence;
- prove crawler isolation, bounded concurrency, and recovery for transient, parser, policy, and poison scenarios;

## Out of scope

- scheduled crawling and second source;
- generic organization-wide incident-management platform;

## Acceptance criteria

### AC01 — Failure visibility

Given crawler work fails or queue age/depth crosses thresholds
when operators inspect the system
then source, parser version, job, safe reason, attempts, queue age, and correlation are visible.

### AC02 — Safe recovery

Given an authorized operator selects a corrected retryable DLQ item
when replay runs
then one new controlled attempt occurs without duplicate snapshot/catalog effects and the action is audited.

### AC03 — Core isolation

Given crawler workers are throttled, paused, or failing repeatedly
when storefront/order workloads run
then crawler concurrency and resources do not consume unbounded capacity or block core commerce.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected real-AWS evidence plus teardown is recorded.

## Architecture impact

- Owning domain: Product Data Ingestion / Platform Operations / Audit
- Domains touched: Ingestion, Back Office operations, Notification hooks
- Persistence impact: Failure records/replay decisions are append-oriented; no duplication of raw payloads beyond retention.
- Events/contracts impact: Operational CrawlFailed/CrawlRecovered notifications use stable job/event ids.
- AWS/IaC impact: CloudWatch metrics/alarms/dashboard, SQS/DLQ redrive controls, bounded Lambda concurrency.
- ADR required? No — operationalizes already accepted crawler resources.

## Security and tenant impact

- Authentication: Only platform/authorized operations roles inspect payload metadata or replay/discard jobs.
- Authorization: Failure views redact raw content/secrets and preserve tenant visibility boundaries.
- Tenant scoping: All merchant-owned reads/writes derive tenant scope from trusted context; client tenant ids cannot override it and cross-tenant denial is tested.
- Sensitive data/secrets: No secrets, tokens, real card data, or unnecessary personal data are stored or logged.
- Abuse/rate-limit considerations: Replay is rate/concurrency limited and cannot override source kill switches.

## Reliability and idempotency impact

- Retry behavior: Replay requires classification and operator intent; no bulk blind replay.
- Timeout semantics: Oldest-message age and stuck-processing thresholds raise actionable alarms.
- Duplicate-delivery behavior: Replay preserves original source/job identity so completed effects remain deduplicated.
- Idempotency key/strategy: Original crawlJobId/source identity plus a distinct audited replay attempt id.
- DLQ/recovery/reconciliation: DLQ is the recovery source; discard and source pause are explicit audited outcomes.

## Observability impact

- Logs: Structured logs include safe tenant/entity/job identifiers, operation, and correlation context.
- Metrics: Queue depth/age, DLQ depth, fetch/parser outcomes, throttles, duration, bytes, replay result.
- Traces/correlation: Preserve request/correlation identifiers across changed boundaries.
- Operational states/errors: Operations UI exposes source health, failure category, and next safe action.

## Cost impact

- Request/compute impact: Built-in metrics preferred; low-cardinality custom metrics and bounded replay only.
- Storage impact: Failure records/replay decisions are append-oriented; no duplication of raw payloads beyond retention.
- Network impact: Normal API traffic only unless external fetching is explicitly in scope.
- New AWS resources/services: CloudWatch metrics/alarms/dashboard, SQS/DLQ redrive controls, bounded Lambda concurrency.
- Free Tier allowance relevant to this task: Use the documented Lambda/DynamoDB/S3/SQS/CloudWatch allowances and non-production caps where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible; alarms/dashboards stay within the learning profile where practical.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview verification.

## Test plan

- Unit: Failure classification, replay eligibility, redaction, and kill-switch enforcement.
- Integration: Real SQS redrive/DLQ, Lambda concurrency, CloudWatch alarms, and duplicate replay.
- Architecture: Enforce domain dependency direction, ownership, and trusted tenant context.
- Contract: Operational failure/replay APIs and notification facts.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Force a poison job to DLQ, repair/replay it once, and verify isolation.
- **Cloud verification required?** Yes — SQS redrive, concurrency, alarms, and worker isolation are real-AWS semantics.
- AWS environment/stack(s) required: dev or ephemeral CrawlerStack plus operations endpoints
- Preview/staging teardown plan: Destroy preview resources and clear synthetic DLQ/raw test data.

