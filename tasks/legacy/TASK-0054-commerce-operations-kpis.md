# TASK-0054 — Deliver commerce and operations KPIs

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 13
Milestone: Milestone C
Depends on: TASK-0024, TASK-0031, TASK-0053

## Goal

Merchants can view accurate daily revenue, order count, average order value, top products, low-stock, failed-payment, and workflow-exception KPIs from event-driven projections.

## Business context

Operational reporting should help run the business and reveal failures without querying every transaction on each page load.

## In scope

- define KPI formulas, time-zone/as-of/freshness semantics, and source-event requirements;
- implement TenantDailySales, ProductSalesSummary, InventorySummary, PaymentFailureSummary, and OperationalException projections;
- expose paginated/date-bounded report APIs with drill-through references and correctness/rebuild tests;

## Out of scope

- P&L, cash/AR/AP, platform-wide usage, custom BI exports, or ad-hoc query engine;
- using draft orders or duplicate events to inflate revenue/order counts;

## Acceptance criteria

### AC01 — Sales KPIs

Given paid/recognized and cancelled/refunded facts are projected according to policy
when a date range is queried
then daily revenue, count, AOV, and top products are correct with as-of/freshness.

### AC02 — Operations KPIs

Given inventory/payment/workflow events arrive
when reports are queried
then low-stock and active failure exceptions reflect current resolved/unresolved state without duplicates.

### AC03 — Tenant/time boundaries

Given two tenants and boundary timestamps exist
when queries run in configured business time zone
then data is isolated and assigned to deterministic reporting days.

### AC04 — Rebuild equivalence

Given a projection is rebuilt from the same retained evidence
when results are compared
then the rebuilt KPI values equal the incremental projection.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence plus cleanup is recorded.

## Architecture impact

- Owning domain: Reporting & Analytics
- Domains touched: Reporting, Sales, Inventory, Payment, Workflow event contracts
- Persistence impact: Add KPI read-model items/indexes only; transactional sources remain owned elsewhere.
- Events/contracts impact: Consume order/payment/inventory/workflow facts required by defined formulas.
- AWS/IaC impact: Existing Reporting worker/read-model/API resources.
- ADR required? No — accepted architecture covers this task.

## Security and tenant impact

- Authentication: Use established merchant/internal identities.
- Authorization: Merchant report permissions and drill-through authorization are explicit.
- Tenant scoping: Trusted tenant context scopes all projections, rules, notifications, and queries; cross-tenant reads/writes are denied.
- Sensitive data/secrets: Store/log only safe aggregate or minimum notification data; no secrets/card data.
- Abuse/rate-limit considerations: Bound date ranges, page sizes, groupings, and rebuild frequency.

## Reliability and idempotency impact

- Retry behavior: Transient consumers retry boundedly; invalid/permanent input does not loop.
- Timeout semantics: Timeout leaves projection/notification lag visible and recoverable.
- Duplicate-delivery behavior: Duplicate/out-of-order events cannot inflate or regress projections/effects.
- Idempotency key/strategy: Projection/effect records use event id and entity version/watermark.
- DLQ/recovery/reconciliation: DLQ/rebuild/reconciliation is documented and does not alter source truth.

## Observability impact

- Logs: Structured logs include safe tenant, projection/rule/notification, event and correlation data.
- Metrics: Measure lag, failures, duplicates, rebuild/recovery, query latency, and outcome counts.
- Traces/correlation: Preserve event correlation/causation into projections and notifications.
- Operational states/errors: APIs show as-of time, projection lag, incomplete source warning, and drill-through ids.

## Cost impact

- Request/compute impact: Bounded event consumption and paginated dashboard/rule traffic.
- Storage impact: Add KPI read-model items/indexes only; transactional sources remain owned elsewhere.
- Network impact: Small API/event payloads only.
- New AWS resources/services: Existing Reporting worker/read-model/API resources.
- Free Tier allowance relevant to this task: Prefer existing EventBridge/SQS/Lambda/DynamoDB/CloudWatch allowances and low-volume schedules.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; projections avoid repeated transactional scans.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: KPI formulas, timezone boundaries, corrections/refunds, exception resolution, and duplicates.
- Integration: Projection updates/rebuild plus paginated tenant report APIs.
- Architecture: Enforce read-model ownership, domain boundaries, event/idempotency and tenant rules.
- Contract: KPI event requirements and report DTOs.
- IaC: CDK assertions, synth, diff, routing/queue/policy checks.
- E2E/manual: Run sale/failure/stock journey and verify merchant KPI values and freshness.
- **Cloud verification required?** Yes — deployed event/read-model behavior and DynamoDB access patterns require AWS verification.
- AWS environment/stack(s) required: Reporting resources plus source-domain test flow
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic projection data.

