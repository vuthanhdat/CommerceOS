# TASK-0060 — Refresh mapped sources safely on a schedule

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 14
Milestone: Milestone D
Depends on: TASK-0059

## Goal

Mapped external products refresh on a policy-compliant schedule with per-source concurrency/rate limits, jitter, and kill switches that prevent uncontrolled crawling.

## Business context

Scheduled refresh creates recurring cost and source load; safety must be enforced before turning it on.

## In scope

- dispatch only known mapped targets through EventBridge Scheduler at source-approved cadence/windows;
- implement per-source concurrency/rate/batch caps, jitter, pause/disable enforcement, overlapping-run suppression, and environment defaults;
- provide schedule management, missed-run/catch-up policy, cost estimate, alarms, and teardown/disable controls;

## Out of scope

- category/search discovery, minute-level refresh, Amazon adapter, or automatic catalog overwrite;
- running schedules in CI/preview by default;

## Acceptance criteria

### AC01 — Bounded scheduled dispatch

Given an active source has mapped targets and an enabled schedule
when the window arrives
then only a bounded deduplicated batch is queued within source rate/concurrency policy.

### AC02 — Kill and environment safety

Given source is paused/disabled or environment is preview/dev default-disabled
when schedule/worker evaluates work
then no unsafe fetch occurs and queued work respects the kill policy.

### AC03 — Overlap and retry

Given scheduler duplicates or a prior run overlaps
when dispatch executes
then one logical refresh per target/window is accepted and backlog remains bounded.

### AC04 — Cost visibility

Given schedule cadence/targets/retries change
when configuration is reviewed
then estimated invocations/requests/storage and guardrail impact are visible before enablement.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence/cleanup is recorded.

## Architecture impact

- Owning domain: Product Data Ingestion / Platform Scheduling
- Domains touched: Ingestion, Source Registry, Crawler operations, Cost governance
- Persistence impact: Add schedule/refresh window/target checkpoint/idempotency state; snapshots remain immutable.
- Events/contracts impact: CrawlScheduled and refresh job contracts include source/target/window identity.
- AWS/IaC impact: EventBridge Scheduler rules/role plus existing SQS/worker/concurrency/alarms.
- ADR required? No — accepted architecture covers the scope.

## Security and tenant impact

- Authentication: Use established merchant/platform/internal identities.
- Authorization: Only authorized operators enable/change schedules; Scheduler role can enqueue only the intended queue.
- Tenant scoping: Trusted tenant context scopes all records, messages, and operations; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets/real card data; source/customer/business fields are minimized and redacted.
- Abuse/rate-limit considerations: Per-source rate/concurrency/batch/window caps, jitter, overlap suppression, and kill switch are mandatory.

## Reliability and idempotency impact

- Retry behavior: Scheduler/dispatcher retries boundedly; worker follows source retry policy.
- Timeout semantics: Missed/stuck windows are observable and use explicit catch-up policy rather than bursts.
- Duplicate-delivery behavior: Target + schedule window key suppresses duplicate enqueue/fetch effects.
- Idempotency key/strategy: SourceId + targetId + scheduledWindow.
- DLQ/recovery/reconciliation: Paused/manual replay/catch-up is audited and bounded; DLQ uses existing operations.

## Observability impact

- Logs: Structured logs include safe tenant/source/entity/operation/event and correlation data.
- Metrics: Track outcomes, failures, retries, duplicates, lag/age, recovery, and latency.
- Traces/correlation: Preserve correlation/causation across changed domains and providers.
- Operational states/errors: Next/last run, enqueued/skipped/throttled/paused, queue age, overlap, and estimated request volume are visible.

## Cost impact

- Request/compute impact: Recurring work proportional to mapped targets and cadence, capped per source.
- Storage impact: Add schedule/refresh window/target checkpoint/idempotency state; snapshots remain immutable.
- Network impact: Policy-approved source requests only; schedules disabled in preview and low/manual in dev.
- New AWS resources/services: EventBridge Scheduler rules/role plus existing SQS/worker/concurrency/alarms.
- Free Tier allowance relevant to this task: Use accepted serverless allowances, disabled/low non-prod schedules, and bounded concurrency.
- Expected monthly cost change or `negligible` with rationale: still small at learning scale but no longer zero-idle; estimate and measure Scheduler/SQS/Lambda/S3/log volume.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Window/key/jitter/batch/kill/overlap/catch-up decisions.
- Integration: Real Scheduler→dispatcher→SQS, duplicate windows, disabled source, concurrency.
- Architecture: Enforce domain ownership, tenant isolation, event/idempotency, and no persistence shortcuts.
- Contract: Refresh schedule and CrawlScheduled/job schemas.
- IaC: CDK assertions, synth, diff, and affected policy/routing checks.
- E2E/manual: Enable a tiny dev schedule, observe bounded refresh, pause source, verify stop, disable schedule.
- **Cloud verification required?** Yes — EventBridge Scheduler timing/IAM and queue/concurrency semantics require AWS.
- AWS environment/stack(s) required: dev/ephemeral CrawlerStack Scheduler resources
- Preview/staging teardown plan: Disable/delete preview schedules and clear synthetic queued work.

