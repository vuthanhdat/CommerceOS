# TASK-0055 — Deliver financial projections and basic P&L

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 13
Milestone: Milestone C
Depends on: TASK-0047, TASK-0050, TASK-0051, TASK-0053

## Goal

Merchants can view traceable inventory value, gross-profit, cash, receivable, payable, and basic P&L projections based on accepted accounting policy and posted financial facts.

## Business context

Financial dashboards are projections for learning/operations, not statutory statements; formulas must reconcile to ledger/source evidence.

## In scope

- define formulas/as-of/freshness and implement inventory value, gross profit, cash, AR, AP, and basic P&L read models;
- consume posted journal/accounting events or Accounting query contracts rather than operational tables;
- provide report APIs/UI drill-through and reconciliation checks against General Ledger/Trial Balance;

## Out of scope

- certified/statutory reports, tax, balance sheet, multi-currency, budgeting/forecasting, or period close;
- inventing missing costs/receivables/payables from incomplete source data;

## Acceptance criteria

### AC01 — Financial projections

Given required balanced posted journals exist
when financial reports are queried
then cash/AR/AP/inventory/revenue/COGS/gross profit/P&L values follow TASK-0044 policy and display as-of/freshness.

### AC02 — Ledger reconciliation

Given report balances are compared to ledger/trial balance for the same scope/date
when reconciliation runs
then differences are zero or explicit actionable projection exceptions.

### AC03 — Correction handling

Given a journal reversal/corrected posting is projected
when events are processed
then reports update once without mutating historical source journals.

### AC04 — Scope disclosure

Given users view financial reports
when labels/help are inspected
then the output is clearly a basic internal projection, not certified compliance.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence plus cleanup is recorded.

## Architecture impact

- Owning domain: Reporting & Analytics / Accounting query contracts
- Domains touched: Reporting, Accounting, Inventory valuation inputs, Back Office
- Persistence impact: Add FinancialSummary/PnL projection records; Accounting remains financial source of truth.
- Events/contracts impact: Consume JournalPosted/JournalReversed or approved Accounting projection contract with versioning.
- AWS/IaC impact: Existing Reporting queue/worker/DynamoDB/API resources.
- ADR required? No — accepted architecture covers this task.

## Security and tenant impact

- Authentication: Use established merchant/internal identities.
- Authorization: Financial reports require accounting/owner permission; drill-through respects Accounting authorization.
- Tenant scoping: Trusted tenant context scopes all projections, rules, notifications, and queries; cross-tenant reads/writes are denied.
- Sensitive data/secrets: Store/log only safe aggregate or minimum notification data; no secrets/card data.
- Abuse/rate-limit considerations: Bound periods/grouping and rebuild/reconciliation batch sizes.

## Reliability and idempotency impact

- Retry behavior: Projection/reconciliation retries are bounded and source journal idempotency is preserved.
- Timeout semantics: Timeout leaves projection/notification lag visible and recoverable.
- Duplicate-delivery behavior: Journal/event id and reversal linkage prevent duplicate financial effect.
- Idempotency key/strategy: Tenant + report period/account dimension + event/journal version.
- DLQ/recovery/reconciliation: Projection mismatch creates a visible exception and rebuild path; source journal is never edited.

## Observability impact

- Logs: Structured logs include safe tenant, projection/rule/notification, event and correlation data.
- Metrics: Measure lag, failures, duplicates, rebuild/recovery, query latency, and outcome counts.
- Traces/correlation: Preserve event correlation/causation into projections and notifications.
- Operational states/errors: As-of, lag, source journal ids, reconciliation status, and mismatch reason are visible.

## Cost impact

- Request/compute impact: Bounded event consumption and paginated dashboard/rule traffic.
- Storage impact: Add FinancialSummary/PnL projection records; Accounting remains financial source of truth.
- Network impact: Small API/event payloads only.
- New AWS resources/services: Existing Reporting queue/worker/DynamoDB/API resources.
- Free Tier allowance relevant to this task: Prefer existing EventBridge/SQS/Lambda/DynamoDB/CloudWatch allowances and low-volume schedules.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; projections avoid repeated transactional scans.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: All formulas/sign conventions, reversal behavior, period boundaries, and reconciliation.
- Integration: Accounting events/query to Reporting projections with duplicates/rebuild.
- Architecture: Enforce read-model ownership, domain boundaries, event/idempotency and tenant rules.
- Contract: Journal projection input and financial report DTOs.
- IaC: CDK assertions, synth, diff, routing/queue/policy checks.
- E2E/manual: Post/reverse journals and reconcile dashboard/P&L to ledger/trial balance.
- **Cloud verification required?** Yes — event/read-model deployment and DynamoDB/report API behavior require AWS evidence.
- AWS environment/stack(s) required: Accounting and Reporting resources
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic projection data.

