# TASK-0032 — Deploy the core Mock Payment Provider

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 7
Milestone: Milestone A
Depends on: TASK-0005

## Goal

An independently deployed Mock Payment Provider can create, authorize, capture, query payment intents, and enforce provider-side idempotency without storing real card data.

## Business context

The provider must behave like an external system so CommerceOS learns integration boundaries before failure engineering is added.

## In scope

- create MockPaymentStack with API, Lambda, DynamoDB state, least-privilege IAM, bounded logs, and environment-safe test controls;
- implement PaymentIntent/PaymentOperation state, create/authorize/capture/query endpoints, request hashing, idempotency retention, and VND money validation;
- publish an OpenAPI/contract fixture and local provider test host independent of Sales;

## Out of scope

- refunds, webhook delivery, timeout/delayed/500 scenarios, or CommerceOS adapter;
- real payment processors, PAN/CVV/card secrets, banking rules, or random chaos;

## Acceptance criteria

### AC01 — Provider lifecycle

Given a valid test payment intent is created
when authorize/capture/query operations run
then the provider returns valid deterministic state transitions and durable results.

### AC02 — Idempotency contract

Given the same key/equivalent request is repeated or the same key has incompatible data
when a state-changing endpoint runs
then equivalent replay returns the original result and incompatible reuse is rejected.

### AC03 — Independent boundary

Given CommerceOS and provider code are inspected/deployed
when dependencies and endpoints are reviewed
then the provider has its own stack/state/API and Sales has no direct provider persistence/helper dependency.

### AC04 — No card data

Given requests, persistence, fixtures, and logs are inspected
when payment tests run
then no real/cardholder data fields or secret material exist.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and selected cloud verification plus teardown evidence is recorded.

## Architecture impact

- Owning domain: Mock Payment
- Domains touched: Mock Payment only; CommerceOS boundary is consumed later
- Persistence impact: Dedicated provider PaymentIntent/PaymentOperation records with request hash and bounded idempotency expiry.
- Events/contracts impact: No webhook yet; provider HTTP API contract v1.
- AWS/IaC impact: Independent MockPaymentStack: HTTP API/Function URL as decided, Lambda, DynamoDB, CloudWatch.
- ADR required? No — independent MockPaymentStack and HTTP boundary are accepted architecture.

## Security and tenant impact

- Authentication: Non-production provider endpoint uses an explicit test-only access policy; test-control capability is not public production behavior.
- Authorization: Least-privilege provider resources, TLS, request validation, and no real card fields/secrets.
- Tenant scoping: All tenant-owned commands/queries use trusted tenant context; client tenant ids are rejected/ignored and cross-tenant access is tested.
- Sensitive data/secrets: No secrets or real payment/card data are stored or logged.
- Abuse/rate-limit considerations: Throttle endpoints, cap payloads/amounts, and restrict failure/test controls to learning environments.

## Reliability and idempotency impact

- Retry behavior: Provider state-changing requests are safe to retry only with idempotency keys.
- Timeout semantics: Core task defines normal endpoint timeouts; ambiguous scenarios are TASK-0035.
- Duplicate-delivery behavior: Duplicate HTTP calls with a key return one stored logical operation result.
- Idempotency key/strategy: Idempotency-Key + operation + request hash with explicit retention.
- DLQ/recovery/reconciliation: N/A — no queue/event consumer.

## Observability impact

- Logs: Structured logs include safe tenant, entity, command/event, and correlation identifiers.
- Metrics: Bounded metrics cover conflicts, failures, and latency for the changed capability.
- Traces/correlation: Preserve correlation across all application/external boundaries.
- Operational states/errors: State transition errors, incompatible key reuse, validation, and provider errors have stable codes/metrics.

## Cost impact

- Request/compute impact: Low-volume API/Lambda/DynamoDB requests.
- Storage impact: Small provider state/idempotency records with TTL where appropriate.
- Network impact: HTTPS boundary between CommerceOS/test clients and provider.
- New AWS resources/services: Independent MockPaymentStack: HTTP API/Function URL as decided, Lambda, DynamoDB, CloudWatch.
- Free Tier allowance relevant to this task: Use documented Lambda, DynamoDB, API Gateway, SQS, and CloudWatch limits where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible; independent stack has no idle compute and bounded logs.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview tests.

## Test plan

- Unit: Provider state machine, money/request hash, idempotency, and invalid transitions.
- Integration: Provider API/state persistence, concurrent duplicate calls, IAM, and TTL configuration.
- Architecture: Enforce domain ownership, inward dependencies, and no cross-domain persistence access.
- Contract: OpenAPI/HTTP contract for create/authorize/capture/query.
- IaC: CDK assertions, synth, and reviewed diff.
- E2E/manual: Deploy provider, create/capture/query a synthetic payment, and replay its key.
- **Cloud verification required?** Yes — independent API Gateway/Lambda/DynamoDB packaging, IAM, and concurrency need real-AWS evidence.
- AWS environment/stack(s) required: ephemeral/dev MockPaymentStack
- Preview/staging teardown plan: Destroy preview provider state; keep dev failure controls restricted.

