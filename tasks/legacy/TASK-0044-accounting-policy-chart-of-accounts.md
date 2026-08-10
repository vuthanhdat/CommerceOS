# TASK-0044 — Define accounting policy and chart of accounts

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 11
Milestone: Milestone C
Depends on: TASK-0009
Execution gate: Produces an ADR or policy record before automatic posting rules are implemented.

## Goal

CommerceOS has an accepted initial accounting policy and tenant chart of accounts that define consistent recognition timing and explicitly avoid statutory-compliance claims.

## Business context

Automatic postings cannot be correct until debit/credit accounts, recognition timing, currency/rounding, and source facts are decided once.

## In scope

- write an ADR/policy for sale revenue/cash-or-receivable, fulfillment COGS/inventory, procurement receipt/invoice/payable, supplier payment, refund, and selected adjustment timing;
- define an initial tenant chart-of-accounts template, account types/codes/status, single-currency VND assumptions, rounding, and control-account rules;
- implement idempotent chart initialization and authorized account list/manage behavior within the approved policy;

## Out of scope

- statutory Vietnamese tax/e-invoice/payroll/depreciation/certification claims;
- journal posting engine, automatic event consumers, P&L/balance sheet, multi-currency, or period close;

## Acceptance criteria

### AC01 — Policy accepted

Given initial operational event scenarios are reviewed
when the accounting ADR/policy is accepted
then every planned posting has a defined recognition trigger, accounts, amounts/source fields, reversal approach, and stated simplifications.

### AC02 — Tenant chart initialized

Given a tenant enables Accounting
when initialization runs repeatedly
then one valid tenant-scoped chart is created with required control accounts and no duplicates.

### AC03 — No false compliance

Given accounting docs/UI/API are reviewed
when scope is presented
then the module is clearly described as internal learning bookkeeping, not certified tax/accounting software.

### AC04 — Account controls

Given a required control account is deleted/deactivated or another tenant targets it
when the command runs
then policy-violating/cross-tenant change is rejected and privileged changes are audited.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected AWS evidence/teardown is recorded.

## Architecture impact

- Owning domain: Accounting / Architecture
- Domains touched: Accounting, Tenant, Sales, Inventory, Procurement, Payment policy inputs
- Persistence impact: Add tenant Account records and chart-template/version metadata; no Journal records yet.
- Events/contracts impact: Policy defines required facts/versions for later posting but does not publish/consume them.
- AWS/IaC impact: Accounting DynamoDB/API resources only if chart implementation is included; no new service.
- ADR required? Yes — accounting integrity/posting policy is a material required decision.

## Security and tenant impact

- Authentication: Use established merchant/internal worker identity.
- Authorization: Account management is limited to accountant/owner permissions and audited for control accounts.
- Tenant scoping: Every record, command, event, and projection is scoped by trusted tenant context; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Validate values/payloads and bound queries, retries, batches, and privileged actions.

## Reliability and idempotency impact

- Retry behavior: Synchronous retry behavior is explicit and protected by command/version keys.
- Timeout semantics: N/A unless an external/cloud boundary is invoked.
- Duplicate-delivery behavior: N/A — no async consumer.
- Idempotency key/strategy: Tenant + chart template version uniquely identifies initialization.
- DLQ/recovery/reconciliation: N/A unless stated.

## Observability impact

- Logs: Structured logs carry safe tenant, source, entity, event/command, and correlation/causation identifiers.
- Metrics: Measure validation rejects, duplicates, failures, latency, backlog/stuck state, and recovery results.
- Traces/correlation: Preserve correlation/causation end-to-end.
- Operational states/errors: Missing/control-account misconfiguration and template version are diagnosable before posting begins.

## Cost impact

- Request/compute impact: Scales with bounded transactional/event/report activity.
- Storage impact: Add tenant Account records and chart-template/version metadata; no Journal records yet.
- Network impact: Bounded internal API/event traffic only.
- New AWS resources/services: Accounting DynamoDB/API resources only if chart implementation is included; no new service.
- Free Tier allowance relevant to this task: Use documented serverless allowances, short logs, small batches, and disabled/low schedules in non-prod.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measured event/workflow/storage impact is material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused preview/dev verification.

## Test plan

- Unit: Account/code/type/control invariants, template initialization, and rounding policy examples.
- Integration: Tenant chart persistence, idempotent initialization, authorization, and cross-tenant denial.
- Architecture: Enforce domain ownership, inward dependencies, event conventions, and tenant rules.
- Contract: Chart-of-accounts API plus documented posting-input requirements.
- IaC: CDK assertions, synth, diff, and affected resource policy checks.
- E2E/manual: Initialize a chart twice, inspect it, and reject an unsafe control-account change.
- **Cloud verification required?** Yes — if chart persistence/API/IAM are implemented, selected DynamoDB and protected API verification is required.
- AWS environment/stack(s) required: Accounting resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic records after evidence.

