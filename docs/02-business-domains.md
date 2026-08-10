# CommerceOS — Business-Domain Baseline

_Canonical business-domain baseline. Originally reconciled by TASK-0087, extended for Subscription & Billing by TASK-0091, and reconciled again on 2026-08-10 after the human product-decision pass, including resolved `PD-004`, `PD-023`, and `PD-044`._

## 1. Purpose and authority

CommerceOS is organized around business capabilities first. This document is the canonical map of bounded contexts, source-of-truth ownership, and cross-domain invariants.

Authority order for business meaning:

1. explicit human product decisions in [`domains/product-decisions.md`](domains/product-decisions.md);
2. this canonical baseline and its detailed domain documents;
3. technical architecture for implementation mechanisms only;
4. candidate task wording last.

A Technical Architect may choose modules, contracts, persistence/access mechanisms, sync/async integration, AWS mapping, and deployment topology, but may not change the business meaning recorded here. A Builder must stop instead of inventing product behavior.

Detailed domain baselines:

- [Tenant Management & Merchant Access](domains/tenant-identity.md)
- [Catalog](domains/catalog.md)
- [Commerce Operations & Cross-Domain Facts](domains/commerce-operations.md)
- [Subscription & Billing](domains/subscription-billing.md)
- [Human Product Decisions](domains/product-decisions.md)
- [Product-Decision → Domain-Baseline Reconciliation](domains/product-decision-reconciliation.md)

This baseline deliberately does **not** select AWS services, databases, table/key/index design, transports, API schemas, deployment units, or orchestration technology.

## 2. Current product-decision state

The 2026-08-10 decision pass and follow-up resolutions have now resolved the full current `PD-001`–`PD-053` register for the approved MVP scope. The register contains no entry currently marked `HUMAN PRODUCT DECISION REQUIRED` or `Deferred`.

Important MVP exclusions are still explicit product boundaries rather than unresolved gates:

- Tenant closure, hard deletion, timed retention, and privacy/legal erasure are **not supported in MVP** under resolved `PD-004`; a future capability requires a new explicit privacy/product decision.
- Non-restock refund behavior is outside the approved `PD-023` MVP refund model and requires a future explicit decision if introduced.
- Enterprise/custom pricing is outside MVP under resolved `PD-044`.

Builders must not infer future semantics merely because the current register has no unresolved item.

## 3. Modeling vocabulary

- **Bounded context** — owns one business language, rules, and authoritative facts.
- **Aggregate root** — business consistency boundary through which a change is accepted/rejected; not a table or service.
- **Entity** — durable identity in a bounded context.
- **Value object** — defined by value, such as Money, SKU, Quantity, Address, or Entitlement value.
- **Business fact** — past-tense statement accepted by its owning context.
- **Request/intent** — asks another context to attempt work; never proof of success.
- **Projection** — derived read model; never accidental transaction/authorization authority.
- **External/provider evidence** — input interpreted by the owning CommerceOS context; never direct authority for unrelated domains.
- **Audit evidence** — append-oriented actor/action/outcome evidence; not domain state.

## 4. Bounded-context map

```text
PLATFORM GOVERNANCE
  Tenant Management ── Merchant Access ── Audit
          │                    │
          └──── trusted Tenant authority ──────────────────┐
                                                          │
PLATFORM COMMERCIAL                                       │
  Subscription & Billing ── effective entitlements ───────┼─────────┐
          │                                               │         │
          └── PlatformCharge / SaaS billing truth         │         │
                                                          │         │
MERCHANT COMMERCE                                         ▼         ▼
  Product Data Ingestion ──review/apply──► Catalog ─────► Storefront
                                                │             │
                                      Pricing ──┘             │ checkout intent
                                                              ▼
  Customer/CRM ───────────────────────────────────────────► Sales
                                                              │
OPERATIONS                                                    ├────► Payments ───► merchant-order Mock Provider
  Procurement ──goods receipt──► Inventory ◄──reservation────┘
        │                            │
        └──────── owned facts ───────┴──────────────┐
                                                   ▼
                                             Accounting
                                                   │
                                                   ▼
                                               Reporting

Supporting contexts: Notification and Files/Media.
```

Arrows show business knowledge/dependency only. They do not prescribe synchronous calls, queues, transactions, databases, modules, or deployment topology.

## 5. Context responsibilities

| Bounded context | Owns | Explicitly does not own |
|---|---|---|
| Tenant Management | Tenant identity, `TenantStatus`, one Business Profile, platform suspension/reactivation policy | authentication, Memberships, Subscription lifecycle, merchant transactions |
| Merchant Access | Invitations, Membership identity/status, one MVP role per Membership, trusted Tenant authority | authentication credentials, Subscription entitlements, Products/Orders/stock |
| Subscription & Billing | Trial/paid Subscription, Plan/PlanVersion accepted terms, EntitlementSets, approved UsageMeters, PlatformCharge/SaaS billing interpretation | TenantStatus, Memberships, Warehouses/stock, merchant Orders/Payments/Journals |
| Catalog | canonical Product, SKU, base selling price, lifecycle/public eligibility, slug, Category/Brand, specifications, Product-media/source associations | stock, final Order price, accounting inventory value, SaaS plan pricing |
| Pricing & Promotion | future authoritative offer/discount rules | canonical Product, immutable Order snapshot, journals |
| Storefront | public tenant experience, transient tenant-bound cart, public projections, checkout intent | canonical Product, Order, stock, Payment |
| Sales & Order Management | accepted Order, immutable commercial snapshots, Sales lifecycle, cancellation/refund request and refund approval decision | current Product, physical stock, provider Payment state, journal truth |
| Customer/CRM | explicit tenant-owned Customer profile/contact preferences | guest Order snapshot, authentication, Sales history source, ledger balance |
| Inventory | Warehouse/Location, OnHand/Reserved/Available, reservations, movements, physical adjustments/receipts/issues/returns | Product merchandising, Subscription policy, accounting valuation |
| Procurement | Supplier, PurchaseOrder commitment, GoodsReceipt, SupplierInvoice/SupplierPayment evidence | Inventory balances, journals, bank/payment-provider execution |
| Payments | merchant-order Payment obligation, attempts, verified/unknown outcome, captures/refunds | CommerceOS SaaS charges, Order agreement, revenue policy |
| Merchant-order Mock Payment Provider | simulated shopper-order provider intents/operations/evidence | SaaS billing, SalesOrder, Inventory, Accounting |
| Accounting | merchant chart of accounts, valuation/posting/refund-correction policy, immutable journals/ledger | operational Order/stock/Payment/Procurement state, SaaS charge truth |
| Reporting | derived operational/financial/usage/platform projections | transactional truth, entitlement authority |
| Product Data Ingestion | DataSource policy/run/snapshot/candidate truth | canonical Product, subscription capability, sell price/publication |
| Notification | delivery/read/acknowledgement state per recipient | source business outcome |
| Audit | append-oriented privileged/security action evidence | domain state/business-event history |
| Files/Media | merchant-uploaded asset identity/safe media metadata | Product's association/publication decision |

No bounded context may gain authority by directly reading/writing another context's private representation as an integration shortcut.

## 6. Trusted Tenant authority and roles

### Multi-tenant Membership (`PD-001`)

One authenticated identity may hold Memberships in multiple Tenants.

- exactly one eligible Tenant: CommerceOS may select it automatically;
- more than one eligible Tenant: the person intentionally selects active Tenant;
- trusted Tenant context is resolved server-side from selected Tenant + eligible Membership;
- client-supplied `tenantId` or `SubjectId` alone never grants authority.

### MVP role model (`PD-003`)

A Membership has exactly one role at a time:

- `Owner` — full merchant administration, subject to domain invariants;
- `Admin` — ordinary merchant operations, staff, Catalog; cannot grant/revoke Owner authority or violate last-owner invariant;
- `Staff` — ordinary operational work only where the owning domain explicitly permits it; no Membership/tenant administration;
- `Viewer` — read-only.

An Active Tenant may have multiple Owners but must retain at least one Active Owner.

### Independent eligibility dimensions

A normal protected mutation may conceptually require:

```text
verified identity
+ trusted selected Tenant
+ Active Membership + sufficient role/domain authority
+ TenantStatus allows operation
+ effective Subscription entitlement allows operation
+ owning aggregate invariant accepts operation
```

Failure in one dimension never silently rewrites another.

## 7. Tenant lifecycle and onboarding

### Tenant lifecycle (`PD-004`)

MVP Tenant lifecycle is intentionally two-state:

```text
Active ──platform Suspend(reason)──► Suspended
   ▲                                     │
   └────platform Reactivate(reason)──────┘
```

Approved rules:

- suspension/reactivation are authorized platform-administration actions, not merchant self-service;
- reason + Audit evidence are required;
- Suspended disables public storefront/checkout and all ordinary merchant mutations;
- authenticated Memberships remain intact;
- Owner/Admin retain controlled read-only history, operational, billing/support/recovery and otherwise-authorized Audit visibility;
- Staff/Viewer retain only normal-role read visibility and no operational mutation;
- platform support may use an explicit privileged read-only investigation path without becoming a Tenant Membership;
- suspension does not delete or rewrite Memberships, Subscription, Orders, Accounting, or other business evidence;
- reactivation only restores Tenant eligibility and never rewrites another lifecycle;
- MVP has no Tenant closure, hard deletion, automatic retention expiry, or privacy/legal erasure;
- Suspended data is retained indefinitely until a future explicit privacy/retention policy supersedes this rule.

### MVP onboarding (`PD-034`, `PD-043`, `PD-044`)

Open self-service registration requires an authenticated identity with verified email plus Business Profile minimum:

- merchant display name;
- explicit IANA business timezone.

The registering identity becomes initial Active Owner. Retry of the same logical onboarding intent is idempotent.

Successful merchant onboarding requires three owned outcomes:

```text
Tenant Management:   Active Tenant
Merchant Access:      Active initial Owner Membership
Subscription/Billing: 30-day no-card Trial + Trial EntitlementSet
```

Approved Trial terms enable all core CommerceOS capabilities with `MaxActiveMemberships=3`, `MaxWarehouses=1`, scheduled product ingestion enabled, and order-volume warning threshold 500. Trial does not auto-convert to Starter at expiry.

The business must not knowingly report complete onboarding while leaving one required accepted outcome absent. The three facts remain owned by their separate contexts; technical consistency/recovery is not decided here.

## 8. Shared Money, quantity, time, and history rules

### Money (`PD-002`)

- merchant-commerce MVP is VND-only;
- Money always carries explicit currency;
- VND uses whole đồng;
- no currency conversion exists in MVP.

Subscription SaaS charges are also VND whole đồng under `PD-046`, but remain separate commercial truth.

### Quantity (`PD-012`)

MVP order/reservation/receipt/issue/return quantities are positive whole units only. No fractional quantity/unit conversion exists.

### Tenant business date (`PD-031`, `PD-039`)

- Tenant Business Profile IANA timezone defines operational business day;
- operational corrections are attributed to occurrence business date and reference original facts;
- Accounting financial reports use Journal `EffectiveDate`;
- source occurrence, Journal EffectiveDate, and PostingTimestamp remain distinct.

### Historical truth

- aggregate identities are immutable/non-reused;
- mutable labels/slug/names never replace canonical identity;
- accepted Order/PO/receipt/PlanVersion/Entitlement/journal history is not rewritten later;
- corrections are explicit new facts, not destructive edits.

## 9. Catalog baseline consequences

Approved Catalog MVP policy includes:

- SKU optional at Draft creation, required before first publication;
- normalized SKU Tenant-unique case-insensitively;
- SKU immutable after first publication and never reusable after Product Archive;
- publish requires Name + SKU + valid Money; zero price allowed;
- Category, Brand, description, and media optional for publication;
- stock is not a publication prerequisite;
- editing Published updates current canonical/public projection directly;
- Published may Archive directly; Archived is terminal/no restore/republish;
- ProductId canonical identity plus Tenant-scoped mutable public slug; no historical redirects required;
- Product has zero/one flat Category and zero/one Brand; references may be retired non-destructively;
- public media uses merchant uploads managed by CommerceOS only; no arbitrary external copy/hotlink;
- public Product may expose approved specifications/SKU/Category/Brand/media while excluding advisory cost/raw ingestion/internal history/private metadata;
- one external source-product identity maps to at most one canonical Product per Tenant; ImportCandidate has explicit Ready/Approved/Applied/Rejected/Superseded lifecycle.

Owner/Admin manage/publish Catalog; there is no separate MVP “catalog manager” role.

## 10. Commerce operations baseline

### Canonical checkout/order sequence (`PD-011`–`PD-018`, `PD-041`, `PD-042`)

```text
authoritative Catalog/Pricing validation
        ↓
price changed? ──yes──► shopper reconfirmation required; no Order placed
        │ no
        ▼
OrderPlaced
        ↓
all-line Inventory reservation
        ↓
full immediate Payment capture attempt
      ┌─┴──────────────────────────────┐
      │                                │
definitive no-commit              OutcomeUnknown
attempt may retry             stock remains held
      │                      reconciliation required
      └──────────────► verified PaymentCaptured
                              ↓
                       OrderConfirmed
                              ↓
                       OrderAllocated
                              ↓
                  whole-order StockIssued/fulfillment
                              ↓
                       OrderFulfilled
                              ↓
                    Completed when no exception remains
```

MVP rules:

- no manual authoritative guest-checkout discount;
- all-or-nothing allocation/fulfillment;
- no partial allocation, split shipment, or backorder;
- one Payment obligation per Order with multiple immutable attempts;
- definitive decline/no-commit terminates an attempt only;
- `OutcomeUnknown` blocks a new capture attempt and time alone never releases held stock;
- Inventory enforces `OnHand >= 0`, `Available >= 0`, `Available = OnHand - Reserved`;
- downward adjustment cannot consume already Reserved stock;
- Owner/Admin/Staff may cancel before Fulfilled; cancellation does not itself prove refund/release.

### Refund approval and recovery (`PD-023`)

Refund is not immediate when requested.

```text
RefundRequested
      ↓ dedicated merchant approval experience
RefundApproved
      ├────► Inventory StockReturned
      ├────► Accounting revenue compensating journal
      ├────► Accounting COGS reversal after StockReturned
      └────► Payments refund operation
                    ↓
          verified PaymentRefunded
                    ↓
          Accounting Cash settlement
```

`RefundRejected` produces none of those effects.

Approved semantics:

- `RefundRequested` has no stock/accounting/payment effect by itself;
- `RefundApproved` means returned goods are accepted as restockable in MVP and authorizes exactly-once `StockReturned` for approved quantity;
- approval creates linked compensating Accounting effects without editing historical journals;
- Payments still requires verified provider evidence before `PaymentRefunded` exists;
- provider ambiguity remains `OutcomeUnknown` and cannot be guessed into Cash movement;
- non-restock refund behavior is outside the current MVP refund policy.

### Guest data (`PD-035`)

Guest checkout creates no shopper/CRM account. Sales keeps immutable per-Order contact/fulfillment snapshot, with name/email required and phone/address only when fulfillment requires them. No automatic profile matching or historical rewrite.

## 11. Procurement and Accounting baseline

### Procurement

- Supplier has Tenant-owned stable identity plus Active/Archived status;
- Draft or Published Products may be purchased; Archived Product may not enter a new PO;
- submitted PO is immutable and cancellable only before GoodsReceipt/Invoice/Payment evidence;
- confirmed GoodsReceipt is immutable; correction uses explicit compensating receipt evidence;
- exactly one SupplierInvoice and one full SupplierPayment per PO in MVP;
- invoice follows full receipt; exact match auto-accepts, variance requires explicit merchant approval;
- SupplierPayment is merchant attestation, not bank execution.

### Accounting learning/MVP policy

Accounting is enabled for each Tenant with platform-defined required control roles including Cash, Customer Deposits, Sales Revenue, Inventory, COGS, AP, GRNI, PPV, Inventory Adjustment Gain/Loss.

Core postings/business triggers:

- `PaymentCaptured` → Cash / Customer Deposits;
- `OrderFulfilled` → Customer Deposits / Sales Revenue;
- moving weighted-average inventory valuation lives in Accounting;
- `StockIssued` is the single COGS trigger → COGS / Inventory;
- accepted receipt accounting uses Inventory / GRNI;
- `SupplierInvoiceRecorded` clears GRNI to AP with approved variance to PPV;
- `SupplierPaymentRecorded` → AP / Cash;
- physical stock adjustments post explicit gain/loss effects;
- journals are balanced, immutable, idempotent by source, and corrected through reversal/compensating entries.

Refund/return accounting under resolved `PD-023`:

- `RefundApproved` for a recognized sale → `Dr Sales Revenue / Cr Customer Deposits` as a compensating journal;
- accepted `StockReturned` → `Dr Inventory / Cr COGS` using original issue-cost provenance;
- verified `PaymentRefunded` → `Dr Customer Deposits / Cr Cash`;
- each effect is linked/idempotent and original posted journals remain immutable.

## 12. Reporting, Ingestion, Notification, and Audit

### Reporting

Operational KPI sources are explicit:

- Order count = `OrderConfirmed` count;
- AOV = confirmed OrderTotal sum / confirmed-order count;
- Top products = confirmed whole-unit quantity by Product snapshot;
- failed-payment rate excludes `OutcomeUnknown` and uses terminal attempt outcomes;
- operational Gross Sales = confirmed OrderTotal sum and is never Accounting revenue.

Refund/correction facts are attributed to their own occurrence business date and do not rewrite original operational counts.

Reporting is projection only, never command/entitlement authority.

### Product Data Ingestion

Base source policy review is platform-owned. Authorized platform admins mark policy Current/global enablement; Tenant Owner/Admin may opt an approved source in/out but cannot override policy. Material source/API/terms/robots/auth policy changes can stale the review; no arbitrary time-only expiry.

PDI snapshots/candidates never directly mutate Catalog.

### Notification

Notification read/acknowledgement state is per recipient. Owner/Admin receive appropriate critical tenant-level security/billing/accounting/operational exceptions; Staff receive permitted operational notifications; Viewer receives no actionable notification in MVP. Acknowledging notification never resolves source business exception.

### Audit

Audit records successful/rejected privileged mutations, security administration, reasoned Tenant suspension/reactivation, accounting corrections, subscription/admin actions, refund approval/rejection, and security-significant tenant-isolation denials. Tenant Audit is readable by Owner/Admin only and must not leak cross-Tenant existence/identifiers.

## 13. Subscription & Billing baseline

### Approved Plan catalog (`PD-044`)

Plan has stable identity and immutable accepted PlanVersions. Accepted version is never edited in place and may later be withdrawn from new sale without rewriting history.

Initial paid monthly catalog:

| Plan | Price/month | MaxActiveMemberships | MaxWarehouses | Scheduled ingestion | Order warning |
|---|---:|---:|---:|---|---:|
| Starter | 199,000 VND | 3 | 1 | No | 500 |
| Growth | 499,000 VND | 10 | 3 | Yes | 2,000 |
| Business | 999,000 VND | 30 | 10 | Yes | 10,000 |

All paid Plans include Catalog, Storefront, Orders/Sales, Inventory, Procurement, Accounting, and Reporting. They differ primarily by scale and scheduled automation. `MaxActiveMemberships` counts all Active roles. Order thresholds are warning-only. Enterprise/custom pricing is out of MVP.

Trial is dedicated terms, not a paid-plan alias: all core capabilities enabled, `MaxActiveMemberships=3`, `MaxWarehouses=1`, scheduled ingestion enabled, order warning 500. Trial does not automatically convert to Starter.

### Trial/paid periods

- successful registration starts 30-day no-card Trial;
- paid MVP cycle is monthly only with explicit billing anchor and month-end fallback;
- SaaS charges are VND whole đồng;
- no tax calculation, statutory invoice, currency conversion, or proration in learning MVP.

### Upgrade

Upgrade is immediate only after required PlatformCharge has verified successful outcome. Successful mid-period upgrade starts a fresh monthly paid period and a new effective EntitlementSet; unused prior-period value is not credited. Declined/Unknown charge leaves existing terms authoritative.

### Downgrade

Downgrade schedules for next renewal boundary. Authoritative owning-domain usage is revalidated against target `MaxActiveMemberships`, `MaxWarehouses`, and other applicable hard limits. If current usage exceeds target hard limit, downgrade is `BlockedByUsage/RemediationRequired`; lower entitlements do not become effective and current terms continue. No foreign-domain data/resource is auto-deleted/disabled.

### Cancellation/delinquency/end

- merchant cancellation means cancel renewal at paid period end;
- definitive renewal failure → `PastDue` with 7-day grace and existing entitlements continue;
- `OutcomeUnknown` is not PastDue;
- grace expiry without successful renewal → `Ended`;
- Ended disables ordinary mutations, scheduled automation, and public commerce, while authenticated read/history/export/recovery remains;
- ending Subscription does not delete Tenant/data/Memberships;
- reactivation starts new accepted period/history.

### Entitlement categories

- core capabilities are enabled on all three paid Plans;
- scheduled product ingestion is a hard capability gate: Starter off, Growth/Business on, Trial on;
- `MaxActiveMemberships` and `MaxWarehouses` are hard growth/activation limits only; existing resources preserved for remediation;
- order-volume meter is idempotent `OrderConfirmed` count in current billing period, warning-only and never blocks shopper checkout;
- overage billing absent;
- `Unlimited` remains explicit when a future approved entitlement uses it.

### SaaS billing provider/admin

MVP uses a dedicated simulated SaaS billing-provider seam supporting success, no-commit/decline, `OutcomeUnknown`, duplicates, retry/idempotency, query/reconciliation, and out-of-order evidence. No real money/card/bank data.

Platform-admin Subscription/Billing support is read-only visibility; no plan/charge/entitlement override backdoor exists. Tenant Management's separate platform suspend/reactivate authority does not mutate Subscription truth.

## 14. Source-of-truth questions

| Business question | Authoritative context |
|---|---|
| Does Tenant exist and what Profile/status does it have? | Tenant Management |
| May authenticated subject act for selected Tenant? | Merchant Access |
| Which Subscription/terms/EntitlementSet govern Tenant now? | Subscription & Billing |
| Which PlanVersion/pricing/limits are currently sellable? | Subscription & Billing |
| How many Active Memberships exist? | Merchant Access |
| How many Warehouses / what stock exists? | Inventory |
| What Product/base price/public eligibility exists? | Catalog |
| What price did shopper agree to? | Sales immutable snapshot |
| What was ordered/commercial state? | Sales |
| Was refund requested/approved/rejected? | Sales |
| What merchant-order payment/refund outcome is verified/unknown? | Payments |
| What physical stock quantity effect occurred? | Inventory |
| What supplier commitment/receipt/invoice/payment evidence exists? | Procurement |
| What has been posted to merchant books / inventory valuation? | Accounting |
| What did external source show? | Product Data Ingestion |
| What CommerceOS SaaS charge/subscription billing outcome is known? | Subscription & Billing |
| What is a dashboard/KPI value? | Reporting projection only |

## 15. Cross-cutting invariants

1. tenant-owned aggregate has one immutable owning TenantId;
2. client identifiers never create authority;
3. one bounded context never mutates another context's source truth as a shortcut;
4. requests/intents are not success facts;
5. projections/UI/cache do not authorize transactional writes;
6. source replay cannot duplicate logical side effects;
7. external timeout/unknown outcome is never guessed into success/failure;
8. historical accepted commercial/accounting evidence is append-oriented/non-destructive;
9. Subscription/Tenant/Membership/Payment/Order/Inventory/Accounting state dimensions remain independent;
10. downgrade/suspension/subscription end never silently destroy foreign-domain data;
11. posted journal balances and immutability are mandatory;
12. Inventory zero-floor/Reserved invariants are concurrency-critical business requirements;
13. every hard entitlement write uses current trusted entitlement plus authoritative owning-domain state;
14. public commerce and ordinary mutations require applicable effective subscription entitlements, while approved read/history/export/recovery remains distinct;
15. refund request never creates return/payment/accounting effects before explicit approval;
16. refund approval may authorize cross-domain effects but never substitutes for Inventory/Payments/Accounting owning facts;
17. Tenant suspension is a platform reasoned action and cannot be bypassed by merchant self-reactivation;
18. Plan name is presentation/commercial identity; effective EntitlementSet, not the plan-name string, authorizes gated operations.

## 16. Business error semantics

Domain errors are stable business outcomes, not HTTP status codes/exceptions.

Common families:

| Outcome family | Meaning |
|---|---|
| `ValidationRejected` | supplied business values invalid; no accepted change |
| `NotFoundOrNotVisible` | absent/not visible in trusted Tenant context; cross-Tenant existence not disclosed |
| `NotAuthorized` | actor lacks required Membership/role/domain authority |
| `TenantSelectionRequired` | several eligible Tenants exist and no intentional active Tenant is resolved |
| `SubscriptionOrEntitlementDenied` | current trusted commercial entitlement does not permit protected operation |
| `Conflict` | uniqueness/idempotency/business claim conflicts |
| `StaleRevision` | attempted update based on older accepted aggregate state |
| `InvalidStateTransition` | requested transition is not approved from current state |
| `AlreadyApplied` | equivalent logical intent/source already accepted; prior result is referenced |
| `OutcomeUnknown` | independent external commit outcome cannot currently be proven |
| `ApprovalRequired` | requested financially/materially sensitive effect requires an explicit approved business decision first |
| `RemediationRequired` | requested downgrade/change cannot become effective until owning-domain usage is remediated |
| `HumanProductDecisionRequired` | a future capability reaches business semantics not covered by the currently resolved product baseline |

Technical Architecture decides transport representation.

## 17. Current human product-decision gates

There are **no unresolved current `PD-*` human product-decision gates** for the approved MVP baseline.

This does not authorize scope invention. Future Tenant deletion/privacy workflows, non-restock refund behavior, Enterprise/custom pricing, new entitlement types/commercial strategies, or other materially new semantics require new explicit product decisions before implementation.

## 18. Downstream reconciliation required

The final product-decision pass and follow-up resolutions occurred **after** the previous TASK-0092 technical baseline. Therefore the Technical Architect must reconcile technical artifacts against this updated business baseline before affected implementation tasks become Ready.

Priority technical rechecks include:

- multi-Tenant Membership selection and one-role authorization;
- onboarding consistency across Tenant + Owner + approved automatic Trial EntitlementSet;
- platform-only reasoned Tenant suspension/reactivation and Suspended read-only access paths;
- `MaxActiveMemberships` authoritative count and hard growth enforcement;
- invitation verified-email/rotation/expiry behavior;
- Catalog SKU/slug/lifecycle/reference/media/import rules;
- checkout reconfirmation and reserve→capture→confirm→allocate sequence;
- refund request/approval/rejection contract and dedicated approval experience;
- exactly-once `StockReturned`, compensating Accounting effects, and provider-evidence `PaymentRefunded` separation;
- PaymentAttempt/OutcomeUnknown/reconciliation and stock hold;
- Inventory zero-floor/reserved invariants;
- immutable Procurement evidence;
- Accounting trigger/valuation/GRNI/date/refund-correction semantics;
- approved Starter/Growth/Business PlanVersions/prices and Trial terms;
- hard `MaxActiveMemberships`/`MaxWarehouses`, scheduled-ingestion gate, and soft order-volume thresholds;
- Subscription monthly periods, upgrade/downgrade/PastDue/end, SaaS provider simulation, and read-only platform Subscription support.

After Technical Architecture reconciliation, Backlog Planner must reconcile candidate tasks and readiness. Resolved product decisions should no longer remain artificial blockers. Future out-of-MVP semantics must become explicit new product work rather than hidden assumptions.

The Domain Architect does not mark implementation tasks Ready.

## 19. Acceptance statement

The current business/domain baseline now:

- represents all approved current product decisions in bounded-context ownership and invariants;
- distinguishes source truth, intent, projection, provider evidence, and Audit evidence;
- records the approved aggregate/lifecycle semantics needed by the current backlog;
- preserves cross-domain ownership rather than absorbing responsibility for convenience;
- contains no unresolved current human product-decision gate for the approved MVP;
- keeps future destructive/privacy/non-restock/Enterprise/new-commercial semantics explicitly outside MVP rather than guessed;
- gives actionable handoff to Technical Architect and Backlog Planner;
- introduces no application code, AWS, persistence, API, or deployment choice.

**Stop condition: DOMAIN BASELINE EXTENDED AND RECONCILED.**
