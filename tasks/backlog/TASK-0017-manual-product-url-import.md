# TASK-0017 — Deliver manual product URL import

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 3
Milestone: Milestone A
Depends on: TASK-0015, TASK-0016

## Goal

A merchant can paste a supported product URL, receive an idempotent queued import job, and track it to a normalized import candidate or actionable failure.

## Business context

Manual URL import is the smallest controlled ingestion experience and avoids uncontrolled discovery crawling.

## In scope

- validate supported source, allowed URL, tenant context, and source activation at the API boundary;
- create one idempotent crawl/import request and expose processing status;
- connect the request to the queued pipeline and create an ImportCandidate from a validated normalized snapshot;

## Out of scope

- automatic mutation of canonical products;
- bulk URL import, scheduled refresh, or discovery crawling;

## Acceptance criteria

### AC01 — Accepted import

Given an authorized merchant submits a permitted supported URL with an idempotency key
when the request is accepted
then one tenant-scoped job is queued and its status can be followed.

### AC02 — Replay safety

Given the same tenant repeats the same request/key
when the API and queue receive the retry
then the original logical job/result is returned without duplicate external fetch side effects.

### AC03 — Actionable rejection

Given a URL is unsupported, disallowed, disabled, malformed, or resolves outside allowed hosts
when submission is attempted
then it is rejected without outbound fetch and with a safe reason.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected real-AWS evidence plus teardown is recorded.

## Architecture impact

- Owning domain: Product Data Ingestion
- Domains touched: Ingestion, Back Office API, Source Registry
- Persistence impact: Add tenant-scoped ImportRequest/ImportCandidate status keyed by idempotency/job identity.
- Events/contracts impact: ImportCandidateCreated after validated snapshot storage; versioned job/status contracts.
- AWS/IaC impact: Existing API/Lambda, SQS crawler pipeline, DynamoDB status records.
- ADR required? No — the task follows accepted architecture; create one only if implementation changes a significant decision.

## Security and tenant impact

- Authentication: Use the established merchant or public identity boundary as applicable.
- Authorization: Only authorized tenant users submit imports; URL validation prevents SSRF, redirects to disallowed hosts, and source bypass.
- Tenant scoping: All merchant-owned reads/writes derive tenant scope from trusted context; client tenant ids cannot override it and cross-tenant denial is tested.
- Sensitive data/secrets: No secrets, tokens, real card data, or unnecessary personal data are stored or logged.
- Abuse/rate-limit considerations: Per-tenant/source request limits, payload size caps, and duplicate suppression.

## Reliability and idempotency impact

- Retry behavior: API retries reuse the original key; worker retries follow TASK-0015 classification.
- Timeout semantics: API returns accepted/processing and never waits for external fetch completion.
- Duplicate-delivery behavior: Duplicate request/message cannot create duplicate jobs or candidates.
- Idempotency key/strategy: Tenant + caller key + normalized target/request hash.
- DLQ/recovery/reconciliation: Failed jobs expose status and link to DLQ/retry only for authorized operations.

## Observability impact

- Logs: Structured logs include safe tenant/entity/job identifiers, operation, and correlation context.
- Metrics: Use built-in service metrics and only bounded business metrics justified by the operational risk.
- Traces/correlation: Preserve request/correlation identifiers across changed boundaries.
- Operational states/errors: Queued/running/succeeded/failed/policy-blocked states include correlation and safe error codes.

## Cost impact

- Request/compute impact: One bounded async job per accepted unique request.
- Storage impact: Add tenant-scoped ImportRequest/ImportCandidate status keyed by idempotency/job identity.
- Network impact: Normal API traffic only unless external fetching is explicitly in scope.
- New AWS resources/services: Existing API/Lambda, SQS crawler pipeline, DynamoDB status records.
- Free Tier allowance relevant to this task: Use the documented Lambda/DynamoDB/S3/SQS/CloudWatch allowances and non-production caps where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measurements are material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview verification.

## Test plan

- Unit: URL normalization/allowlist, idempotency, and status transitions.
- Integration: API-to-SQS-to-candidate path with duplicates and failures.
- Architecture: Enforce domain dependency direction, ownership, and trusted tenant context.
- Contract: Manual import request/status and ImportCandidate v1.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Submit one approved URL and observe a candidate; retry and prove one job.
- **Cloud verification required?** Yes — API Gateway, Lambda, SQS, DynamoDB, and IAM behavior require AWS verification.
- AWS environment/stack(s) required: CommerceStack import endpoint and dev/preview CrawlerStack
- Preview/staging teardown plan: Destroy ephemeral resources and record intentionally retained dev resources.

