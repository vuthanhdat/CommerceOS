# TASK-0074 — Replace cost assumptions with operational measurements

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 16
Milestone: Milestone E
Depends on: TASK-0068, TASK-0072, TASK-0073

## Goal

CommerceOS operational dashboards and cost model use measured CloudWatch/Cost Explorer data to show reliability, latency, usage, and dominant cost drivers for learning and beta profiles.

## Business context

Phase 16 ends by replacing speculative cost/observability assumptions with real evidence before architecture audit or extraction.

## In scope

- define bounded platform/service/business SLI dashboard and alarm set using built-in metrics first;
- collect representative measured request, duration, storage, event, queue, workflow, CDN, Cognito, log, retry, and backup data from prior tests;
- update docs/04-cost-model.md and guardrail thresholds with reconciled estimates, uncertainty, dominant drivers, and review cadence;

## Out of scope

- third-party APM, high-cardinality custom metrics, contractual SLA, or automated billing decisions;
- hiding unexpected spend by excluding failed/retry/test traffic;

## Acceptance criteria

### AC01 — Operational dashboard

Given representative traffic/failures exist
when platform dashboard is opened
then API/Lambda/DynamoDB/SQS/DLQ/EventBridge/workflow/crawler/payment/accounting/report health and freshness are visible.

### AC02 — Measured cost baseline

Given test/learning usage is collected
when service units are normalized
then learning/beta estimates cite measured assumptions, current pricing date/region caveats, and dominant cost drivers.

### AC03 — Budget alignment

Given actual/forecast usage is compared to thresholds
when review completes
then alerts/guardrails match the remaining credit and deviations have explicit action/owner.

### AC04 — Bounded telemetry

Given logging/metrics are inspected
when cost/cardinality/retention checks run
then secrets/PII are absent and custom metrics/log growth stay within justified limits.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and cloud evidence, cost, and teardown/retention decisions are recorded.

## Architecture impact

- Owning domain: Observability / Cost Governance / Platform Operations
- Domains touched: All deployed stacks and docs/04-cost-model.md
- Persistence impact: CloudWatch/Cost Explorer data and small operational projections only; no business truth migration.
- Events/contracts impact: Measure event volume/failures; no contract change by default.
- AWS/IaC impact: CloudWatch dashboards/alarms/log retention and Cost Explorer/Budgets views; use existing services.
- ADR required? No unless measurement motivates a material architecture/paid observability change.

## Security and tenant impact

- Authentication: Platform telemetry/cost access is restricted; merchant dashboard exposes only tenant-safe relevant indicators.
- Authorization: Logs/metrics avoid tokens, PII, raw webhook/source payloads and unsafe tenant cardinality.
- Tenant scoping: Tenant-owned data and async context remain scoped by trusted identity; explicit audited platform access is the only cross-tenant path.
- Sensitive data/secrets: Secrets/PII/payment/audit data are minimized, protected, and redacted from logs/tests.
- Abuse/rate-limit considerations: Bound custom metric dimensions, dashboard queries, log verbosity/retention and cost polling.

## Reliability and idempotency impact

- Retry behavior: Measure retry amplification and alarm on abnormal loops/backlogs.
- Timeout semantics: Measure stuck/unknown duration and recovery latency.
- Duplicate-delivery behavior: Measure duplicate suppression without counting duplicates as business outcomes.
- Idempotency key/strategy: Telemetry identifies logical versus attempted operations where possible.
- DLQ/recovery/reconciliation: Dashboards link to existing runbooks/operations and show recovery outcome/time.

## Observability impact

- Logs: Structured, redacted logs retain safe tenant/entity/event/operation/correlation context.
- Metrics: Use built-in metrics first; measure security, saturation, errors, latency, failures, recovery, and cost at bounded cardinality.
- Traces/correlation: Verify end-to-end correlation/causation through affected journeys.
- Operational states/errors: Primary scope: one coherent health/cost evidence surface with documented ownership/freshness.

## Cost impact

- Request/compute impact: Mostly built-in telemetry queries; avoid continuous expensive polling/custom metrics.
- Storage impact: Explicit per-log-group retention and measured ingestion/storage.
- Network impact: Measured and bounded; no unapproved fixed-cost network component.
- New AWS resources/services: CloudWatch dashboards/alarms/log retention and Cost Explorer/Budgets views; use existing services.
- Free Tier allowance relevant to this task: Respect the approximately USD 100 credit envelope and normal $0–$5/month target.
- Expected monthly cost change or `negligible` with rationale: record actual telemetry cost and updated monthly scenario estimates; keep normal dev near target.
- Estimated one-off cloud-test/load-test cost, if any: Small measurement period; no additional load beyond approved prior campaigns.

## Test plan

- Unit: Metric/unit normalization and cost model calculation tests if automated.
- Integration: Alarm/metric/log emission and dashboard links under representative failures.
- Architecture: Re-run and extend tenant/domain/event/IaC/security guardrails for discovered recurring risks.
- Contract: Operational metric naming/dimensions and cost-model assumption schema.
- IaC: CDK assertions/synth/diff plus real AWS policy/resource verification.
- E2E/manual: Generate representative success/failure, verify dashboard/alarms, reconcile measured cost units, update docs.
- **Cloud verification required?** Yes — CloudWatch/Budgets/Cost Explorer and real usage evidence require AWS.
- AWS environment/stack(s) required: dev plus completed ephemeral test evidence; no new persistent staging
- Preview/staging teardown plan: Remove temporary dashboards/alarms/logs not retained and document final persistent telemetry cost.

