# TASK-0036 — Deliver signed, retryable, deduplicated payment webhooks

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 8
Milestone: Milestone B
Depends on: TASK-0032, TASK-0035

## Goal

The provider delivers signed webhooks through a retryable queue, and CommerceOS verifies, deduplicates, stores, and translates each provider event into at most one internal payment fact.

## Business context

Provider payment commit and merchant callback delivery are separate effects; duplicate, delayed, failed, and reordered webhooks are normal distributed-system behavior.

## In scope

- add provider webhook event/signature contract, delivery queue, retry/backoff, delivery records, dispatcher, and DLQ;
- add CommerceOS callback endpoint with signature/staleness validation, fast acknowledgement, durable event-id deduplication, and tenant/reference lookup;
- support duplicate and webhook-before-response scenarios and publish internal payment facts once after verified state mapping;

## Out of scope

- scheduled PaymentUnknown reconciliation, refund webhooks, generic notification delivery, or real provider secrets;
- trusting callback tenant/amount/reference without matching internal payment state;

## Acceptance criteria

### AC01 — Verified callback

Given a valid signed provider webhook references an existing payment
when CommerceOS receives it
then signature/time/reference/state are verified and one internal payment fact is recorded.

### AC02 — Duplicate and reordering safety

Given the same webhook is delivered multiple times or before the synchronous response
when callbacks are handled
then at most one logical payment/order effect occurs and state never regresses.

### AC03 — Delivery retry and DLQ

Given the CommerceOS callback is transiently unavailable or permanently fails
when provider delivery retries
then bounded attempts occur and exhausted delivery reaches a visible provider DLQ.

### AC04 — Invalid webhook

Given signature, timestamp, amount, currency, or merchant reference is invalid
when callback is sent
then it is rejected safely without changing payment/order state.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Mock Payment / CommerceOS Payment boundary
- Domains touched: Mock Payment, Payment integration, API, Sales event hooks, Operations
- Persistence impact: Add provider WebhookDelivery and CommerceOS received-event/dedup/payment history records.
- Events/contracts impact: Versioned provider payment.* webhook schema and internal PaymentAuthorized/Captured/Failed facts with required envelope fields.
- AWS/IaC impact: Provider SQS webhook queue/DLQ and dispatcher Lambda; Commerce callback API/Lambda, DynamoDB, CloudWatch alarms.
- ADR required? No — webhook queue topology is documented; event publication mechanism becomes formal in TASK-0048.

## Security and tenant impact

- Authentication: CommerceOS authenticates provider callbacks with test signature/secret rotation strategy; endpoint is not user-authenticated.
- Authorization: Verify signature before processing, prevent replay/stale events, use managed secret/configuration, and redact signatures.
- Tenant scoping: Trusted tenant context scopes every merchant record and message; client tenant ids cannot override it.
- Sensitive data/secrets: No real card data or secrets are stored/logged; personal/business data is minimized and redacted.
- Abuse/rate-limit considerations: Strict body/time limits, rate limits, event-id dedup, and no arbitrary callback target.

## Reliability and idempotency impact

- Retry behavior: Provider retries transient HTTP results with bounded exponential backoff; permanent validation errors are not retried indefinitely.
- Timeout semantics: Delivery timeout means unknown acknowledgement and may redeliver; consumer dedup makes it safe.
- Duplicate-delivery behavior: Webhook event id and state/version guard suppress duplicate/out-of-order effects.
- Idempotency key/strategy: Provider event id is unique; internal event/payment transition uses provider event/source key.
- DLQ/recovery/reconciliation: Provider DLQ inspection/redrive is authorized/audited; invalid events remain rejected.

## Observability impact

- Logs: Structured logs carry safe tenant/entity/operation/event and correlation identifiers.
- Metrics: Measure outcomes, failures, retries, duplicates, latency, and stuck states at bounded cardinality.
- Traces/correlation: Preserve correlation/causation across all changed boundaries.
- Operational states/errors: Delivery attempts/status/HTTP result and CommerceOS accepted/duplicate/rejected outcomes are correlated.

## Cost impact

- Request/compute impact: SQS/Lambda requests scale with webhooks and bounded retries.
- Storage impact: Add provider WebhookDelivery and CommerceOS received-event/dedup/payment history records.
- Network impact: Signed HTTPS callback from provider to CommerceOS.
- New AWS resources/services: Provider SQS webhook queue/DLQ and dispatcher Lambda; Commerce callback API/Lambda, DynamoDB, CloudWatch alarms.
- Free Tier allowance relevant to this task: Use accepted serverless allowances and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible; queue volume and retries are small/bounded.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for targeted failure/cloud tests.

## Test plan

- Unit: Signature/staleness, state mapping, dedup, out-of-order, and retry classification.
- Integration: Real SQS redrive/DLQ, callback API, secrets/config, duplicate delivery, and IAM.
- Architecture: Check domain boundaries, tenant context, and no persistence shortcuts.
- Contract: Provider webhook v1 and internal Payment event v1 schemas.
- IaC: CDK assertions, synth, diff, and selected deployment checks.
- E2E/manual: Capture, deliver duplicate/reordered webhook, simulate callback outage to DLQ, then recover.
- **Cloud verification required?** Yes — SQS delivery/redrive, API timeouts, Lambda/IAM, and CloudWatch alarms need AWS verification.
- AWS environment/stack(s) required: MockPaymentStack plus Payment callback resources
- Preview/staging teardown plan: Destroy preview queues/functions and clear synthetic webhook/payment records.

