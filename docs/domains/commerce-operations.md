# Commerce Operations & Cross-Domain Fact Baseline

_Medium-depth runway for Sales, Inventory, Payments, Procurement, Accounting, Reporting, Product Data Ingestion, and supporting contexts. Reconciled by TASK-0087._

## 1. Purpose

This document defines source-of-truth ownership, aggregate/state meaning, and business-fact vocabulary far enough to support dependency planning. It does not finalize distant feature detail or choose interaction, deployment, storage, or cloud mechanisms.

A future contract may use a different versioned event name, but it must preserve the owner and meaning recorded here.

## 2. Cross-domain fact rule

For every cross-domain effect, distinguish:

```text
Request to attempt work
          ↓
owning context accepts or rejects
          ↓
past-tense owned business fact
          ↓
another context decides its own effect
```

Examples:

- `ReserveStock` is a request; `StockReserved` is Inventory's fact.
- `CapturePayment` is a request; `PaymentCaptured` is Payments' fact after verified provider evidence.
- a provider callback is evidence; it is not an Order fact.
- `GeneratePosting` is a request; `JournalPosted` is Accounting's fact.
- a queue retry or projection rebuild is an operational signal; it is not a sale, receipt, or payment.

## 3. Authoritative business-fact catalog

| Fact | Owner | Meaning | Explicitly does not mean |
|---|---|---|---|
| `MerchantTenantRegistered` | Tenant Management | a usable merchant tenant and initial ownership outcome was accepted | authentication alone succeeded |
| `MembershipActivated` / `MembershipDisabled` | Merchant Access | subject gained/lost active authority in one Tenant | token was created/revoked by an identity provider |
| `ProductPublished` / `ProductUnpublished` | Catalog | canonical Product became/became no longer public-eligible | product is in stock or publicly deployed everywhere |
| `ProductImported` | Catalog | merchant-approved source fields were applied to a canonical Product | source obtained continuing update authority |
| `OrderPlaced` | Sales | one commercial order with immutable lines/prices/totals was accepted | stock reserved, payment captured, or revenue recognized |
| `OrderConfirmed` | Sales | the order met the approved commercial confirmation rule | an unverified provider callback arrived |
| `OrderAllocated` | Sales | Sales accepted evidence that every required line has an active Inventory reservation | stock was physically issued |
| `OrderFulfilled` | Sales | all required fulfillment effects were accepted | COGS was posted automatically |
| `OrderCancelled` | Sales | Sales accepted cancellation under the approved policy | any required stock release/refund already succeeded unless stated separately |
| `RefundRequested` | Sales | merchant/customer refund intent passed Sales eligibility | money was refunded or goods returned |
| `StockReserved` | Inventory | exact quantities were held under an active Reservation | Sales order is confirmed or paid |
| `StockReleased` | Inventory | an active Reservation was ended without issue for the stated quantity | Sales order was cancelled |
| `StockIssued` | Inventory | reserved physical stock left on-hand and reservation balances exactly once | customer order status or COGS changed |
| `StockReceived` | Inventory | on-hand stock increased for an accepted source | Procurement receipt or AP posting necessarily exists |
| `StockReturned` / `StockAdjusted` | Inventory | exact physical quantity effect and reason were recorded | refund/accounting consequence was accepted |
| `PaymentAuthorized` | Payments | provider evidence shows funds are authorized under the mock-provider semantics | money was captured or order confirmed |
| `PaymentCaptured` | Payments | provider evidence shows the stated amount/currency captured once | revenue recognition policy has been selected |
| `PaymentDeclined` | Payments | provider gave a definitive business decline for an attempt | every future attempt for the order is forbidden |
| `PaymentOutcomeBecameUnknown` | Payments | CommerceOS cannot yet determine whether the independent provider committed | provider failed, declined, or timed out as a durable provider state |
| `PaymentRefunded` | Payments | provider evidence shows the stated refund amount accepted once | returned inventory or accounting correction occurred |
| `PurchaseOrderSubmitted` | Procurement | merchant committed the submitted supplier/line/commercial snapshot | supplier accepted, goods arrived, or AP exists |
| `GoodsReceiptRecorded` | Procurement | authorized staff recorded immutable physical-receipt evidence against a PO | Inventory has applied StockReceived or Accounting has recognized AP |
| `SupplierInvoiceRecorded` | Procurement | merchant recorded immutable supplier-invoice evidence | invoice was paid or journal posted |
| `SupplierPaymentRecorded` | Procurement | merchant attested an external supplier-payment occurrence | CommerceOS executed bank payment or Accounting cash/AP changed |
| `JournalPosted` | Accounting | balanced immutable journal became ledger truth | source operational context changed |
| `JournalReversed` | Accounting | a new linked reversal journal was posted | original journal was edited/deleted |
| `SourceSnapshotCaptured` | Product Data Ingestion | one external observation was captured with provenance | canonical Product changed |
| `ImportCandidateCreated` | Product Data Ingestion | normalized source facts are ready for merchant review | candidate was accepted or published |

`CartCheckedOut` is not an integration fact in this baseline. If retained later, it must mean a distinct analytics fact rather than duplicate `OrderPlaced`.

## 4. Sales & Order Management

### Responsibility and aggregate

Sales owns the accepted commercial agreement between a Tenant and a customer/shopper.

`SalesOrder` is the aggregate root. It owns:

- immutable Tenant and Order identity;
- customer reference plus immutable checkout contact/address snapshot when collected;
- one or more OrderLines;
- immutable ProductId, displayed SKU/name, quantity, unit-price, discount, currency, and line-total snapshots;
- order-level totals and one currency;
- Sales-owned lifecycle/history;
- cancellation and refund-request eligibility/history;
- stable accepted checkout-intent identity.

Sales does not own current Product, current Customer profile, stock, payment-provider state, or journals.

### Cart and checkout

The Storefront cart is tenant-bound, transient, and untrusted. Displayed price is an estimate until Sales accepts checkout.

Checkout invariants:

1. trusted storefront context determines Tenant; cart/request TenantId cannot grant or redirect authority;
2. every Product is re-resolved as currently sellable by Catalog;
3. quantity is positive and within approved limits;
4. all lines use one approved currency;
5. totals are calculated from authoritative resolved values, not browser totals;
6. all lines succeed or no SalesOrder is placed;
7. the same tenant + checkout intent + equivalent request creates at most one logical SalesOrder;
8. reuse of the same intent identity for materially different input is a conflict;
9. Catalog changes after placement never rewrite the Order;
10. Customer/CRM changes after placement never rewrite the Order's contact/address snapshot.

Price-change confirmation, quantity units, manual discounts, and guest policy remain `PD-011`, `PD-012`, and `PD-013`.

### State dimensions

The earlier single flat status mixes commercial, payment, fulfillment, and refund meaning. The baseline therefore defines meanings by dimension; `PD-014` selects the approved initial combined presentation and reserve/pay sequence, while `PD-042` selects cancellation eligibility/effects and the completion trigger.

Sales-owned commercial states/facts:

- `Draft` — optional merchant-created order not yet placed; guest checkout may skip it;
- `Placed` — immutable commercial snapshot accepted;
- `Confirmed` — commercial confirmation condition selected by `PD-014` was met;
- `Cancelled` — cancellation was accepted under the stage/effect policy selected by `PD-042`;
- `Completed` — terminal business completion trigger selected by `PD-042` was met.

Sales-owned fulfillment view, based on Inventory evidence:

- `Unallocated` — required reservation evidence is not complete;
- `Allocated` — all required lines have active reservations;
- `Fulfilled` — all required lines have accepted stock issue evidence.

Payment view, based on Payments evidence:

- `NotRequested`, `Pending`, `OutcomeUnknown`, `Authorized`, `Captured`, `DefinitiveNoCommitObserved`, `PartiallyRefunded`, `Refunded`; whether a no-commit observation closes the Order path is `PD-017`.

Refund state is not allowed to erase fulfillment history. `PartiallyRefunded` and `Refunded` are financial/refund dimensions, not replacements for `Fulfilled`.

Meaning constraints:

- `PaymentOutcomeBecameUnknown` is nonterminal and cannot be converted to decline/failure because time passed.
- `OrderAllocated` exists only after all required Reservation evidence is accepted.
- `OrderFulfilled` exists only after all required issue evidence is accepted.
- `PaymentDeclined` may end one attempt without deciding whether the SalesOrder may accept another attempt; see `PD-017`.
- cancellation after capture may require a separate refund; cancellation after allocation may require separate stock release. Sales must not report those independent effects as complete merely because cancellation was accepted.

### Sales errors

- `ORDER_LINE_NOT_SELLABLE` — Catalog says a Product is not currently eligible; distinct from insufficient stock.
- `ORDER_PRICE_CHANGED` — authoritative resolved price differs under the approved shopper-confirmation policy.
- `CHECKOUT_INTENT_CONFLICT` — same intent identity used for non-equivalent input.
- `ORDER_STATE_TRANSITION_INVALID` — requested Sales transition is not permitted.
- `ORDER_PAYMENT_OUTCOME_UNKNOWN` — order cannot take an unsafe terminal payment-dependent transition.
- `ORDER_ALLOCATION_INCOMPLETE` — all required line reservations are not accepted.
- `ORDER_CANCELLATION_NOT_ALLOWED` / `ORDER_REFUND_NOT_ALLOWED` — approved eligibility rule failed.

## 5. Inventory

### Responsibility and aggregates

Inventory owns physical stock truth.

- `StockItem` is the balance-consistency aggregate root for one Tenant, Warehouse/Location, and Catalog Product reference.
- `StockReservation` has stable identity/source and its own lifecycle; it may be modeled within the StockItem consistency boundary without changing business ownership.
- `StockMovement` is immutable evidence of one accepted quantity effect.
- `InventoryAdjustment` records authorized adjustment reason/evidence and results in a movement when accepted.
- `Warehouse` is a tenant-owned reference aggregate. One active warehouse is the initial operating scope; multi-warehouse allocation is later.

### Quantity invariants

The universally accepted quantity relationship is:

```text
Reserved >= 0
Available = OnHand - Reserved
```

`PD-041` must select whether OnHand/Available may be negative, whether backorder exists, the reservation floor, and how adjustment decreases interact with reserved quantity. Until then, TASK-0028–0031 cannot implement an assumed zero floor or an assumed negative-stock policy.

Every non-zero accepted stock effect creates exactly one StockMovement. Initialization at zero is not a movement. A replay of the same logical source creates no additional movement or balance change; incompatible reuse is a conflict.

| Movement | OnHand effect | Reserved effect |
|---|---:|---:|
| Receive | +quantity | 0 |
| Reserve | 0 | +quantity |
| Release | 0 | -quantity |
| Issue | -quantity | -quantity |
| Return | +quantity | 0 |
| AdjustmentIncrease | +quantity | 0 |
| AdjustmentDecrease | -quantity | 0 |

### Reservation lifecycle

```text
Active ──Release──► Released
   └────Issue─────► Issued
```

An already released quantity cannot later be issued, and an already issued quantity cannot be released/issued again. Whether a Reservation is whole-quantity only or remains Active with partially released/issued quantities is `PD-015`; expiry/hold policy is `PD-018`.

An ambiguous Payment outcome never by itself authorizes release; see `PD-018`.

### Low stock

Low stock is an Inventory-owned derived condition, not a balance. `LowStockDetected` may represent a threshold crossing, but Reporting/Notification cannot treat it as stock truth. Threshold basis/scope and reset behavior remain `PD-019`.

### Inventory errors

- `INSUFFICIENT_AVAILABLE_STOCK`
- `RESERVATION_ALREADY_TERMINAL`
- `RESERVATION_SOURCE_CONFLICT`
- `STOCK_ADJUSTMENT_REJECTED_BY_POLICY` — applicable when the approved `PD-041` floor/reservation interaction rejects the change
- `STOCK_MOVEMENT_ALREADY_APPLIED`
- `WAREHOUSE_OR_PRODUCT_REFERENCE_INVALID`

## 6. Payments and the Mock Payment Provider

### Two bounded contexts

The two contexts must not be conflated:

```text
Sales amount due
      ↓
CommerceOS Payments ──provider request/evidence──► Mock Payment Provider
      │                                                │
      └──merchant-side known state                     └──provider committed state
```

### CommerceOS Payments

`Payment` is the named merchant-side aggregate concept for an Order's payment obligation. Whether one aggregate represents the Order obligation with several attempts or each accepted payment instruction is separate remains `PD-016`. Regardless of cardinality, Payments owns:

- immutable Tenant/Payment identity and SalesOrder reference;
- amount and currency requested;
- provider intent/reference mapping;
- PaymentAttempts and observations;
- current verified known outcome and ambiguity status;
- captured and refunded cumulative amounts;
- Refund entities/operations and their source intent.

Baseline invariants:

1. amount/currency originate from the accepted Sales obligation, not browser/provider callbacks alone;
2. equivalent repeated operations have one logical effect;
3. the same operation identity with different amount/currency is a conflict;
4. total verified captured amount applied to the Order cannot exceed the accepted Sales obligation;
5. cumulative successful refund cannot exceed captured amount;
6. only verified provider evidence may create capture/refund facts or definitive attempt decline/no-commit outcomes;
7. timeout, network error, missing callback, and caller cancellation do not prove provider failure;
8. while outcome is unknown, unsafe capture retry, Sales failure, stock release, and accounting posting are prohibited until reconciliation establishes a fact;
9. duplicate/out-of-order evidence cannot regress terminal known state or duplicate a fact.

Payment aggregate/attempt cardinality, immediate capture versus authorize/capture, retry after decline, and ambiguous hold/escalation remain `PD-016` through `PD-018`.

### Payment aggregate conditions and attempt outcomes

Merchant-side aggregate conditions that do not depend on the unresolved cardinality choice:

- `Initiated/Open` — payment obligation/instruction was accepted and may have attempt activity;
- `Authorized` — provider authorization was verified when the approved flow uses authorization;
- `Captured` — verified captured amount satisfies the approved aggregate condition;
- `OutcomeUnknown` — nonterminal overlay stating CommerceOS cannot determine whether the provider committed;
- `PartiallyRefunded` / `Refunded` — cumulative verified refund condition.

Attempt outcomes are separate:

- `Pending`;
- `Authorized` or `Captured` with verified evidence;
- `Declined` — definitive business decline for that attempt;
- `DefinitiveNoCommit` — verified provider outcome proving that attempt did not commit;
- `TransientFailure` — retryability depends on approved policy and never proves a business decline;
- `TimedOut/UnknownObservation` — caller observation requiring inquiry/reconciliation.

Whether a definitive decline/no-commit closes only an attempt, permits a new attempt, or closes the Order payment path remains `PD-017`. `TimedOut` is never a durable Mock Payment Provider business state.

### Mock Payment Provider

The provider owns its `PaymentIntent`, provider operations, `Refund`, and callback-delivery attempts. It supports deterministic scenarios but does not know or mutate SalesOrder, Inventory, or Accounting.

Provider facts/callback messages are external evidence. CommerceOS Payments verifies and translates them into its own owned facts; Sales and Accounting do not consume provider-private state as authority.

### Payment errors/outcomes

- `PAYMENT_DECLINED` — definitive attempt outcome.
- `PAYMENT_OUTCOME_UNKNOWN` — not a failure and requires inquiry/reconciliation.
- `PAYMENT_OPERATION_CONFLICT` — operation identity reused incompatibly.
- `PAYMENT_AMOUNT_MISMATCH` — evidence does not match merchant obligation.
- `PAYMENT_REFUND_EXCEEDS_CAPTURED`
- `PAYMENT_ALREADY_CAPTURED` / `PAYMENT_ALREADY_REFUNDED` — prior accepted result is returned/referenced when equivalent.

## 7. Procurement

### Responsibility and aggregates

Procurement owns merchant purchasing evidence:

- `Supplier` — tenant-owned supplier identity/profile/status;
- `PurchaseOrder` — supplier commitment and lines;
- `GoodsReceipt` — immutable physical-receipt document/evidence;
- `SupplierInvoiceRecord` — immutable external invoice evidence/reference;
- `SupplierPaymentRecord` — merchant-attested external payment evidence.

`PurchaseOrder` is the aggregate root for commitment/lifecycle. A submitted PO preserves its supplier, product, quantity, unit-cost, currency, and total snapshot. Whether/how submitted orders may be amended or cancelled is `PD-025`.

`GoodsReceipt` has its own stable identity even when the initial scope allows one complete receipt. A retry cannot create a second receipt for the same logical occurrence. A wrongly confirmed physical receipt is corrected with explicit corrective evidence; it is not silently edited away.

### State dimensions

The documented happy path remains:

```text
Draft → Submitted → Goods received → Invoice recorded → Payment recorded → Closed
```

These are not assumed to be one inseparable enum. Procurement needs honest independent dimensions because receipt, Inventory application, invoice, and payment evidence can temporarily differ:

- PO commitment: `Draft`, `Submitted`, `Closed`, and any future approved cancellation/correction state;
- receipt: `NotReceived`, `Received` (partial/over-receipt later);
- Inventory application: `Pending`, `Applied`, `NeedsAttention`;
- invoice: `NotRecorded`, `Recorded`;
- supplier settlement: `Unpaid`, `Paid` for the initial full-payment scope.

Recording physical receipt remains true even if Inventory application fails; Procurement exposes recovery state instead of erasing the receipt. Inventory owns the resulting `StockReceived` effect.

Invoice cardinality/timing/matching, receipt correction, Supplier rules, PO line eligibility, and payment attestation semantics remain `PD-025` and `PD-027` through `PD-029`.

### Procurement errors

- `SUPPLIER_NOT_ELIGIBLE`
- `PURCHASE_ORDER_NOT_EDITABLE`
- `GOODS_RECEIPT_QUANTITY_NOT_ALLOWED`
- `GOODS_RECEIPT_SOURCE_CONFLICT`
- `INVENTORY_APPLICATION_PENDING_OR_FAILED`
- `SUPPLIER_INVOICE_CONFLICT_OR_MISMATCH`
- `SUPPLIER_PAYMENT_EVIDENCE_INVALID`
- `PURCHASE_ORDER_NOT_CLOSABLE`

## 8. Accounting

### Responsibility and aggregates

Accounting owns ledger representation, not operational truth.

- `ChartOfAccounts` owns the Tenant's account definitions and policy-valid account lifecycle under `PD-038`.
- `JournalEntry` is the aggregate root containing JournalLines, source references, posting status, and reversal links.
- `PostingPolicy` describes the approved mapping from one authoritative operational fact to one logical accounting effect.
- General Ledger and Trial Balance are Accounting-owned derivations from posted journals, not separate sources of truth.

`ChartOfAccounts` is tenant-owned. Account stable identity, code/name/type, control-account designation, and lifecycle are Accounting facts, but template/customization, enablement, code reuse, required controls, and deactivation rules remain `PD-038`. A Builder may not infer a statutory chart or allow a referenced/control account to disappear merely because CRUD would be convenient.

### Journal states and invariants

```text
Draft ──Post valid balanced entry──► Posted
Posted ──Reverse──► original remains immutable Posted
                   + new linked Posted reversal journal
```

- A rejected `PostJournal` attempt leaves the Journal Draft and produces a rejection outcome/evidence; `Rejected` is not a Journal state in this baseline.
- Posted JournalEntry identity, lines, amounts, accounts, dates, source, and narrative are immutable.
- Every Posted Journal balances total debit and credit in one currency.
- A source logical effect can create at most one posting; replay returns/references the prior result.
- Correction uses a linked reversal plus, when needed, a new corrected journal.
- Reversal does not delete or mutate the original and cannot be applied repeatedly to create duplicate reversals.
- Every system-generated posting identifies the operational fact and policy version that justified it.
- A journal preserves source occurrence time, accounting/effective date, and posting time as distinct concepts; `PD-039` selects which date drives the ledger and any backdating/period rule.

### Accounting-trigger decision matrix

Exactly one authoritative source fact/logical source key must be selected for each effect. Candidate tasks may not consume both alternatives and rely on timing to avoid duplication.

| Economic effect | Operational owner | Decision required before posting task is Ready |
|---|---|---|
| sale/revenue and Cash versus AR | Sales/Payments provide candidate facts; Accounting owns posting | `PD-020` chooses recognition fact and account treatment |
| COGS and Inventory reduction | Inventory/Sales provide candidate facts | `PD-021` chooses one trigger, valuation method, and immutable cost-snapshot owner |
| received inventory and AP/interim liability | Procurement/Inventory provide candidate facts | `PD-022` chooses receipt/invoice timing and variance treatment |
| supplier payment Cash/AP | Procurement supplies evidence approved under `PD-029` | `PD-022` chooses only the Accounting recognition/posting policy |
| customer refund/return | Payments/Sales/Inventory provide distinct facts | `PD-023` chooses contra/reversal timing and restock/COGS treatment |
| stock adjustment gain/expense | Inventory supplies reasoned adjustment fact | `PD-024` chooses financially postable reasons/accounts |

`PaymentCaptured` versus `OrderConfirmed`, `StockIssued` versus `OrderFulfilled`, and `GoodsReceiptRecorded` versus `StockReceived` are not interchangeable aliases. Human accounting policy must select one logical trigger per effect.

### Accounting errors

- `JOURNAL_NOT_BALANCED`
- `JOURNAL_ACCOUNT_NOT_POSTABLE`
- `JOURNAL_ALREADY_POSTED`
- `POSTING_SOURCE_ALREADY_APPLIED`
- `POSTING_POLICY_UNDEFINED`
- `JOURNAL_NOT_REVERSIBLE` / `JOURNAL_ALREADY_REVERSED`
- `ACCOUNTING_PERIOD_NOT_OPEN` only after period policy is explicitly introduced

## 9. Reporting & Analytics

Reporting owns rebuildable projections, projection progress/freshness, and metric definitions once approved. It never owns or repairs the source transaction.

Rules:

1. operational KPIs derive from facts owned by operational contexts;
2. financial reports derive from Accounting journal/ledger facts and reconcile to them;
3. a projection exposes its `as-of` time/freshness where lag matters;
4. rebuild/replay does not duplicate totals;
5. late correction/reversal updates projections without rewriting the source fact;
6. reporting failure never fails or reverses a committed transaction.

Operational order count, AOV, top-product, and failed-payment definitions remain `PD-030`; business date/timezone and correction attribution remain `PD-031`. Financial revenue/gross profit inherit Accounting decisions `PD-020`, `PD-021`, `PD-038`, and `PD-039` rather than selecting independent Reporting source facts. A task may not present a metric label before its numerator, denominator, eligibility, date basis, source facts, and financial account grouping where relevant are approved.

Low-stock is an Inventory projection. General Ledger and Trial Balance are Accounting derivations. Reporting may compose them for dashboards but cannot become their source.

## 10. Product Data Ingestion

### Responsibility and aggregates

Product Data Ingestion owns policy-governed external observations and merchant import proposals.

- `DataSource` — source identity with separate policy-review validity and operating status. Operating states may include Active/Paused/Disabled, but Active is eligible to acquire only while a current approved policy review exists. Authority/scope is `PD-026`.
- `AcquisitionRequest` / `AcquisitionRun` — business-visible attempt to obtain one allowed external observation; technical worker states are not business states.
- `SourceSnapshot` — immutable external observation with source identity, URL/reference, captured time, and acquisition provenance; it exists even if parsing/normalization later fails.
- `NormalizedSourceProduct` — accepted or rejected structured interpretation linked to one SourceSnapshot, with parser/schema provenance and distinct absent/unknown/failure semantics.
- `ImportCandidate` — tenant-bound proposal created only from an accepted NormalizedSourceProduct, including exact proposed fields and candidate lifecycle.

Baseline rules:

1. source policy must permit acquisition before an attempt is accepted;
2. a policy block, CAPTCHA/access restriction, or unsupported authentication is not permission to bypass controls;
3. the same logical capture identity creates at most one observation; a genuinely new retrieval at a new captured time may create another immutable snapshot;
4. absent source value, parsing failure, and explicit source “unknown/unavailable” remain distinct; normalization failure does not erase the captured SourceSnapshot;
5. raw/normalized external data is never the canonical Product;
6. only explicitly selected merchant-approved fields are proposed/applied;
7. Catalog owns the resulting canonical value and external link;
8. a newer snapshot never silently overwrites an older candidate or Product;
9. source-specific technical queue/retry/DLQ states are operational signals, not commerce facts.

ImportCandidate recognizes conceptually `ReadyForReview`, `ApprovedForApplication`, `ApplicationPending`, `Applied`, `NeedsAttention`, `Rejected`, `Superseded`, and `Expired`; exact transitions, mapping cardinality, and expiry/supersession are `PD-040`. `ApprovedForApplication` means the merchant selected exact proposed fields; it does not mean Catalog changed. `Applied` is true only after Catalog accepts the canonical change represented by `ProductImported`/the applicable Catalog fact.

Business fact candidates:

- `DataSourcePolicyReviewApproved`, `DataSourcePolicyReviewRequired`, `DataSourceActivated`, `DataSourcePaused`, `DataSourceDisabled`
- `SourceAcquisitionPolicyBlocked`
- `SourceSnapshotCaptured`
- `SourceProductNormalized`, `SourceNormalizationRejected`
- `ImportCandidateCreated`, `ImportCandidateApprovedForApplication`, `ImportCandidateApplied`, `ImportCandidateApplicationNeedsAttention`, `ImportCandidateRejected`, `ImportCandidateSuperseded`

`CrawlQueued`, `CrawlStarted`, retry count, callback delivery, and DLQ placement are operational facts/telemetry unless a later product decision gives them merchant business meaning.

## 11. Customer/CRM, Pricing, Storefront, Notification, Audit, Files/Media

### Customer/CRM

- owns editable tenant customer profile/contact preferences;
- Sales owns immutable order contact/address snapshots;
- the initial merchant customer order-history/total-spend query is a Sales-owned projection; later Reporting KPIs are separately owned projections;
- Accounting owns receivable ledger truth;
- guest-to-profile matching, minimum order/customer data, erasure/anonymization, and sensitive-data policy remain `PD-035` before the applicable Customer tasks become Ready.

### Pricing & Promotion

- Catalog owns base selling price;
- Pricing owns eligibility and calculation rules that transform it into an offer;
- Sales owns the accepted snapshot;
- manual discount authority/limits and future promotions cannot be invented inside Sales (`PD-013`).

### Storefront

- owns customer interaction and transient tenant-bound cart;
- public Product/order/availability views are projections;
- checkout request is untrusted intent until Sales accepts it;
- storefront route/slug/custom-domain choices do not change Tenant or Product ownership.

### Notification

- owns notification delivery intent, recipient/audience, delivery attempt/outcome, and read/acknowledgement state;
- never owns the order/payment/stock/crawl/accounting exception it describes;
- notification failure cannot roll back a business transaction;
- acknowledgement cannot imply source exception resolution;
- audience/routing, per-user versus shared state, and Read/Acknowledged lifecycle semantics are pending `PD-032`.

### Audit

- owns append-oriented actor/action/target/outcome/correlation evidence;
- business context still owns the action/result;
- cross-tenant denial evidence must not leak target existence to tenant-visible readers;
- exact coverage/readers are pending `PD-033`.

### Files/Media

- may later own reusable merchant-uploaded binary assets and their lifecycle;
- Catalog owns Product association/order/public metadata;
- Product Data Ingestion owns source media observation/reference;
- rights to copy/display external content remain policy decisions, not technical reachability assumptions.

## 12. Inputs to TASK-0088 and TASK-0089

Technical Architecture must preserve:

- one authoritative owner per fact;
- provider evidence versus CommerceOS Payment truth;
- nonterminal unknown payment outcome and inquiry/reconciliation requirement;
- atomic Inventory invariants within its business consistency boundary;
- truthful cross-domain partial/recovery states without shared persistence;
- one logical accounting source key per economic effect;
- rebuildable projections that never authorize source transactions.

Backlog Planning must reconcile candidate tasks that currently:

- require Confirmed before allocation while also reserving before payment confirmation;
- omit Payment OutcomeUnknown from the order/payment model;
- treat provider `Failed` or a declined attempt as automatically terminal for the Order without `PD-017`;
- conflate flat Order status dimensions;
- leave cancellation eligibility and completion trigger undefined;
- let Sales invent Pricing discount policy;
- leave multi-line allocation/partial effects undefined;
- assume a negative-stock/reservation floor without `PD-041`;
- treat Catalog cost reference as possible COGS authority;
- allow two alternative accounting triggers for one effect;
- equate Procurement goods receipt with Inventory stock receipt;
- call merchant-attested external supplier payment `SupplierPaid` without clarifying evidence;
- conflate DataSource policy approval with operating activation or candidate approval with Catalog application;
- name KPIs without formulas.

Tasks affected by pending decisions in `product-decisions.md` remain Outline/Refined, not Ready.
