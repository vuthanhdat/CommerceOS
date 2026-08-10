# TASK-0057 — Deliver authorized scheduled promotions

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Later product capability
Milestone: Unscheduled
Depends on: TASK-0025, TASK-0048

## Goal

Authorized merchants can schedule a simple promotion that deterministically changes the sellable offer during a bounded window while checkout snapshots the applied rule.

## Business context

Catalog base price and manual discount are insufficient for time-based offers; pricing rules must remain owned by Pricing, not hidden in Storefront or Sales.

## In scope

- introduce Pricing & Promotion domain with Promotion lifecycle, eligible products/categories, fixed/percentage rule, start/end, priority/conflict policy, and version;
- implement create/schedule/cancel/query plus active-offer calculation contract used by Storefront and checkout;
- activate/expire via bounded Scheduler or time-aware query as decided, emit versioned facts, and preserve rule snapshot in Sales;

## Out of scope

- coupons, customer segments, price lists, stacking engine, flash-sale scale, or dynamic pricing;
- changing Catalog base price or retroactively repricing placed orders;

## Acceptance criteria

### AC01 — Promotion window

Given an authorized merchant schedules a valid promotion
when time enters/leaves the window
then the active offer applies/expires deterministically according to explicit conflict/clock policy.

### AC02 — Checkout snapshot

Given a shopper checks out under an active rule
when Sales resolves the offer
then order stores base price, rule/version, discount, and final price unchanged by later edits/expiry.

### AC03 — Authorization/validation

Given unauthorized, cross-tenant, overlapping-invalid, negative, or excessive rule is submitted
when command runs
then it is rejected and sensitive changes are audited.

### AC04 — Duplicate scheduling

Given activation/expiry or commands repeat
when processing runs
then rule state/events change once without duplicate discount effects.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence plus cleanup is recorded.

## Architecture impact

- Owning domain: Pricing & Promotion
- Domains touched: Pricing, Catalog query, Storefront, Sales checkout, Audit
- Persistence impact: Add tenant Promotion/rule/version/status records and active-offer access pattern.
- Events/contracts impact: PromotionScheduled/Activated/Expired/PriceRuleChanged v1.
- AWS/IaC impact: DynamoDB/API plus low-frequency EventBridge Scheduler only if needed by selected design.
- ADR required? No if using accepted Scheduler/pay-per-use pattern; document design and create ADR if topology materially changes.

## Security and tenant impact

- Authentication: Use established merchant/internal identities.
- Authorization: Promotion management requires pricing permission and privileged discounts are audited.
- Tenant scoping: Trusted tenant context scopes all projections, rules, notifications, and queries; cross-tenant reads/writes are denied.
- Sensitive data/secrets: Store/log only safe aggregate or minimum notification data; no secrets/card data.
- Abuse/rate-limit considerations: Bound discount values, date ranges, eligible-item count, overlaps, schedules, and query complexity.

## Reliability and idempotency impact

- Retry behavior: Activation/expiry repeats safely; deterministic invalid rules do not retry.
- Timeout semantics: Time source is injected/testable; missed schedule is reconciled from stored window/status.
- Duplicate-delivery behavior: Rule version and scheduled operation key suppress duplicate transitions/events.
- Idempotency key/strategy: Tenant + promotionId + version + transition.
- DLQ/recovery/reconciliation: Reconcile stored window versus status after missed/failed schedule; no stale offer beyond defined tolerance.

## Observability impact

- Logs: Structured logs include safe tenant, projection/rule/notification, event and correlation data.
- Metrics: Measure lag, failures, duplicates, rebuild/recovery, query latency, and outcome counts.
- Traces/correlation: Preserve event correlation/causation into projections and notifications.
- Operational states/errors: Scheduled/active/expired/cancelled/conflicted states and next transition/failure are visible.

## Cost impact

- Request/compute impact: Offer evaluation and low-frequency transitions; no minute-level unnecessary schedules.
- Storage impact: Add tenant Promotion/rule/version/status records and active-offer access pattern.
- Network impact: Small API/event payloads only.
- New AWS resources/services: DynamoDB/API plus low-frequency EventBridge Scheduler only if needed by selected design.
- Free Tier allowance relevant to this task: Prefer existing EventBridge/SQS/Lambda/DynamoDB/CloudWatch allowances and low-volume schedules.
- Expected monthly cost change or `negligible` with rationale: negligible; Scheduler allowance and invocation count documented.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Rule validation, time windows, conflict/rounding, offer calculation, snapshot.
- Integration: Promotion persistence, Scheduler/transition idempotency if used, Storefront/checkout contract.
- Architecture: Enforce read-model ownership, domain boundaries, event/idempotency and tenant rules.
- Contract: ActiveOffer query and promotion event/order snapshot schemas.
- IaC: CDK assertions, synth, diff, routing/queue/policy checks.
- E2E/manual: Schedule promotion, browse/check out during window, expire, and prove order snapshot unchanged.
- **Cloud verification required?** Yes — if Scheduler and deployed DynamoDB/API integration are used, real semantics require AWS verification.
- AWS environment/stack(s) required: Pricing resources and selected Scheduler rules
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic projection data.

