# CommerceOS Human Product-Decision Register

_Created by TASK-0087 on 2026-08-09._

## 1. Rule

Every entry below is a material product, business-policy, or accounting-policy question that cannot be safely inferred from the current approved product documents.

Status vocabulary:

- **HUMAN PRODUCT DECISION REQUIRED** — no default is approved; listed candidate tasks cannot become Ready when the decision affects their scope.
- **Deferred** — intentionally postponed and does not block the stated current frontier, provided the safe interim constraint is respected.
- **Resolved** — human-approved choice, rationale, and approval date are recorded and affected domain documents are updated.

All entries are owned by the CommerceOS product owner/human maintainer. A recommendation or common industry practice is not approval. Technical Architecture may preserve alternatives but may not resolve these questions through an AWS, persistence, API, or project-structure choice.

The safe interim constraint under each decision is mandatory until resolution.

## 2. First-frontier decisions — Tenant, Merchant Access, and Catalog

### PD-001 — Membership cardinality and tenant selection

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** May one authenticated merchant identity hold memberships in multiple tenants? If yes, how does the person intentionally select the active tenant and what prevents confused-deputy behavior?
- **Why material:** Subject identity alone cannot determine trusted Tenant context if membership is many-to-many.
- **Decision gate:** before TASK-0007 can become Ready.
- **Affected tasks:** TASK-0007, TASK-0008, all protected tenant operations.
- **Safe interim constraint:** never infer Tenant from SubjectId alone and never let a client-supplied TenantId become authority.

### PD-002 — Functional currency, precision, and rounding

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is the initial product VND-only, or does each Tenant select one functional currency? What amount precision and rounding rule applies?
- **Why material:** Tenant onboarding, Catalog price validation, Sales totals, supplier evidence, payment amounts, journals, and reports must use one consistent policy.
- **Decision gate:** before TASK-0006 and TASK-0010 can become Ready.
- **Affected tasks:** TASK-0006, TASK-0010–0012, TASK-0024–0025, TASK-0032–0037, TASK-0041–0055.
- **Safe interim constraint:** Money always includes currency; no implicit conversion; do not hard-code VND from examples.

### PD-003 — Initial roles, role cardinality, and authority matrix

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Does a Membership have one role or multiple roles? May there be multiple Owners? Which role may grant Admin/Owner, manage staff, manage/publish Catalog, and perform later sensitive actions?
- **Why material:** Product wording says “roles” while candidate tasks usually assume one role, and “catalog manager” is not a defined role.
- **Decision gate:** before TASK-0008, TASK-0011, TASK-0012, or TASK-0013 can become Ready.
- **Affected tasks:** TASK-0008–0013 and later authorization-sensitive tasks.
- **Safe interim constraint:** an Active Tenant retains an Active Owner, Viewer remains non-mutating, and no role name grants a capability without the approved matrix; Catalog/staff tasks remain blocked rather than assume Owner/Admin mappings.

### PD-004 — Tenant suspension, reactivation, and closure behavior

- **Status:** Deferred until platform administration refinement
- **Question:** What can staff, shoppers, and support personnel read or do while a Tenant is Suspended? Is closure supported, and what retention/recovery behavior applies?
- **Why material:** Tenant status can gate every domain and public storefront.
- **Decision gate:** before TASK-0068 or any earlier suspension capability becomes Ready.
- **Affected tasks:** TASK-0068 and later lifecycle/privacy work; TASK-0007 must at least reject ordinary Suspended-tenant work.
- **Safe interim constraint:** Suspended denies ordinary merchant/public commerce; it does not delete data or silently disable/reactivate Memberships.

### PD-005 — SKU requiredness, mutability, and reuse

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is SKU required when Draft is created or only before publication? Is uniqueness case-sensitive and what merchant-visible normalization applies? May SKU change after publication? May an Archived Product's normalized SKU be reused?
- **Why material:** TASK-0010 omits SKU while TASK-0011 treats tenant-wide uniqueness as foundational; historical references and merchant operations differ by policy.
- **Decision gate:** before TASK-0010/0011 can become Ready.
- **Affected tasks:** TASK-0010, TASK-0011, TASK-0012, Sales snapshots, import mapping.
- **Safe interim constraint:** ProductId is immutable authority; any assigned SKU is normalized and tenant-unique; no task assumes reuse.

### PD-006 — Sellable/publication-required fields and zero-price products

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Must publication require positive price, Category, Brand, description, and/or media? Are free/zero-priced Products supported?
- **Why material:** TASK-0012 requires “valid sellable fields” but none are approved beyond broad product attributes.
- **Decision gate:** before TASK-0012 can become Ready.
- **Affected tasks:** TASK-0012, TASK-0020–0025, import review.
- **Safe interim constraint:** publication always requires valid name, SKU, and Money; private/advisory cost is never public; stock is not a publication prerequisite.

### PD-007 — Published editing and archive lifecycle

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Does editing a Published Product update the live projection immediately or create a draft revision requiring republish? Must a Published Product be explicitly unpublished before archive? Is Archived terminal or restorable?
- **Why material:** lifecycle transitions, public consistency, error behavior, and UI recovery depend on it.
- **Decision gate:** before TASK-0012/0013 can become Ready.
- **Affected tasks:** TASK-0012, TASK-0013, public Catalog tasks.
- **Safe interim constraint:** Archived is never publishable; no unapproved restoration/live-revision mechanism may be invented.

### PD-008 — Public Product address/slug policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is a Product addressed by immutable id, mutable slug, or both? Who generates slugs, how are collisions handled, and do renamed slugs redirect?
- **Why material:** the product definition shows `{productSlug}` but no domain owns its lifecycle.
- **Decision gate:** before TASK-0012 public projection contract or TASK-0020 becomes Ready.
- **Affected tasks:** TASK-0012, TASK-0020–0022, SEO/custom-domain work.
- **Safe interim constraint:** ProductId remains immutable identity; no slug is treated as authority across tenants.

### PD-009 — Category and Brand organization policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** May a Product have one or multiple Categories? Are categories hierarchical? Are Category/Brand names unique? What happens to referenced Products when a reference is retired?
- **Why material:** TASK-0011 asks for Category/Brand management without defining cardinality or retirement behavior.
- **Decision gate:** before TASK-0011 can become Ready.
- **Affected tasks:** TASK-0011–0013, Storefront filters, imports.
- **Safe interim constraint:** Category/Brand are tenant-owned stable references; no delete/archive/retire or cascading Product behavior is implemented until approved.

### PD-010 — External media display and rights evidence

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** May merchants display externally hosted media by attestation, only after explicit license evidence/source approval, or only use merchant-owned uploads? Is hotlinking allowed?
- **Why material:** a reachable URL is not evidence of display/copy rights, and TASK-0012 must define “policy-safe.”
- **Decision gate:** before external media is public under TASK-0012.
- **Affected tasks:** TASK-0012, TASK-0018, Storefront/media work.
- **Safe interim constraint:** no external binary is copied or republished without explicit permission; preserve source attribution/reference metadata.

### PD-034 — Registration admission, uniqueness, and Business Profile minimum

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is registration open self-service, invite/claim based, or platform-provisioned? Which verified person becomes initial Owner? Is duplicate-business detection required beyond retry idempotency, and which Business Profile fields are mandatory?
- **Why material:** “a business can register” does not define admission or a safe real-business uniqueness key; TASK-0006 cannot invent legal/company requirements.
- **Decision gate:** before TASK-0006 can become Ready.
- **Affected tasks:** TASK-0006–0008.
- **Safe interim constraint:** successful onboarding is one complete Active Tenant + initial Active Owner outcome; retry of the same intent cannot create another Tenant; no unverified legal/tax uniqueness claim is used.

### PD-036 — Invitation recipient binding and duplicate-invitation policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Does acceptance bind to a verified email claim, a pre-bound external SubjectId, or another verified identifier? How do aliases/identity changes behave, and what happens for duplicate pending invitations, resend, or an existing Disabled Membership?
- **Why material:** “the intended user accepts” is a security/business rule, not an implementation detail.
- **Decision gate:** before TASK-0008 can become Ready.
- **Affected tasks:** TASK-0008 and invitation notification work.
- **Safe interim constraint:** an invitation is single-use, tenant-bound, and cannot activate an unmatched subject or silently reactivate a Disabled Membership.

### PD-037 — Product specification and public-field policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Are specification names unique, may values carry units/multiple entries, how are they ordered, and are any specifications private? Is SKU or source attribution public, and which canonical fields form `PublicProduct`?
- **Why material:** TASK-0010 needs ProductSpecification semantics and TASK-0012 must define public exposure without leaking private/advisory fields.
- **Decision gate:** before TASK-0010 and TASK-0012 can become Ready.
- **Affected tasks:** TASK-0010–0013, Storefront, ingestion import review.
- **Safe interim constraint:** a specification is at least merchant-approved name/value data; public projection excludes advisory cost, raw/source-review data, internal history, and private merchant metadata.

## 3. Checkout, Sales, Inventory, and Payment decisions

### PD-011 — Checkout repricing behavior

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** If the authoritative current price differs from the storefront estimate, does checkout accept the new price, reject and require shopper confirmation, or use another tolerance rule?
- **Decision gate:** before TASK-0025 can become Ready.
- **Affected tasks:** TASK-0023, TASK-0025, Storefront checkout UX.
- **Safe interim constraint:** client price never overrides server-resolved price and no order is partially placed.

### PD-012 — Sellable quantity and unit-of-measure policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Are initial Product, Order, Reservation, Receipt, and Issue quantities positive whole units only, or are decimal quantities/units of measure supported?
- **Decision gate:** before TASK-0025 and TASK-0028 can become Ready.
- **Affected tasks:** TASK-0025, TASK-0028–0031, TASK-0041–0042, returns.
- **Safe interim constraint:** quantities are positive and use one explicitly approved unit semantics; no implicit fractional rounding.

### PD-013 — Manual discount ownership and limits

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which actors may apply manual discounts, to which order/channel, by fixed amount or percentage, and under what limits? Can guest checkout ever submit one?
- **Decision gate:** before discount behavior in TASK-0025 can become Ready.
- **Affected tasks:** TASK-0025, TASK-0027, Pricing/Promotion tasks.
- **Safe interim constraint:** Pricing owns the rule, Sales owns only the accepted snapshot; untrusted guest input cannot grant a discount.

### PD-014 — Canonical order dimensions and reserve/pay/confirm sequence

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** What is the initial business sequence among OrderPlaced, stock hold/reservation, payment authorization/capture, OrderConfirmed, and OrderAllocated? How are commercial, payment, fulfillment, and refund states presented without conflation?
- **Why material:** TASK-0031 allocates only Confirmed orders, while payment flow/tasks reserve before capture/confirmation.
- **Decision gate:** before TASK-0024, TASK-0031, or TASK-0034 can become Ready.
- **Affected tasks:** TASK-0024–0027, TASK-0031, TASK-0034–0040.
- **Safe interim constraint:** OrderAllocated means all required Inventory reservations are accepted; Payment OutcomeUnknown is nonterminal; no state may claim an independent effect that has not occurred.

### PD-042 — Order cancellation and completion policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** From which commercial/payment/fulfillment stages may an Order be cancelled, who may do so, and which separate refund/release effects are then required? What exact business trigger moves Fulfilled to Completed, and is completion automatic or a merchant action?
- **Why material:** TASK-0024 needs a valid state machine, TASK-0027 performs cancellation, and later Inventory/Payment effects must not be implied before they occur.
- **Decision gate:** before TASK-0024, TASK-0027, TASK-0031, or TASK-0034 can become Ready.
- **Affected tasks:** TASK-0024, TASK-0027, TASK-0031, TASK-0034–0040.
- **Safe interim constraint:** cancellation never asserts that required stock release/refund succeeded; Completed is not emitted without an approved trigger.

### PD-015 — Multi-line allocation and fulfillment

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is initial allocation all-or-nothing, or may an Order be partially allocated/backordered? Is issue/fulfillment whole-order only or partial by line/quantity?
- **Decision gate:** before TASK-0031 can become Ready.
- **Affected tasks:** TASK-0030, TASK-0031, returns/refunds.
- **Safe interim constraint:** do not report OrderAllocated/Fulfilled until every required line has the corresponding accepted Inventory fact; partial effects remain visible/recoverable, never hidden.

### PD-016 — Payment model and capture policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is there one Payment per Order with multiple attempts, or a Payment per attempt? Does MVP immediately capture, or authorize then capture later? Are multiple/partial tenders explicitly excluded?
- **Decision gate:** before TASK-0032–0034 can become Ready.
- **Affected tasks:** TASK-0032–0038, Sales/payment/accounting integration.
- **Safe interim constraint:** provider attempts do not become separate order obligations accidentally; captured amount cannot exceed the accepted Sales amount.

### PD-017 — Definitive no-capture, decline, retry, and order effect

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which verified provider outcomes prove no commit versus remain transient/unknown? After a definitive decline/no-commit, may the same Order accept another attempt/method, or does it enter a terminal PaymentFailed/cancelled path?
- **Decision gate:** before TASK-0033, TASK-0034, or TASK-0037 can become Ready.
- **Affected tasks:** TASK-0033–0037, Sales operations.
- **Safe interim constraint:** a stable decline/no-commit is not blindly retried as if transient and ends only the attempt unless human policy says it closes the Order payment path; transport failure never proves it.

### PD-018 — Ambiguous payment stock hold and escalation

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** How long does an Order with Payment OutcomeUnknown retain reserved stock? Is there any permitted automatic expiry, and what human escalation/override is allowed?
- **Decision gate:** before TASK-0034/0037 and any reservation-expiry feature can become Ready.
- **Affected tasks:** TASK-0034–0038, TASK-0030–0031, operational recovery.
- **Safe interim constraint:** age/timeout alone never proves payment failure and never authorizes unsafe retry, cancellation, or release.

### PD-019 — Low-stock policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is low stock based on Available or OnHand, per warehouse or tenant total, with what default/disabled threshold and crossing/reset behavior?
- **Decision gate:** before TASK-0031 low-stock behavior can become Ready.
- **Affected tasks:** TASK-0031, TASK-0054/0056.
- **Safe interim constraint:** low-stock remains a derived indicator and cannot change balances.

### PD-041 — Negative stock, backorder, reservation floor, and adjustment interaction

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** May OnHand or Available become negative? Is backorder supported? What availability floor must reservation enforce, and may an adjustment decrease consume quantity already reserved?
- **Why material:** the accepted invariant defines `Available = OnHand - Reserved` but explicitly requires negative-stock behavior to be a product decision.
- **Decision gate:** before TASK-0028–0031 can become Ready.
- **Affected tasks:** TASK-0028–0031, checkout/allocation, adjustments, future backorders.
- **Safe interim constraint:** no zero-floor or negative-stock policy is implemented by assumption; Reserved cannot be negative and every accepted effect remains auditable/idempotent.

### PD-035 — Guest customer data, matching, and privacy lifecycle

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which contact/address fields are minimally required per order, when may a guest be linked to or create a tenant Customer profile, what constitutes a duplicate profile, and how do retention/anonymization rules preserve required order/accounting history?
- **Decision gate:** before TASK-0026 can become Ready.
- **Affected tasks:** TASK-0026, TASK-0069, TASK-0083, guest order access.
- **Safe interim constraint:** Sales owns the immutable minimum order snapshot; CRM owns editable profiles; checkout does not require/create a shopper account by implication; no broad PII or cross-tenant matching.

## 4. Procurement and accounting-policy decisions

### PD-020 — Sale/revenue recognition and Cash versus AR

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which single operational fact recognizes a sale/revenue? Does captured mock payment post Cash, or is Accounts Receivable used, and under what order/payment timing?
- **Why material:** current tasks permit PaymentCaptured or an unspecified sale-recognition fact, risking duplicate revenue.
- **Decision gate:** before TASK-0044 and TASK-0050 can become Ready.
- **Affected tasks:** TASK-0044, TASK-0049, TASK-0050, financial reporting.
- **Safe interim constraint:** no posting consumes both PaymentCaptured and OrderConfirmed for the same logical sale.

### PD-021 — Inventory valuation and COGS trigger

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which context owns the immutable cost snapshot, which valuation method applies, and does one StockIssued or OrderFulfilled fact trigger COGS?
- **Why material:** Catalog cost reference is advisory, Inventory currently owns quantity only, and candidate tasks permit two trigger alternatives.
- **Decision gate:** before TASK-0044/0051 can become Ready.
- **Affected tasks:** TASK-0028–0031, TASK-0042, TASK-0044, TASK-0051, profit reporting.
- **Safe interim constraint:** Catalog cost reference cannot be posted as COGS; exactly one logical source fact may create the effect.

### PD-022 — Accounting recognition for procurement and supplier settlement

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Given Procurement evidence approved under `PD-029`, is inventory/AP recognized at GoodsReceipt, SupplierInvoice, or through an interim received-not-invoiced concept? How are PO/receipt/invoice variance and supplier-settlement Cash/AP posted?
- **Decision gate:** before TASK-0044 or TASK-0051 can become Ready.
- **Affected tasks:** TASK-0044, TASK-0051–0052.
- **Safe interim constraint:** GoodsReceiptRecorded, StockReceived, SupplierInvoiceRecorded, and SupplierPaymentRecorded remain distinct facts; no pair creates duplicate Inventory/AP effects.

### PD-023 — Refund/return accounting

- **Status:** Deferred until returns/refunds refinement, then HUMAN PRODUCT DECISION REQUIRED
- **Question:** Does refund use contra revenue or reversal, on what recognition date, and how do restock/non-restock outcomes affect COGS and inventory?
- **Decision gate:** before TASK-0050 refund posting or TASK-0063–0066 can become Ready.
- **Affected tasks:** TASK-0050, TASK-0063–0066, reports.
- **Safe interim constraint:** PaymentRefunded, Order refund state, StockReturned, and JournalPosted are separate facts; none implies all others.

### PD-024 — Financial treatment of stock adjustments

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which adjustment reasons create accounting effects, and which gain/expense accounts/policies apply?
- **Decision gate:** before adjustment posting in TASK-0051 can become Ready.
- **Affected tasks:** TASK-0029, TASK-0044, TASK-0051.
- **Safe interim constraint:** every adjustment has an explicit reason; not every physical correction is assumed to have the same accounting treatment.

### PD-038 — Chart of accounts and account lifecycle policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is Accounting enabled automatically or opt-in, which account template/types/codes and control accounts are required, what may the merchant customize, are codes unique/reusable, and when may a referenced/control account be deactivated?
- **Decision gate:** before TASK-0044 or TASK-0045 can become Ready.
- **Affected tasks:** TASK-0044–0052, financial reporting.
- **Safe interim constraint:** no jurisdictional/certified chart is implied; a posted journal's account references remain historically stable and required control-account policy cannot be bypassed by deletion.

### PD-039 — Journal effective date, posting date, and backdating

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which accounting/effective date drives General Ledger and Trial Balance, how does it relate to source occurrence and posting time, is backdating allowed, and what period/open-date rules apply before formal period close exists?
- **Decision gate:** before TASK-0044, TASK-0045, or TASK-0047 can become Ready.
- **Affected tasks:** TASK-0044–0047, TASK-0050–0055.
- **Safe interim constraint:** preserve source occurrence, accounting/effective, and posting timestamps distinctly; do not silently use processing/server time as ledger policy.

### PD-025 — Submitted purchase-order amendment/cancellation

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** What does Submitted mean, may a submitted PO be amended/cancelled, and what explicit correction evidence is required?
- **Decision gate:** before TASK-0041 can become Ready.
- **Affected tasks:** TASK-0041–0043.
- **Safe interim constraint:** submitted supplier/line/commercial snapshot is not silently edited.

### PD-027 — Supplier and PO-line eligibility

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** What minimum Supplier identity/status/uniqueness is required? May archived/unpublished Products be purchased? Are tax, freight, and line/order discounts excluded initially?
- **Decision gate:** before TASK-0041 can become Ready.
- **Affected tasks:** TASK-0041–0043.
- **Safe interim constraint:** Supplier and Product references are tenant-local and immutable in a submitted PO snapshot; no tax/freight behavior is inferred.

### PD-028 — Goods-receipt confirmation and correction

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Does PO “received” follow physical GoodsReceipt confirmation or successful Inventory application? How is an erroneous confirmed receipt corrected without destructive mutation?
- **Decision gate:** before TASK-0042 can become Ready.
- **Affected tasks:** TASK-0042, TASK-0051, TASK-0084.
- **Safe interim constraint:** physical receipt remains immutable evidence; Inventory application has honest Pending/Applied/NeedsAttention state and may not erase the receipt.

### PD-029 — Supplier invoice and payment evidence

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is there exactly one invoice/full payment per initial PO, may invoice precede receipt, what matching tolerance applies, and which amount/date/reference evidence is required?
- **Decision gate:** before TASK-0043 can become Ready.
- **Affected tasks:** TASK-0043, TASK-0051, Accounts Payable reporting.
- **Safe interim constraint:** use `SupplierPaymentRecorded` for merchant attestation; never imply CommerceOS executed bank payment.

## 5. Product Data Ingestion, Reporting, Notification, and Audit decisions

### PD-026 — Source governance, policy review, and operating authority

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is DataSource policy platform-owned, tenant-owned, or shared with tenant overlays? Who may perform/approve policy review, activate/pause/disable a source, and what makes a review stale or require renewal?
- **Decision gate:** before TASK-0014 can become Ready.
- **Affected tasks:** TASK-0014–0017, TASK-0059–0062.
- **Safe interim constraint:** policy-review validity and operating status are separate; acquisition is blocked without current approval even if a source was previously Active.

### PD-040 — External mapping cardinality and ImportCandidate lifecycle

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** May one external source product map to several canonical Products within a Tenant? What exact Ready/approve/apply/reject/supersede/expire transitions apply, and does a newer snapshot stale an older candidate?
- **Decision gate:** before TASK-0018 can become Ready.
- **Affected tasks:** TASK-0018, TASK-0059–0062.
- **Safe interim constraint:** source snapshots/candidates remain Ingestion-owned; mappings/canonical changes remain Catalog-owned; merchant approval does not mean Product changed, and only confirmed Catalog acceptance makes a candidate Applied.

### PD-030 — Operational commerce KPI definitions

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** What operational facts/formulas define order count, AOV, top products, and failed-payment rate, including cancellation/refund treatment and denominator? If a non-ledger “gross sales” metric is desired, how is it labeled distinctly from accounting revenue?
- **Decision gate:** before TASK-0054 can become Ready.
- **Affected tasks:** TASK-0054, TASK-0056.
- **Safe interim constraint:** no operational metric label is published without approved numerator, denominator, eligibility, source facts, and freshness; financial revenue/gross profit inherit `PD-020`, `PD-021`, `PD-038`, and `PD-039` plus ledger account grouping.

### PD-031 — Reporting business date and correction attribution

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which Tenant timezone defines operational business day, and are refunds/corrections attributed to occurrence date or original transaction date in each report? Financial reports must first follow journal effective-date policy `PD-039`.
- **Decision gate:** before time-bucketed TASK-0054/0055 reports become Ready.
- **Affected tasks:** TASK-0053–0056.
- **Safe interim constraint:** preserve event/effective timestamps and do not silently use infrastructure/server timezone as business policy.

### PD-032 — Notification audience, read, and acknowledgement semantics

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which roles/users receive each selected business/operational outcome? Is state per recipient or shared tenant audience? Are Unread, Read, and Acknowledged distinct, who may cause each transition, are they terminal/regressible, and does one actor affect others?
- **Decision gate:** before TASK-0056 notification behavior can become Ready.
- **Affected tasks:** TASK-0056, TASK-0085.
- **Safe interim constraint:** acknowledgement never resolves the underlying source exception or transaction.

### PD-033 — Audit coverage, readers, and security-denial visibility

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which privileged successes/rejections require Audit evidence, which tenant roles may read it, and what details of attempted cross-tenant targets may be tenant-visible?
- **Decision gate:** before TASK-0009 can become Ready.
- **Affected tasks:** TASK-0009 and every later privileged domain action.
- **Safe interim constraint:** Audit retains actor/trusted-tenant/action/outcome/correlation safely; tenant-visible evidence never discloses another tenant's entity existence or sensitive input.

## 6. Resolution template

When the human resolves an entry, replace its status and append:

```text
Decision:
Rationale:
Approved by:
Approved on:
Affected baseline documents updated:
Affected candidate tasks notified:
```

The Technical Architect and Backlog Planner then determine architecture/task consequences. Resolution does not by itself make a candidate task Ready.
