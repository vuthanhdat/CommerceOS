# TASK-0025 — Create orders idempotently with price and discount snapshots

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 5
Milestone: Milestone A
Depends on: TASK-0023, TASK-0024

## Goal

A repeated checkout intent creates at most one correctly priced SalesOrder whose product status, prices, quantities, and authorized manual discount are revalidated server-side.

## Business context

Checkout crosses anonymous Storefront and transactional Sales. Browser values are untrusted, and repeated requests must not duplicate orders.

## In scope

- implement CheckoutCart application flow that resolves public tenant/catalog facts and captures an immutable price snapshot;
- validate product publication, quantities, currency, manual discount permission/limits, and calculate subtotal/discount/total server-side;
- persist idempotency request hash/result with bounded retention and return the same logical order for valid retries;

## Out of scope

- inventory reservation, payment request, shipping/tax calculation, coupons, or workflow orchestration;
- automatic promotion rules beyond an authorized manual discount;

## Acceptance criteria

### AC01 — Authoritative checkout

Given a shopper submits valid published products and quantities
when checkout succeeds
then Sales stores one order with server-resolved prices, calculations, tenant, and line snapshots.

### AC02 — Idempotent replay

Given the same tenant/key and equivalent request is sent repeatedly
when checkout processes the attempts
then exactly one logical order exists and the stored result is returned.

### AC03 — Conflict and tampering

Given the same key has a different request or the client tampers with price/tenant/discount
when checkout runs
then the request is rejected or server values prevail without creating an extra order.

### AC04 — Publication validation

Given a product becomes unpublished or unavailable before checkout
when the request is evaluated
then the invalid line is rejected and no partial order is committed.

### AC05 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then all repository checks pass and real-AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Sales & Order Management
- Domains touched: Storefront, Sales, Catalog application query, Authorization
- Persistence impact: Add checkout idempotency records with request hash/result/TTL and atomic link to SalesOrder.
- Events/contracts impact: CartCheckedOut/OrderPlaced are domain facts; external publication waits for TASK-0048.
- AWS/IaC impact: Sales API/Lambda and DynamoDB transactional/conditional operations.
- ADR required? No — follows accepted architecture; create one if a significant new decision emerges.

## Security and tenant impact

- Authentication: Use the established merchant identity or explicit anonymous storefront boundary.
- Authorization: Anonymous checkout is bound to resolved storefront tenant; manual discount requires authenticated permission or is absent for guest checkout.
- Tenant scoping: Tenant is resolved from storefront context and all catalog/order operations use it; body tenantId is ignored/rejected.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Cap line count/quantity/payload, throttle anonymous checkout, and retain idempotency records for a bounded period.

## Reliability and idempotency impact

- Retry behavior: Equivalent request/key replay is safe; permanent validation/conflict is not retried blindly.
- Timeout semantics: Client timeout after commit is resolved by replaying the same key/querying the checkout result.
- Duplicate-delivery behavior: Repeated HTTP requests cannot create duplicate orders.
- Idempotency key/strategy: Resolved tenant + Idempotency-Key with canonical request hash and atomic result record.
- DLQ/recovery/reconciliation: N/A — no asynchronous boundary introduced.

## Observability impact

- Logs: Structured logs contain operation, safe tenant/entity identifiers, and correlation id.
- Metrics: Use built-in metrics and bounded business metrics for meaningful risk.
- Traces/correlation: Preserve correlation across every API/application boundary changed here.
- Operational states/errors: Price/product change, invalid discount, key conflict, and post-commit replay have explicit states/codes.

## Cost impact

- Request/compute impact: One bounded Catalog resolution and DynamoDB transaction per unique checkout.
- Storage impact: Add checkout idempotency records with request hash/result/TTL and atomic link to SalesOrder.
- Network impact: Normal web/API payloads; avoid unbounded responses.
- New AWS resources/services: Sales API/Lambda and DynamoDB transactional/conditional operations.
- Free Tier allowance relevant to this task: Use existing pay-per-use services and documented learning-profile limits.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded preview/dev checks.

## Test plan

- Unit: Money arithmetic, snapshot construction, publication/discount rules, canonical request hash.
- Integration: DynamoDB transaction/idempotency races, tampered inputs, and cross-tenant checkout.
- Architecture: Verify domain ownership, inward dependencies, and no cross-domain persistence shortcuts.
- Contract: CheckoutRequest/CheckoutResult v1 and Catalog resolution query.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Submit a cart twice with the same key and prove one correctly priced order.
- **Cloud verification required?** Yes — DynamoDB transactional/idempotency behavior and public API wiring require AWS verification.
- AWS environment/stack(s) required: Catalog/Sales endpoints and tables in CommerceStack
- Preview/staging teardown plan: Destroy preview resources; document retained dev state.

