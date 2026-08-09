# TASK-0033 — Prove payment success, decline, and idempotency contracts

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 7
Milestone: Milestone A
Depends on: TASK-0032

## Goal

Automated contracts prove deterministic payment success and decline behavior, including safe repeated calls, for both provider and CommerceOS clients.

## Business context

Before ambiguous failures are introduced, the normal external contract and stable business-decline semantics must be repeatable.

## In scope

- implement pm_success and pm_declined deterministic methods/scenarios across create/authorize/capture;
- add provider and consumer contract tests for schemas, status/error mapping, amounts, currency, merchant reference, and idempotency;
- add provider metrics for attempts, success, decline, errors, and duplicates suppressed;

## Out of scope

- timeouts, delayed success, HTTP 500 retries, webhooks, refunds, or order handling;
- random failure selection;

## Acceptance criteria

### AC01 — Success scenario

Given a payment uses pm_success
when authorize/capture is called with valid keys
then Captured is returned/persisted and equivalent retries return the same result.

### AC02 — Stable decline

Given a payment uses pm_declined
when the payment operation is retried
then the same business decline is returned and no captured state appears.

### AC03 — Contract compatibility

Given provider and CommerceOS client fixtures use contract v1
when producer and consumer suites run
then schemas, error taxonomy, money/reference fields, and idempotency behavior agree.

### AC04 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and selected cloud verification plus teardown evidence is recorded.

## Architecture impact

- Owning domain: Mock Payment
- Domains touched: Mock Payment and Payment adapter contract tests
- Persistence impact: Uses provider state/idempotency records from TASK-0032.
- Events/contracts impact: No webhook/internal event yet.
- AWS/IaC impact: Existing MockPaymentStack; built-in/custom bounded metrics.
- ADR required? No — accepted domain/serverless rules cover this task.

## Security and tenant impact

- Authentication: Use established merchant/Storefront identity boundaries.
- Authorization: Only deterministic method tokens are accepted; no user-provided executable failure configuration.
- Tenant scoping: All tenant-owned commands/queries use trusted tenant context; client tenant ids are rejected/ignored and cross-tenant access is tested.
- Sensitive data/secrets: No secrets or real payment/card data are stored or logged.
- Abuse/rate-limit considerations: Test scenarios are restricted and calls throttled; no high-volume random tests.

## Reliability and idempotency impact

- Retry behavior: Success replays stored result; decline is permanent and not retried as transient.
- Timeout semantics: N/A — normal bounded responses only in this task.
- Duplicate-delivery behavior: Concurrent duplicate calls create one payment operation result.
- Idempotency key/strategy: Provider Idempotency-Key and request hash.
- DLQ/recovery/reconciliation: N/A — no queue/event consumer.

## Observability impact

- Logs: Structured logs include safe tenant, entity, command/event, and correlation identifiers.
- Metrics: Bounded metrics cover conflicts, failures, and latency for the changed capability.
- Traces/correlation: Preserve correlation across all application/external boundaries.
- Operational states/errors: Metrics distinguish success, business decline, invalid request, conflict, and duplicate suppression.

## Cost impact

- Request/compute impact: Usage scales with bounded business requests.
- Storage impact: Uses provider state/idempotency records from TASK-0032.
- Network impact: Normal API traffic only.
- New AWS resources/services: Existing MockPaymentStack; built-in/custom bounded metrics.
- Free Tier allowance relevant to this task: Use documented Lambda, DynamoDB, API Gateway, SQS, and CloudWatch limits where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at the learning profile.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview tests.

## Test plan

- Unit: Scenario selection, decline stability, response mapping, and retry classification.
- Integration: Concurrent provider API calls and state query for both methods.
- Architecture: Enforce domain ownership, inward dependencies, and no cross-domain persistence access.
- Contract: Provider v1 producer/consumer contract suite.
- IaC: CDK assertions, synth, and reviewed diff.
- E2E/manual: Run pm_success and pm_declined against deployed provider and verify query/replay.
- **Cloud verification required?** Yes — deployed provider API/state/idempotency and client interoperability need AWS evidence.
- AWS environment/stack(s) required: dev/preview MockPaymentStack
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic data.

