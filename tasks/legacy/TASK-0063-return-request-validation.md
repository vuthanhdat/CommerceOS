# TASK-0063 — Validate and record return requests

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 15
Milestone: Milestone D
Depends on: TASK-0024, TASK-0034

## Goal

Authorized staff can create a tenant-owned return request whose refundable quantity/amount and eligible order lines are validated against immutable sales and payment history.

## Business context

Returns begin a cross-domain compensation flow; Sales must first own a clear request and eligibility decision before provider, inventory, or accounting effects occur.

## In scope

- introduce ReturnRequest/ReturnLine lifecycle, reason, requested/refundable quantity/amount, and source SalesOrder references;
- implement authorization, time/status/quantity/already-refunded validation through Sales/Payment query contracts;
- deliver return create/list/detail/review APIs with audit and optimistic/idempotent request behavior;

## Out of scope

- provider refund, physical inventory return, accounting contra/reversal, workflow orchestration, exchange, or shipping logistics;
- editing original order/payment/fulfillment facts;

## Acceptance criteria

### AC01 — Eligible return

Given a fulfilled/paid order has refundable lines/amount
when authorized staff submit a valid request
then one pending/approved ReturnRequest captures immutable source/amount/quantity evidence.

### AC02 — Over/duplicate prevention

Given requested cumulative quantity/amount exceeds eligible remainder or request repeats
when validation runs
then over-return is rejected and equivalent replay returns the existing logical request.

### AC03 — Tenant/permission safety

Given unauthorized or cross-tenant actor targets an order
when return is requested
then access is denied/non-disclosing and attempt is audited.

### AC04 — No downstream effect yet

Given return request is approved
when state is inspected
then payment, inventory, and accounting remain unchanged until their explicit commands run.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence/cleanup is recorded.

## Architecture impact

- Owning domain: Sales & Order Management
- Domains touched: Sales, Payment query, Authorization, Audit, Back Office
- Persistence impact: Add tenant ReturnRequest/lines/status history and source/order uniqueness/cumulative tracking.
- Events/contracts impact: RefundRequested/ReturnApproved facts for later workflow, versioned with source refs.
- AWS/IaC impact: Existing Sales API/DynamoDB resources.
- ADR required? No — accepted architecture covers the scope.

## Security and tenant impact

- Authentication: Use established merchant/platform/internal identities.
- Authorization: Return/refund request permissions are explicit; high-value actions are audited.
- Tenant scoping: Trusted tenant context scopes all records, messages, and operations; cross-tenant access is denied/tested.
- Sensitive data/secrets: Return reason/customer/order context is minimized/redacted.
- Abuse/rate-limit considerations: Bound lines/amounts/text/time window and repeated requests.

## Reliability and idempotency impact

- Retry behavior: Retryable writes use explicit operation/source keys.
- Timeout semantics: No ambiguous external timeout unless stated.
- Duplicate-delivery behavior: N/A unless a repeatable command is introduced.
- Idempotency key/strategy: Tenant + order + return request key/hash; optimistic cumulative eligibility.
- DLQ/recovery/reconciliation: N/A unless stated.

## Observability impact

- Logs: Structured logs include safe tenant/source/entity/operation/event and correlation data.
- Metrics: Track outcomes, failures, retries, duplicates, lag/age, recovery, and latency.
- Traces/correlation: Preserve correlation/causation across changed domains and providers.
- Operational states/errors: Ineligible state, remaining amount/quantity, conflict, review status, and audit correlation are visible.

## Cost impact

- Request/compute impact: Bounded business/scheduled/event workload.
- Storage impact: Add tenant ReturnRequest/lines/status history and source/order uniqueness/cumulative tracking.
- Network impact: Only approved bounded external/internal traffic.
- New AWS resources/services: Existing Sales API/DynamoDB resources.
- Free Tier allowance relevant to this task: Use accepted serverless allowances, disabled/low non-prod schedules, and bounded concurrency.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; measure scheduled/retry usage.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Eligibility, cumulative quantities/amounts, lifecycle, replay, authorization.
- Integration: Sales/Payment query contracts, concurrent requests, tenant isolation, audit.
- Architecture: Enforce domain ownership, tenant isolation, event/idempotency, and no persistence shortcuts.
- Contract: ReturnRequest APIs and RefundRequested/ReturnApproved v1.
- IaC: CDK assertions, synth, diff, and affected policy/routing checks.
- E2E/manual: Create valid/invalid/duplicate return requests for paid fulfilled order.
- **Cloud verification required?** Yes — DynamoDB concurrency and protected API integration require AWS verification.
- AWS environment/stack(s) required: Sales/Return resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and remove synthetic data/schedules.

