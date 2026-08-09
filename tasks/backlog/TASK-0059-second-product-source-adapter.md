# TASK-0059 — Add a second policy-approved product source

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 14
Milestone: Milestone D
Depends on: TASK-0019
Execution gate: Policy review must approve the selected source.

## Goal

A second Vietnamese product source can produce normalized snapshots through the same policy, adapter, queue, and import contracts without source-specific logic leaking into Catalog.

## Business context

A second source proves the adapter boundary is real and exposes normalization differences before scheduled intelligence is added.

## In scope

- perform current policy/robots/official-feed review and activate one approved second source with explicit rates/allowed content;
- implement adapter URL/fetch/parse/normalize/validate plus sanitized permitted fixture suite;
- prove the shared pipeline/import UI handles missing/different fields and attribution without special Catalog persistence logic;

## Out of scope

- scheduled refresh, Amazon, discovery crawling, automatic cross-source matching, or content/image copying;
- sharing selectors/credentials or bypass behavior between adapters;

## Acceptance criteria

### AC01 — Policy gate

Given the candidate second source is reviewed
when activation is requested
then only an approved source with allowed patterns/rates/content rules becomes usable.

### AC02 — Contract parity

Given first- and second-source fixtures run
when normalized results are compared
then both satisfy the same versioned schema while preserving source-specific absence/failure semantics.

### AC03 — Shared import flow

Given a second-source URL is submitted/reviewed
when the pipeline completes
then merchant imports selected fields through existing contracts and Catalog remains source-agnostic.

### AC04 — Source isolation

Given second adapter fails/pauses
when first source and commerce flows run
then failure is isolated and the kill switch works independently.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence/cleanup is recorded.

## Architecture impact

- Owning domain: Product Data Ingestion
- Domains touched: Ingestion adapters, Source Registry, import UI; Catalog contract unchanged
- Persistence impact: Reuse source/snapshot/import schemas; record distinct adapter/parser versions and source identity.
- Events/contracts impact: Reuse versioned crawl/import facts; no source-specific public event schema.
- AWS/IaC impact: Existing CrawlerStack; no new service.
- ADR required? No — accepted architecture covers the scope.

## Security and tenant impact

- Authentication: Use established merchant/platform/internal identities.
- Authorization: Approved host/path only, least privilege, no SSRF, credentials via managed reference if needed.
- Tenant scoping: Trusted tenant context scopes all records, messages, and operations; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets/real card data; source/customer/business fields are minimized and redacted.
- Abuse/rate-limit considerations: Per-source rate/concurrency/size/time limits and immediate independent kill switch.

## Reliability and idempotency impact

- Retry behavior: Second-source transient policy mirrors shared classifier but respects source-specific 429/terms.
- Timeout semantics: Timeout remains explicit and is queried/reconciled before unsafe retry.
- Duplicate-delivery behavior: Duplicate messages/events/workflow callbacks cannot repeat effects.
- Idempotency key/strategy: Stable source/event/operation keys protect all side effects.
- DLQ/recovery/reconciliation: Source-specific DLQ/metrics/replay remain distinguishable.

## Observability impact

- Logs: Structured logs include safe tenant/source/entity/operation/event and correlation data.
- Metrics: Track outcomes, failures, retries, duplicates, lag/age, recovery, and latency.
- Traces/correlation: Preserve correlation/causation across changed domains and providers.
- Operational states/errors: Per-source/parser version success, status, policy block, failure, bytes, latency, and change stats.

## Cost impact

- Request/compute impact: Bounded business/scheduled/event workload.
- Storage impact: Reuse source/snapshot/import schemas; record distinct adapter/parser versions and source identity.
- Network impact: One approved second source in bounded manual dev sampling; CI is fixture-only.
- New AWS resources/services: Existing CrawlerStack; no new service.
- Free Tier allowance relevant to this task: Use accepted serverless allowances, disabled/low non-prod schedules, and bounded concurrency.
- Expected monthly cost change or `negligible` with rationale: negligible; sample count/concurrency capped and no schedule yet.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Complete second-source parser fixtures and policy/URL validation.
- Integration: Shared pipeline/import with adapter selection, failures, duplicates, and tenant isolation.
- Architecture: Enforce domain ownership, tenant isolation, event/idempotency, and no persistence shortcuts.
- Contract: Adapter conformance to NormalizedSourceProduct v1.
- IaC: CDK assertions, synth, diff, and affected policy/routing checks.
- E2E/manual: Manually import one approved product from each source through the same review flow.
- **Cloud verification required?** Yes — deployed worker networking/pipeline and independent source controls need bounded AWS verification.
- AWS environment/stack(s) required: dev CrawlerStack only
- Preview/staging teardown plan: Delete test raw data as required and leave both sources manual/paused according to policy.

