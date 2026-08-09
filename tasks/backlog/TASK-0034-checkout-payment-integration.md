# TASK-0034 — Integrate checkout with the payment boundary

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 7
Milestone: Milestone A
Depends on: TASK-0025, TASK-0030, TASK-0033

## Goal

Checkout reserves inventory, calls the Mock Payment Provider through a port, confirms the order on success, and releases stock plus records PaymentFailed on stable decline.

## Business context

Milestone A requires a real sell flow while preserving provider, Sales, and Inventory ownership and making every repeated step safe.

## In scope

- introduce CommerceOS Payment application model/port and infrastructure HTTP adapter without provider persistence coupling;
- extend checkout to reserve Inventory, create/capture mock payment with stable keys, and map success/decline;
- commit honest Sales/Payment state/history and release reservation after decline with end-to-end correlation;

## Out of scope

- ambiguous timeout, delayed success, webhook handling, reconciliation, refund, or Step Functions;
- real payment provider or real card data;

## Acceptance criteria

### AC01 — Successful checkout

Given published product stock exists and pm_success is selected
when checkout runs
then one order, reservation, captured payment reference, and Confirmed/Allocated state exist with no duplicate effects.

### AC02 — Declined checkout

Given stock is reserved and pm_declined returns
when checkout handles the stable decline
then order reaches PaymentFailed, reservation is released exactly once, and payment history is retained.

### AC03 — Retry safety

Given the browser or application repeats checkout/reserve/payment steps
when the same logical intent is processed
then one order/payment/reservation outcome exists and incompatible key reuse is rejected.

### AC04 — Boundary integrity

Given the modules are inspected
when payment integration is followed
then Sales uses a payment port/contract and never accesses Mock Payment state storage.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and selected cloud verification plus teardown evidence is recorded.

## Architecture impact

- Owning domain: Sales / Payment integration / Inventory
- Domains touched: Storefront, Sales, Inventory, CommerceOS Payment adapter, Mock Payment
- Persistence impact: CommerceOS stores tenant payment intent/attempt/reference state; each domain retains its own records.
- Events/contracts impact: PaymentRequested/Captured/Failed and order/inventory domain facts are defined locally; reliable publication waits for TASK-0048.
- AWS/IaC impact: Commerce API/Lambda outbound HTTPS to MockPaymentStack plus existing DynamoDB resources.
- ADR required? No — synchronous v1 integration is the documented learning progression.

## Security and tenant impact

- Authentication: Use established merchant/Storefront identity boundaries.
- Authorization: Payment method tokens are deterministic test inputs; internal provider refs/operations are authorized and tenant-bound.
- Tenant scoping: All tenant-owned commands/queries use trusted tenant context; client tenant ids are rejected/ignored and cross-tenant access is tested.
- Sensitive data/secrets: No PAN/CVV/card secrets; redact provider/test secrets and response bodies where necessary.
- Abuse/rate-limit considerations: Throttle checkout/provider calls, cap amounts/lines, and restrict test methods to non-production learning scope.

## Reliability and idempotency impact

- Retry behavior: Only transport/transient errors with known safe idempotency semantics retry; decline never retries as transient.
- Timeout semantics: This task treats unexpected timeout as an explicit unimplemented/failed integration path and does not assume decline; full PaymentUnknown handling is TASK-0035–0037.
- Duplicate-delivery behavior: Duplicate checkout/payment/reserve/release calls do not repeat effects.
- Idempotency key/strategy: Checkout key derives stable order/payment operation keys such as payment:{orderId}:capture:v1.
- DLQ/recovery/reconciliation: Stable decline compensates by idempotent release; ambiguous recovery is deferred but state must remain diagnosable.

## Observability impact

- Logs: Structured logs include safe tenant, entity, command/event, and correlation identifiers.
- Metrics: Checkout outcome, provider latency/result, reservation/release result, and duplicate suppression.
- Traces/correlation: Preserve correlation across all application/external boundaries.
- Operational states/errors: Correlation links checkout, order, reservation, provider intent, and payment attempt; partial failure is visible.

## Cost impact

- Request/compute impact: One bounded synchronous provider call chain per unique checkout.
- Storage impact: CommerceOS stores tenant payment intent/attempt/reference state; each domain retains its own records.
- Network impact: HTTPS between CommerceOS and Mock Payment; timeouts are bounded.
- New AWS resources/services: Commerce API/Lambda outbound HTTPS to MockPaymentStack plus existing DynamoDB resources.
- Free Tier allowance relevant to this task: Use documented Lambda, DynamoDB, API Gateway, SQS, and CloudWatch limits where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; no always-on resource.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview tests.

## Test plan

- Unit: Orchestration decisions, state mapping, key derivation, decline compensation.
- Integration: Sales–Inventory–Payment contracts, duplicate calls, success/decline, and partial failure.
- Architecture: Enforce domain ownership, inward dependencies, and no cross-domain persistence access.
- Contract: CommerceOS payment port/provider client and internal Payment state contracts.
- IaC: CDK assertions, synth, and reviewed diff.
- E2E/manual: Storefront checkout with pm_success and pm_declined including stock/order assertions.
- **Cloud verification required?** Yes — real API-to-provider networking, Lambda/DynamoDB/IAM, and concurrency need AWS verification.
- AWS environment/stack(s) required: CommerceStack, Inventory/Sales resources, and MockPaymentStack
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic data.

