# TASK-0015 — Build the queued ingestion and snapshot pipeline

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 3
Milestone: Milestone A
Depends on: TASK-0004, TASK-0014

## Goal

A bounded crawl job can be queued, fetched by a controlled worker, saved as a short-lived raw snapshot, normalized, and stored separately from the merchant catalog.

## Business context

Crawler latency and failure require SQS backpressure, while source snapshots must never become canonical products by accident.

## In scope

- define crawl job, raw snapshot, normalized source product, and pipeline-state contracts;
- add SQS queue/DLQ, bounded Lambda worker, S3 raw bucket with seven-day lifecycle, and DynamoDB snapshot persistence;
- implement retry classification, idempotent job handling, source-policy enforcement, and correlation across the pipeline;

## Out of scope

- a real source adapter beyond a fixture/test adapter;
- merchant import, scheduled refresh, discovery crawl, or catalog mutation;

## Acceptance criteria

### AC01 — Successful fixture pipeline

Given an active fixture source and valid crawl job exist
when the worker processes the job
then one raw object and one normalized immutable snapshot are stored with source, parser/schema version, hashes, and correlation data.

### AC02 — Bounded failure

Given a transient failure repeats beyond the configured attempts
when the queue redrive policy is exhausted
then the message reaches the DLQ and no canonical product is written.

### AC03 — Duplicate safety

Given the same job/message is delivered more than once
when workers process the duplicates
then only one logical snapshot/result is produced or duplicates are explicitly linked without repeated side effects.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected real-AWS evidence plus teardown is recorded.

## Architecture impact

- Owning domain: Product Data Ingestion
- Domains touched: Ingestion, Platform Operations; Catalog is not mutated
- Persistence impact: Add immutable normalized snapshots and job/idempotency state in DynamoDB plus seven-day raw S3 objects.
- Events/contracts impact: Versioned crawl-job contract and ProductSourceCrawled only after successful validated storage.
- AWS/IaC impact: CrawlerStack with SQS/DLQ, Lambda worker, S3 lifecycle, DynamoDB, IAM, CloudWatch alarms/logs.
- ADR required? No — SQS/S3/DynamoDB crawler architecture is already accepted.

## Security and tenant impact

- Authentication: Use the established merchant or public identity boundary as applicable.
- Authorization: Workers use least-privilege access and validate source registry/allowed URLs before outbound fetch.
- Tenant scoping: Jobs and snapshots carry validated tenant context where tenant-owned; shared source configuration is explicit.
- Sensitive data/secrets: No secrets, tokens, real card data, or unnecessary personal data are stored or logged.
- Abuse/rate-limit considerations: Validate input size/shape and bound expensive or externally reachable operations.

## Reliability and idempotency impact

- Retry behavior: Retry only transient network/5xx/approved-429 conditions with bounded backoff and jitter; policy/parser/validation failures are terminal.
- Timeout semantics: Fetch timeout becomes a classified failed attempt and never an assumed snapshot.
- Duplicate-delivery behavior: Job id and source capture identity suppress repeated writes/effects.
- Idempotency key/strategy: Stable crawlJobId plus source/target/capture identity.
- DLQ/recovery/reconciliation: DLQ records retain safe context and support reviewed replay after the cause is corrected.

## Observability impact

- Logs: Structured logs include safe tenant/entity/job identifiers, operation, and correlation context.
- Metrics: Queue age/depth, DLQ depth, worker errors/duration, fetch outcomes, bytes, and snapshot results.
- Traces/correlation: Preserve request/correlation identifiers across changed boundaries.
- Operational states/errors: Every pipeline state and failure classification is queryable without inspecting raw logs only.

## Cost impact

- Request/compute impact: Lambda/SQS requests are capped by worker concurrency and batch size.
- Storage impact: Raw S3 expires after seven days; normalized snapshots use documented retention.
- Network impact: Only registry-approved outbound source requests; CI uses fixtures and no live network.
- New AWS resources/services: CrawlerStack with SQS/DLQ, Lambda worker, S3 lifecycle, DynamoDB, IAM, CloudWatch alarms/logs.
- Free Tier allowance relevant to this task: Use the documented Lambda/DynamoDB/S3/SQS/CloudWatch allowances and non-production caps where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible for fixture/low-volume manual jobs; uncontrolled concurrency is mechanically prevented.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview verification.

## Test plan

- Unit: Pipeline state transitions, retry classifier, normalization envelope, hashes, and idempotency.
- Integration: SQS duplicate/redrive, S3 lifecycle/policy, DynamoDB snapshots, and worker IAM.
- Architecture: Enforce domain dependency direction, ownership, and trusted tenant context.
- Contract: CrawlJob v1 and NormalizedSourceProduct v1 schemas.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Queue a fixture job, store raw/normalized results, then prove poison work reaches DLQ.
- **Cloud verification required?** Yes — SQS visibility/redrive, Lambda, S3 lifecycle, IAM, and DynamoDB behavior require AWS verification.
- AWS environment/stack(s) required: CrawlerStack in an ephemeral preview or dev
- Preview/staging teardown plan: Destroy preview resources and test objects; retain only documented dev resources with schedules disabled.

