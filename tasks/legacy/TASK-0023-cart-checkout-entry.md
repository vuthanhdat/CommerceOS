# TASK-0023 — Deliver customer cart behavior and checkout entry

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 5
Milestone: Milestone A
Depends on: TASK-0021

## Goal

A shopper can add, remove, and change product quantities in a tenant-bound cart and submit a validated checkout request without the Storefront owning order state.

## Business context

Cart is customer interaction state; checkout is the explicit boundary into Sales and must preserve tenant/product identity safely.

## In scope

- implement tenant-bound cart state with add/remove/update/clear behavior and persistence appropriate to guest browsing;
- show price estimates from public catalog while clearly treating final checkout price as server-owned;
- define checkout request validation, idempotency-key generation, loading/error recovery, and frontend tests;

## Out of scope

- creating the SalesOrder, reserving stock, or invoking payment;
- cross-tenant carts, server-side long-lived carts, coupons, shipping, or tax;

## Acceptance criteria

### AC01 — Cart behavior

Given a shopper browses one tenant storefront
when items are added, updated, or removed
then the cart totals/quantities update deterministically and never mix another tenant's products.

### AC02 — Server authority

Given browser cart prices or product details are modified
when checkout is submitted
then the request contains identities/quantities only as untrusted input and cannot dictate final price or tenant.

### AC03 — Safe repeat submission

Given a shopper retries after a network/UI interruption
when the same checkout intent is resubmitted
then the stable idempotency key is reused until a definitive response or explicit new attempt.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then all repository checks pass and cloud verification is explicitly N/A unless AWS semantics changed.

## Architecture impact

- Owning domain: Storefront
- Domains touched: Storefront UI and future Sales checkout contract
- Persistence impact: Browser-local guest cart only with tenant binding and versioned schema; no business source of truth.
- Events/contracts impact: Checkout request contract only; CartCheckedOut is emitted later by Sales after acceptance.
- AWS/IaC impact: No new resource; existing public/catalog and future Sales APIs.
- ADR required? No — follows accepted architecture; create one if a significant new decision emerges.

## Security and tenant impact

- Authentication: Use the established merchant identity or explicit anonymous storefront boundary.
- Authorization: Anonymous cart data is untrusted; protected/private catalog fields are never stored in it.
- Tenant scoping: Cart is cleared or isolated when tenant route changes; server resolves tenant from the storefront boundary.
- Sensitive data/secrets: Do not persist unnecessary customer contact data in cart storage.
- Abuse/rate-limit considerations: Quantity, line count, payload size, and rapid submission are bounded.

## Reliability and idempotency impact

- Retry behavior: Frontend reuses the same idempotency key for the same checkout intent.
- Timeout semantics: No external ambiguity is introduced unless stated.
- Duplicate-delivery behavior: N/A — no at-least-once consumer introduced.
- Idempotency key/strategy: Client key is caller-generated but server validation/semantics are defined by TASK-0025.
- DLQ/recovery/reconciliation: N/A — no asynchronous boundary introduced.

## Observability impact

- Logs: Structured logs contain operation, safe tenant/entity identifiers, and correlation id.
- Metrics: Use built-in metrics and bounded business metrics for meaningful risk.
- Traces/correlation: Preserve correlation across every API/application boundary changed here.
- Operational states/errors: UI distinguishes validation, unavailable product, price change, and indeterminate network result.

## Cost impact

- Request/compute impact: Scales with bounded user traffic.
- Storage impact: Browser-local guest cart only with tenant binding and versioned schema; no business source of truth.
- Network impact: Normal web/API payloads; avoid unbounded responses.
- New AWS resources/services: No new resource; existing public/catalog and future Sales APIs.
- Free Tier allowance relevant to this task: Use existing pay-per-use services and documented learning-profile limits.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume.
- Estimated one-off cloud-test/load-test cost, if any: None expected.

## Test plan

- Unit: Cart reducer/state migration, tenant switching, totals display, limits, and key lifecycle.
- Integration: Frontend checkout adapter contract with tampered cart values and retries.
- Architecture: Verify domain ownership, inward dependencies, and no cross-domain persistence shortcuts.
- Contract: CheckoutRequest v1 consumer contract.
- IaC: N/A unless infrastructure changes.
- E2E/manual: Build a cart, refresh, change quantity, and submit/retry the same checkout intent.
- **Cloud verification required?** No — cart/UI behavior is local; server-side checkout is TASK-0025.
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

