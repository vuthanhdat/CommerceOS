# TASK-0061 — Track source changes, price history, and parser health

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 14
Milestone: Milestone D
Depends on: TASK-0060

## Goal

Merchants and operators can see external price/availability/specification changes over time and detect parser degradation using versioned hashes, snapshots, metrics, and controlled reprocessing.

## Business context

Refresh is valuable only when meaningful changes are separated from parser noise and source drift becomes diagnosable.

## In scope

- persist configurable price history and normalized/content/price/spec hashes with change classification;
- emit ProductSourcePriceChanged/AvailabilityChanged/SpecsChanged/ParserBehaviorChanged facts and expose merchant source history;
- build per-source/parser-version dashboard/alarms and controlled reprocessing of retained permitted raw fixtures/snapshots;

## Out of scope

- automatic canonical selling-price changes, unbounded raw retention, cross-source product matching, or advanced discovery;
- re-fetching live sources during CI;

## Acceptance criteria

### AC01 — Meaningful change

Given a refreshed normalized snapshot differs in price/availability/specs
when comparison runs
then one correctly classified versioned change/history entry is stored and duplicate identical refresh produces none.

### AC02 — Merchant safety

Given source data changes
when merchant views history
then canonical product/overrides remain unchanged unless a separate explicit import action occurs.

### AC03 — Parser health

Given failure/missing-field/layout metrics rise for an adapter version
when threshold is crossed
then operator sees alarm, affected samples/versions, and can pause source.

### AC04 — Controlled reprocess

Given retained permitted raw sample is selected
when new parser version reprocesses it
then a traceable result is produced without pretending it was a new live capture or duplicating history.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence/cleanup is recorded.

## Architecture impact

- Owning domain: Product Data Ingestion / Reporting
- Domains touched: Ingestion, Reporting dashboard, Catalog mapping reference, Notification
- Persistence impact: Add source price/change history, hash/version metadata, parser metrics summaries, reprocess lineage; retention is bounded.
- Events/contracts impact: Versioned source-change and parser-behavior facts with stable snapshot/source ids.
- AWS/IaC impact: Existing crawler/report resources plus bounded CloudWatch metrics/alarms; no new service.
- ADR required? No — accepted architecture covers the scope.

## Security and tenant impact

- Authentication: Use established merchant/platform/internal identities.
- Authorization: Source history respects tenant mappings; raw/debug access is restricted and redacted.
- Tenant scoping: Trusted tenant context scopes all records, messages, and operations; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets/real card data; source/customer/business fields are minimized and redacted.
- Abuse/rate-limit considerations: Retention, reprocess batches, metric cardinality, and history pages are bounded.

## Reliability and idempotency impact

- Retry behavior: Duplicate refresh/reprocess uses hash/snapshot identity; parser failure does not blindly retry live fetch.
- Timeout semantics: Timeout remains explicit and is queried/reconciled before unsafe retry.
- Duplicate-delivery behavior: Hash + source snapshot/version prevents duplicate change events/history.
- Idempotency key/strategy: SourceId + productId + capturedAt/snapshotId + change type/version.
- DLQ/recovery/reconciliation: Parser alarm supports pause, fixture fix, new version, and controlled reprocess; failed change consumers use DLQ.

## Observability impact

- Logs: Structured logs include safe tenant/source/entity/operation/event and correlation data.
- Metrics: Track outcomes, failures, retries, duplicates, lag/age, recovery, and latency.
- Traces/correlation: Preserve correlation/causation across changed domains and providers.
- Operational states/errors: Source/parser version success/failure/missing field/changes/lag/reprocess outcomes are visible.

## Cost impact

- Request/compute impact: Incremental hashing/comparison per refresh plus bounded dashboard queries.
- Storage impact: Normalized/price history retention is configurable; raw remains seven days by default.
- Network impact: Only approved bounded external/internal traffic.
- New AWS resources/services: Existing crawler/report resources plus bounded CloudWatch metrics/alarms; no new service.
- Free Tier allowance relevant to this task: Use accepted serverless allowances, disabled/low non-prod schedules, and bounded concurrency.
- Expected monthly cost change or `negligible` with rationale: small but recurring; track snapshot/history/log/metric growth and update model if material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Normalization hashes, change classification, duplicate suppression, parser-version lineage.
- Integration: Refresh→history/event→dashboard/notification, parser alarm, reprocess.
- Architecture: Enforce domain ownership, tenant isolation, event/idempotency, and no persistence shortcuts.
- Contract: Source change/parser behavior event schemas and history APIs.
- IaC: CDK assertions, synth, diff, and affected policy/routing checks.
- E2E/manual: Change a fixture price/layout, refresh, view event/history/alarm, reprocess under new parser.
- **Cloud verification required?** Yes — deployed refresh/event/metrics/alarms/read-model integration needs AWS.
- AWS environment/stack(s) required: Crawler/Reporting resources
- Preview/staging teardown plan: Destroy ephemeral resources and remove synthetic data/schedules.

