# TASK-0043 — Track supplier invoices, payments, and procurement operations

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 10
Milestone: Milestone C
Depends on: TASK-0041, TASK-0042

## Goal

Merchant staff can record supplier invoice references and payment status, close eligible purchase orders, and operate the complete initial procurement lifecycle without mutating Accounting directly.

## Business context

Procurement must represent liabilities/payment facts as operational truth that Accounting can later consume explicitly.

## In scope

- implement RecordSupplierInvoice, MarkSupplierPaid, and ClosePurchaseOrder with valid initial lifecycle and source references;
- deliver back-office invoice/payment/status/history views and role controls;
- define SupplierInvoiceRecorded and SupplierPaid business facts containing stable posting inputs without financial-table coupling;

## Out of scope

- actual bank/payment integration, partial supplier payment, statutory invoice/tax behavior, or journal creation;
- blindly marking unpaid/ineligible POs closed;

## Acceptance criteria

### AC01 — Invoice and payment lifecycle

Given a received PO is eligible
when invoice reference then supplier payment are recorded
then status/history progress through Invoiced/Paid with immutable references.

### AC02 — Close eligibility

Given a paid PO with required receipt/invoice data is closed or an ineligible PO is targeted
when ClosePurchaseOrder runs
then eligible order closes once and invalid state is rejected.

### AC03 — Accounting boundary

Given supplier invoice/payment facts are inspected
when downstream accounting is planned
then Procurement exposes explicit event/contract data and never reads/writes journal tables.

### AC04 — Tenant/role protection

Given unauthorized or cross-tenant actor records payment
when the command runs
then it is denied and privileged attempts/outcomes are auditable.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Procurement
- Domains touched: Procurement, Back Office, Accounting event contract, Audit
- Persistence impact: Extend PO state/history with immutable supplier invoice/payment references and idempotency keys.
- Events/contracts impact: Versioned SupplierInvoiceRecorded, SupplierPaid, PurchaseOrderClosed facts ready for TASK-0048.
- AWS/IaC impact: Existing Procurement API/Lambda/DynamoDB resources.
- ADR required? No — existing accepted decisions cover the task.

## Security and tenant impact

- Authentication: Use established merchant/internal/provider identities.
- Authorization: Invoice/payment/close permissions are explicit and high-impact payment-status changes are audited.
- Tenant scoping: Trusted tenant context scopes every merchant record and message; client tenant ids cannot override it.
- Sensitive data/secrets: Supplier invoice references/contact metadata are minimized and redacted; no bank credentials.
- Abuse/rate-limit considerations: Bound reference/text sizes and repeated status-change attempts.

## Reliability and idempotency impact

- Retry behavior: Retry behavior is deterministic and unsafe duplicate writes are protected.
- Timeout semantics: No ambiguous external timeout unless stated.
- Duplicate-delivery behavior: N/A — no at-least-once consumer.
- Idempotency key/strategy: Tenant + PO + invoice/payment operation key and aggregate version.
- DLQ/recovery/reconciliation: N/A unless explicitly in scope.

## Observability impact

- Logs: Structured logs carry safe tenant/entity/operation/event and correlation identifiers.
- Metrics: Measure outcomes, failures, retries, duplicates, latency, and stuck states at bounded cardinality.
- Traces/correlation: Preserve correlation/causation across all changed boundaries.
- Operational states/errors: Missing receipt/invoice, invalid transition, duplicate payment, and stale state are visible.

## Cost impact

- Request/compute impact: Scales with bounded business activity and retry policy.
- Storage impact: Extend PO state/history with immutable supplier invoice/payment references and idempotency keys.
- Network impact: Only bounded API/event traffic.
- New AWS resources/services: Existing Procurement API/Lambda/DynamoDB resources.
- Free Tier allowance relevant to this task: Use accepted serverless allowances and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; measure workflows/retries where relevant.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for targeted failure/cloud tests.

## Test plan

- Unit: Invoice/payment/close transition rules, totals/reference validation, and events.
- Integration: DynamoDB conditional transitions, authorization/audit, and event serialization.
- Architecture: Check domain boundaries, tenant context, and no persistence shortcuts.
- Contract: Procurement APIs and SupplierInvoiceRecorded/SupplierPaid event v1 payloads.
- IaC: CDK assertions, synth, diff, and selected deployment checks.
- E2E/manual: Complete PO from draft through closed and view history; deny invalid/duplicate transition.
- **Cloud verification required?** Yes — DynamoDB conditional state, API/IAM, and event contract wiring require selected AWS verification.
- AWS environment/stack(s) required: Procurement/Audit resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral stacks and synthetic data after evidence collection.

