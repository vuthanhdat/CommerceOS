# TASK-0070 — Harden API edge and resource consumption limits

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 16
Milestone: Milestone E
Depends on: TASK-0069
Execution gate: WAF is introduced only if review justifies its cost and an ADR is accepted when material.

## Goal

Public and protected APIs plus serverless resources are hardened with measured throttling, concurrency, throughput, CDN/security, and cost limits without introducing unapproved standing-cost infrastructure.

## Business context

Scale-to-zero services can still amplify abuse/cost or exhaust downstream capacity; edge/resource limits must match measured traffic and Free Tier constraints.

## In scope

- define/implement API Gateway route throttles/quotas/payload limits and safe errors for public, merchant, admin, webhook, and test-control endpoints;
- set/review Lambda reserved concurrency, SQS batch/visibility, DynamoDB maximum throughput/capacity, Scheduler cadence, CloudFront cache/security headers and origin policy;
- evaluate WAF/CDN protection using current risk/pricing; create ADR/cost estimate before any paid WAF or standing-cost addition;

## Out of scope

- NAT Gateway, ALB, EC2, RDS, paid WAF, or other fixed-cost service without accepted ADR;
- application/business correctness changes unrelated to limits;

## Acceptance criteria

### AC01 — Bounded APIs

Given route classes are load/abuse reviewed
when limits are applied
then anonymous/expensive/admin/webhook/test routes have explicit throttling/payload behavior and legitimate small usage remains functional.

### AC02 — Resource containment

Given crawler/workers/queues/tables/functions are inspected
when guardrails are applied
then a burst cannot cause unbounded concurrency/throughput/retry/log cost or starve core commerce.

### AC03 — CDN/origin security

Given CloudFront/S3/public API policies are reviewed
when tests run
then private origins, tenant-safe cache keys, TLS/headers, and public-only data remain enforced.

### AC04 — Cost gate

Given paid WAF or new material service is proposed
when decision process runs
then no resource is added until ADR, monthly estimate, alternatives, and human acceptance exist.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and cloud evidence, cost, and teardown/retention decisions are recorded.

## Architecture impact

- Owning domain: Platform Security / Reliability / Cost
- Domains touched: API, Web, all Lambda/queue/table/scheduler resources
- Persistence impact: No business model change; capacity/resource policy config is versioned in CDK.
- Events/contracts impact: No event contract change unless throttling/retry behavior requires documented producer response semantics.
- AWS/IaC impact: API Gateway, Lambda concurrency, DynamoDB capacity/limits, SQS, Scheduler, CloudFront/S3, CloudWatch; WAF only if accepted.
- ADR required? Conditional — required for paid WAF or any new standing-cost/security architecture.

## Security and tenant impact

- Authentication: Test-control/admin/webhook endpoints have stronger identity restrictions than public routes.
- Authorization: Least privilege, origin protection, route-specific limits, secure headers, and no information leakage in throttled errors.
- Tenant scoping: Tenant-owned data and async context remain scoped by trusted identity; explicit audited platform access is the only cross-tenant path.
- Sensitive data/secrets: Secrets/PII/payment/audit data are minimized, protected, and redacted from logs/tests.
- Abuse/rate-limit considerations: Primary scope: request rates, sizes, concurrency, queue amplification, retries, cache abuse, and cost denial-of-service.

## Reliability and idempotency impact

- Retry behavior: Throttled downstream retries honor backoff/jitter and budgets; no retry storm.
- Timeout semantics: Timeouts align across API/Lambda/provider/queue visibility and return explicit states.
- Duplicate-delivery behavior: Rate/retry changes preserve idempotency for repeated operations.
- Idempotency key/strategy: Existing keys remain required; throttling response does not cause clients to mint new unsafe keys.
- DLQ/recovery/reconciliation: Alarms/runbook identify saturation and safe shed/pause/recovery actions.

## Observability impact

- Logs: Structured, redacted logs retain safe tenant/entity/event/operation/correlation context.
- Metrics: Use built-in metrics first; measure security, saturation, errors, latency, failures, recovery, and cost at bounded cardinality.
- Traces/correlation: Verify end-to-end correlation/causation through affected journeys.
- Operational states/errors: Throttle, concurrency, queue age, table throttle, cache/error, and cost indicators are dashboarded.

## Cost impact

- Request/compute impact: May reduce/amortize work; test chosen limits with bounded traffic.
- Storage impact: No business model change; capacity/resource policy config is versioned in CDK.
- Network impact: Measured and bounded; no unapproved fixed-cost network component.
- New AWS resources/services: API Gateway, Lambda concurrency, DynamoDB capacity/limits, SQS, Scheduler, CloudFront/S3, CloudWatch; WAF only if accepted.
- Free Tier allowance relevant to this task: Respect the approximately USD 100 credit envelope and normal $0–$5/month target.
- Expected monthly cost change or `negligible` with rationale: no material standing-cost increase by default; quantify custom metrics/WAF/capacity changes.
- Estimated one-off cloud-test/load-test cost, if any: Estimate and approve before execution; record actual spend/request volume afterward.

## Test plan

- Unit: Configuration validators and retry/backpressure policies.
- Integration: Real route throttles, Lambda concurrency, SQS visibility/batch, DynamoDB limits, CloudFront origin/cache.
- Architecture: Re-run and extend tenant/domain/event/IaC/security guardrails for discovered recurring risks.
- Contract: 429/limit/retry-after and payload-limit behavior.
- IaC: CDK assertions/synth/diff plus real AWS policy/resource verification.
- E2E/manual: Bounded burst verifies graceful throttle, core-flow availability, and no runaway backlog.
- **Cloud verification required?** Yes — throttling/concurrency/throughput/CDN/IAM semantics require AWS.
- AWS environment/stack(s) required: ephemeral staging with affected stacks
- Preview/staging teardown plan: Destroy load-test/staging resources and remove generated data/logs per retention.

