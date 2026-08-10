# CommerceOS Human Product-Decision Register

_Created by TASK-0087 on 2026-08-09. Extended for Subscription & Billing by TASK-0091 on 2026-08-10. Initial first-frontier decisions PD-001–PD-010 reviewed by the human Product Owner on 2026-08-10._

## 1. Rule

Every entry below is a material product, business-policy, or accounting-policy question that cannot be safely inferred from the current approved product documents.

Status vocabulary:

- **HUMAN PRODUCT DECISION REQUIRED** — no default is approved; listed candidate tasks cannot become Ready when the decision affects their scope.
- **Deferred** — intentionally postponed and does not block the stated current frontier, provided the safe interim constraint is respected.
- **Resolved** — a human-approved choice, mandatory rationale, approver, and approval date are recorded. Downstream baseline/task propagation must also be tracked explicitly and may remain pending until the responsible agent reconciles it.

All entries are owned by the CommerceOS product owner/human maintainer. A recommendation or common industry practice is not approval. Technical Architecture may preserve alternatives but may not resolve these questions through an AWS, persistence, API, or project-structure choice.

The safe interim constraint under each unresolved/deferred decision is mandatory until resolution. For a resolved decision, the recorded Decision and Rationale are authoritative product policy; downstream architecture and backlog work must preserve them unless a later human decision explicitly supersedes them.

A resolution without a rationale is incomplete. The rationale should explain why the selected option is appropriate now, the important trade-offs it accepts, and why rejected complexity is being deferred.

## 2. First-frontier decisions — Tenant, Merchant Access, and Catalog

### PD-001 — Membership cardinality and tenant selection

- **Status:** Resolved
- **Question:** May one authenticated merchant identity hold memberships in multiple tenants? If yes, how does the person intentionally select the active tenant and what prevents confused-deputy behavior?
- **Decision:** One authenticated identity may hold Memberships in multiple Tenants. If exactly one eligible Tenant exists the product may select it automatically; if multiple eligible Tenants exist the person must intentionally select the active Tenant. Every protected request uses trusted server-validated Tenant context derived from the selected Tenant plus an eligible Membership; a client-supplied TenantId is never authority by itself.
- **Rationale:** Multi-tenant membership is a natural B2B SaaS requirement for owners, operators, accountants, or support users who may work across businesses. Supporting it now avoids a later identity/authorization redesign. Explicit selection plus server-side Membership validation prevents confused-deputy and cross-tenant authorization mistakes.
- **Why material:** Subject identity alone cannot determine trusted Tenant context if membership is many-to-many.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require baseline/backlog reconciliation before Ready.
- **Affected tasks:** TASK-0007, TASK-0008, all protected tenant operations.
- **Approved policy constraint:** never infer Tenant from SubjectId alone and never let a client-supplied TenantId become authority.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-002 — Functional currency, precision, and rounding

- **Status:** Resolved
- **Question:** Is the initial product VND-only, or does each Tenant select one functional currency? What amount precision and rounding rule applies?
- **Decision:** CommerceOS MVP is VND-only. Every monetary value is still represented as Money with an explicit currency. Merchant-facing VND amounts use whole đồng with no fractional minor unit. CommerceOS MVP performs no currency conversion.
- **Rationale:** Multi-currency would immediately introduce exchange-rate policy, FX gain/loss, settlement, tax, accounting, and rounding complexity that is not required for the current learning/MVP scope. Keeping Money currency-aware avoids baking VND into primitive types and preserves a clean future path to multi-currency support.
- **Why material:** Tenant onboarding, Catalog price validation, Sales totals, supplier evidence, payment amounts, journals, and reports must use one consistent policy.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require baseline/backlog reconciliation before Ready.
- **Affected tasks:** TASK-0006, TASK-0010–0012, TASK-0024–0025, TASK-0032–0037, TASK-0041–0055.
- **Approved policy constraint:** Money always includes currency; no implicit conversion; VND-only is an explicit product policy rather than an implicit implementation assumption.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-003 — Initial roles, role cardinality, and authority matrix

- **Status:** Resolved
- **Question:** Does a Membership have one role or multiple roles? May there be multiple Owners? Which role may grant Admin/Owner, manage staff, manage/publish Catalog, and perform later sensitive actions?
- **Decision:** A Membership has exactly one role at a time in the MVP. Initial roles are Owner, Admin, Staff, and Viewer. A Tenant may have multiple Owners and an Active Tenant must retain at least one Active Owner. Owner has full merchant administration authority. Admin may manage ordinary merchant operations, staff, and Catalog but may not grant/revoke Owner authority or remove the last Active Owner. Staff may perform ordinary operational work allowed by the relevant domain policy but may not manage Memberships/roles or other tenant-administration concerns. Viewer is read-only. Catalog administration/publishing requires Owner or Admin in the MVP. Later sensitive capabilities may introduce narrower permissions through an explicit future product decision rather than multiple roles now.
- **Rationale:** One role per Membership gives the MVP a small, understandable authorization model and avoids prematurely building a custom RBAC/permission engine. Multiple Owners avoid a single-account administrative dead end. The role boundaries preserve a straightforward migration path toward permission-based/custom roles if product demand later justifies the complexity.
- **Why material:** Product wording says “roles” while candidate tasks usually assume one role, and “catalog manager” is not a defined role.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require baseline/backlog reconciliation before Ready.
- **Affected tasks:** TASK-0008–0013 and later authorization-sensitive tasks.
- **Approved policy constraint:** an Active Tenant retains an Active Owner; Viewer remains non-mutating; Owner changes cannot bypass the last-owner invariant.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-004 — Tenant suspension, reactivation, and closure behavior

- **Status:** Deferred until platform administration refinement
- **Question:** What can staff, shoppers, and support personnel read or do while a Tenant is Suspended? Is closure supported, and what retention/recovery behavior applies?
- **Interim human direction:** While Suspended, ordinary merchant mutations and public commerce are denied. Suspension does not delete Tenant data, Memberships, Subscription, Orders, accounting history, or other business evidence. Reactivation restores ordinary eligibility subject to all other independent business constraints. Tenant closure, deletion, retention, recovery windows, and privacy/legal erasure semantics remain deferred.
- **Rationale:** Minimal suspension semantics are needed to keep current authorization behavior deterministic, but closure/retention couples product policy to accounting, billing, audit, privacy, legal, and recovery requirements that are not needed at the present implementation frontier. Deferring those semantics avoids inventing irreversible lifecycle behavior too early.
- **Why material:** Tenant status can gate every domain and public storefront.
- **Decision gate:** before TASK-0068 or any earlier suspension capability becomes Ready.
- **Affected tasks:** TASK-0068 and later lifecycle/privacy work; TASK-0007 must at least reject ordinary Suspended-tenant work.
- **Safe interim constraint:** Suspended denies ordinary merchant/public commerce; it does not delete data or silently disable/reactivate Memberships.
- **Approved by:** CommerceOS human Product Owner for the interim direction only
- **Approved on:** 2026-08-10

### PD-005 — SKU requiredness, mutability, and reuse

- **Status:** Resolved
- **Question:** Is SKU required when Draft is created or only before publication? Is uniqueness case-sensitive and what merchant-visible normalization applies? May SKU change after publication? May an Archived Product's normalized SKU be reused?
- **Decision:** SKU is optional when a Draft Product is first created but is mandatory before first publication. SKU uniqueness is case-insensitive within a Tenant using a stable normalized representation. After the Product has been Published for the first time its SKU is immutable. A normalized SKU that has been used by an Archived Product is not reusable.
- **Rationale:** Allowing SKU-less Draft creation keeps merchant editing/import workflows flexible, while requiring a stable SKU before publication gives Inventory, Sales, integrations, and operational workflows a durable business reference. Immutability and non-reuse remove historical ambiguity and avoid later references resolving to a different Product.
- **Why material:** TASK-0010 omits SKU while TASK-0011 treats tenant-wide uniqueness as foundational; historical references and merchant operations differ by policy.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require baseline/backlog reconciliation before Ready.
- **Affected tasks:** TASK-0010, TASK-0011, TASK-0012, Sales snapshots, import mapping.
- **Approved policy constraint:** ProductId is immutable authority; any assigned SKU is normalized and tenant-unique; a SKU cannot change after first publication or be reused after archive.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-006 — Sellable/publication-required fields and zero-price products

- **Status:** Resolved
- **Question:** Must publication require positive price, Category, Brand, description, and/or media? Are free/zero-priced Products supported?
- **Decision:** Publication requires a valid Name, SKU, and Money value. Price may be zero. Category, Brand, description, and media are optional for publication. Stock is not a publication prerequisite; a Published Product may independently be out of stock/unavailable for sale.
- **Rationale:** Publication/catalog visibility and inventory availability are separate business concerns and should not be coupled. A small required-field set keeps the MVP usable and import-friendly. Allowing zero-price Products avoids introducing a special exception later for samples, free items, or future promotion use cases.
- **Why material:** TASK-0012 requires “valid sellable fields” but none are approved beyond broad product attributes.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require baseline/backlog reconciliation before Ready.
- **Affected tasks:** TASK-0012, TASK-0020–0025, import review.
- **Approved policy constraint:** publication requires valid Name, SKU, and Money; private/advisory cost is never public; stock is not a publication prerequisite.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-007 — Published editing and archive lifecycle

- **Status:** Resolved
- **Question:** Does editing a Published Product update the live projection immediately or create a draft revision requiring republish? Must a Published Product be explicitly unpublished before archive? Is Archived terminal or restorable?
- **Decision:** In the MVP, editing a Published Product updates the canonical Product and its public projection without creating a separate draft revision workflow. A Published Product may be Archived directly without an intermediate unpublish action. Archived is terminal in the MVP and an Archived Product cannot be restored or republished.
- **Rationale:** Draft-revision, approval, preview, scheduled publication, rollback, and restoration would turn Catalog into a CMS-style versioning system before the product needs it. Direct editing plus a terminal Archive state keeps lifecycle invariants small and clear while leaving versioned publishing as a future explicit capability.
- **Why material:** lifecycle transitions, public consistency, error behavior, and UI recovery depend on it.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require baseline/backlog reconciliation before Ready.
- **Affected tasks:** TASK-0012, TASK-0013, public Catalog tasks.
- **Approved policy constraint:** Archived is never publishable/restorable in the MVP; no hidden live-revision mechanism is implied.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-008 — Public Product address/slug policy

- **Status:** Resolved
- **Question:** Is a Product addressed by immutable id, mutable slug, or both? Who generates slugs, how are collisions handled, and do renamed slugs redirect?
- **Decision:** ProductId is the immutable canonical Product identity. A Published Product also has a tenant-scoped slug for its public URL. The product may propose a slug from the Product name and the merchant may edit it. Normalized slug must be unique within the Tenant. In the MVP, changing a slug does not require historical redirect support; ProductId remains unchanged and is never replaced by the slug as domain authority.
- **Rationale:** Stable immutable identity prevents rename/address changes from corrupting references, while a human-readable slug provides a better storefront URL than exposing only an opaque id. Tenant-scoped uniqueness is sufficient for a multi-tenant storefront and avoids prematurely building redirect history/SEO lifecycle machinery.
- **Why material:** the product definition shows `{productSlug}` but no domain owns its lifecycle.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require baseline/backlog reconciliation before Ready.
- **Affected tasks:** TASK-0012, TASK-0020–0022, SEO/custom-domain work.
- **Approved policy constraint:** ProductId remains immutable identity; slug is an address/presentation concern and never cross-tenant authority.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-009 — Category and Brand organization policy

- **Status:** Resolved
- **Question:** May a Product have one or multiple Categories? Are categories hierarchical? Are Category/Brand names unique? What happens to referenced Products when a reference is retired?
- **Decision:** In the MVP a Product may reference zero or one Category and zero or one Brand. Category is non-hierarchical. Category and Brand are Tenant-owned stable references whose normalized names are unique case-insensitively within the Tenant. A Category/Brand that is referenced by Products is not hard-deleted; it may be archived/retired while existing Product references remain historically intact.
- **Rationale:** Single-category/non-hierarchical classification is sufficient for the MVP and avoids early tree management, cycle handling, breadcrumb/SEO semantics, inheritance, and many-to-many filtering complexity. Stable ids and non-destructive retirement preserve historical references while leaving a clean migration path to ProductCategory associations and hierarchical taxonomy later.
- **Why material:** TASK-0011 asks for Category/Brand management without defining cardinality or retirement behavior.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require baseline/backlog reconciliation before Ready.
- **Affected tasks:** TASK-0011–0013, Storefront filters, imports.
- **Approved policy constraint:** Category/Brand are tenant-owned stable references; retirement never cascades destructive changes into Product history.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-010 — External media display and rights evidence

- **Status:** Resolved
- **Question:** May merchants display externally hosted media by attestation, only after explicit license evidence/source approval, or only use merchant-owned uploads? Is hotlinking allowed?
- **Decision:** Public Product media in the MVP must be provided through merchant uploads managed by CommerceOS. CommerceOS does not copy external binaries from arbitrary URLs and does not support external-media hotlinking for public Product media. The merchant remains responsible for having the rights to content they upload.
- **Rationale:** External URLs introduce availability, hotlink protection, tracking, mutable-content, performance, malware, and rights/licensing ambiguity outside CommerceOS control. Merchant-provided managed uploads create a simpler and more reliable product boundary; storage/CDN implementation remains a Technical Architecture decision rather than part of this product decision.
- **Why material:** a reachable URL is not evidence of display/copy rights, and TASK-0012 must define “policy-safe.”
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require baseline/backlog reconciliation before Ready.
- **Affected tasks:** TASK-0012, TASK-0018, Storefront/media work.
- **Approved policy constraint:** external binary copy/hotlink is not supported for public Product media in the MVP; source attribution/reference metadata for ingestion evidence remains a separate concern.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

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

## 6. Subscription & Billing decisions

The decisions in this section are intentionally unresolved by TASK-0091. The domain baseline defines ownership and safe invariants while these policy choices remain human gates.

### PD-043 — Subscription acquisition, trial, and tenant-without-subscription policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** After merchant registration, is subscription acquisition an automatic trial, explicit plan selection, platform provisioning, or another path? Is trial supported; if so, what duration, payment-method requirement, entitlement set, and expiry behavior apply? May a Tenant exist or perform ordinary commerce without an Active subscription?
- **Why material:** This determines whether subscription activation is part of onboarding, when the first entitlement set becomes effective, and what happens before/after a trial.
- **Decision gate:** before any subscription-acquisition/trial task or any onboarding task that couples Tenant activation to Subscription can become Ready.
- **Affected tasks:** TASK-0006–0009 when subscription-coupled, future Subscription & Billing acquisition/onboarding tasks, TASK-0092 architecture alternatives.
- **Safe interim constraint:** Tenant registration and Subscription activation remain separate business facts; do not create an automatic trial, assume a default paid plan, or assume ordinary commerce eligibility without an approved policy.

### PD-044 — Plan catalog, versioning, accepted terms, and commercial package policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which initial plans are actually offered, which prices/terms belong to each, how are versions identified and made available/retired, and may an accepted PlanVersion ever be edited versus replaced by a new version? Is Enterprise/custom pricing in scope?
- **Why material:** Starter/Growth/Business and any discussed prices are commercial hypotheses, while historical subscription meaning must survive future catalog changes.
- **Decision gate:** before plan-catalog management, plan selection, or plan-change tasks become Ready.
- **Affected tasks:** future plan/subscription tasks, entitlement-definition tasks, merchant subscription UI/history.
- **Safe interim constraint:** marketing plan names/prices are not domain constants outside Subscription & Billing; once a Tenant accepts terms, later catalog edits cannot retroactively rewrite the historical accepted terms or EntitlementSet.

### PD-045 — Billing-cycle and subscription-period policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Does the product support monthly, annual, both, or another billing cycle? How are billing anchors, effective period boundaries, renewal dates, leap/month-end behavior, and business timezone/date semantics defined?
- **Why material:** SubscriptionPeriod, renewal, cancellation timing, usage windows, and charges cannot share a stable meaning without an approved period policy.
- **Decision gate:** before recurring-period/renewal or cycle-sensitive usage/billing tasks become Ready.
- **Affected tasks:** future SubscriptionPeriod, renewal, metering, billing-history, cancellation, upgrade/downgrade tasks.
- **Safe interim constraint:** every effective subscription/entitlement period is explicit; no monthly or annual cadence is inferred from examples.

### PD-046 — CommerceOS SaaS currency, tax, invoice, and proration policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which currency/currencies may CommerceOS charge merchants in, what precision/rounding applies, are taxes included/added/ignored in the learning phase, what does “invoice” mean, and is proration supported for mid-period changes?
- **Why material:** These choices determine the commercial amount and legal/business meaning of PlatformCharge evidence and whether mid-cycle plan changes create immediate financial effects.
- **Decision gate:** before a task creates priced PlatformCharges, invoices, tax calculations, or proration.
- **Affected tasks:** future SaaS billing/charge/history tasks and upgrade/cancellation flows that depend on charge adjustments.
- **Safe interim constraint:** SaaS Money always carries currency; do not calculate tax, claim legally compliant invoicing, perform currency conversion, or prorate by convention.

### PD-047 — Upgrade effective-time and charge-precondition policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is an upgrade effective immediately, at the next cycle, or after another condition? If a charge is required, must verified settlement precede higher entitlements, may entitlements become effective before settlement, and how does proration interact with the transition?
- **Why material:** A plan-change request must not accidentally grant higher capabilities or create duplicate charges.
- **Decision gate:** before any upgrade execution task becomes Ready.
- **Affected tasks:** future plan-change, EntitlementSet effectivity, PlatformCharge, merchant UI/history tasks.
- **Safe interim constraint:** `PlanChangeRequested` does not grant higher entitlements; only the approved effective condition may produce a new effective EntitlementSet, and charge outcome remains a separate fact.

### PD-048 — Downgrade timing and excess-resource remediation policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is downgrade immediate or next-cycle? If current usage exceeds target limits, must the merchant remediate first, may excess resources be grandfathered/read-only, is temporary overage allowed, or is another policy used? Which owning-domain actions constitute accepted remediation?
- **Why material:** A lower plan may conflict with existing Memberships, Warehouses, ingestion capability, or other usage; forcing compliance by destructive cross-domain mutation would corrupt business state.
- **Decision gate:** before any downgrade execution/remediation task becomes Ready.
- **Affected tasks:** future subscription plan-change work plus entitlement-enforced Merchant Access, Inventory, Ingestion, and other domains.
- **Safe interim constraint:** if authoritative current usage exceeds a target hard limit, downgrade does not become effective; record blocked/remediation-required state. Never delete, disable, archive, or rewrite another context's business data merely to satisfy the target plan.

### PD-049 — Cancellation, expiry, grace, delinquency, reactivation, suspension, and retention policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Is cancellation immediate or end-of-cycle? What happens at expiry? Are grace/past-due states supported, for how long, and what becomes readable versus mutable? Can a subscription be reactivated, does billing delinquency ever restrict access, and how long is tenant data retained/recoverable after subscription end?
- **Why material:** Commercial subscription state, billing standing, TenantStatus, Membership access, and data retention are independent dimensions and cannot safely be collapsed.
- **Decision gate:** before cancellation/expiry/delinquency/reactivation/restriction implementation or subscription-driven retention work becomes Ready.
- **Affected tasks:** future lifecycle/recovery/support tasks and any ordinary-operation gating based on subscription state.
- **Safe interim constraint:** cancellation request or billing failure/unknown outcome never deletes tenant data, disables Memberships, or mutates TenantStatus by implication; timeout/age alone never proves delinquency or termination.

### PD-050 — Hard, soft, overage, unlimited, and enforcement-point policy

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** For each entitlement/limit, is it a hard block, warning/soft threshold, allowed overage, grace state, or operationally unlimited? At which owning-domain command does enforcement occur, and what read/recovery operations remain allowed while over limit?
- **Why material:** Staff, Warehouse, ingestion, API/webhook, accounting/reporting, and future limits have different operational consequences; one generic “limit exceeded” convention would be unsafe.
- **Decision gate:** before any entitlement-enforced write task becomes Ready for the affected capability.
- **Affected tasks:** future Subscription/Entitlement work and every domain task that enforces a plan capability/limit.
- **Safe interim constraint:** use trusted capability/limit decisions rather than plan-name checks; a stale UI/Reporting projection never authorizes a hard-limit write; no task silently destroys data or blocks recovery access to enforce a limit.

### PD-051 — Order-volume limit and shopper-checkout behavior

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** How is order volume counted, what time/window/eligibility rules apply, and may reaching/exceeding a plan threshold ever reject shopper checkout? If not, is the threshold warning-only, overage-billed, or an operational follow-up signal?
- **Why material:** Blocking public checkout due to a merchant subscription threshold directly harms the merchant's customer flow and must be an explicit product choice.
- **Decision gate:** before an order-volume entitlement can affect checkout or before billing depends on metered order volume.
- **Affected tasks:** future UsageMeter/order-volume work, Sales checkout, Reporting/platform-admin usage views.
- **Safe interim constraint:** an order-volume threshold must not silently reject an otherwise valid shopper checkout; accepted Sales facts may be counted idempotently for visibility without authorizing an unapproved block/overage effect.

### PD-052 — SaaS billing-provider strategy for learning/MVP and later real operation

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** For the learning/MVP phase, is CommerceOS SaaS billing domain-only with no execution, simulated through a dedicated billing seam, or connected to a real provider? What is the later provider-selection/transition intent?
- **Why material:** Technical Architecture must know whether to design only the provider boundary or also near-term execution/reconciliation; choosing Stripe/Paddle/etc. is explicitly out of TASK-0091 scope.
- **Decision gate:** before any real/simulated SaaS billing-provider execution task becomes Ready.
- **Affected tasks:** TASK-0092 provider-boundary choices, future PlatformCharge/billing attempt/webhook/reconciliation tasks.
- **Safe interim constraint:** remain provider-agnostic; do not route SaaS billing through the existing merchant-order Payments/Mock Payment Provider by convenience; do not store real card data; external timeout/duplicate/out-of-order evidence retains explicit unknown/reconciliation semantics.

### PD-053 — Platform-admin subscription/billing support and override authority

- **Status:** HUMAN PRODUCT DECISION REQUIRED
- **Question:** Which platform administrators, if any, may manually assign/comp/change a plan, cancel/reactivate a subscription, adjust billing evidence, or override an entitlement/usage restriction? Which actions require tenant notice/consent and what audit evidence is mandatory?
- **Why material:** Platform support visibility is in product scope, but a hidden cross-tenant override path would bypass subscription truth and tenant/security controls.
- **Decision gate:** before any platform-admin mutation/support tool for Subscription & Billing becomes Ready.
- **Affected tasks:** future platform-admin subscription/billing operations, Audit, support/recovery tooling.
- **Safe interim constraint:** platform administrators may have approved visibility/projections only; no direct subscription/entitlement/charge mutation or tenant-wide bypass is assumed. Any later mutation uses explicit Subscription & Billing business commands and Audit evidence under approved authority.

## 7. Resolution template

When the human resolves an entry, replace its status and append the complete resolution record below. **Rationale is mandatory**; a Decision without a Rationale is not a complete product resolution.

```text
Decision:
Rationale (why this option, important trade-offs, and why deferred complexity is not needed now):
Approved by:
Approved on:
Affected baseline documents updated:
Affected candidate tasks notified:
```

If baseline/task propagation cannot be completed in the same change, record it explicitly as `Pending <responsible role> reconciliation` rather than claiming it is updated. The product decision itself may be Resolved while propagation is pending, but an affected candidate task does not become Ready until the responsible architecture/domain/backlog reconciliation has been completed.

The Technical Architect and Backlog Planner then determine architecture/task consequences. Resolution does not by itself make a candidate task Ready.
