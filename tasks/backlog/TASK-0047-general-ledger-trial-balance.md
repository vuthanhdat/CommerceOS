# TASK-0047 — Deliver general ledger and trial balance

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 11
Milestone: Milestone C
Depends on: TASK-0045

## Goal

Authorized merchant accountants can query a tenant general ledger and as-of trial balance derived from posted immutable journals, with debits equal credits.

## Business context

The first useful accounting output must prove the ledger can be inspected without scanning or mutating operational-domain tables.

## In scope

- implement efficient posted-line queries by account/date/source and a paginated General Ledger API/UI;
- calculate tenant as-of Trial Balance with debit/credit movement and ending balances according to account type policy;
- provide drill-through from report line to journal/source reference and expose projection/query freshness;

## Out of scope

- P&L, balance sheet, AR/AP, tax statements, period close, exports, or direct reads from Sales/Inventory;
- including draft/rejected journals in financial balances;

## Acceptance criteria

### AC01 — General ledger

Given posted and draft journals exist
when an accountant queries account/date range
then only posted lines appear in deterministic order with opening/movement/ending context.

### AC02 — Trial balance

Given posted journals exist through an as-of date
when trial balance is generated
then total debit equals total credit and each balance traces to ledger/journals.

### AC03 — Tenant and authorization

Given unauthorized/cross-tenant user queries or drills through
when the request runs
then access is denied without leaking financial data.

### AC04 — Performance shape

Given a bounded learning dataset is queried
when ledger/trial APIs run
then documented indexes/projection avoid repeated full-table scans and paginate predictably.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected AWS evidence/teardown is recorded.

## Architecture impact

- Owning domain: Accounting
- Domains touched: Accounting, Back Office
- Persistence impact: Use Accounting-owned journal indexes or read projection; no operational-domain persistence access.
- Events/contracts impact: No new event; reads derive only from posted Accounting truth.
- AWS/IaC impact: DynamoDB queries/projection and Accounting API/Lambda routes.
- ADR required? No — accepted architecture covers this scope.

## Security and tenant impact

- Authentication: Use established merchant/internal worker identity.
- Authorization: Financial reports require accounting/view permission; source drill-through respects originating domain authorization.
- Tenant scoping: Every record, command, event, and projection is scoped by trusted tenant context; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Bound date range/page size and report frequency; avoid unbounded scans/exports.

## Reliability and idempotency impact

- Retry behavior: Synchronous retry behavior is explicit and protected by command/version keys.
- Timeout semantics: N/A unless an external/cloud boundary is invoked.
- Duplicate-delivery behavior: N/A — no async consumer.
- Idempotency key/strategy: Stable command/source/event identifiers protect every repeatable effect.
- DLQ/recovery/reconciliation: N/A unless stated.

## Observability impact

- Logs: Structured logs carry safe tenant, source, entity, event/command, and correlation/causation identifiers.
- Metrics: Measure validation rejects, duplicates, failures, latency, backlog/stuck state, and recovery results.
- Traces/correlation: Preserve correlation/causation end-to-end.
- Operational states/errors: Report as-of/freshness, pagination, missing index/projection failure, and source-link errors are visible.

## Cost impact

- Request/compute impact: Scales with bounded transactional/event/report activity.
- Storage impact: Use Accounting-owned journal indexes or read projection; no operational-domain persistence access.
- Network impact: Bounded internal API/event traffic only.
- New AWS resources/services: DynamoDB queries/projection and Accounting API/Lambda routes.
- Free Tier allowance relevant to this task: Use documented serverless allowances, short logs, small batches, and disabled/low schedules in non-prod.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measured event/workflow/storage impact is material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused preview/dev verification.

## Test plan

- Unit: Account balance sign rules, opening/movement/ending totals, and trial-balance equality.
- Integration: DynamoDB/index queries, pagination, as-of boundaries, tenant isolation, and journal drill-through.
- Architecture: Enforce domain ownership, inward dependencies, event conventions, and tenant rules.
- Contract: General Ledger and Trial Balance API DTOs.
- IaC: CDK assertions, synth, diff, and affected resource policy checks.
- E2E/manual: Post journals, view ledger/trial balance, verify totals and source link.
- **Cloud verification required?** Yes — DynamoDB index/access behavior and protected report API require AWS verification.
- AWS environment/stack(s) required: Accounting resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic records after evidence.

