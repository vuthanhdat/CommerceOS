# First scheduled Product promotion — technical design

This document implements TASK-0242's technical design and preserves PD-054 exactly.

## Boundary and contracts

`Pricing` owns immutable accepted Promotion terms, cancellation evidence and the effective-price decision. It has its own Domain, Application, Contracts and Infrastructure projects. Pricing Application may depend only on Pricing Domain and producer-owned Catalog/Tenancy contracts; it never reads Catalog or Sales persistence.

Merchant commands receive trusted Merchant Access context: `ScheduleProductPromotion(tenant, productId, promotionalUnitPriceVnd, effectiveFrom, effectiveUntil, correlationId)` and `CancelProductPromotion(tenant, promotionId, correlationId)`. Only resolved Owner/Admin capability may invoke them. The schedule command uses a Catalog-owned eligibility query returning current Published/sellable status and VND base price; no client price, role or tenant selector is authority.

The producer-owned query is `GetEffectivePrice(tenantId, productId, evaluatedAt)` and returns `baseUnitPriceVnd`, `effectiveUnitPriceVnd`, optional `promotionId`, optional applied promotional price, and evaluation instant. It returns no generic discount primitive. Storefront receives this query through an explicit Pricing Contracts project.

## Persistence and concurrency

Pricing uses one LocalStack DynamoDB table, with `PK=TENANT#t` and `SK=PROMOTION#promotionId` for immutable terms/history. One bounded `PRODUCT#productId#SCHEDULE` aggregate item contains the active/future interval index and revision. Schedule reads this one owner-local item strongly, validates every retained interval in memory, then transactionally writes the immutable Promotion plus updated index under a `Revision` condition. This makes arbitrary overlap checks concurrency-safe; adjacency is valid. A first-slice bounded schedule count prevents the aggregate transaction from exceeding DynamoDB limits. Cancel uses the same expected-revision index transaction and writes terminal cancellation evidence; accepted terms are never updated or deleted.

The application rejects malformed, ambiguous or nonexistent tenant-local wall times before persistence. Delivery/API converts tenant IANA local input to unambiguous UTC instants; Pricing Domain receives only instants and enforces finite `[from, until)` plus non-backdating against an injected authoritative clock. A query evaluates `from <= now < min(until, cancelledAt)` and applies `min(currentCatalogBasePrice, acceptedPromotionalPrice)` only when Catalog still says sellable and the promotion strictly lowers current base price.

## Checkout, Sales and public projection

Storefront asks Catalog for sellability/base identity and Pricing for the authoritative effective price in the same validation attempt. Any changed effective price follows existing `CHECKOUT_RECONFIRMATION_REQUIRED`; an unavailable/invalid Pricing decision fails closed and does not accept a client discount. Sales snapshots add accepted base unit price, effective unit price, optional applied Promotion ID, optional accepted promotion unit price, and pricing evaluation instant for every accepted line. Historical orders, Accounting and refunds use that immutable Sales snapshot only.

Public product display includes base/effective price, generic promotion indication and end instant only when the query has an actually-applied beneficial Promotion. Upcoming, cancelled, expired and non-beneficial schedules are not public promotion displays.

## Operations and verification

Every command/query carries correlation; decision logs retain only tenant hash, product/promotion IDs and outcome. Metrics cover overlap conflict, catalogue-ineligible rejection, stale cancel, query failure and repricing required. CDK provisions `commerceos-<profile>-<instance>-pricing`; no new entitlement or deployment unit is introduced. Focused tests cover role/tenant denial, DST rejection, overlap race, adjacency, cancel/time boundary, base-price decrease non-benefit, availability failure, checkout reconfirmation and immutable Sales provenance. LocalStack verifies conditional transaction behavior; AWS equivalence is not claimed.

## TASK-0243 implementation scope

TASK-0243 shall add the Pricing module/table/Contracts, Catalog eligibility adapter, Storefront decision adapter, Sales snapshot fields and migration-safe persistence mapping; it shall add unit, conditional-DynamoDB, Storefront reprice and cross-tenant tests. No coupon, percentage, stacking, segment or price-list behavior is permitted.
