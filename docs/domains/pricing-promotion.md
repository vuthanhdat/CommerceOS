# CommerceOS — Pricing & Promotion Domain Baseline

_Date: 2026-08-14. Created by TASK-0241 from the Product Owner-approved first-slice direction and the resolved PD-054 policy._

## 1. Purpose and authority

This document defines the business/domain semantics for the first CommerceOS Pricing & Promotion slice. It does not select persistence technology, API wire schemas, LocalStack resources, deployment topology, or synchronization mechanisms.

Authority order remains:

1. `docs/domains/product-decisions.md`, especially `PD-013` and `PD-054`;
2. `docs/02-business-domains.md` and this detailed domain baseline;
3. accepted ADRs and technical architecture for implementation mechanisms;
4. planning/task wording last.

Pricing & Promotion is the authoritative bounded context for effective promotional-price decisions. Catalog remains authoritative for canonical Product, base selling price and Product sellability/publication. Sales remains authoritative for the immutable accepted Order commercial snapshot. Accounting and Refund behavior use the accepted Sales amount and never reconstruct historical price from mutable Catalog/Pricing state.

## 2. First-slice boundary

The first supported capability is deliberately narrow:

- one scheduled promotion targets exactly one Tenant-owned Product;
- the promotion defines one explicit final promotional unit price in VND whole đồng;
- it applies to all shoppers on the public storefront checkout path;
- Owner and Admin may schedule/cancel promotions;
- Staff and Viewer may not mutate Pricing;
- there are no coupons, manual order discounts, percentage/fixed-amount discount formulas, Category/cart/order-wide promotions, customer segments, price lists, variants, Buy-X-Get-Y rules, stacking, priority rules or generic promotion engine behavior;
- no separate subscription entitlement is introduced for this slice: a Tenant otherwise eligible for normal Catalog/Storefront commerce may use this Pricing capability.

A later capability that broadens these semantics requires a new explicit product decision rather than extending this slice by convention.

## 3. Promotion identity and lifecycle

### 3.1 No Draft lifecycle in the first slice

The first slice uses create-and-schedule semantics. An accepted scheduling command creates one immutable Promotion identity and immutable scheduled commercial terms.

There is no persisted merchant-editable Promotion Draft in this slice. If the merchant wants different terms after scheduling, the existing Promotion must be cancelled when cancellation is still meaningful and a new Promotion must be scheduled.

### 3.2 Accepted lifecycle

The persistent business lifecycle is intentionally small:

```text
Schedule accepted
      ↓
Scheduled ───────────────► temporal window ends
    │                         ↓
    └── Cancel ─────────► historical/terminal
```

`Upcoming`, `Active`, and `Expired` are temporal interpretations of an accepted non-cancelled schedule, not merchant commands that must fire exactly at a wall-clock boundary.

For a non-cancelled Promotion:

- `Upcoming`: authoritative current instant is before `EffectiveFrom`;
- `Active`: current instant is inside `[EffectiveFrom, EffectiveUntil)`;
- `Expired`: current instant is at or after `EffectiveUntil`.

Cancellation is terminal for that Promotion. Expired Promotions are historical and cannot be edited, rescheduled, or reactivated.

### 3.3 Cancellation

Owner/Admin may cancel an Upcoming or Active Promotion. Cancellation takes effect at the authoritative cancellation instant and does not alter any previously accepted Order.

A cancelled Promotion's actual eligibility interval is truncated to:

`[EffectiveFrom, min(EffectiveUntil, CancelledAt))`.

This allows a replacement Promotion to begin at the cancellation instant without creating an effective overlap. Cancellation after the scheduled interval has already expired has no new business effect and must not rewrite history.

## 4. Scheduling and time semantics

An accepted schedule must satisfy all of the following:

- target Product belongs to the trusted Tenant;
- Product is currently Published when the schedule is accepted;
- `EffectiveFrom` resolves to the current authoritative instant or a future instant; backdated scheduling is unsupported;
- `EffectiveUntil` is a finite instant strictly after `EffectiveFrom`;
- effective interval is start-inclusive/end-exclusive: `[EffectiveFrom, EffectiveUntil)`;
- adjacent intervals are allowed, for example `[A,B)` followed by `[B,C)`;
- accepted effective intervals for the same Tenant + Product must not overlap after cancellation truncation is considered.

Merchant-facing scheduling input/display uses the Tenant Business Profile IANA timezone. The accepted business terms must resolve to unambiguous instants. A nonexistent or ambiguous local wall-clock time caused by timezone/DST rules is rejected rather than guessed. Technical architecture decides the exact command representation, but client/server infrastructure timezone is never Pricing business policy.

Effective-price evaluation uses a trusted server-side/current authoritative instant. A browser-provided timestamp is never authority for promotion applicability.

## 5. Promotional Money rules

The first slice uses the CommerceOS merchant Money policy:

- VND only;
- whole đồng;
- explicit currency;
- no currency conversion.

At schedule acceptance:

- promotional unit price may be zero;
- promotional unit price must be strictly less than the Product's authoritative current Catalog base price;
- therefore a Product whose current base price is zero cannot accept a promotional price in this slice;
- equal-to-base or above-base promotional price is rejected.

The accepted promotional unit price is an absolute final unit price, not a percentage/fixed reduction formula. It is immutable after scheduling.

## 6. Catalog base-price and sellability interaction

Pricing never makes a Product sellable. A Promotion can affect the commercial price only when Catalog currently says the Product is sellable/public for that Tenant.

A later Catalog change does not mutate or cancel the Promotion record.

During an otherwise Active Promotion, the authoritative effective unit price is conceptually:

`min(current Catalog base unit price, scheduled promotional unit price)`.

Consequences:

- if current Catalog base price remains above the promotional price, the Promotion is applied;
- if Catalog later lowers base price to equal or below the promotional price, Catalog base price wins and the Promotion is temporarily non-beneficial/not applied;
- the Promotion is not auto-cancelled by that base-price change;
- if Catalog base price later rises above the promotional price again before the Promotion ends/cancels, the Promotion may apply again;
- a Promotion never increases the shopper's price;
- if Product becomes Unpublished, public Pricing is ineffective while it remains Unpublished;
- if an Unpublished Product is republished before the Promotion interval ends, it may become promotion-eligible again;
- an Archived Product is terminal under Catalog policy, so an existing Promotion can never make it public/sellable again.

This preserves Catalog's independent mutation authority and avoids requiring Catalog to ask Pricing for permission to change base price.

## 7. Non-overlap and stacking policy

The first slice has no stacking or priority engine.

For one Tenant + Product, at most one accepted Promotion may be effective at any instant. Overlapping accepted schedule intervals are rejected. The rule must be concurrency-safe in implementation; a read-then-write best-effort check is not sufficient.

A Promotion that exists but is temporarily non-beneficial because Catalog base price is lower still occupies its accepted time interval for overlap purposes. Merchants must cancel it before scheduling an overlapping replacement.

## 8. Public storefront presentation

Future scheduled Promotions are not exposed publicly before `EffectiveFrom`.

When a Promotion is currently applied to a sellable Product, the public price projection may expose:

- current Catalog base unit price;
- authoritative effective promotional unit price;
- a generic indication that the price is promotional;
- Promotion end instant for normal explanatory display.

The first slice does not require countdown timers, coupon codes, marketing labels, percentage-saved calculations, or scheduled-promotion previews.

When no Promotion is applied — including before start, after end/cancellation, while Product is not sellable, or while Catalog base price is already less than/equal to the scheduled promotional price — the public projection behaves as normal Catalog base pricing and must not present a misleading strike-through promotion.

## 9. Checkout and shopper consent

Storefront/cart values remain estimates and untrusted input. Final order placement must use current authoritative Catalog sellability plus Pricing evaluation.

The existing `PD-011` rule remains unchanged:

- any difference between the shopper-confirmed estimate and the current authoritative effective price prevents order placement for that attempt;
- checkout returns refreshed authoritative pricing and requires explicit shopper reconfirmation;
- this applies whether the change came from Catalog base-price edits, Promotion start/end/cancellation, or a Promotion becoming beneficial/non-beneficial after a Catalog change;
- no tolerance and no silent lower/higher-price acceptance exists.

A client-provided discount, PromotionId, total, timestamp, or price never grants a commercial benefit.

## 10. Sales snapshot and historical truth

Once Sales accepts an Order, later Catalog/Pricing changes never reprice it.

For each accepted Order line, Sales must conceptually retain enough immutable commercial evidence to stand alone without querying mutable Pricing state, including at least:

- accepted Product identity/snapshot information already required by Sales;
- Catalog base unit price observed by authoritative pricing at acceptance;
- accepted effective unit price;
- applied PromotionId when a Promotion actually lowered the price, otherwise no applied Promotion identity;
- the accepted promotional unit price when a Promotion applied;
- authoritative pricing evaluation instant.

Because first-slice Promotion terms are immutable once scheduled, PromotionId is sufficient as the stable terms identity; a separate merchant-editable Promotion version lifecycle is not required by product semantics. Technical architecture may still use an internal contract/version field for schema evolution, but it must not imply editable historical promotion terms.

Scheduled/cancelled/expired Promotion records are retained non-destructively in the MVP. Historical explanation may reference them, but Sales correctness, refund amount and Accounting posting must remain derivable from Sales-owned accepted evidence.

## 11. Accounting and refund consequences

This slice uses net accepted commercial amounts:

- Payment obligation/order total is based on the accepted effective Sales price;
- revenue recognition uses the effective amount actually accepted on the Order;
- no contra-revenue/discount account is introduced merely because a Promotion existed;
- approved refund amount/corrections use the applicable accepted Order-line effective amount, never current Catalog/Pricing values;
- COGS/inventory valuation remains independent of selling-price promotion policy.

A later requirement for gross-sales-versus-discount accounting presentation requires a new Accounting/product decision.

## 12. Tenant and authorization rules

Pricing mutations require the same trusted Tenant authority principles as other merchant domains:

- client `tenantId` is never authorization by itself;
- active eligible Membership must be resolved server-side;
- Owner/Admin may schedule and cancel;
- Staff/Viewer may not mutate Promotions;
- Tenant/Subscription state must otherwise permit ordinary merchant commerce mutations;
- no plan-name check or new Pricing-specific entitlement is introduced by this slice.

Public evaluation uses resolved `PublicTenantContext`; a Suspended Tenant or otherwise commerce-ineligible Tenant cannot become sellable through Pricing.

## 13. Business invariants summary

1. One Promotion targets one Tenant + Product.
2. Accepted scheduled terms are immutable.
3. No Draft/edit-in-place lifecycle exists in the first slice.
4. Interval is `[from, until)` and finite.
5. Backdated scheduling is unsupported.
6. Accepted intervals for one Tenant + Product never overlap.
7. Promotional price is VND whole đồng, `>= 0`, and `< current base price` at schedule acceptance.
8. Pricing never raises price: effective price is the lower of current Catalog base and active scheduled promotional price.
9. Pricing never overrides Catalog sellability/publication.
10. Only Owner/Admin mutate Pricing.
11. Shopper/client values never become pricing authority.
12. Any current authoritative-price difference requires reconfirmation before Order placement.
13. Sales snapshots accepted base/effective pricing and Promotion provenance immutably.
14. Later Promotion/Catalog changes never reprice historical Orders.
15. Accounting/refunds use accepted effective Sales amounts.

## 14. Explicit later scope

The following remain intentionally unsupported until a new product decision exists:

- percentage or fixed-amount discount formulas;
- coupons/promo codes;
- manual merchant-entered order discounts;
- promotion stacking and priority;
- Category/Brand/cart/order-wide promotions;
- customer-specific/segment pricing;
- quantity tiers/Buy-X-Get-Y;
- price lists/channel pricing;
- product-variant promotions;
- flash-sale orchestration/countdowns;
- promotion-specific subscription packaging;
- gross-discount/contra-revenue accounting presentation.

**Stop condition: FIRST PRICING/PROMOTION DOMAIN SLICE FULLY SPECIFIED.**
