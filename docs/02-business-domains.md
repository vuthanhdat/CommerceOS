# CommerceOS — Business-Domain Baseline

_Baseline reconciled by TASK-0087 on 2026-08-09._

## 1. Purpose and authority

CommerceOS is organized around business capabilities first. This document is the canonical map of bounded contexts, fact ownership, and cross-domain invariants. It refines the directional examples in the product, feature, roadmap, and candidate-task documents.

When documents disagree about business ownership or meaning:

1. an explicit human product decision takes precedence;
2. this baseline and its linked domain documents take precedence over candidate-task assumptions;
3. a Technical Architect may choose implementation mechanisms but may not change the business meaning;
4. a Builder must stop rather than fill a documented product-decision gap.

This baseline deliberately does not select services, persistence schemas or keys, deployment boundaries, message transports, or synchronous versus asynchronous integration.

Detailed baseline documents:

- [Tenant & Merchant Access](domains/tenant-identity.md)
- [Catalog](domains/catalog.md)
- [Commerce Operations and Cross-Domain Facts](domains/commerce-operations.md)
- [Human Product Decisions](domains/product-decisions.md)

## 2. Modeling vocabulary

- A **bounded context** owns a business language, its rules, and the authoritative facts in that language.
- An **aggregate root** is the consistency boundary through which a business change is accepted or rejected. It is not a table or deployment unit.
- An **entity** has durable identity inside a bounded context.
- A **value object** is defined by its value, such as Money, SKU, Quantity, or Address.
- A **business fact** is a past-tense statement that the owning context has accepted. The names in this baseline are semantic candidates, not published event schemas.
- A **request** asks another context to attempt work. It is not evidence that the work succeeded.
- A **projection** is derived for reading. It is never allowed to become an accidental transactional source of truth.
- An **audit record** describes actor and security/operational context. It is not a substitute for a business fact.

## 3. Bounded-context map

```text
PLATFORM GOVERNANCE
  Tenant Management ── Merchant Access ── Audit
          │                    │
          └──────── trusted tenant authority ────────┐
                                                     │
MERCHANT COMMERCE                                    ▼
  Product Data Ingestion ──review/import──► Catalog ──► Storefront
                                                │            │
                                      Pricing ──┘            │ checkout intent
                                                             ▼
  Customer/CRM ───────────────────────────────────────────► Sales
                                                             │
OPERATIONS                                                   ├────► Payments ───► Mock Payment Provider
  Procurement ──goods receipt──► Inventory ◄──allocation────┘
        │                            │
        └──────── business facts ────┴──────────────┐
                                                    ▼
                                              Accounting
                                                    │
                                                    ▼
                                                Reporting

Supporting contexts: Notification and Files/Media.
```

The arrows show knowledge or business dependency, not a call style, service boundary, queue, transaction, or data-store relationship.

## 4. Context responsibilities

| Bounded context | Owns | Does not own |
|---|---|---|
| Tenant Management | Tenant identity, lifecycle, and one tenant-owned Business Profile | login credentials, staff membership, merchant transactions |
| Merchant Access | invitations, tenant memberships, role assignment(s) under the approved cardinality policy, active/disabled access status | authentication credentials, products, orders, domain-specific transaction approval |
| Catalog | merchant canonical products, SKU policy, category/brand references, publication eligibility, base selling price, media/source associations | stock, negotiated/final order price, external source snapshots, accounting cost |
| Pricing & Promotion | rules that turn a catalog base price into an eligible offer | canonical product, captured order price, journal value |
| Storefront | public experience, tenant-bound transient cart, public projections and checkout intent | products, orders, inventory, payment state |
| Sales & Order Management | accepted checkout, commercial order, immutable order-line snapshots, cancellation intent, refund request and order history | current product, physical stock, provider payment, journal entries |
| Customer/CRM | merchant-owned customer profile and contact preferences | authentication, order history source, receivable ledger balance |
| Inventory | stock by product/location, reservations, movements, adjustments, and receipt/issue/return quantity effects | product merchandising, Procurement's physical goods-receipt evidence, order lifecycle, monetary ledger |
| Procurement | suppliers, purchase commitments, goods-receipt documents, supplier-invoice business references and operational settlement status | stock balances, journal entries, payment-provider behavior |
| Payments | CommerceOS's payment obligation, attempts, known/unknown outcome, captured/refunded amounts, mapping to provider references | provider-internal state, order agreement, revenue recognition |
| Mock Payment Provider | simulated provider payment intents, operations, refunds, and callback attempts | CommerceOS order, internal Payment, accounting policy |
| Accounting | chart of accounts, posting policy, journal entries/lines, reversals, ledger truth | operational order, stock, payment, or purchase state |
| Reporting | derived operational and financial projections | any transactional source of truth |
| Product Data Ingestion | source policy evidence, acquisition runs, immutable external snapshots, normalization results, import candidates | canonical merchant product, sell price, publication state |
| Notification | delivery intent and delivery outcome for non-critical notifications | the business outcome that caused a notification |
| Audit | append-oriented evidence of actor, action, target, outcome, and correlation | domain state or business-event history |
| Files/Media | merchant-owned binary asset identity and safe media metadata when such assets are introduced | a Product's decision to associate/order media, external-content rights policy |

No context may claim authority by reading or writing another context's private representation.

## 5. Source-of-truth rules

| Business question | Authoritative context | Other contexts may hold only |
|---|---|---|
| Does this merchant tenant exist and what is its profile? | Tenant Management | identity/reference and approved projection |
| May this authenticated subject act for this tenant now? | Merchant Access | trusted decision/result, never a client assertion |
| What product does the merchant currently offer? | Catalog | public or task-specific snapshot |
| What base price is currently configured? | Catalog | resolved offer or immutable order snapshot |
| What price did the shopper agree to? | Sales | copied immutable line/total snapshot |
| How much stock exists, is held, or was moved? | Inventory | availability projection or movement reference |
| What did the customer order and what is its commercial state? | Sales | order reference and approved projection |
| What payment outcome does CommerceOS currently know? | Payments | payment reference and business facts |
| What did the simulated provider commit? | Mock Payment Provider | verified provider result/reference |
| What did the merchant order from a supplier? | Procurement | purchase or receipt reference |
| What has been posted to the books? | Accounting | financial projections or journal reference |
| What did an external source show at a point in time? | Product Data Ingestion | source-snapshot reference and merchant-approved imported values |
| What is a dashboard/KPI value? | Reporting | a derived value with provenance and freshness, never a command authority |

### Price and cost vocabulary

The following terms are intentionally different:

- **Catalog base selling price** — the merchant's current starting price for a Product.
- **Resolved offer price** — a price after applicable Pricing rules, when that context is active.
- **Order price snapshot** — the immutable price accepted by Sales for a particular order line.
- **Catalog cost reference** — optional merchant planning/reference data only.
- **Inventory cost basis** — the value assigned to stock according to an approved accounting/valuation policy.
- **Posted accounting amount** — the amount recorded in an immutable journal.

Changing one does not silently rewrite another.

## 6. Cross-cutting business invariants

### Tenant authority

- Every tenant-owned aggregate has one immutable owning TenantId.
- Tenant authority comes from a resolved active Membership or an explicitly modeled platform-administration path, never from request data.
- Knowing another tenant's identifier or entity identifier confers no visibility.
- An authentication credential proves identity; it does not by itself prove an active tenant membership.

### Identity and history

- Internal aggregate identifiers are immutable and are not reused.
- Mutable merchant labels such as names or SKUs do not replace aggregate identity.
- Historical business documents retain snapshots or stable references required to preserve their original meaning.

### Money and quantity

- A monetary value always carries currency; amounts with different currencies are not added or compared as if equivalent.
- An accepted order uses one currency unless a later approved product decision adds conversion behavior.
- Quantities are explicit positive values for order, reservation, receipt, and issue operations. Adjustment direction is represented by its reason/type, not an ambiguous signed input.
- Rounding, functional currency, tax, and inventory valuation policy remain explicit human decisions where listed in the decision register.

### Business facts

- Fact names are past tense and state what the owning context accepted.
- `Requested`, `Scheduled`, `Queued`, `Delivered`, or `Failed` is a business fact only when that occurrence has business meaning; a worker/job state alone is technical telemetry.
- A request such as `ReserveStock` or `CapturePayment` is not proof of `StockReserved` or `PaymentCaptured`.
- Consumers may derive their own state from an owned fact but must not retroactively change the producer's fact.
- The same source fact must not create the same logical inventory movement, payment effect, or accounting posting twice.

### Projections and public views

- Storefront and Reporting expose projections; neither can authorize a transaction from stale projected data.
- Checkout revalidates current Catalog eligibility and authoritative commercial inputs.
- Inventory availability shown publicly is informative until Inventory accepts a reservation.
- Projection lag is represented honestly rather than treated as a change in transactional truth.

## 7. First delivery frontier

Tenant Management, Merchant Access, and Catalog are specified at implementation-refinement depth in the linked documents.

Important consequences for the current candidate backlog:

- registration must produce a usable tenant with an initial active Owner as one business outcome; TASK-0006 through TASK-0008 currently split that outcome circularly and must be reconciled before any becomes Ready;
- verified authentication and active membership are separate checks;
- a user-supplied TenantId never selects authority;
- Product is the Catalog aggregate root; Category and Brand are independent tenant-owned reference aggregates;
- Catalog owns publication eligibility and base selling price, but not stock, final order price, or accounting cost;
- public availability requires `Published`, while stock availability remains an Inventory concern;
- exact human decisions that still gate a candidate task are listed by decision ID rather than embedded by a Builder.

## 8. Medium-depth runway

Sales, Inventory, Payments, Procurement, Accounting, Reporting, Product Data Ingestion, Customer/CRM, Pricing, Notification, Audit, and Files/Media are refined in [Commerce Operations and Cross-Domain Facts](domains/commerce-operations.md).

That runway is sufficient to establish:

- who owns each source fact;
- which state dimensions must not be conflated;
- which business invariant a future integration must preserve;
- which apparent event names are requests or technical states rather than accepted facts;
- where a human product/accounting decision must precede task readiness.

It is not intended to finalize detailed behavior for distant unscheduled features.

## 9. Business error semantics

Domain outcomes are stable business meanings, not HTTP status codes or exception classes.

| Outcome family | Meaning |
|---|---|
| ValidationRejected | supplied business values are invalid; no change is accepted |
| NotFoundOrNotVisible | the aggregate is absent or not visible in the trusted tenant context; cross-tenant existence is not disclosed |
| NotAuthorized | the actor lacks the required business permission in the trusted context |
| Conflict | a uniqueness or mutually exclusive business claim already exists |
| StaleRevision | the attempted change was based on an older aggregate revision |
| InvalidStateTransition | the requested action is not allowed from the aggregate's current state |
| AlreadyApplied | the same logical intent was already accepted and its prior result is returned or referenced |
| OutcomeUnknown | the caller cannot yet know whether an external/independent boundary committed; it is not converted into failure |
| PolicyBlocked | a governing merchant/platform/source policy prohibits the action |

Context-specific error codes are documented in the detailed baselines. Technical transport mappings belong to TASK-0088.

## 10. Decision and handoff rule

The [Human Product Decisions](domains/product-decisions.md) register is part of this baseline. A pending decision does not authorize a default.

- If a pending decision is marked as a blocker for a candidate task, that task cannot pass the Ready gate.
- Technical design must preserve the alternatives until the human decision is recorded.
- Distant decisions may remain deferred when they cannot affect the first implementation frontier.
- Decisions that change this baseline must update the affected domain document and the register; chat-only conclusions are insufficient.
