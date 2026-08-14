# F15 — Later CRM & Pricing/Promotion

## Feature goal
Preserve explicitly documented post-MVP product capabilities without smuggling their semantics into current Sales/Catalog code.

## Source requirements
REQ-CRM-001, REQ-PRI-001.

## Scope
Later explicit Customer profile/contact preferences and a deliberately narrow first Pricing/Promotion slice introduced through separate product/domain design, technical design and implementation gates.

## Out of scope
Automatic guest-to-customer matching, rewriting historical Order snapshots, manual authoritative guest discounts, speculative promotion engine, coupons, stacking/priority rules, customer-specific pricing and broad rule-engine abstractions.

## Pricing first-slice product/domain baseline — resolved

TASK-0241 completed on 2026-08-14 and is authoritative through `PD-054` plus `docs/domains/pricing-promotion.md`.

The approved slice is:

- one scheduled Promotion targets one Tenant-owned Product and all public storefront shoppers;
- accepted schedule terms are immutable and contain an explicit final promotional VND unit price;
- no persisted merchant-editable Draft; changes require cancel + new Promotion;
- periods are finite `[EffectiveFrom, EffectiveUntil)`, no backdating, no overlap for one Tenant + Product;
- Owner/Admin schedule/cancel; Staff/Viewer cannot mutate Pricing;
- Catalog remains base-price and sellability authority;
- Pricing effective unit price while otherwise active is `min(current Catalog base price, scheduled promotional unit price)`;
- promotions therefore never increase shopper price and may become temporarily non-beneficial after a Catalog base-price decrease;
- public promotion presentation exists only while the Promotion actually lowers a sellable Product price;
- browser/cart price, discount, timestamp and total are never authoritative;
- checkout retains PD-011 explicit reconfirmation for any authoritative effective-price change;
- Sales keeps immutable accepted base/effective pricing and applied Promotion provenance;
- Accounting/refund use the accepted effective Sales amount, with no new contra-revenue model;
- no Pricing-specific subscription entitlement or plan-name gate is introduced for this slice.

Coupons, percentage/fixed-reduction formulas, manual order discounts, Category/cart/order-wide promotions, customer/segment pricing, price lists, BOGO/quantity tiers, variants, flash-sale orchestration and stacking/priority remain future product decisions.

## Task sequence

- `TASK-0240` — implement explicit Customer/CRM profiles after core reporting/data boundaries are stable.
- `TASK-0241` — **Done 2026-08-14**. Product/domain design gate closed by PD-054 and `pricing-promotion.md`; no application code was changed.
- `TASK-0242` — **Done 2026-08-14**. Technical contracts, persistence/access patterns, concurrency enforcement, checkout/Sales integration and LocalStack mapping are recorded in `docs/architecture/pricing-first-slice-technical-design.md`.
- `TASK-0243` — **Done 2026-08-14**. The approved scheduled Product promotional-price slice is implemented exactly as specified by PD-054/TASK-0242.

The split is intentional: a Builder must never be asked to invent promotion lifecycle/time/accounting/authorization semantics while implementing Pricing code.

## Definition of Done
Later capabilities have explicit owning-context semantics and integrate through contracts rather than hidden flags in Sales/Catalog. The first Pricing slice is complete; future Pricing capabilities require their own product decisions rather than extensions by implication.
