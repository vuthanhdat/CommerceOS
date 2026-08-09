# TASK-0058 — Add coupons, price lists, segments, and campaign pricing

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Later product capability
Milestone: Unscheduled
Depends on: TASK-0026, TASK-0057

## Goal

CommerceOS can apply tenant-owned coupons, price lists, customer segments, and campaign/flash-sale rules through one explicit, bounded pricing engine with explainable stacking.

## Business context

Advanced pricing adds combinatorial and abuse risk; it belongs in Pricing with deterministic evaluation, authorization, and snapshots.

## In scope

- model coupons/redemption constraints, price lists, customer-segment eligibility, campaign rules, priorities/exclusions/stacking, and rule versions;
- implement evaluate/explain offer contract for Storefront/checkout and atomic coupon/redemption/idempotency controls;
- provide management/redemption operations, audit, schedule safety, performance/cost limits, and comprehensive rule matrix tests;

## Out of scope

- machine-learning/dynamic personalized pricing, real loyalty program, tax/shipping, or unbounded arbitrary rule language;
- retroactive order repricing or trusting client-calculated discounts;

## Acceptance criteria

### AC01 — Deterministic offer

Given eligible/ineligible customer/cart context and active rules exist
when pricing evaluates
then one explainable result follows priority/stacking/rounding policy with rule versions.

### AC02 — Coupon safety

Given limited coupon is redeemed concurrently or checkout retries
when redemption commits
then usage limits are not exceeded and one checkout consumes at most one logical redemption.

### AC03 — Snapshot and replay

Given priced checkout repeats or rules later change
when Sales processes it
then equivalent replay returns the same order/discount snapshot and new checkout uses current rules.

### AC04 — Bounded engine

Given pathological rule counts/inputs are submitted or evaluated
when limits are reached
then the system rejects/bounds work with observable reason rather than scanning/evaluating unbounded rules.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence plus cleanup is recorded.

## Architecture impact

- Owning domain: Pricing & Promotion
- Domains touched: Pricing, Customer segment contract, Storefront, Sales, Audit
- Persistence impact: Add Coupon/PriceList/Segment/Campaign rules, indexes, redemption/idempotency records.
- Events/contracts impact: CouponRedeemed and PriceRule/Campaign facts only when consumers need them; version all public schemas.
- AWS/IaC impact: DynamoDB/API and bounded schedule/event integration using existing services.
- ADR required? No unless a new rule engine/service or cross-domain integration mechanism is proposed.

## Security and tenant impact

- Authentication: Use established merchant/internal identities.
- Authorization: Pricing/campaign management and manual overrides have granular permission/audit; segment data is minimal.
- Tenant scoping: Trusted tenant context scopes all projections, rules, notifications, and queries; cross-tenant reads/writes are denied.
- Sensitive data/secrets: Segment/coupon data avoids unnecessary PII and logs redact codes where abuse risk exists.
- Abuse/rate-limit considerations: Rate-limit coupon attempts; cap rules/stacking/depth/dates/redemptions and prevent enumeration.

## Reliability and idempotency impact

- Retry behavior: Redemption/checkout uses atomic idempotency; scheduled transitions reconcile.
- Timeout semantics: N/A unless external work is introduced.
- Duplicate-delivery behavior: Duplicate checkout/event cannot consume coupon twice or duplicate rule transition.
- Idempotency key/strategy: Tenant + checkout/order + coupon/rule version; conditional redemption counters.
- DLQ/recovery/reconciliation: Unknown redemption outcome is queried by checkout key before retry; expired campaigns reconcile.

## Observability impact

- Logs: Structured logs include safe tenant, projection/rule/notification, event and correlation data.
- Metrics: Measure lag, failures, duplicates, rebuild/recovery, query latency, and outcome counts.
- Traces/correlation: Preserve event correlation/causation into projections and notifications.
- Operational states/errors: Offer explanation, rejected coupon reason (without enumeration), rule/version, usage, conflict and schedule status are visible.

## Cost impact

- Request/compute impact: Bounded event consumption and paginated dashboard/rule traffic.
- Storage impact: Add Coupon/PriceList/Segment/Campaign rules, indexes, redemption/idempotency records.
- Network impact: Small API/event payloads only.
- New AWS resources/services: DynamoDB/API and bounded schedule/event integration using existing services.
- Free Tier allowance relevant to this task: Prefer existing EventBridge/SQS/Lambda/DynamoDB/CloudWatch allowances and low-volume schedules.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; projections avoid repeated transactional scans.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Full rule matrix, eligibility, stacking, rounding, limits, concurrency decisions.
- Integration: DynamoDB conditional redemptions, Pricing–Customer/Storefront/Sales contracts, tenant isolation.
- Architecture: Enforce read-model ownership, domain boundaries, event/idempotency and tenant rules.
- Contract: Pricing evaluation/explanation, coupon redemption, and order snapshot schemas.
- IaC: CDK assertions, synth, diff, routing/queue/policy checks.
- E2E/manual: Use price list/segment/coupon/campaign at checkout, race final redemption, inspect explanation/snapshot.
- **Cloud verification required?** Yes — DynamoDB concurrency, deployed API, and optional scheduled transitions require AWS verification.
- AWS environment/stack(s) required: Pricing plus Customer/Sales integration resources
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic projection data.

