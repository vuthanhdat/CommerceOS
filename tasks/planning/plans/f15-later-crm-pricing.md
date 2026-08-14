# F15 — Later CRM & Pricing/Promotion

## Feature goal
Preserve explicitly documented post-MVP product capabilities without smuggling their semantics into current Sales/Catalog code.

## Source requirements
REQ-CRM-001, REQ-PRI-001.

## Scope
Later explicit Customer profile/contact preferences and a deliberately narrow first Pricing/Promotion slice introduced through separate product/domain design, technical design and implementation gates.

## Out of scope
Automatic guest-to-customer matching, rewriting historical Order snapshots, manual authoritative guest discounts in current MVP, speculative promotion engine, coupons, stacking/priority rules, customer-specific pricing and broad rule-engine abstractions.

## Pricing first-slice direction
The Product Owner approved the starting direction on 2026-08-14:

- first slice is a scheduled Product promotional price;
- the promotion supplies an explicit final promotional unit price;
- one promotion targets one Product and applies to all storefront shoppers;
- no stacking or overlapping accepted promotion periods for one Product;
- Owner/Admin manage promotions; Staff/Viewer do not;
- Catalog keeps base price and sellability authority;
- Pricing owns authoritative effective promotional price;
- Sales keeps immutable accepted pricing provenance;
- browser/cart discount/total is never authoritative;
- accepted effective Order amount remains the Accounting/refund commercial amount for this slice.

Remaining lifecycle/time/base-price/public-display/history semantics are resolved by TASK-0241 before technical design.

## Task sequence

- `TASK-0240` — implement explicit Customer/CRM profiles after core reporting/data boundaries are stable.
- `TASK-0241` — **product/domain design only**. Resolve and record the first Pricing/Promotion slice semantics. No application code. This task no longer depends on the architecture audit because product policy must not be decided by technical topology.
- `TASK-0242` — **technical design only**. After TASK-0241 and TASK-0234, define Pricing contracts, persistence/access patterns, concurrency enforcement, checkout/Sales integration, LocalStack mapping and any required ADR. No application code.
- `TASK-0243` — **implementation only**. Implement the approved scheduled Product promotional-price slice exactly as specified by TASK-0241/TASK-0242.

The split is intentional: a Builder must never be asked to invent promotion lifecycle/stacking/time/accounting semantics while implementing Pricing code.

## Definition of Done
Later capabilities have explicit owning-context semantics and integrate through contracts rather than hidden flags in Sales/Catalog. The first Pricing slice is considered implementation-ready only when TASK-0241 has closed product/domain ambiguity and TASK-0242 has closed material technical-design ambiguity.
