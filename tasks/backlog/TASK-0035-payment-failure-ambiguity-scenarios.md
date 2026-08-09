# TASK-0035 — Add deterministic payment failure and ambiguity scenarios

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 8
Milestone: Milestone B
Depends on: TASK-0033, TASK-0034

## Goal

The Mock Payment Provider deterministically reproduces transient 500s, timeout-before-commit, timeout-after-commit, delayed success, and webhook-before-response ordering without random tests.

## Business context

Payment timeout can leave provider state unknown; CommerceOS must experience and classify ambiguity before an orchestration decision is made.

## In scope

- implement pm_provider_500, pm_timeout_before_commit, pm_timeout_after_commit, pm_delayed_success, and pm_webhook_before_response behavior;
- add bounded latency/failure controls and durable provider state that distinguishes caller observation from provider outcome;
- add repeatable provider/consumer tests for retry classification, state query, and out-of-order response timing;

## Out of scope

- CommerceOS reconciliation resolution, signed webhook verification, refunds, or random chaos;
- treating TimedOut as a durable provider payment failure;

## Acceptance criteria

### AC01 — Transient 500

Given a scenario is configured to fail a bounded number of attempts
when capture is retried with the same idempotency key
then the configured 500s occur and the later success commits only once.

### AC02 — Timeout distinction

Given timeout-before-commit and timeout-after-commit scenarios run
when the caller times out then queries provider state
then the first has no committed capture while the second reports the single committed capture.

### AC03 — Delayed and reordered success

Given delayed/webhook-before-response scenarios run
when provider state and timing are inspected
then Pending/committed state and callback ordering are deterministic and repeatable.

### AC04 — No false failure

Given a client request times out
when provider durable state is read
then the provider never converts the network observation alone into a Declined/Failed business outcome.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Mock Payment
- Domains touched: Mock Payment and CommerceOS payment client contract tests
- Persistence impact: Extend provider intent/operation state with deterministic scenario attempts/timing; reuse idempotency records.
- Events/contracts impact: Provider webhook facts are shaped for TASK-0036 but signature/delivery reliability is not implemented here.
- AWS/IaC impact: Existing MockPaymentStack Lambda/DynamoDB plus bounded test-only latency/failure controls.
- ADR required? No — existing accepted decisions cover the task.

## Security and tenant impact

- Authentication: Use established merchant/internal/provider identities.
- Authorization: Failure controls remain restricted to non-production/test identities and cannot target arbitrary callback URLs.
- Tenant scoping: Trusted tenant context scopes every merchant record and message; client tenant ids cannot override it.
- Sensitive data/secrets: No real card data or secrets are stored/logged; personal/business data is minimized and redacted.
- Abuse/rate-limit considerations: Maximum latency, failure count, attempts, and concurrent scenarios are capped.

## Reliability and idempotency impact

- Retry behavior: Provider scenario controls make retry counts deterministic; consumer must reuse keys and never retry stable decline.
- Timeout semantics: Before/after commit semantics are explicit and queryable; timeout alone produces unknown caller state.
- Duplicate-delivery behavior: Repeated calls with the same key return/complete one stored operation despite timing scenarios.
- Idempotency key/strategy: Provider Idempotency-Key plus request hash/state-machine operation.
- DLQ/recovery/reconciliation: State query is the recovery primitive; webhook/reconciliation are later tasks.

## Observability impact

- Logs: Structured logs carry safe tenant/entity/operation/event and correlation identifiers.
- Metrics: Measure outcomes, failures, retries, duplicates, latency, and stuck states at bounded cardinality.
- Traces/correlation: Preserve correlation/causation across all changed boundaries.
- Operational states/errors: Metrics/logs distinguish injected 500, before/after-commit timeout, pending, delayed completion, and reordering.

## Cost impact

- Request/compute impact: Scales with bounded business activity and retry policy.
- Storage impact: Extend provider intent/operation state with deterministic scenario attempts/timing; reuse idempotency records.
- Network impact: Only bounded API/event traffic.
- New AWS resources/services: Existing MockPaymentStack Lambda/DynamoDB plus bounded test-only latency/failure controls.
- Free Tier allowance relevant to this task: Use accepted serverless allowances and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; measure workflows/retries where relevant.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for targeted failure/cloud tests.

## Test plan

- Unit: Scenario state machines, attempt counters, commit timing, and retry classifier.
- Integration: Deployed provider calls with controlled client timeouts and subsequent state query.
- Architecture: Check domain boundaries, tenant context, and no persistence shortcuts.
- Contract: Failure-scenario behavior added to provider/consumer v1 contract fixtures.
- IaC: CDK assertions, synth, diff, and selected deployment checks.
- E2E/manual: Run every deterministic scenario and assert final provider state/side-effect count.
- **Cloud verification required?** Yes — real HTTP/Lambda timeout and deployed state timing must be sampled on AWS.
- AWS environment/stack(s) required: dev/preview MockPaymentStack
- Preview/staging teardown plan: Destroy ephemeral stacks and synthetic data after evidence collection.

