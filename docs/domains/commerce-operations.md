# Commerce Operations & Cross-Domain Fact Baseline

_Reconciled after the 2026-08-10 human product-decision pass. This document incorporates approved operational/accounting decisions `PD-011`–`PD-042` where applicable. `PD-023` remains intentionally deferred._

## 1. Purpose

This document defines business ownership, aggregate/state meaning, cross-domain facts, and MVP invariants for Sales, Inventory, Payments, Procurement, Accounting, Reporting, Product Data Ingestion, Notification, Audit, Customer/CRM, and related supporting concerns.

It does **not** choose databases, AWS services, transports, sync/async mechanisms, deployment boundaries, or API schemas.

## 2. Cross-domain fact rule

For every cross-domain effect:

```text
request to attempt work
        ↓
owning context accepts/rejects
        ↓
owned past-tense business fact
        ↓
consumer decides its own effect
```

Examples:

- `ReserveStock` is not `StockReserved`.
- provider evidence is not `PaymentCaptured` until Payments verifies/accepts it.
- `GoodsReceiptRecorded` is not `StockReceived`.
- `PaymentCaptured` is not revenue recognition.
- `StockIssued` is not `OrderFulfilled`.
- `JournalPosted` never changes the source operational fact.

Replay of the same logical source must not create the same inventory, payment, usage, or accounting effect twice.

## 3. Shared MVP value rules

### Money (`PD-002`)

- CommerceOS merchant-commerce MVP is VND-only.
- Money always includes explicit currency.
- Merchant-facing VND amounts use whole đồng.
- No currency conversion exists in MVP.

### Quantity (`PD-012`)

- Product/order/reservation/receipt/issue quantities are positive whole units only.
- Fractional quantities, variable units of measure, conversions, and fractional rounding are out of scope.

## 4. Authoritative fact catalog

| Fact | Owner | Accepted business meaning | Does not imply |
|---|---|---|---|
| `OrderPlaced` | Sales | immutable commercial order snapshot accepted | stock held, payment captured, revenue recognized |
| `OrderConfirmed` | Sales | verified full capture condition for MVP order confirmation has been met | stock issued or revenue journal already posted |
| `OrderAllocated` | Sales | confirmed Order is backed by all required active Inventory reservations | physical fulfillment |
| `OrderFulfilled` | Sales | all required whole-order fulfillment/issue evidence accepted | refund/correction history disappears |
| `OrderCancelled` | Sales | merchant cancellation accepted before fulfillment | reservation release/refund succeeded |
| `RefundRequested` | Sales | refund intent accepted as eligible | money refunded, stock returned, accounting corrected |
| `StockReserved` | Inventory | required quantity held | Order confirmed/paid |
| `StockReleased` | Inventory | reservation ended without issue | Order cancelled |
| `StockIssued` | Inventory | physical quantity left OnHand and reservation effect applied exactly once | Sales completed or COGS already posted |
| `StockReceived` | Inventory | OnHand increased for accepted source evidence | PO receipt/invoice/accounting all succeeded |
| `StockReturned` | Inventory | accepted physical return increased OnHand | refund/revenue/COGS correction accepted |
| `StockAdjusted` | Inventory | accepted physical adjustment changed quantity for explicit reason | reservation/payment/sale changed |
| `PaymentCaptured` | Payments | verified provider evidence proves full Order amount captured | Sales revenue recognized |
| `PaymentDeclined` | Payments | one PaymentAttempt definitively declined/no-commit under provider semantics | Payment obligation/order is terminal |
| `PaymentOutcomeBecameUnknown` | Payments | commit outcome cannot yet be proven | provider failed or declined |
| `PaymentRefunded` | Payments | verified refund amount accepted | Inventory or Accounting correction occurred |
| `PurchaseOrderSubmitted` | Procurement | immutable supplier/line/commercial commitment snapshot submitted | supplier acceptance, receipt, AP |
| `GoodsReceiptRecorded` | Procurement | confirmed immutable physical receipt evidence | Inventory application or journal succeeded |
| `SupplierInvoiceRecorded` | Procurement | supplier invoice evidence accepted | payment/journal succeeded |
| `SupplierPaymentRecorded` | Procurement | merchant attested external supplier payment | CommerceOS executed a bank payment |
| `JournalPosted` | Accounting | balanced immutable journal became ledger truth | source domain changed |
| `JournalReversed` | Accounting | linked compensating/reversal journal posted | original journal edited/deleted |
| `SourceSnapshotCaptured` | Product Data Ingestion | external observation captured with provenance | Catalog changed |
| `ImportCandidateCreated` | Product Data Ingestion | normalized external evidence ready for review | candidate approved/applied |

## 5. Sales & Order Management

### 5.1 Responsibility and aggregate

`SalesOrder` is the aggregate root for the accepted commercial agreement between one Tenant and shopper/customer.

It owns:

- immutable Tenant/Order identity;
- immutable checkout intent identity;
- immutable order-line ProductId, displayed SKU/name, whole-unit quantity, accepted unit price, currency, and line totals;
- authoritative Order total;
- immutable guest contact/fulfillment snapshot when supplied;
- commercial lifecycle;
- Sales-owned view of allocation/fulfillment evidence;
- cancellation/refund-request intent/history.

Sales does not own current Product, current stock, provider state, or journal truth.

### 5.2 Guest checkout and customer data (`PD-035`)

Guest checkout does not require or create a shopper account or CRM Customer.

Per Order snapshot:

- recipient/display name: required;
- email: required;
- phone: required only when selected fulfillment method needs it;
- shipping address: required only when selected fulfillment method needs it.

MVP performs no automatic guest-to-Customer matching or profile creation. Any future link must be explicit/verified and never rewrite historical Order snapshots.

No automatic anonymization/deletion of historical commercial/accounting evidence exists in MVP; future privacy/retention policy must supersede this explicitly.

### 5.3 Checkout repricing (`PD-011`)

Displayed cart price is an estimate until final authoritative checkout validation.

If **any** authoritative current price differs from the shopper-confirmed estimate:

1. no Order is finally placed for that attempt;
2. refreshed authoritative price is returned/presented;
3. shopper must explicitly reconfirm;
4. a new placement attempt may then proceed.

No tolerance band and no silent acceptance of either lower or higher changed price exists in MVP.

### 5.4 Discounts (`PD-013`)

Manual discounts are not supported in MVP public/guest checkout.

- untrusted clients never submit an authoritative discount;
- initial order pricing uses authoritative Catalog/Pricing results only;
- future manual discount/promotion capability belongs to Pricing product policy and must not be smuggled into Sales.

### 5.5 Canonical order dimensions and happy path (`PD-014`, `PD-015`)

Commercial, payment, allocation, fulfillment, and refund dimensions remain separate.

Approved happy path:

```text
authoritative checkout validation
        ↓
OrderPlaced
        ↓
all-line Inventory reservation/hold accepted
        ↓
full immediate Payment capture attempt
        ↓
verified PaymentCaptured
        ↓
OrderConfirmed
        ↓
OrderAllocated (all required reservations active)
        ↓
whole-order fulfillment / all required StockIssued evidence
        ↓
OrderFulfilled
        ↓
Completed when no cancellation/refund exception remains
```

MVP has:

- all-or-nothing allocation across all order lines/quantities;
- no backorder;
- no partial allocation;
- no split shipment;
- no partial fulfillment.

`OrderAllocated` is not asserted before Order confirmation even if a reservation exists.

### 5.6 Commercial states

```text
Placed ──verified capture──► Confirmed ──whole fulfillment──► Fulfilled ──► Completed
  │                              │
  └────────cancel before Fulfilled──────────────────────────► Cancelled
```

`Completed` is terminal for normal commerce operations.

### 5.7 Cancellation (`PD-042`)

Owner/Admin/Staff may cancel an Order any time before it is Fulfilled. Shopper self-service cancellation is out of scope.

Cancellation is only a Sales commercial fact:

- if stock is reserved, Inventory release is separately required;
- if payment is captured, Payments refund is separately required;
- outcomes remain independently visible;
- Sales must not report foreign-domain effects as complete merely because cancellation was accepted.

A Fulfilled Order is not cancellable under MVP normal cancellation semantics.

### 5.8 Sales invariants

- trusted storefront Tenant context determines Tenant; cart/request TenantId never grants authority;
- every Product is revalidated as currently sellable;
- all lines use whole-unit quantities and VND Money;
- totals are server-authoritative business values, never browser totals;
- one logical checkout intent produces at most one logical Order for equivalent input;
- incompatible reuse of the same intent is a conflict;
- later Catalog/Customer changes never rewrite Order snapshots;
- `OutcomeUnknown` blocks unsafe payment-dependent terminal transitions.

## 6. Inventory

### 6.1 Responsibility

Inventory owns physical quantity truth by Tenant + Product + Warehouse/Location.

Core concepts:

- `Warehouse` — Tenant-owned reference aggregate;
- `StockItem` — quantity consistency boundary;
- `StockReservation` — stable source-bound reservation lifecycle;
- `StockMovement` — immutable accepted quantity effect;
- `InventoryAdjustment` — reasoned physical correction request/evidence.

### 6.2 Quantity invariants (`PD-041`)

```text
OnHand >= 0
Reserved >= 0
Available = OnHand - Reserved
Available >= 0
```

MVP does not support negative OnHand, negative Available, or backorder.

Reservation requires current authoritative `Available >= requested quantity` and must preserve the invariant under concurrency.

A downward adjustment may not reduce `OnHand` below `Reserved`. Existing reservations must first be explicitly changed/released through their owning workflow, or the adjustment is rejected/handled as an exception.

### 6.3 Stock movements

| Movement | OnHand effect | Reserved effect |
|---|---:|---:|
| Receive | +q | 0 |
| Reserve | 0 | +q |
| Release | 0 | -q |
| Issue | -q | -q |
| Return | +q | 0 |
| AdjustmentIncrease | +q | 0 |
| AdjustmentDecrease | -q | 0 |

Every accepted non-zero physical quantity effect produces exactly one immutable movement/effect. Replay must not duplicate it.

### 6.4 Reservation and ambiguous payment (`PD-018`)

For an Order whose Payment is `OutcomeUnknown`:

- reserved stock remains held until reconciliation proves Captured or definitive NoCommit/Declined;
- time passage alone never expires/releases the hold;
- if automated/provider inquiry cannot resolve it, the case becomes `NeedsAttention` for merchant/platform support visibility;
- neither merchant nor support may manually declare financial success/failure without provider evidence in MVP.

This conservative rule prevents releasing stock for a payment that may actually have committed.

### 6.5 Low stock (`PD-019`)

Low stock is derived operational state based on `Available`, evaluated per Product + Warehouse.

- threshold is optional; absent means low-stock detection disabled;
- condition becomes active when Available crosses from above threshold to `<= threshold`;
- condition clears when Available rises above threshold;
- it is not stock truth and does not alter balances.

## 7. Payments and Mock Payment Provider

### 7.1 Payment model (`PD-016`)

MVP has **one Payment obligation per Order** with multiple immutable `PaymentAttempt`s.

- full accepted Order amount is captured immediately;
- authorize-only/capture-later is out of scope;
- split tender, multiple tenders, partial capture, and over-capture are out of scope;
- cumulative verified refunds may not exceed captured amount.

### 7.2 Attempt outcomes and retry (`PD-017`)

A provider-verifiable Declined/Rejected/NoCommit result terminates only that PaymentAttempt.

- Payment obligation remains open for another attempt unless Order is separately cancelled;
- prior attempt history remains immutable;
- transport error, timeout, missing callback, or ambiguous provider response is `OutcomeUnknown`, not failure;
- no new capture attempt may start while the previous attempt remains unknown;
- reconciliation/provider inquiry must establish the terminal outcome first.

### 7.3 Payment invariants

- amount/currency originate from accepted Sales obligation;
- equivalent operation retry has one logical effect;
- operation identity reused with different amount/currency is conflict;
- verified captured amount cannot exceed Order obligation;
- only verified provider evidence creates capture/refund/definitive no-commit facts;
- duplicate/out-of-order evidence cannot regress known state or duplicate effects.

Mock Payment Provider remains a separate bounded context that simulates merchant-order payment behavior. It does not own SalesOrder, Inventory, Accounting, or CommerceOS SaaS subscription billing.

## 8. Procurement

### 8.1 Supplier and PO eligibility (`PD-027`)

`Supplier` has stable Tenant-owned identity, display name, and `Active/Archived` status.

- Supplier name is not a legal uniqueness key.
- New PurchaseOrders require Active Supplier.
- Draft or Published Catalog Products may be purchased.
- Archived Products cannot be added to new POs.
- procurement tax, freight, and PO discount calculations are excluded from MVP.

### 8.2 PurchaseOrder (`PD-025`)

`PurchaseOrder` is the aggregate root for supplier commitment.

`Submitted` freezes supplier, Product lines, quantities, and commercial terms as immutable snapshot.

Submitted PO:

- is never edited in place;
- may be cancelled only while no confirmed GoodsReceipt, SupplierInvoice, or SupplierPayment evidence exists;
- after downstream evidence exists, amendment/cancellation is not supported in MVP; later correction/return workflow or replacement PO is required as applicable.

### 8.3 GoodsReceipt (`PD-028`)

Confirmed physical GoodsReceipt evidence is immutable.

- PO received quantity follows confirmed physical GoodsReceipt evidence, not downstream Inventory-application success;
- Inventory application is separately tracked as `Pending/Applied/NeedsAttention` conceptually;
- erroneous confirmed receipt is corrected by a new explicit compensating/correction record referencing the original;
- derived received quantity uses net accepted receipt/correction evidence;
- original receipt is never destructively edited.

### 8.4 Supplier invoice/payment (`PD-029`)

MVP supports exactly one SupplierInvoice and one full SupplierPayment per PO.

SupplierInvoice:

- only after PO is fully received under MVP receipt policy;
- records supplier invoice reference/date, Money amount/currency, and related PO;
- exact match is required for automatic acceptance;
- variance requires explicit merchant approval and remains preserved as Accounting variance evidence.

SupplierPayment:

- only after invoice exists;
- full-payment evidence only in MVP;
- records payment date, Money amount, and external/reference text;
- is merchant attestation, not bank execution.

## 9. Accounting

CommerceOS accounting is a **learning/MVP bookkeeping model**, not a claim of statutory/tax/GAAP/IFRS/Vietnam-accounting compliance.

### 9.1 Core invariants

- posted journals are immutable;
- every journal balances debit == credit;
- corrections use new reversal/compensating entries;
- source fact identity is retained for idempotency/traceability;
- source replay cannot create a second logical posting.

### 9.2 Chart of accounts (`PD-038`)

Accounting is enabled automatically for every MVP Tenant with a minimal platform-defined learning chart including at least:

- Cash;
- Customer Deposits / Unearned Revenue;
- Sales Revenue;
- Inventory;
- COGS;
- Accounts Payable;
- GRNI;
- Purchase Price Variance;
- Inventory Adjustment Gain;
- Inventory Adjustment Loss/Expense.

Rules:

- required control-account semantic roles are platform-defined;
- merchants may add non-control accounts and edit display names where allowed;
- account identity/code is Tenant-unique;
- once referenced by posted journal, account identity/code is not reused;
- posted references remain immutable;
- non-required accounts may be deactivated, not hard-deleted;
- required control accounts cannot be deactivated while corresponding capability remains enabled.

### 9.3 Sale/revenue recognition (`PD-020`)

MVP is prepaid/cash commerce with no Accounts Receivable.

`PaymentCaptured` posts:

```text
Dr Cash
Cr Customer Deposits / Unearned Revenue
```

Whole-order `OrderFulfilled` is the single revenue-recognition trigger:

```text
Dr Customer Deposits / Unearned Revenue
Cr Sales Revenue
```

`OrderConfirmed` does not post revenue.

### 9.4 Inventory valuation and COGS (`PD-021`)

MVP uses moving weighted-average inventory cost as Accounting valuation truth, based on accepted Procurement receipt cost evidence.

Inventory owns quantity, not accounting value.

`StockIssued` is the single COGS trigger:

```text
Dr COGS
Cr Inventory
```

using immutable issued quantity + applicable Accounting weighted-average cost snapshot. `OrderFulfilled` must not create a second COGS posting for the same issue.

### 9.5 Procurement accounting (`PD-022`)

MVP uses GRNI:

Confirmed physical receipt/accounting acceptance:

```text
Dr Inventory
Cr GRNI
```

SupplierInvoiceRecorded:

```text
Dr GRNI
Cr Accounts Payable
```

Any approved invoice-versus-receipt difference is posted to Purchase Price Variance rather than rewriting receipt history.

SupplierPaymentRecorded:

```text
Dr Accounts Payable
Cr Cash
```

Each source fact is independently idempotent.

### 9.6 Stock-adjustment accounting (`PD-024`)

Every physical adjustment has an explicit reason.

Adjustment decrease:

```text
Dr Inventory Adjustment Loss/Expense
Cr Inventory
```

using applicable weighted-average cost.

Adjustment increase:

```text
Dr Inventory
Cr Inventory Adjustment Gain
```

using the approved valuation basis recorded/accepted for that adjustment. Catalog advisory cost is never implicit authority for this value.

Reservation changes are not physical adjustments and create no accounting posting. Administrative metadata correction creates no quantity/value posting.

### 9.7 Journal dates (`PD-039`)

- General Ledger and Trial Balance use `Journal EffectiveDate`.
- Automated journal EffectiveDate is the approved source business date interpreted in Tenant Business Profile IANA timezone.
- `PostingTimestamp` separately records when journal was actually committed.
- source occurrence timestamp, EffectiveDate, and PostingTimestamp remain distinct.
- manual journals in MVP use current Tenant business date only;
- user-selected past/future EffectiveDate and formal backdating are not supported until later period-control policy exists.

### 9.8 Refund/return accounting — deferred (`PD-023`)

No specific refund/return posting model is approved yet.

Mandatory interim rules:

- `PaymentRefunded`, Sales refund/cancellation, `StockReturned`, and `JournalPosted` remain separate facts;
- no refund event automatically implies stock, revenue, COGS, or journal effects not confirmed by owning domain;
- posted history remains immutable;
- future correction uses explicit compensating/reversal journals.

**HUMAN PRODUCT DECISION REQUIRED** before refund/return accounting implementation (`TASK-0050` refund portion / `TASK-0063`–`TASK-0066` or equivalent) becomes Ready.

## 10. Reporting

Reporting owns projections only; it is never transaction or entitlement authority.

### 10.1 Operational KPI definitions (`PD-030`)

For selected Tenant business-date window:

- **Order count** = count of `OrderConfirmed` facts;
- **AOV** = sum of authoritative OrderTotal snapshots for confirmed Orders / confirmed-order count;
- **Top products** = rank by sum of confirmed ordered whole-unit quantity by Product snapshot;
- **Failed-payment rate** = terminal definitive failed PaymentAttempts / all terminal PaymentAttempts; `OutcomeUnknown` excluded;
- **Operational Gross Sales** = sum of confirmed OrderTotal snapshots, explicitly labeled operational gross sales and never accounting revenue.

Cancellation/refund amounts are shown separately and do not rewrite original operational event counts.

### 10.2 Business date/corrections (`PD-031`)

Tenant Business Profile IANA timezone defines operational business-day boundaries.

Operational corrections/cancellations/refunds are attributed to the business date on which the correcting fact occurs and retain reference to original transaction. They do not rewrite original event date.

Financial reports use Accounting `Journal EffectiveDate`, not operational occurrence date.

## 11. Product Data Ingestion

### 11.1 Source governance (`PD-026`)

Base DataSource policy and policy-review approval are platform-owned.

- only authorized platform administrators may mark source policy review Current and globally enable/disable source operation;
- Tenant Owner/Admin may opt a globally approved/enabled source in/out for that Tenant;
- Tenant cannot override platform policy;
- review becomes stale on material source/API/terms/robots/authentication-policy change or explicit platform-reviewer action;
- no arbitrary time-based review expiry exists in MVP;
- acquisition requires both Current platform approval and Tenant enablement;
- subscription entitlement remains an independent additional gate where scheduled/automated ingestion is plan-controlled.

### 11.2 Source/candidate ownership

PDI owns:

- source identity/policy evidence;
- acquisition run history;
- immutable source snapshots;
- normalized candidate evidence;
- ImportCandidate lifecycle until Catalog accepts application.

Catalog owns canonical Product. Source changes never directly mutate Product.

`PD-040` one-to-one Tenant source-product mapping and candidate lifecycle are detailed in `catalog.md` and remain authoritative here as well.

## 12. Notification (`PD-032`)

Notification state is per recipient, never one shared Tenant flag.

```text
Unread ──Read──► Read
   └──Acknowledge──► Acknowledged
Read ──Acknowledge──► Acknowledged
```

- acknowledgement implies Read and is terminal only for notification acknowledgement;
- one recipient's action never changes another recipient's state;
- Owner/Admin receive Tenant-level critical security, billing, accounting, and operational exceptions;
- Staff receive operational notifications for capabilities they are allowed to act on;
- Viewer receives no actionable notifications in MVP;
- acknowledging a notification never resolves the source-domain exception.

Delivery success/failure remains Notification truth, not source-business outcome.

## 13. Audit (`PD-033`)

Audit stores append-oriented evidence for:

- successful and rejected privileged mutations;
- Membership/role/security administration;
- Accounting posting/correction actions;
- Subscription/platform-admin actions;
- security-significant Tenant-isolation denials.

Evidence includes actor, trusted Tenant, action, safe target identity where permitted, outcome, timestamp, correlation, and safe reason metadata.

Tenant Audit is readable by Owner/Admin only in MVP.

Tenant-visible denial evidence must not reveal another Tenant/entity's existence or identifiers. Protected platform-security evidence may retain additional investigation detail only where genuinely required.

Audit never becomes source domain state.

## 14. Customer/CRM

Customer/CRM owns explicit Tenant customer profiles/contact preferences when such profile is deliberately created/linked.

It does not own:

- guest Order snapshots;
- Sales order history truth;
- authentication identity;
- receivable ledger balances.

MVP guest checkout does not automatically create/match CRM Customer (`PD-035`).

## 15. Cross-domain operational sequence

Approved MVP commerce flow:

```text
Catalog sellable facts + authoritative price
            ↓
shopper reconfirms if price changed
            ↓
Sales OrderPlaced
            ↓
Inventory all-line reservation
            ↓
Payments full capture attempt
      ┌─────┴──────────────┐
      │                    │
 definitive no-commit   OutcomeUnknown
      │                    │
 retry allowed        stock remains held
      │              reconciliation required
      └──────► verified PaymentCaptured
                         ↓
                  Sales OrderConfirmed
                         ↓
                  Sales OrderAllocated
                         ↓
                 Inventory StockIssued
                         ↓
                  Sales OrderFulfilled
                         ↓
       Accounting revenue + COGS use distinct facts
                         ↓
                    Order Completed
```

The sequence expresses business dependencies only, not orchestration technology.

## 16. Business error families

### Sales

- `ORDER_LINE_NOT_SELLABLE`
- `ORDER_PRICE_CHANGED_RECONFIRM_REQUIRED`
- `CHECKOUT_INTENT_CONFLICT`
- `ORDER_ALLOCATION_INCOMPLETE`
- `ORDER_PAYMENT_OUTCOME_UNKNOWN`
- `ORDER_CANCELLATION_NOT_ALLOWED`
- `ORDER_STATE_TRANSITION_INVALID`

### Inventory

- `INSUFFICIENT_AVAILABLE_STOCK`
- `NEGATIVE_STOCK_NOT_ALLOWED`
- `ADJUSTMENT_WOULD_CONSUME_RESERVED_STOCK`
- `RESERVATION_ALREADY_TERMINAL`
- `RESERVATION_SOURCE_CONFLICT`
- `STOCK_MOVEMENT_ALREADY_APPLIED`
- `WAREHOUSE_OR_PRODUCT_REFERENCE_INVALID`

### Payments

- `PAYMENT_DECLINED`
- `PAYMENT_OUTCOME_UNKNOWN`
- `PAYMENT_OPERATION_CONFLICT`
- `PAYMENT_AMOUNT_MISMATCH`
- `PAYMENT_REFUND_EXCEEDS_CAPTURED`

### Procurement

- `SUPPLIER_NOT_ACTIVE`
- `PO_PRODUCT_NOT_PURCHASABLE`
- `PO_ALREADY_SUBMITTED_IMMUTABLE`
- `PO_CANCELLATION_NOT_ALLOWED`
- `GOODS_RECEIPT_CORRECTION_REQUIRED`
- `SUPPLIER_INVOICE_NOT_ELIGIBLE`
- `SUPPLIER_INVOICE_VARIANCE_APPROVAL_REQUIRED`

### Accounting

- `JOURNAL_UNBALANCED`
- `JOURNAL_ALREADY_POSTED`
- `SOURCE_POSTING_ALREADY_APPLIED`
- `CONTROL_ACCOUNT_CHANGE_FORBIDDEN`
- `MANUAL_EFFECTIVE_DATE_NOT_ALLOWED`
- `REFUND_ACCOUNTING_POLICY_UNRESOLVED`

Transport/status-code mapping remains a Technical Architecture concern.

## 17. Remaining human product decision

For the contexts in this document, all listed MVP policy decisions are resolved **except**:

- `PD-023` — refund/return accounting treatment.

Its safe interim constraints are encoded in section 9.8. Builders must not invent contra-revenue/restock/COGS reversal rules.

## 18. Downstream reconciliation handoff

### Technical Architect

The technical baseline was completed before these decisions were approved and must be reconciled where contracts/state/access patterns previously preserved alternatives. Preserve especially:

- price-reconfirmation round trip before placement;
- all-line reservation before full immediate capture;
- one Payment obligation with multiple attempts;
- `OutcomeUnknown` reconciliation with indefinite stock hold until evidence;
- zero-floor Inventory invariants and non-destructive adjustments;
- immutable submitted PO / receipt-correction evidence;
- GRNI + weighted-average valuation + distinct revenue/COGS triggers;
- Tenant-local business date versus journal posting timestamp;
- source-governance authority split;
- per-recipient Notification state and non-disclosing Audit.

No AWS/persistence/transport choice is made here.

### Backlog Planner

Reconcile candidate tasks to remove obsolete `PD-011`–`PD-022`, `PD-024`–`PD-042` planning gates where their product behavior is now approved. Keep refund/return accounting work gated by `PD-023`.

**Stop condition: DOMAIN BASELINE READY for approved Commerce Operations MVP semantics; HUMAN PRODUCT DECISION REQUIRED only for `PD-023` scope.**
