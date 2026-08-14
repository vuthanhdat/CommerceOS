# F06 — Storefront, Cart & Checkout

## Feature goal
Expose eligible Tenant Products publicly and turn an untrusted cart into one authoritative idempotent guest Order intent.

## Source requirements
REQ-STO-001..002, REQ-CHK-001..002, REQ-SAL-001.

## Blocking design gap
OQ-001 resolved by PD-052: public routing uses the Tenant-owned globally unique `/{storefrontSlug}` binding; old values are permanently retired and custom domains/redirects remain out of scope.

## Scope
PublicTenantContext; current Tenant status check; product list/detail/filter/search; transient cart; checkout revalidation; strict repricing/reconfirmation; guest contact snapshot; idempotent Order placement handoff.

## Out of scope
Custom domains unless OQ-001 explicitly approves them; shopper accounts; manual checkout discounts; backorder/partial allocation.

## Task sequence
TASK-0150 -> TASK-0151 -> TASK-0152 -> TASK-0153 -> TASK-0154.

## Definition of Done
Suspended Tenant never serves commerce; public routes cannot confer merchant authority; stale price creates reconfirmation rather than an Order; replay cannot create duplicate logical Order.
