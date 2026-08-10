# TASK-0014 — Establish the product-source registry and policy gate

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 3
Milestone: Milestone A
Depends on: TASK-0009

## Goal

Authorized operators can register, pause, and disable external product sources, but no HTML source can become active without recorded current policy review and bounded access settings.

## Business context

External collection creates legal, operational, and cost risk; source policy must be explicit system state rather than hidden adapter constants.

## In scope

- introduce DataSource configuration including mode, host patterns, status, rate, concurrency, retention, adapter version, and policy evidence;
- implement Source Registry management and authorization with an immediate kill switch;
- define and execute the implementation-time robots/terms/API review workflow before adapter activation;

## Out of scope

- fetching or parsing a live product page;
- Amazon integration, schedules, discovery crawling, or catalog import;

## Acceptance criteria

### AC01 — Policy-gated activation

Given a source lacks current policy review, allowed URL patterns, or bounded rates
when an operator attempts to activate it
then activation is rejected and no crawl work can be enqueued.

### AC02 — Kill switch

Given an active source is paused or disabled
when new or queued work is evaluated
then new dispatch stops and workers refuse unsafe fetches according to the documented policy.

### AC03 — Audited configuration

Given a privileged source setting changes
when the change succeeds
then actor, reason, old/new safe summary, policy date, and correlation id are auditable.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected real-AWS evidence plus teardown is recorded.

## Architecture impact

- Owning domain: Product Data Ingestion / Audit
- Domains touched: Ingestion, Authorization, Audit, Platform Operations
- Persistence impact: Add tenant/platform-scoped DataSource registry records; credentials are references, not stored raw configuration.
- Events/contracts impact: Source configuration changes use explicit application/audit contracts; no crawl event yet.
- AWS/IaC impact: DynamoDB registry persistence and protected API routes; no scheduler or worker.
- ADR required? No — implements the accepted ingestion boundary; a new source mechanism may require an ADR later.

## Security and tenant impact

- Authentication: Use the established merchant or public identity boundary as applicable.
- Authorization: Only permitted tenant/platform roles manage source configuration; shared-source versus tenant override ownership is explicit.
- Tenant scoping: All merchant-owned reads/writes derive tenant scope from trusted context; client tenant ids cannot override it and cross-tenant denial is tested.
- Sensitive data/secrets: API credentials, if ever required, are referenced through managed secret configuration and never returned/logged.
- Abuse/rate-limit considerations: Allowed hosts, URL patterns, rates, concurrency, and raw retention are mandatory and bounded.

## Reliability and idempotency impact

- Retry behavior: Synchronous failures are deterministic; retryable writes use the stated idempotency strategy.
- Timeout semantics: N/A unless an external boundary is called.
- Duplicate-delivery behavior: N/A — no at-least-once consumer is introduced.
- Idempotency key/strategy: Use optimistic concurrency or a stable command key where duplicate writes are unsafe.
- DLQ/recovery/reconciliation: N/A — no asynchronous work is introduced.

## Observability impact

- Logs: Structured logs include safe tenant/entity/job identifiers, operation, and correlation context.
- Metrics: Use built-in service metrics and only bounded business metrics justified by the operational risk.
- Traces/correlation: Preserve request/correlation identifiers across changed boundaries.
- Operational states/errors: Policy-blocked, paused, disabled, invalid-host, and misconfigured states are explicit.

## Cost impact

- Request/compute impact: Scales with user requests or explicitly bounded background jobs.
- Storage impact: Add tenant/platform-scoped DataSource registry records; credentials are references, not stored raw configuration.
- Network impact: Normal API traffic only unless external fetching is explicitly in scope.
- New AWS resources/services: DynamoDB registry persistence and protected API routes; no scheduler or worker.
- Free Tier allowance relevant to this task: Use the documented Lambda/DynamoDB/S3/SQS/CloudWatch allowances and non-production caps where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measurements are material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview verification.

## Test plan

- Unit: Registry state machine, host matching, activation prerequisites, and redaction.
- Integration: Registry authorization, persistence, kill-switch checks, and audit records.
- Architecture: Enforce domain dependency direction, ownership, and trusted tenant context.
- Contract: DataSource management and worker-policy lookup contracts.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Create a disabled source, record policy evidence, activate it, then pause it and prove dispatch denial.
- **Cloud verification required?** Yes — registry persistence, protected API behavior, and IAM should be verified on AWS.
- AWS environment/stack(s) required: Crawler/registry resources selected in CommerceStack or CrawlerStack
- Preview/staging teardown plan: Destroy ephemeral resources and record intentionally retained dev resources.

