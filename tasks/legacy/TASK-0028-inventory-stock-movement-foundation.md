# TASK-0028 — Establish single-warehouse inventory and movement history

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 6
Milestone: Milestone A
Depends on: TASK-0009, TASK-0011

## Goal

A tenant has one active warehouse with auditable stock items and movements that always satisfy Available = OnHand - Reserved.

## Business context

Inventory owns physical stock truth; Catalog may identify products but cannot store or mutate quantities.

## In scope

- introduce Inventory Domain/Application/Infrastructure with Warehouse, StockItem, StockMovement, and source-reference models;
- create the initial single active warehouse per tenant while keeping identities compatible with later multi-warehouse support;
- persist/query stock balances and append-only movement history with documented access patterns;

## Out of scope

- stock receipts/adjustments, reservations, fulfillment, valuation, or multiple active warehouses;
- Catalog-owned price/product mutation and Accounting journals;

## Acceptance criteria

### AC01 — Warehouse stock foundation

Given a tenant and canonical product exist
when its stock item is initialized in the active warehouse
then OnHand, Reserved, and Available are consistent and movement history is queryable.

### AC02 — Invariant enforcement

Given a write would make quantities inconsistent or mutate history
when the command runs
then it is rejected and no partial balance/movement write occurs.

### AC03 — Domain ownership

Given Catalog or another module needs availability
when the integration is reviewed
then it uses an Inventory query/projection contract and never reads/writes Inventory persistence directly.

### AC04 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and selected cloud verification plus teardown evidence is recorded.

## Architecture impact

- Owning domain: Inventory
- Domains touched: Inventory, Catalog identity contract, API
- Persistence impact: Add tenant/warehouse/product StockItem balances and append-only StockMovement records with source/idempotency keys.
- Events/contracts impact: Inventory facts remain local until reliable publication in TASK-0048.
- AWS/IaC impact: DynamoDB Inventory access patterns and API/Lambda routes in CommerceStack.
- ADR required? No — accepted domain/serverless rules cover this task.

## Security and tenant impact

- Authentication: Use established merchant/Storefront identity boundaries.
- Authorization: Inventory views require warehouse/view permissions; initialization is an authorized application operation.
- Tenant scoping: All tenant-owned commands/queries use trusted tenant context; client tenant ids are rejected/ignored and cross-tenant access is tested.
- Sensitive data/secrets: No secrets or real payment/card data are stored or logged.
- Abuse/rate-limit considerations: Validate and bound quantities, payloads, concurrency, and externally callable operations.

## Reliability and idempotency impact

- Retry behavior: Retries use explicit command/idempotency semantics.
- Timeout semantics: No external ambiguous timeout unless stated.
- Duplicate-delivery behavior: N/A unless a retryable command is stated.
- Idempotency key/strategy: Stock initialization and movement append use conditional uniqueness/source keys.
- DLQ/recovery/reconciliation: N/A — no queue/event consumer.

## Observability impact

- Logs: Structured logs include safe tenant, entity, command/event, and correlation identifiers.
- Metrics: Bounded metrics cover conflicts, failures, and latency for the changed capability.
- Traces/correlation: Preserve correlation across all application/external boundaries.
- Operational states/errors: Missing warehouse/product, invariant violation, version conflict, and duplicate source are explicit.

## Cost impact

- Request/compute impact: Usage scales with bounded business requests.
- Storage impact: Add tenant/warehouse/product StockItem balances and append-only StockMovement records with source/idempotency keys.
- Network impact: Normal API traffic only.
- New AWS resources/services: DynamoDB Inventory access patterns and API/Lambda routes in CommerceStack.
- Free Tier allowance relevant to this task: Use documented Lambda, DynamoDB, API Gateway, SQS, and CloudWatch limits where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at the learning profile.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview tests.

## Test plan

- Unit: Quantity value objects, Available invariant, movement immutability, and warehouse rules.
- Integration: DynamoDB atomic balance/movement persistence, access patterns, and tenant isolation.
- Architecture: Enforce domain ownership, inward dependencies, and no cross-domain persistence access.
- Contract: Inventory balance and movement query/application contracts.
- IaC: CDK assertions, synth, and reviewed diff.
- E2E/manual: Initialize stock and inspect a zero-balance movement-safe record for one tenant.
- **Cloud verification required?** Yes — DynamoDB atomic/access behavior, API integration, and IAM require real-AWS verification.
- AWS environment/stack(s) required: Inventory resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic data.

