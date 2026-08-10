# TASK-0086 — Deliver a basic balance-sheet projection

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Later product capability
Milestone: Unscheduled
Depends on: TASK-0047, TASK-0055

## Goal

Authorized merchants can view a basic as-of balance-sheet projection that reconciles Assets = Liabilities + Equity using posted Accounting data and clearly states its simplified scope.

## Business context

The Product Definition names balance sheet as later accounting/reporting output; it depends on stable chart, ledger, and financial projections.

## In scope

- extend accounting policy/chart mapping for balance-sheet account classification, retained/current earnings simplification, as-of and reversal behavior;
- implement tenant Assets/Liabilities/Equity projection/query with drill-through and reconciliation to Trial Balance;
- deliver Back Office balance-sheet view, freshness/as-of, imbalance exception, scope disclosure, and tests;

## Out of scope

- certified financial statement, cash-flow statement, consolidation, period close, multi-currency, tax, or complex equity accounting;
- deriving balances directly from operational tables;

## Acceptance criteria

### AC01 — Balanced statement

Given valid posted journals and account classifications exist
when balance sheet is generated as of a date
then Assets equals Liabilities plus Equity under documented policy and all lines trace to trial balance/accounts.

### AC02 — Correction/as-of

Given journals/reversals occur around report date
when different as-of reports run
then each uses only applicable posted entries and corrections affect the proper period without source mutation.

### AC03 — Exception visibility

Given account mapping is missing or projection differs from trial balance
when report/reconciliation runs
then statement is marked incomplete/imbalanced with actionable account/source detail rather than fabricated balance.

### AC04 — Scope and access

Given merchant views report
when authorization/labels are checked
then only permitted tenant financial data appears and simplified non-certified scope is explicit.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected cloud evidence, cost, and cleanup are recorded.

## Architecture impact

- Owning domain: Accounting / Reporting
- Domains touched: Accounting policy/chart/ledger, Reporting projections, Back Office
- Persistence impact: Add balance-sheet mapping/projection/reconciliation records derived only from posted Accounting truth.
- Events/contracts impact: Consume JournalPosted/Reversed or Accounting projection contract; no operational-table input.
- AWS/IaC impact: Existing Accounting/Reporting DynamoDB/worker/API resources; no new service.
- ADR required? Yes if TASK-0044 accounting policy must materially change; otherwise update the accepted policy documentation.

## Security and tenant impact

- Authentication: Use the established merchant/shopper/internal identity boundary.
- Authorization: Accounting/owner permission required; drill-through follows journal authorization.
- Tenant scoping: Trusted tenant context scopes all data/actions; public/shopper identities can access only explicitly authorized tenant resources.
- Sensitive data/secrets: Minimize/redact PII, secrets, and provider data; no real card data.
- Abuse/rate-limit considerations: Bound as-of ranges/grouping/rebuild and query frequency.

## Reliability and idempotency impact

- Retry behavior: Projection updates/rebuild retry idempotently by journal/event version.
- Timeout semantics: Stale/unavailable projection reports freshness rather than pretending current state.
- Duplicate-delivery behavior: Journal/event identity and reversal linkage prevent double effect.
- Idempotency key/strategy: Tenant + balance-sheet period/account + journal/event version.
- DLQ/recovery/reconciliation: Reconciliation mismatch creates exception and supports rebuild; journals remain immutable.

## Observability impact

- Logs: Structured/redacted logs include safe tenant/entity/operation/event and correlation data.
- Metrics: Measure success/failure, duplicates, latency, backlog/stuck state, and relevant usage/cost.
- Traces/correlation: Preserve correlation/causation through all changed boundaries.
- Operational states/errors: As-of/freshness/account mapping/imbalance/source drill-through/rebuild status visible.

## Cost impact

- Request/compute impact: Bounded projection updates and report queries.
- Storage impact: Add balance-sheet mapping/projection/reconciliation records derived only from posted Accounting truth.
- Network impact: Bounded API/CDN/provider traffic only.
- New AWS resources/services: Existing Accounting/Reporting DynamoDB/worker/API resources; no new service.
- Free Tier allowance relevant to this task: Validate current pricing/allowances at scheduling time and keep non-production usage bounded.
- Expected monthly cost change or `negligible` with rationale: negligible; read model avoids full transactional scans.
- Estimated one-off cloud-test/load-test cost, if any: Estimate before execution and record actual bounded test usage.

## Test plan

- Unit: Account classification, retained earnings simplification, equation, as-of/reversal.
- Integration: Journal events/query to projection, rebuild/reconcile, tenant authorization.
- Architecture: Enforce domain ownership, tenant isolation, inward dependencies, and event/idempotency rules.
- Contract: BalanceSheet DTO and account-mapping policy.
- IaC: CDK assertions/synth/diff and affected real-AWS policy/resource tests.
- E2E/manual: Post/reverse representative journals and generate reconciled as-of balance sheets.
- **Cloud verification required?** Yes — deployed projection/event/DynamoDB/report API behavior requires AWS.
- AWS environment/stack(s) required: Accounting and Reporting resources
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic data; document retained configuration.

