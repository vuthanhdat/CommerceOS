# CommerceOS — Business-Domain Baseline

_Baseline reconciled by TASK-0087 on 2026-08-09 and extended for Subscription & Billing by TASK-0091 on 2026-08-10._

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
- [Subscription & Billing](domains/subscription-billing.md)
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
          └──────── trusted tenant authority ────────────────┐
                                                            │
PLATFORM COMMERCIAL                                         │
  Subscription & Billing ── effective entitlements/limits ──┼──────┐
          │                                                 │      │
          └── CommerceOS SaaS charge truth                  │      │
                                                            │      │
MERCHANT COMMERCE                                           ▼      ▼
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

`Subscription & Billing` is one bounded context initially. It owns the merchant's commercial relationship with CommerceOS, including accepted plan/terms, subscription lifecycle, effective entitlements/limits, approved usage-meter truth, and CommerceOS SaaS charge evidence. It is deliberately separate from merchant-order `Payments` and merchant bookkeeping `Accounting`. The internal Plan, Subscription, Entitlement/Usage, and PlatformCharge concepts may split later only when business evolution justifies separate boundaries.

## 4. Context responsibilities

| Bounded context | Owns | Does not own |
|---|---|---|
| Tenant Management | Tenant identity, lifecycle, and one tenant-owned Business Profile | login credentials, staff membership, subscription/billing lifecycle, merchant transactions |
| Merchant Access | invitations, tenant memberships, role assignment(s) under the approved cardinality policy, active/disabled access status | authentication credentials, subscription plan/entitlement truth, products, orders, domain-specific transaction approval |
| Subscription & Billing | CommerceOS plan/version/accepted commercial terms; merchant subscription lifecycle; effective entitlement sets and approved metered usage; CommerceOS SaaS platform charge/provider evidence | TenantStatus, Memberships, Warehouses, merchant Orders, shopper/order Payments, merchant journals, Audit/Reporting truth |
| Catalog | merchant canonical products, SKU policy, category/brand references, publication eligibility, base selling price, media/source associations | stock, negotiated/final order price, external source snapshots, accounting cost |
| Pricing & Promotion | rules that turn a catalog base price into an eligible offer | canonical product, captured order price, journal value |
| Storefront | public experience, tenant-bound transient cart, public projections and checkout intent | products, orders, inventory, payment state |
| Sales & Order Management | accepted checkout, commercial order, immutable order-line snapshots, cancellation intent, refund request and order history | current product, physical stock, provider payment, subscription truth, journal entries |
| Customer/CRM | merchant-owned customer profile and contact preferences | authentication, order history source, receivable ledger balance |
| Inventory | stock by product/location, reservations, movements, adjustments, and receipt/issue/return quantity effects; Warehouse/Location truth | product merchandising, subscription limit policy, Procurement's physical goods-receipt evidence, order lifecycle, monetary ledger |
| Procurement | suppliers, purchase commitments, goods-receipt documents, supplier-invoice business references and operational settlement status | stock balances, journal entries, payment-provider behavior |
| Payments | merchant-order payment obligation, attempts, known/unknown outcome, captured/refunded amounts, mapping to order-payment provider references | CommerceOS SaaS subscription charges, provider-internal state, order agreement, revenue recognition |
| Mock Payment Provider | simulated provider payment intents, operations, refunds, and callback attempts for merchant-order payment learning scenarios | CommerceOS subscription billing, CommerceOS order, internal Payment, accounting policy |
| Accounting | merchant chart of accounts, posting policy, journal entries/lines, reversals, ledger truth | CommerceOS SaaS platform charge truth, operational order, stock, payment, or purchase state |
| Reporting | derived operational, financial, usage, and platform projections | any transactional source of truth or entitlement authority |
| Product Data Ingestion | source policy evidence, acquisition runs, immutable external snapshots, normalization results, import candidates | subscription capability entitlement, canonical merchant product, sell price, publication state |
| Notification | delivery intent and delivery outcome for non-critical notifications | the business outcome that caused a notification |
| Audit | append-oriented evidence of actor, action, target, outcome, and correlation | domain state or business-event history |
| Files/Media | merchant-owned binary asset identity and safe media metadata when such assets are introduced | a Product's decision to associate/order media, external-content rights policy |

No context may claim authority by reading or writing another context's private representation.

## 5. Source-of-truth rules

| Business question | Authoritative context | Other contexts may hold only |
|---|---|---|
| Does this merchant tenant exist and what is its profile/status? | Tenant Management | identity/reference and approved projection |
| May this authenticated subject act for this tenant now? | Merchant Access | trusted decision/result, never a client assertion |
| Which CommerceOS subscription/accepted terms govern this Tenant now? | Subscription & Billing | subscription reference/status projection |
| Which capability or limit is effective for this Tenant now? | Subscription & Billing | trusted entitlement decision/result and provenance |
| What current staff/Warehouse/order/source usage exists? | the context that owns that business truth; approved cumulative metering may be Subscription & Billing-owned when derived from source facts | usage evidence/projection, never foreign aggregate truth |
| What CommerceOS SaaS platform charge outcome is currently known? | Subscription & Billing | billing-history projection/reference |
| What product does the merchant currently offer? | Catalog | public or task-specific snapshot |
| What base price is currently configured? | Catalog | resolved offer or immutable order snapshot |
| What price did the shopper agree to? | Sales | copied immutable line/total snapshot |
| How much stock exists, is held, or was moved? | Inventory | availability projection or movement reference |
| What did the customer order and what is its commercial state? | Sales | order reference and approved projection |
| What merchant-order payment outcome does CommerceOS currently know? | Payments | payment reference and business facts |
| What did the simulated merchant-order provider commit? | Mock Payment Provider | verified provider result/reference |
| What did the merchant order from a supplier? | Procurement | purchase or receipt reference |
| What has been posted to the merchant's books? | Accounting | financial projections or journal reference |
| What did an external source show at a point in time? | Product Data Ingestion | source-snapshot reference and merchant-approved imported values |
| What is a dashboard/KPI value? | Reporting | a derived value with provenance and freshness, never a command authority |

### Subscription, payment, and accounting vocabulary

The following terms are intentionally different:

- **CommerceOS Subscription** — the merchant Tenant's commercial relationship with the CommerceOS platform.
- **Effective EntitlementSet** — capabilities/limits currently produced by accepted subscription terms and approved policy.
- **PlatformCharge** — a CommerceOS SaaS charge/evidence item owed by the merchant to CommerceOS.
- **Payment** — the merchant's shopper/order payment obligation in the existing Payments bounded context.
- **Merchant Journal** — an immutable entry in the merchant's own Accounting books.

A PlatformCharge is not a shopper Payment and does not become a merchant Journal by implication.

### Price and cost vocabulary

The following terms are intentionally different:

- **Catalog base selling price** — the merchant's current starting price for a Product.
- **Resolved offer price** — a price after applicable Pricing rules, when that context is active.
- **Order price snapshot** — the immutable price accepted by Sales for a particular order line.
- **Catalog cost reference** — optional merchant planning/reference data only.
- **Inventory cost basis** — the value assigned to stock according to an approved accounting/valuation policy.
- **Posted accounting amount** — the amount recorded in an immutable journal.
- **CommerceOS SaaS plan/charge price** — commercial platform pricing owned by Subscription & Billing and never a Catalog/Product selling price.

Changing one does not silently rewrite another.

## 6. Cross-cutting business invariants

### Tenant authority

- Every tenant-owned aggregate has one immutable owning TenantId.
- Tenant authority comes from a resolved active Membership or an explicitly modeled platform-administration path, never from request data.
- Knowing another tenant's identifier or entity identifier confers no visibility.
- An authentication credential proves identity; it does not by itself prove an active tenant membership.
- Subscription/entitlement decisions use trusted Tenant context; a client-provided plan, entitlement, limit, or billing claim is never authority.

### Independent eligibility dimensions

Tenant lifecycle, Membership authority, subscription commercial eligibility, billing outcome, and a domain aggregate's own invariant are separate dimensions.

Conceptually an ordinary protected operation may require:

```text
verified identity
    + active Membership/capability
    + TenantStatus permits operation
    + effective subscription entitlement permits operation
    + owning domain invariant accepts operation
```

Failure in one dimension never silently changes another. In particular:

- Tenant suspension does not cancel a Subscription by implication;
- cancellation/delinquency does not disable Memberships or mutate TenantStatus by implication;
- payment-provider uncertainty does not become subscription failure;
- a subscription downgrade never deletes or rewrites another context's business history.

### Subscription and entitlement history

- A subscription always preserves enough accepted commercial-term provenance to explain historical entitlement periods.
- Marketing plan names/prices are not authority outside Subscription & Billing.
- Plan edits or plan changes never retroactively rewrite historical Orders, Membership history, Inventory, source snapshots, merchant journals, or prior EntitlementSets.
- A requested plan change is not an effective plan change.
- A requested cancellation is not proof that the subscription has ended.
- When target hard limits are below authoritative current usage, the safe interim rule is to block downgrade effectivity and require remediation/product policy; Subscription & Billing never destroys foreign-domain data to force compliance.
- A hard-limit write must be checked using a current trusted entitlement plus authoritative owning-domain usage/state; a stale Reporting/UI projection cannot authorize it.
- `Unlimited` must be explicit policy, not a missing entitlement record.

### Identity and history

- Internal aggregate identifiers are immutable and are not reused.
- Mutable merchant labels such as names or SKUs do not replace aggregate identity.
- Historical business documents retain snapshots or stable references required to preserve their original meaning.

### Money and quantity

- A monetary value always carries currency; amounts with different currencies are not added or compared as if equivalent.
- An accepted order uses one currency unless a later approved product decision adds conversion behavior.
- Quantities are explicit positive values for order, reservation, receipt, and issue operations. Adjustment direction is represented by its reason/type, not an ambiguous signed input.
- Rounding, functional currency, tax, inventory valuation, and CommerceOS SaaS currency/tax/proration policy remain explicit human decisions where listed in the decision register.

### Business facts

- Fact names are past tense and state what the owning context accepted.
- `Requested`, `Scheduled`, `Queued`, `Delivered`, or `Failed` is a business fact only when that occurrence has business meaning; a worker/job state alone is technical telemetry.
- A request such as `ReserveStock`, `CapturePayment`, or `RequestPlanChange` is not proof of `StockReserved`, `PaymentCaptured`, or `SubscriptionPlanChanged`.
- Consumers may derive their own state from an owned fact but must not retroactively change the producer's fact.
- The same source fact must not create the same logical inventory movement, merchant-order payment effect, subscription usage count, PlatformCharge effect, or accounting posting twice.
- External SaaS billing timeout/missing callback is an unknown observation when commit status cannot be proven; it is not converted into success/failure/delinquency merely because time passes.

### Projections and public views

- Storefront and Reporting expose projections; neither can authorize a transaction from stale projected data.
- Checkout revalidates current Catalog eligibility and authoritative commercial inputs.
- Inventory availability shown publicly is informative until Inventory accepts a reservation.
- Subscription/billing dashboards expose projections/history; they do not grant entitlements or mutate subscription state.
- Projection lag is represented honestly rather than treated as a change in transactional truth.

## 7. First delivery frontier

Tenant Management, Merchant Access, and Catalog remain specified at implementation-refinement depth in the linked documents.

Important consequences for the current candidate backlog:

- registration must produce a usable tenant with an initial active Owner as one business outcome; TASK-0006 through TASK-0008 currently split that outcome circularly and must be reconciled before any becomes Ready;
- verified authentication and active membership are separate checks;
- a user-supplied TenantId never selects authority;
- Product is the Catalog aggregate root; Category and Brand are independent tenant-owned reference aggregates;
- Catalog owns publication eligibility and base selling price, but not stock, final order price, accounting cost, or SaaS plan pricing;
- public availability requires `Published`, while stock availability remains an Inventory concern;
- exact human decisions that still gate a candidate task are listed by decision ID rather than embedded by a Builder;
- TASK-0091 does **not** silently make Subscription part of the existing Tenant onboarding transaction. Whether registration starts a trial/selects a plan, whether a Tenant may exist without an Active subscription, and when first entitlements become effective are `PD-043` and must be resolved before a subscription-coupled onboarding task can become Ready.

## 8. Medium-depth runway

Sales, Inventory, Payments, Procurement, Accounting, Reporting, Product Data Ingestion, Customer/CRM, Pricing, Notification, Audit, and Files/Media are refined in [Commerce Operations and Cross-Domain Facts](domains/commerce-operations.md).

Subscription & Billing is refined separately in [Subscription & Billing Domain Baseline](domains/subscription-billing.md) because it is a newly explicit platform-commercial capability that cross-cuts multiple tenant-owned domains without taking ownership of them.

The runway is sufficient to establish:

- who owns each source fact;
- which state dimensions must not be conflated;
- which business invariant a future integration must preserve;
- which apparent event names are requests or technical states rather than accepted facts;
- how effective entitlements remain independent of marketing plan-name checks;
- how downgrade preserves existing business data when target limits are lower than current usage;
- where a human product/accounting/subscription policy decision must precede task readiness.

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
| PolicyBlocked | a governing merchant/platform/source/subscription policy prohibits the action |
| EntitlementDenied | the trusted effective subscription terms do not grant the requested capability |
| EntitlementLimitReached | an approved hard limit rejects the requested increase using authoritative current usage/state |
| DowngradeBlockedByUsage | target hard limits are below current authoritative usage and downgrade cannot safely become effective |

Context-specific error codes are documented in the detailed baselines. Technical transport mappings belong to Technical Architecture.

## 10. Decision and handoff rule

The [Human Product Decisions](domains/product-decisions.md) register is part of this baseline. A pending decision does not authorize a default.

- If a pending decision is marked as a blocker for a candidate task, that task cannot pass the Ready gate.
- Technical design must preserve the alternatives until the human decision is recorded.
- Distant decisions may remain deferred when they cannot affect the first implementation frontier.
- Decisions that change this baseline must update the affected domain document and the register; chat-only conclusions are insufficient.
- Subscription & Billing decisions `PD-043` through `PD-053` govern trial/acquisition, plan/version policy, billing cycle/currency/tax/proration, upgrade/downgrade, cancellation/delinquency/retention, limit enforcement, order-volume behavior, provider strategy, and platform-admin override authority.
- TASK-0092 must reconcile module/contracts/persistence/integration/security/reliability architecture against the extended domain baseline without resolving those product decisions through technical convenience.
- TASK-0089 must then reconcile Backlog V2 and keep every `PD-*`-gated implementation task non-Ready until its decision gate is satisfied.
