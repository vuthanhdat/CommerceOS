# TASK-0056 — Deliver merchant dashboards and in-app notifications

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 13
Milestone: Milestone C / D
Depends on: TASK-0019, TASK-0038, TASK-0052, TASK-0054, TASK-0055

## Goal

Merchant users receive a coherent dashboard and tenant-scoped in-app operational notifications for important business outcomes and failures without notification failure affecting source transactions.

## Business context

Users need one place to see KPIs and act on payment, inventory, workflow, crawler, accounting, and DLQ exceptions.

## In scope

- compose KPI/financial/operations cards with freshness and drill-through in Back Office;
- implement Notification domain/read model and async consumers for selected order, payment, low-stock, goods-received, workflow, crawler, journal, and DLQ facts;
- support unread/read/acknowledged lifecycle, deduplication, tenant/role routing, bounded retention, and notification DLQ/operations;

## Out of scope

- email/SMS/push, marketing messages, complex preferences, or notification-driven business state mutation;
- blocking/rolling back source transactions when notification delivery fails;

## Acceptance criteria

### AC01 — Dashboard

Given merchant opens dashboard with projected data
when cards load
then KPIs, financial projections, active exceptions, as-of/freshness, and authorized drill-through are coherent.

### AC02 — Notification creation

Given a selected event/failure is delivered repeatedly
when notification consumer runs
then one role/tenant-appropriate in-app notification is created and source transaction remains unchanged.

### AC03 — Notification lifecycle

Given authorized user marks/acknowledges a notification
when command repeats
then state changes once with history while unauthorized/cross-tenant access is denied.

### AC04 — Failure isolation

Given notification consumer fails until retry exhaustion
when source business flow is inspected
then source remains committed and failure is visible in Notification DLQ/operations.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence plus cleanup is recorded.

## Architecture impact

- Owning domain: Reporting / Notification / Back Office
- Domains touched: Reporting, Notification, all selected event producers, Authorization, Operations
- Persistence impact: Add tenant/user/role Notification records with source-event uniqueness, status, TTL/retention; dashboard reads projections only.
- Events/contracts impact: Consume selected versioned events; notification state facts remain internal unless a consumer emerges.
- AWS/IaC impact: Notification SQS/DLQ and Lambda plus DynamoDB/read APIs; existing Reporting resources.
- ADR required? No — accepted architecture covers this task.

## Security and tenant impact

- Authentication: Use established merchant/internal identities.
- Authorization: Notification visibility is tenant/role/user-scoped; platform alerts use explicit platform-admin authority.
- Tenant scoping: Trusted tenant context scopes all projections, rules, notifications, and queries; cross-tenant reads/writes are denied.
- Sensitive data/secrets: Messages contain minimum safe summaries and ids, not raw payloads, secrets, or unnecessary PII.
- Abuse/rate-limit considerations: Deduplicate noisy events, rate/batch limits, retention, pagination, and no recursive alert storms.

## Reliability and idempotency impact

- Retry behavior: Transient consumer failures retry boundedly; invalid/unsupported events quarantine.
- Timeout semantics: Timeout leaves projection/notification lag visible and recoverable.
- Duplicate-delivery behavior: Source event + notification type/audience creates one logical notification.
- Idempotency key/strategy: Tenant + sourceEventId + notificationType + audience.
- DLQ/recovery/reconciliation: Notification DLQ can replay safely; acknowledged state is not regressed.

## Observability impact

- Logs: Structured logs include safe tenant, projection/rule/notification, event and correlation data.
- Metrics: Measure lag, failures, duplicates, rebuild/recovery, query latency, and outcome counts.
- Traces/correlation: Preserve event correlation/causation into projections and notifications.
- Operational states/errors: Dashboard/notification projection lag, DLQ, unread count, dedup and delivery failure are visible.

## Cost impact

- Request/compute impact: Low-volume queue/worker/read traffic; cards batch/parallelize bounded queries.
- Storage impact: Notification retention/TTL and projection sizes are bounded.
- Network impact: Small API/event payloads only.
- New AWS resources/services: Notification SQS/DLQ and Lambda plus DynamoDB/read APIs; existing Reporting resources.
- Free Tier allowance relevant to this task: Prefer existing EventBridge/SQS/Lambda/DynamoDB/CloudWatch allowances and low-volume schedules.
- Expected monthly cost change or `negligible` with rationale: negligible; built-in metrics and low-cardinality custom measures only.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Audience routing, message redaction, lifecycle, dedup, dashboard composition.
- Integration: Event→Notification queue, DLQ/duplicates, tenant/role APIs, projection freshness.
- Architecture: Enforce read-model ownership, domain boundaries, event/idempotency and tenant rules.
- Contract: Notification event mapping and dashboard/notification DTOs.
- IaC: CDK assertions, synth, diff, routing/queue/policy checks.
- E2E/manual: Generate payment/low-stock/failure facts, view dashboard notifications, acknowledge one, and force a DLQ case.
- **Cloud verification required?** Yes — SQS/Lambda/DLQ/IAM and deployed projection APIs need AWS verification.
- AWS environment/stack(s) required: Notification/Reporting resources and selected producers
- Preview/staging teardown plan: Destroy preview queue/worker/read models and clear synthetic notifications.

