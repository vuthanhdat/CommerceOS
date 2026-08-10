# TASK-0041 — Manage suppliers and purchase orders

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 10
Milestone: Milestone C
Depends on: TASK-0009, TASK-0011

## Goal

Merchant purchasing staff can manage tenant-owned suppliers and create, edit, submit, and view purchase orders through a valid lifecycle.

## Business context

Procurement is a second major business flow and must own supplier commitments independently of Catalog, Inventory, and Accounting.

## In scope

- introduce Procurement Domain/Application/Infrastructure with Supplier, PurchaseOrder, lines, totals, status, and history;
- implement tenant-scoped supplier management and Draft-to-Submitted PO commands/queries with immutable submitted commercial snapshots;
- deliver back-office supplier/PO list, create, edit, submit, and detail views;

## Out of scope

- goods receipt, supplier invoice/payment, accounting, partial receipts, or automatic replenishment;
- direct writes to Catalog/Inventory persistence;

## Acceptance criteria

### AC01 — Supplier and PO management

Given authorized purchasing staff create a supplier and valid draft PO
when the PO is edited and submitted
then one tenant-owned submitted order preserves supplier/item/quantity/cost snapshots.

### AC02 — Lifecycle and immutability

Given a submitted PO is edited inconsistently or skips states
when the command runs
then invalid transition/data mutation is rejected and history remains auditable.

### AC03 — Tenant isolation

Given another tenant knows supplier/PO ids
when queries or commands run
then no data or existence is disclosed.

### AC04 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Procurement
- Domains touched: Procurement, Catalog product-reference query, Back Office, Authorization
- Persistence impact: Add Supplier/PO/line/status-history items and tenant/status/supplier access patterns with optimistic concurrency.
- Events/contracts impact: PurchaseOrderCreated/Submitted domain facts; external publication waits for TASK-0048.
- AWS/IaC impact: DynamoDB and Procurement API/Lambda routes in CommerceStack.
- ADR required? No — existing accepted decisions cover the task.

## Security and tenant impact

- Authentication: Use established merchant/internal/provider identities.
- Authorization: Purchasing permissions are explicit; supplier details are tenant-private.
- Tenant scoping: Trusted tenant context scopes every merchant record and message; client tenant ids cannot override it.
- Sensitive data/secrets: Store only necessary supplier contact/reference data and redact it from logs.
- Abuse/rate-limit considerations: Bound line counts, quantities, amounts, pagination, and text inputs.

## Reliability and idempotency impact

- Retry behavior: Retry behavior is deterministic and unsafe duplicate writes are protected.
- Timeout semantics: No ambiguous external timeout unless stated.
- Duplicate-delivery behavior: N/A — no at-least-once consumer.
- Idempotency key/strategy: Create/submit use command ids and PO version/conditional transitions.
- DLQ/recovery/reconciliation: N/A unless explicitly in scope.

## Observability impact

- Logs: Structured logs carry safe tenant/entity/operation/event and correlation identifiers.
- Metrics: Measure outcomes, failures, retries, duplicates, latency, and stuck states at bounded cardinality.
- Traces/correlation: Preserve correlation/causation across all changed boundaries.
- Operational states/errors: Invalid product/supplier, stale version, duplicate submission, and total validation have stable errors.

## Cost impact

- Request/compute impact: Scales with bounded business activity and retry policy.
- Storage impact: Add Supplier/PO/line/status-history items and tenant/status/supplier access patterns with optimistic concurrency.
- Network impact: Only bounded API/event traffic.
- New AWS resources/services: DynamoDB and Procurement API/Lambda routes in CommerceStack.
- Free Tier allowance relevant to this task: Use accepted serverless allowances and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; measure workflows/retries where relevant.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for targeted failure/cloud tests.

## Test plan

- Unit: PO totals/snapshots, lifecycle, supplier validation, and immutable submission.
- Integration: DynamoDB access/concurrency, tenant isolation, Catalog query contract, API auth.
- Architecture: Check domain boundaries, tenant context, and no persistence shortcuts.
- Contract: Supplier and PO HTTP/application contracts.
- IaC: CDK assertions, synth, diff, and selected deployment checks.
- E2E/manual: Create supplier, draft/edit/submit PO, and inspect history.
- **Cloud verification required?** Yes — DynamoDB access/concurrency and deployed API/IAM require AWS evidence.
- AWS environment/stack(s) required: Procurement resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral stacks and synthetic data after evidence collection.

