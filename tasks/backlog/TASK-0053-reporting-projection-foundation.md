# TASK-0053 — Establish event-driven reporting projections

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 13
Milestone: Milestone C
Depends on: TASK-0048

## Goal

Reporting consumes versioned domain events into tenant-scoped, rebuildable read models without scanning or owning transactional business data.

## Business context

Dashboards need read-optimized projections; analytics failure must not roll back committed commerce transactions.

## In scope

- introduce Reporting Domain/Application/Infrastructure boundaries and projection/checkpoint/idempotency contracts;
- route selected events through a bounded queue/worker into tenant-scoped read models with version/watermark/freshness;
- implement duplicate/out-of-order policy, rebuild/backfill from retained event evidence where feasible, DLQ, and operational lag visibility;

## Out of scope

- specific KPI calculations and dashboard UI;
- data warehouse/Athena/Glue/OpenSearch or direct transactional-table scans;

## Acceptance criteria

### AC01 — Projection update

Given a supported committed domain event is delivered
when projection worker handles it
then the tenant read model updates once and records event/version/watermark/freshness.

### AC02 — Duplicate and ordering

Given events duplicate or arrive out of order
when worker processes them
then aggregates are not inflated/regressed and unsupported gaps become visible.

### AC03 — Failure isolation

Given Reporting worker fails or DLQs
when source transaction completes
then commerce remains committed while projection lag/failure is observable and recoverable.

### AC04 — Boundary integrity

Given Reporting implementation is reviewed
when input data is followed
then it uses public events/contracts and owns only read models, not producer persistence.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence plus cleanup is recorded.

## Architecture impact

- Owning domain: Reporting & Analytics
- Domains touched: Reporting, Platform Events, all selected producer contracts, Operations
- Persistence impact: Add tenant projection records, event checkpoints/inbox, and rebuild metadata in Reporting-owned storage.
- Events/contracts impact: Consume selected versioned domain events; no vague database-change payloads.
- AWS/IaC impact: Reporting SQS/DLQ, Lambda projection worker, DynamoDB read models, EventBridge rules, CloudWatch.
- ADR required? No — event-driven projections are accepted; create ADR if a new analytics service is proposed.

## Security and tenant impact

- Authentication: Use established merchant/internal identities.
- Authorization: Worker validates tenant/event source; report APIs require tenant permissions.
- Tenant scoping: Trusted tenant context scopes all projections, rules, notifications, and queries; cross-tenant reads/writes are denied.
- Sensitive data/secrets: Store/log only safe aggregate or minimum notification data; no secrets/card data.
- Abuse/rate-limit considerations: Bound query windows, rule complexity, event batches, and update frequency.

## Reliability and idempotency impact

- Retry behavior: Transient worker/storage errors retry boundedly; unsupported schema/data is quarantined.
- Timeout semantics: Timeout leaves projection/notification lag visible and recoverable.
- Duplicate-delivery behavior: Event id/entity version/checkpoint prevents duplicate inflation and stale regression.
- Idempotency key/strategy: Tenant + projection + eventId/entity version.
- DLQ/recovery/reconciliation: DLQ replay and bounded rebuild/backfill preserve source truth; lag remains explicit.

## Observability impact

- Logs: Structured logs include safe tenant, projection/rule/notification, event and correlation data.
- Metrics: Measure lag, failures, duplicates, rebuild/recovery, query latency, and outcome counts.
- Traces/correlation: Preserve event correlation/causation into projections and notifications.
- Operational states/errors: Projection lag/watermark, queue age, DLQ, duplicates, rejects, and rebuild progress are visible.

## Cost impact

- Request/compute impact: One bounded update per event; avoids repeated transactional scans.
- Storage impact: Add tenant projection records, event checkpoints/inbox, and rebuild metadata in Reporting-owned storage.
- Network impact: Small API/event payloads only.
- New AWS resources/services: Reporting SQS/DLQ, Lambda projection worker, DynamoDB read models, EventBridge rules, CloudWatch.
- Free Tier allowance relevant to this task: Prefer existing EventBridge/SQS/Lambda/DynamoDB/CloudWatch allowances and low-volume schedules.
- Expected monthly cost change or `negligible` with rationale: negligible for learning volume; event/read-model growth is measured.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Projection state transitions, duplicate/out-of-order policy, checkpoint/rebuild logic.
- Integration: EventBridge→SQS→worker, duplicate/DLQ, DynamoDB read models, IAM.
- Architecture: Enforce read-model ownership, domain boundaries, event/idempotency and tenant rules.
- Contract: Projection input schemas and base report query/freshness DTO.
- IaC: CDK assertions, synth, diff, routing/queue/policy checks.
- E2E/manual: Publish sample domain events, duplicate/reorder one, query correct projection, rebuild it.
- **Cloud verification required?** Yes — EventBridge/SQS/Lambda/DynamoDB and redrive behavior require AWS verification.
- AWS environment/stack(s) required: Reporting/Async resources
- Preview/staging teardown plan: Destroy preview queues/workers/read models and clear synthetic events.

