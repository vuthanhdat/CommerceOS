# TASK-0016 — Implement the first policy-approved source adapter

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 3
Milestone: Milestone A
Depends on: TASK-0014, TASK-0015
Execution gate: Policy review must approve one Vietnamese electronics source.

## Goal

The policy-approved first Vietnamese electronics adapter can parse permitted saved fixtures deterministically and perform a small controlled manual live verification without bypassing source protections.

## Business context

The MVP needs one realistic source, but adapter implementation must follow current policy evidence and stay isolated from canonical Catalog logic.

## In scope

- select one source only after a current robots/terms/official-API review and record the decision;
- implement source-specific URL validation, fetch, parse, normalize, and validation behind the adapter contract;
- add sanitized permitted fixtures for normal, missing-field, changed-layout, blocked, and invalid content cases plus a bounded manual live sample;

## Out of scope

- scheduled refresh, category discovery, anti-bot bypass, CAPTCHA handling, or multiple sources;
- automatic copying of descriptions/images or changing merchant selling price;

## Acceptance criteria

### AC01 — Policy-approved adapter

Given the policy gate approves one source and allowed URL patterns
when the adapter is enabled
then only permitted targets and configured rates can be fetched.

### AC02 — Deterministic parsing

Given the committed fixture suite is parsed
when normal and degraded fixtures are processed
then normalized fields distinguish absent, parse failure, and source-unavailable values with adapter/schema versions.

### AC03 — Safe source failure

Given the source returns 403, CAPTCHA, unexpected layout, or policy denial
when the adapter processes the response
then it stops without bypass attempts and emits a classified operational failure.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected real-AWS evidence plus teardown is recorded.

## Architecture impact

- Owning domain: Product Data Ingestion
- Domains touched: Ingestion adapter infrastructure and policy registry; Catalog is untouched
- Persistence impact: No new persistence shape beyond TASK-0015 snapshots and fixture files.
- Events/contracts impact: Adapter returns the normalized contract; pipeline owns crawl success/failure facts.
- AWS/IaC impact: Existing crawler worker may make tightly bounded outbound HTTPS calls; no new managed service.
- ADR required? No for the selected HTML adapter; use an ADR if an official API or new paid dependency changes architecture.

## Security and tenant impact

- Authentication: Use the established merchant or public identity boundary as applicable.
- Authorization: Live fetch is restricted to approved hosts/paths and source settings; no user-controlled arbitrary URL/SSRF.
- Tenant scoping: Imported targets remain associated with the requesting trusted tenant/job.
- Sensitive data/secrets: Sanitize fixtures and responses; do not commit credentials, personal data, or restricted content.
- Abuse/rate-limit considerations: Concurrency one by default, policy-defined rate, jitter, size/time limits, and immediate kill switch.

## Reliability and idempotency impact

- Retry behavior: Only approved transient failures retry; 403/CAPTCHA/policy/parser validation failures do not.
- Timeout semantics: Timeout becomes an explicit processing/failure state; it is not treated as success.
- Duplicate-delivery behavior: Duplicate delivery cannot create a second logical side effect.
- Idempotency key/strategy: Stable job/event/source identifiers protect repeated effects.
- DLQ/recovery/reconciliation: Parser failures go to reviewed operational handling; replay uses retained fixture/raw data when permitted.

## Observability impact

- Logs: Structured logs include safe tenant/entity/job identifiers, operation, and correlation context.
- Metrics: Per-source status codes, parser/validation failures, duration, bytes, and policy blocks.
- Traces/correlation: Preserve request/correlation identifiers across changed boundaries.
- Operational states/errors: Parser version and exact classified reason are visible for every failure.

## Cost impact

- Request/compute impact: Scales with user requests or explicitly bounded background jobs.
- Storage impact: No new persistence shape beyond TASK-0015 snapshots and fixture files.
- Network impact: One approved external host in manual dev verification; CI is fixture-only.
- New AWS resources/services: Existing crawler worker may make tightly bounded outbound HTTPS calls; no new managed service.
- Free Tier allowance relevant to this task: Use the documented Lambda/DynamoDB/S3/SQS/CloudWatch allowances and non-production caps where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible; live sample count and concurrency are explicitly capped.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview verification.

## Test plan

- Unit: All saved parser fixtures and URL/policy validation.
- Integration: Adapter plugged into the queue pipeline with a fake HTTP transport.
- Architecture: Enforce domain dependency direction, ownership, and trusted tenant context.
- Contract: Source adapter and normalized product schemas.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: One manually approved URL produces a normalized source snapshot in dev.
- **Cloud verification required?** Yes — the deployed worker's networking, timeouts, IAM, and snapshot integration need one bounded AWS sample; source policy must be rechecked at implementation time.
- AWS environment/stack(s) required: dev CrawlerStack only
- Preview/staging teardown plan: Delete test raw objects/snapshots as appropriate; keep the source paused or manual-only after verification.

