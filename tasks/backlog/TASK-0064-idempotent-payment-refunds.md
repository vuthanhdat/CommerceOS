# TASK-0064 — Refund mock payments idempotently

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 15
Milestone: Milestone D
Depends on: TASK-0032, TASK-0063

## Goal

An approved return can create one idempotent Mock Payment refund and converge on a verified provider refund outcome under retry, timeout, and duplicate callback.

## Business context

Refund retries can duplicate money effects just like capture; provider and CommerceOS need explicit refund state/query/webhook contracts.

## In scope

- implement provider refund endpoint/entity/query, partial/full cumulative amount limits, deterministic success/transient/timeout scenarios, and idempotency;
- implement CommerceOS refund port/adapter/internal state with stable keys, provider query, and signed refund webhook handling;
- link refund attempts/results to ReturnRequest/order/payment with duplicate/out-of-order/ambiguous recovery;

## Out of scope

- inventory return, accounting compensation, final Return completion, real money, or chargebacks;
- refunding beyond captured/unrefunded amount;

## Acceptance criteria

### AC01 — Successful refund

Given approved refundable request and captured payment exist
when refund executes
then one provider Refund and one verified internal refund result exist for the correct amount.

### AC02 — Replay/concurrency

Given same refund key repeats or concurrent refunds approach remaining amount
when provider/CommerceOS process them
then one logical refund commits and cumulative refunds never exceed captured amount.

### AC03 — Ambiguous outcome

Given refund times out after possible commit or webhook/query races
when reconciliation runs
then state remains unknown until verified and then resolves once without repeat refund.

### AC04 — Invalid request

Given amount/payment/reference/signature is invalid
when refund/callback is processed
then it is rejected with no Return/inventory/accounting completion.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence/cleanup is recorded.

## Architecture impact

- Owning domain: Mock Payment / CommerceOS Payment
- Domains touched: Mock Payment, Payment integration, Sales Return, Webhook/Reconciliation
- Persistence impact: Add provider Refund and CommerceOS refund attempt/state/idempotency/dedup records.
- Events/contracts impact: Provider payment.refunded webhook and internal PaymentRefunded v1.
- AWS/IaC impact: Extend MockPaymentStack API/DynamoDB/webhook queue and Commerce payment resources.
- ADR required? No — accepted architecture covers the scope.

## Security and tenant impact

- Authentication: Use established merchant/platform/internal identities.
- Authorization: Refund requires internal authorized return flow; signed callbacks and references/amounts are verified.
- Tenant scoping: Trusted tenant context scopes all records, messages, and operations; cross-tenant access is denied/tested.
- Sensitive data/secrets: No card data; redact secrets/signatures/provider raw responses.
- Abuse/rate-limit considerations: Amount/count limits, restricted test controls, throttling, cumulative conditional check.

## Reliability and idempotency impact

- Retry behavior: State-changing refund retries only with original key; query first after timeout.
- Timeout semantics: Refund timeout is PaymentRefundUnknown until query/webhook evidence.
- Duplicate-delivery behavior: Duplicate request/webhook cannot duplicate provider/internal refund effect.
- Idempotency key/strategy: ReturnRequestId/refund operation version maps to provider Idempotency-Key.
- DLQ/recovery/reconciliation: Scheduled/manual query resolves unknown; provider callback DLQ/replay remains safe.

## Observability impact

- Logs: Structured logs include safe tenant/source/entity/operation/event and correlation data.
- Metrics: Track outcomes, failures, retries, duplicates, lag/age, recovery, and latency.
- Traces/correlation: Preserve correlation/causation across changed domains and providers.
- Operational states/errors: Refund attempts/outcomes/unknown age/webhook/query/retry and cumulative amount are correlated.

## Cost impact

- Request/compute impact: Bounded business/scheduled/event workload.
- Storage impact: Add provider Refund and CommerceOS refund attempt/state/idempotency/dedup records.
- Network impact: Only approved bounded external/internal traffic.
- New AWS resources/services: Extend MockPaymentStack API/DynamoDB/webhook queue and Commerce payment resources.
- Free Tier allowance relevant to this task: Use accepted serverless allowances, disabled/low non-prod schedules, and bounded concurrency.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; measure scheduled/retry usage.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Refund amount/lifecycle/idempotency/timeout decision/signature mapping.
- Integration: Concurrent provider refunds, timeout/query/webhook race, DLQ, Return link.
- Architecture: Enforce domain ownership, tenant isolation, event/idempotency, and no persistence shortcuts.
- Contract: Provider refund API/webhook and internal PaymentRefunded v1.
- IaC: CDK assertions, synth, diff, and affected policy/routing checks.
- E2E/manual: Run success, transient retry, timeout-after-commit, duplicate webhook/refund.
- **Cloud verification required?** Yes — real provider API/webhook/SQS/DynamoDB concurrency and timeout semantics require AWS.
- AWS environment/stack(s) required: MockPaymentStack plus Payment/Return resources
- Preview/staging teardown plan: Destroy ephemeral resources and remove synthetic data/schedules.

