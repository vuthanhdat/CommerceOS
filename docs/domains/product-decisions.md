# CommerceOS Human Product-Decision Register

_Created by TASK-0087 on 2026-08-09. Extended for Subscription & Billing by TASK-0091 on 2026-08-10. PD-001–PD-010 were explicitly reviewed by the human Product Owner on 2026-08-10. The remaining register was resolved or intentionally deferred on 2026-08-10 under the Product Owner's delegated instruction to process the remaining decisions using the same MVP-first principles._

## 1. Rule

Every entry below is a material product, business-policy, or accounting-policy question that cannot be safely inferred from the current approved product documents.

Status vocabulary:

- **HUMAN PRODUCT DECISION REQUIRED** — no default is approved; listed candidate tasks cannot become Ready when the decision affects their scope.
- **Deferred** — intentionally postponed and does not block the stated current frontier, provided the recorded interim constraint is respected.
- **Resolved** — a human-approved or human-delegated choice, mandatory rationale, approver/authority, and approval date are recorded. Downstream baseline/task propagation must also be tracked explicitly and may remain pending until the responsible agent reconciles it.

All entries are owned by the CommerceOS Product Owner/human maintainer. A recommendation or common industry practice is not approval unless the human explicitly delegates resolution authority for that pass. Technical Architecture may preserve alternatives but may not resolve product-policy questions through an AWS, persistence, API, provider, or project-structure choice.

The safe interim constraint under each unresolved/deferred decision is mandatory until resolution. For a resolved decision, the recorded **Decision** and **Rationale** are authoritative product policy until a later human decision explicitly supersedes them.

A resolution without a rationale is incomplete. The rationale must explain why the selected option is appropriate now, the important trade-offs it accepts, and why rejected complexity is being deferred.

Unless an entry says otherwise, resolved decisions below still require **Domain Architect / Technical Architect / Backlog Planner reconciliation** before affected candidate tasks may become Ready.

## 2. First-frontier decisions — Tenant, Merchant Access, and Catalog

### PD-001 — Membership cardinality and tenant selection

- **Status:** Resolved
- **Question:** May one authenticated merchant identity hold memberships in multiple tenants? If yes, how does the person intentionally select the active tenant and what prevents confused-deputy behavior?
- **Decision:** One authenticated identity may hold Memberships in multiple Tenants. If exactly one eligible Tenant exists the product may select it automatically; if multiple eligible Tenants exist the person must intentionally select the active Tenant. Every protected request uses trusted server-validated Tenant context derived from the selected Tenant plus an eligible Membership; a client-supplied TenantId is never authority by itself.
- **Rationale:** Multi-tenant membership is a natural B2B SaaS requirement for owners, operators, accountants, or support users who may work across businesses. Supporting it now avoids a later identity/authorization redesign. Explicit selection plus server-side Membership validation prevents confused-deputy and cross-tenant authorization mistakes.
- **Why material:** Subject identity alone cannot determine trusted Tenant context if membership is many-to-many.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require reconciliation before Ready.
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
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require reconciliation before Ready.
- **Affected tasks:** TASK-0006, TASK-0010–0012, TASK-0024–0025, TASK-0032–0037, TASK-0041–0055.
- **Approved policy constraint:** Money always includes currency; no implicit conversion; VND-only is explicit product policy rather than an implementation assumption.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-003 — Initial roles, role cardinality, and authority matrix

- **Status:** Resolved
- **Question:** Does a Membership have one role or multiple roles? May there be multiple Owners? Which role may grant Admin/Owner, manage staff, manage/publish Catalog, and perform later sensitive actions?
- **Decision:** A Membership has exactly one role at a time in the MVP. Initial roles are Owner, Admin, Staff, and Viewer. A Tenant may have multiple Owners and an Active Tenant must retain at least one Active Owner. Owner has full merchant administration authority. Admin may manage ordinary merchant operations, staff, and Catalog but may not grant/revoke Owner authority or remove the last Active Owner. Staff may perform ordinary operational work allowed by the relevant domain policy but may not manage Memberships/roles or tenant-administration concerns. Viewer is read-only. Catalog administration/publishing requires Owner or Admin in the MVP.
- **Rationale:** One role per Membership gives the MVP a small, understandable authorization model and avoids prematurely building a custom RBAC/permission engine. Multiple Owners avoid a single-account administrative dead end while preserving a path toward permission-based/custom roles later.
- **Why material:** Product wording says “roles” while candidate tasks usually assume one role, and “catalog manager” is not a defined role.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require reconciliation before Ready.
- **Affected tasks:** TASK-0008–0013 and later authorization-sensitive tasks.
- **Approved policy constraint:** an Active Tenant retains an Active Owner; Viewer remains non-mutating; Owner changes cannot bypass the last-owner invariant.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-004 — Tenant suspension, reactivation, and closure behavior

- **Status:** Deferred until platform administration/privacy refinement
- **Question:** What can staff, shoppers, and support personnel read or do while a Tenant is Suspended? Is closure supported, and what retention/recovery behavior applies?
- **Interim human direction:** While Suspended, ordinary merchant mutations and public commerce are denied. Suspension does not delete Tenant data, Memberships, Subscription, Orders, accounting history, or other business evidence. Reactivation restores ordinary eligibility subject to all other independent business constraints. Tenant closure, deletion, retention windows, recovery windows, and privacy/legal erasure semantics remain deferred.
- **Rationale:** Minimal suspension semantics are needed to keep current authorization deterministic, but closure/retention couples product policy to accounting, billing, audit, privacy, legal, and recovery requirements that are not needed at the current frontier. Deferring them avoids inventing irreversible lifecycle behavior too early.
- **Decision gate:** before TASK-0068 or any earlier suspension capability becomes Ready.
- **Affected tasks:** TASK-0068 and later lifecycle/privacy work; TASK-0007 must at least reject ordinary Suspended-tenant work.
- **Safe interim constraint:** Suspended denies ordinary merchant/public commerce; it does not delete data or silently disable/reactivate Memberships.
- **Approved by:** CommerceOS human Product Owner for interim direction only
- **Approved on:** 2026-08-10

### PD-005 — SKU requiredness, mutability, and reuse

- **Status:** Resolved
- **Question:** Is SKU required when Draft is created or only before publication? Is uniqueness case-sensitive and what merchant-visible normalization applies? May SKU change after publication? May an Archived Product's normalized SKU be reused?
- **Decision:** SKU is optional when a Draft Product is first created but mandatory before first publication. SKU uniqueness is case-insensitive within a Tenant using a stable normalized representation. After first publication the SKU is immutable. A normalized SKU used by an Archived Product is not reusable.
- **Rationale:** SKU-less Drafts keep editing/import flexible, while a stable SKU before publication gives Inventory, Sales, integrations, and operations a durable reference. Immutability and non-reuse remove historical ambiguity.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require reconciliation before Ready.
- **Affected tasks:** TASK-0010, TASK-0011, TASK-0012, Sales snapshots, import mapping.
- **Approved policy constraint:** ProductId is immutable authority; any assigned SKU is normalized and tenant-unique; a SKU cannot change after first publication or be reused after archive.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-006 — Sellable/publication-required fields and zero-price products

- **Status:** Resolved
- **Question:** Must publication require positive price, Category, Brand, description, and/or media? Are free/zero-priced Products supported?
- **Decision:** Publication requires a valid Name, SKU, and Money value. Price may be zero. Category, Brand, description, and media are optional. Stock is not a publication prerequisite; a Published Product may independently be out of stock/unavailable for sale.
- **Rationale:** Publication/catalog visibility and inventory availability are separate concerns. A small required-field set keeps the MVP usable and import-friendly. Allowing zero-price Products avoids a future special case for samples, free items, or promotions.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require reconciliation before Ready.
- **Affected tasks:** TASK-0012, TASK-0020–0025, import review.
- **Approved policy constraint:** publication requires valid Name, SKU, and Money; private/advisory cost is never public; stock is not a publication prerequisite.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-007 — Published editing and archive lifecycle

- **Status:** Resolved
- **Question:** Does editing a Published Product update the live projection immediately or create a draft revision requiring republish? Must a Published Product be explicitly unpublished before archive? Is Archived terminal or restorable?
- **Decision:** In the MVP, editing a Published Product updates the canonical Product and its public projection without a separate draft-revision workflow. A Published Product may be Archived directly. Archived is terminal in the MVP and cannot be restored or republished.
- **Rationale:** Draft revision, approval, preview, scheduled publication, rollback, and restoration would turn Catalog into a CMS-style versioning system before the product needs it. Direct editing plus terminal Archive keeps lifecycle invariants small and clear.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require reconciliation before Ready.
- **Affected tasks:** TASK-0012, TASK-0013, public Catalog tasks.
- **Approved policy constraint:** Archived is never publishable/restorable in the MVP; no hidden live-revision mechanism is implied.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-008 — Public Product address/slug policy

- **Status:** Resolved
- **Question:** Is a Product addressed by immutable id, mutable slug, or both? Who generates slugs, how are collisions handled, and do renamed slugs redirect?
- **Decision:** ProductId is the immutable canonical Product identity. A Published Product also has a tenant-scoped slug for its public URL. The product may propose a slug from Product name and the merchant may edit it. Normalized slug must be unique within the Tenant. In the MVP, changing a slug does not require historical redirects.
- **Rationale:** Stable immutable identity prevents rename/address changes from corrupting references while a human-readable slug gives a better storefront URL. Tenant-scoped uniqueness is sufficient and avoids premature redirect/SEO lifecycle machinery.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require reconciliation before Ready.
- **Affected tasks:** TASK-0012, TASK-0020–0022, SEO/custom-domain work.
- **Approved policy constraint:** ProductId remains immutable authority; slug is an address/presentation concern and never cross-tenant authority.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-009 — Category and Brand organization policy

- **Status:** Resolved
- **Question:** May a Product have one or multiple Categories? Are categories hierarchical? Are Category/Brand names unique? What happens to referenced Products when a reference is retired?
- **Decision:** In the MVP a Product may reference zero or one Category and zero or one Brand. Category is non-hierarchical. Category and Brand are Tenant-owned stable references whose normalized names are unique case-insensitively within the Tenant. Referenced Category/Brand records are not hard-deleted; they may be retired while existing Product references remain intact.
- **Rationale:** Single-category/non-hierarchical classification is sufficient for MVP and avoids tree management, cycle handling, breadcrumb/SEO semantics, inheritance, and many-to-many filtering complexity. Stable ids and non-destructive retirement preserve history.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require reconciliation before Ready.
- **Affected tasks:** TASK-0011–0013, Storefront filters, imports.
- **Approved policy constraint:** retirement never cascades destructive changes into Product history.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-010 — External media display and rights evidence

- **Status:** Resolved
- **Question:** May merchants display externally hosted media by attestation, only after explicit license evidence/source approval, or only use merchant-owned uploads? Is hotlinking allowed?
- **Decision:** Public Product media in the MVP must be provided through merchant uploads managed by CommerceOS. CommerceOS does not copy arbitrary external binaries and does not support external-media hotlinking for public Product media. The merchant remains responsible for rights to uploaded content.
- **Rationale:** External URLs introduce availability, hotlink protection, tracking, mutable-content, performance, malware, and rights ambiguity outside CommerceOS control. Managed uploads create a smaller, more reliable product boundary; storage/CDN selection remains a Technical Architecture decision.
- **Decision gate:** Resolved on 2026-08-10; affected tasks still require reconciliation before Ready.
- **Affected tasks:** TASK-0012, TASK-0018, Storefront/media work.
- **Approved policy constraint:** external binary copy/hotlink is not supported for public Product media in the MVP; ingestion source evidence remains a separate concern.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-034 — Registration admission, uniqueness, and Business Profile minimum

- **Status:** Resolved
- **Question:** Is registration open self-service, invite/claim based, or platform-provisioned? Which verified person becomes initial Owner? Is duplicate-business detection required beyond retry idempotency, and which Business Profile fields are mandatory?
- **Decision:** MVP registration is open self-service for an authenticated identity with a verified email. The verified identity that successfully initiates registration becomes the initial Active Owner. Minimum Business Profile fields are a merchant display name and an explicit IANA business timezone. No legal/tax-identity uniqueness or cross-tenant duplicate-business detection is performed in MVP; retry of the same registration intent must remain idempotent.
- **Rationale:** Self-service onboarding is the smallest useful SaaS admission model. Requiring only display name and timezone avoids inventing jurisdiction-specific company/tax requirements, while explicit timezone is needed for reporting/business-date semantics. Duplicate-business detection without a trusted legal identifier would create false positives and unnecessary coupling.
- **Why material:** “a business can register” does not define admission or a safe real-business uniqueness key.
- **Decision gate:** Resolved; TASK-0006–0008 require reconciliation before Ready.
- **Affected tasks:** TASK-0006–0008.
- **Approved policy constraint:** one successful onboarding intent produces one Active Tenant plus one initial Active Owner; SubjectId or business name alone is not a global business-uniqueness key.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-036 — Invitation recipient binding and duplicate-invitation policy

- **Status:** Resolved
- **Question:** Does acceptance bind to a verified email claim, a pre-bound external SubjectId, or another verified identifier? How do aliases/identity changes behave, and what happens for duplicate pending invitations, resend, or an existing Disabled Membership?
- **Decision:** An invitation is Tenant-bound and addressed to one normalized email. Acceptance requires an authenticated identity carrying a verified email that matches the invitation recipient. There may be at most one active pending invitation per Tenant + normalized recipient email. Resend rotates/reissues the invitation credential and invalidates the previous credential rather than creating another independent invitation. An existing Active Membership makes acceptance a harmless already-member outcome. An existing Disabled Membership is not silently reactivated; authorized Membership re-enable is a separate action. Invitation credentials expire after 7 days and are single-use.
- **Rationale:** Verified-email binding is understandable and implementable without prematurely coupling invitations to a specific external identity provider's SubjectId. One active invitation and rotating resend semantics avoid duplicate acceptance paths. Explicit re-enable protects Membership lifecycle authority.
- **Why material:** “the intended user accepts” is a security/business rule, not an implementation detail.
- **Decision gate:** Resolved; TASK-0008 requires reconciliation before Ready.
- **Affected tasks:** TASK-0008 and invitation notification work.
- **Approved policy constraint:** an invitation cannot activate an unmatched identity or silently reactivate a Disabled Membership.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-037 — Product specification and public-field policy

- **Status:** Resolved
- **Question:** Are specification names unique, may values carry units/multiple entries, how are they ordered, and are any specifications private? Is SKU or source attribution public, and which canonical fields form `PublicProduct`?
- **Decision:** In MVP each ProductSpecification has a normalized name unique within that Product, one text value, an optional unit, and merchant-controlled display order. Specifications are public when the Product is Published; private specifications are not supported initially. `PublicProduct` may expose ProductId, slug, name, description, Money/price, SKU, Category, Brand, approved media, specifications, and derived availability. Advisory cost, ingestion raw/source-review data, internal history, and merchant-private metadata are never public. Ingestion source attribution is not public by default and requires a later explicit publication policy if desired.
- **Rationale:** One value plus optional unit covers normal catalog specifications without introducing arbitrary nested schema or multi-value semantics. A single public/private rule keeps projection logic auditable. Exposing SKU is useful operationally, while source-review and advisory data carry different ownership and rights concerns.
- **Decision gate:** Resolved; TASK-0010–0013 require reconciliation before Ready.
- **Affected tasks:** TASK-0010–0013, Storefront, ingestion import review.
- **Approved policy constraint:** public projection is explicit allow-list data; private/advisory/source-review fields never leak by omission-based filtering.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

## 3. Checkout, Sales, Inventory, and Payment decisions

### PD-011 — Checkout repricing behavior

- **Status:** Resolved
- **Question:** If the authoritative current price differs from the storefront/cart estimate, does checkout accept the new price, reject and require shopper confirmation, or use another tolerance rule?
- **Decision:** Any difference between the shopper-confirmed estimate and the current authoritative server price prevents final order placement. Checkout returns the refreshed authoritative price and requires explicit shopper reconfirmation before a new placement attempt. No tolerance rule and no silent lower/higher-price acceptance exists in MVP.
- **Rationale:** Explicit reconfirmation prevents surprise charges and avoids asymmetric rules for price increases versus decreases. It also keeps the server authoritative while making the shopper's commercial consent clear.
- **Decision gate:** Resolved; TASK-0023/TASK-0025 require reconciliation before Ready.
- **Affected tasks:** TASK-0023, TASK-0025, Storefront checkout UX.
- **Approved policy constraint:** client price never overrides server price and no order is partially placed.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-012 — Sellable quantity and unit-of-measure policy

- **Status:** Resolved
- **Question:** Are initial Product, Order, Reservation, Receipt, and Issue quantities positive whole units only, or are decimal quantities/units of measure supported?
- **Decision:** MVP supports positive whole-unit quantities only. Fractional quantities, variable units of measure, implicit conversion, and fractional rounding are out of scope.
- **Rationale:** Whole units keep checkout, reservation, inventory concurrency, procurement, returns, and accounting consistent while avoiding unit-conversion and decimal-precision policy before the product needs weighed/measured goods.
- **Decision gate:** Resolved; affected quantity tasks require reconciliation before Ready.
- **Affected tasks:** TASK-0025, TASK-0028–0031, TASK-0041–0042, returns.
- **Approved policy constraint:** every accepted quantity is a positive integer in the Product's MVP unit semantics.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-013 — Manual discount ownership and limits

- **Status:** Resolved
- **Question:** Which actors may apply manual discounts, to which order/channel, by fixed amount or percentage, and under what limits? Can guest checkout ever submit one?
- **Decision:** Manual discounts are not supported in MVP public/guest checkout and untrusted clients may never submit an authoritative discount. Initial order pricing consists only of authoritative Catalog/Pricing results. A future merchant-entered manual discount or promotion capability requires a new Pricing product decision rather than being smuggled into Sales.
- **Rationale:** Removing manual discounts from initial checkout avoids authorization, stacking, abuse, percentage/fixed rounding, audit, and accounting edge cases while preserving Pricing as the owner of future discount policy.
- **Decision gate:** Resolved for MVP; discount-specific future work remains non-Ready until separately refined.
- **Affected tasks:** TASK-0025, TASK-0027, future Pricing/Promotion tasks.
- **Approved policy constraint:** Sales may snapshot only an accepted authoritative price; guest input cannot grant a discount.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-014 — Canonical order dimensions and reserve/pay/confirm sequence

- **Status:** Resolved
- **Question:** What is the initial business sequence among OrderPlaced, stock hold/reservation, payment authorization/capture, OrderConfirmed, and OrderAllocated? How are commercial, payment, fulfillment, and refund states presented without conflation?
- **Decision:** MVP uses independent commercial, payment, inventory-allocation, and fulfillment dimensions. The canonical happy path is: authoritative checkout validation → `OrderPlaced` commercial snapshot → all-line Inventory hold/reservation accepted → full Payment capture attempt → verified `PaymentCaptured` → `OrderConfirmed` → `OrderAllocated` once the confirmed Order is backed by all required accepted Inventory reservations → whole-order fulfillment. An Inventory hold may exist before commercial confirmation, but `OrderAllocated` is not asserted until the Order is Confirmed. Payment `OutcomeUnknown` is nonterminal and blocks any new capture attempt until reconciliation.
- **Rationale:** Reserving before capture reduces oversell risk, while confirming only after verified capture prevents Sales from claiming payment success prematurely. Separate state dimensions avoid impossible combined states and make compensation/reconciliation explicit.
- **Why material:** candidate tasks previously mixed Confirmed, reservation, allocation, and payment timing.
- **Decision gate:** Resolved; TASK-0024–0027, TASK-0031, TASK-0034–0040 require reconciliation before Ready.
- **Approved policy constraint:** no state claims an independent effect that has not actually occurred; `OrderAllocated` means all required Inventory reservations are accepted.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-042 — Order cancellation and completion policy

- **Status:** Resolved
- **Question:** From which stages may an Order be cancelled, who may do so, which separate refund/release effects are required, and what triggers Completed?
- **Decision:** In MVP an authorized merchant Owner/Admin/Staff may cancel an Order any time before it is Fulfilled. Shopper self-service cancellation is out of scope. Cancellation is a commercial decision only: if stock is reserved, release is separately required; if payment is captured, refund is separately required and their outcomes remain independently visible. A whole-order `Fulfilled` Order automatically becomes `Completed` only when no cancellation/refund exception is outstanding. Completed is terminal for normal commerce operations.
- **Rationale:** Merchant-side pre-fulfillment cancellation covers the operational need without introducing guest identity/recovery flows. Keeping refund and stock release independent prevents a single state change from falsely claiming external/foreign-domain effects.
- **Decision gate:** Resolved; affected Sales/Inventory/Payment tasks require reconciliation before Ready.
- **Affected tasks:** TASK-0024, TASK-0027, TASK-0031, TASK-0034–0040.
- **Approved policy constraint:** Order cancellation never asserts refund or reservation release succeeded unless the owning domains confirm those effects.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-015 — Multi-line allocation and fulfillment

- **Status:** Resolved
- **Question:** Is initial allocation all-or-nothing, or may an Order be partially allocated/backordered? Is issue/fulfillment whole-order only or partial by line/quantity?
- **Decision:** MVP allocation is all-or-nothing across all order lines and quantities. Backorder, partial allocation, split shipment, and partial fulfillment are not supported. `OrderAllocated` and `Fulfilled` are asserted only when every required line/quantity satisfies the corresponding Inventory effect.
- **Rationale:** All-or-nothing semantics dramatically reduce compensation, customer communication, payment/refund, inventory, and reporting complexity while preserving a clear upgrade path to partial fulfillment later.
- **Decision gate:** Resolved; TASK-0030/TASK-0031 require reconciliation before Ready.
- **Affected tasks:** TASK-0030, TASK-0031, returns/refunds.
- **Approved policy constraint:** partial effects remain visible/recoverable operational evidence but never masquerade as an allocated/fulfilled Order.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-016 — Payment model and capture policy

- **Status:** Resolved
- **Question:** Is there one Payment per Order with multiple attempts, or a Payment per attempt? Does MVP immediately capture, or authorize then capture later? Are multiple/partial tenders excluded?
- **Decision:** MVP has one Payment obligation per Order with multiple immutable PaymentAttempts. The full accepted Order amount is captured immediately; authorize-only/capture-later is not supported. Multiple tenders, split payments, partial captures, and over-capture are excluded.
- **Rationale:** One Payment obligation preserves the Order's financial meaning while multiple attempts model retries cleanly. Immediate full capture fits the Mock Provider learning scope and avoids authorization-expiry, partial tender, and incremental capture complexity.
- **Decision gate:** Resolved; TASK-0032–0038 require reconciliation before Ready.
- **Affected tasks:** TASK-0032–0038, Sales/payment/accounting integration.
- **Approved policy constraint:** captured amount cannot exceed or diverge from the accepted Order amount; attempts never become separate order obligations.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-017 — Definitive no-capture, decline, retry, and order effect

- **Status:** Resolved
- **Question:** Which verified provider outcomes prove no commit versus remain transient/unknown? After a definitive decline/no-commit, may the same Order accept another attempt/method, or does it enter a terminal path?
- **Decision:** A provider-verifiable Declined/Rejected/NoCommit outcome terminates only that PaymentAttempt. The Order/Payment obligation remains eligible for a new attempt unless separately cancelled. A transport error, timeout, missing callback, or ambiguous provider response is `OutcomeUnknown`, not failure; no new capture attempt may start until provider query/reconciliation proves the prior attempt's terminal outcome.
- **Rationale:** Treating definitive decline and ambiguity differently prevents both unsafe duplicate charging and unnecessary terminal order failure. Keeping the Order payable after a real decline supports normal retry behavior without hiding attempt history.
- **Decision gate:** Resolved; TASK-0033–0037 require reconciliation before Ready.
- **Affected tasks:** TASK-0033–0037, Sales operations.
- **Approved policy constraint:** timeout never proves failure; unknown outcome blocks blind retry.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-018 — Ambiguous payment stock hold and escalation

- **Status:** Resolved
- **Question:** How long does an Order with Payment OutcomeUnknown retain reserved stock? Is automatic expiry permitted and what escalation/override is allowed?
- **Decision:** In MVP, stock reserved for an `OutcomeUnknown` Payment remains held until reconciliation produces evidence of Captured or NoCommit/Declined. Time passage alone never expires the hold. If automated/provider-query reconciliation cannot resolve the outcome, the case becomes `NeedsAttention` and is surfaced to merchant/platform support, but neither merchant nor platform support may manually declare financial success/failure without provider evidence under the MVP policy.
- **Rationale:** Holding stock can reduce availability in a rare ambiguity, but releasing it based only on time can create a paid order with no stock. The conservative policy favors financial/inventory correctness and makes operational recovery explicit.
- **Decision gate:** Resolved; TASK-0034/0037 and reservation-expiry work require reconciliation before Ready.
- **Affected tasks:** TASK-0034–0038, TASK-0030–0031, operational recovery.
- **Approved policy constraint:** age/timeout alone never authorizes retry, cancellation, failure, or stock release.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-019 — Low-stock policy

- **Status:** Resolved
- **Question:** Is low stock based on Available or OnHand, per warehouse or tenant total, with what default/disabled threshold and crossing/reset behavior?
- **Decision:** Low stock is based on `Available`, evaluated per Product + Warehouse. Threshold is optional and disabled when absent. A low-stock condition becomes active when Available crosses from above the configured threshold to at-or-below it and clears when Available rises above the threshold. Low-stock is derived operational state only.
- **Rationale:** Available reflects quantity that can actually be promised after reservations. Per-warehouse evaluation matches replenishment reality and avoids hiding a shortage behind stock elsewhere. Crossing semantics prevent notification spam.
- **Decision gate:** Resolved; low-stock behavior requires reconciliation before Ready.
- **Affected tasks:** TASK-0031, TASK-0054/TASK-0056.
- **Approved policy constraint:** low-stock state never mutates inventory balances.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-041 — Negative stock, backorder, reservation floor, and adjustment interaction

- **Status:** Resolved
- **Question:** May OnHand or Available become negative? Is backorder supported? What availability floor must reservation enforce, and may an adjustment decrease consume quantity already reserved?
- **Decision:** MVP does not allow negative OnHand or negative Available and does not support backorders. Reservation requires current authoritative Available >= requested quantity and must enforce the invariant concurrency-safely. A downward stock adjustment may not reduce OnHand below Reserved; existing reservations must first be explicitly released/changed through their owning workflow or the correction must be rejected/handled as an exception.
- **Rationale:** Zero-floor inventory creates strong, testable invariants and avoids oversell/backorder semantics. Protecting Reserved quantity prevents an administrative adjustment from silently invalidating accepted customer commitments.
- **Why material:** `Available = OnHand - Reserved` is foundational but does not itself define negative-stock policy.
- **Decision gate:** Resolved; TASK-0028–0031 require reconciliation before Ready.
- **Affected tasks:** TASK-0028–0031, checkout/allocation, adjustments, future backorders.
- **Approved policy constraint:** OnHand, Reserved, and Available never become negative in accepted MVP state.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-035 — Guest customer data, matching, and privacy lifecycle

- **Status:** Resolved for MVP; long-term privacy/erasure policy remains future work
- **Question:** Which contact/address fields are minimally required per order, when may a guest be linked to/create a tenant Customer profile, what constitutes a duplicate profile, and how do retention/anonymization rules preserve required history?
- **Decision:** Guest checkout does not require or create a shopper account/CRM Customer. Sales stores the immutable contact/fulfillment snapshot required for that Order: recipient/display name and email are required; phone and shipping address are required only when the selected fulfillment method needs them. MVP performs no automatic guest-to-Customer matching or profile creation. Any future link requires explicit verified identity/merchant action and must not rewrite historical Order snapshots. MVP performs no automatic anonymization/deletion of historical Order/accounting evidence; a dedicated privacy/retention policy must supersede this before such behavior is introduced.
- **Rationale:** Minimal per-order data supports fulfillment and contact without creating unreliable identity matching from unverified PII. Keeping CRM linkage explicit prevents accidental profile merges. Deferring automated erasure avoids inventing legal retention behavior while preserving immutable commercial/accounting evidence.
- **Decision gate:** Resolved for current checkout scope; privacy deletion/anonymization work remains blocked pending a dedicated later decision.
- **Affected tasks:** TASK-0026, TASK-0069, TASK-0083, guest order access.
- **Approved policy constraint:** no broad PII collection, automatic cross-tenant matching, or destructive rewrite of historical Order/accounting evidence.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

## 4. Procurement and accounting-policy decisions

Accounting policies below define the **CommerceOS learning/MVP accounting model**. They are not claims of statutory, tax, GAAP, IFRS, or Vietnam-accounting compliance.

### PD-020 — Sale/revenue recognition and Cash versus AR

- **Status:** Resolved
- **Question:** Which single operational fact recognizes a sale/revenue? Does captured mock payment post Cash, or is Accounts Receivable used, and under what order/payment timing?
- **Decision:** MVP is prepaid/cash commerce with no Accounts Receivable. Verified `PaymentCaptured` posts Cash and a Customer Deposits/Unearned Revenue liability, not Sales Revenue. Whole-order `OrderFulfilled` is the single revenue-recognition trigger and reclassifies the corresponding customer deposit to Sales Revenue. `OrderConfirmed` does not post revenue.
- **Rationale:** Recognizing revenue at fulfillment better reflects delivery of the merchant obligation while still recording cash when captured. Using a deposit liability keeps payment timing and revenue timing distinct and prevents duplicate revenue from PaymentCaptured plus OrderConfirmed/Fulfilled.
- **Why material:** previous candidate tasks allowed competing revenue triggers.
- **Decision gate:** Resolved; accounting/Sales tasks require reconciliation before Ready.
- **Affected tasks:** TASK-0044, TASK-0049, TASK-0050, financial reporting.
- **Approved policy constraint:** exactly one logical revenue-recognition source fact exists for an Order; payment capture alone is not revenue.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect/Accounting reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-021 — Inventory valuation and COGS trigger

- **Status:** Resolved
- **Question:** Which context owns the immutable cost snapshot, which valuation method applies, and does StockIssued or OrderFulfilled trigger COGS?
- **Decision:** MVP uses a moving weighted-average inventory cost maintained as Accounting valuation truth from accepted Procurement receipt cost evidence. Inventory continues to own quantity, not accounting value. `StockIssued` is the single COGS trigger: Accounting consumes the immutable issued quantity plus the applicable weighted-average cost snapshot and posts COGS / Inventory. `OrderFulfilled` must not create a second COGS effect for the same issue.
- **Rationale:** Weighted average is simpler than FIFO/layer tracking while still teaching real valuation behavior. Using StockIssued ties COGS to the physical quantity movement and avoids duplicate effects from Sales fulfillment events. Keeping valuation in Accounting preserves Inventory's quantity ownership.
- **Why material:** Catalog cost is advisory and cannot become accounting cost authority.
- **Decision gate:** Resolved; Inventory/Accounting/Procurement tasks require reconciliation before Ready.
- **Affected tasks:** TASK-0028–0031, TASK-0042, TASK-0044, TASK-0051, profit reporting.
- **Approved policy constraint:** Catalog advisory cost is never posted as COGS; every stock issue creates at most one COGS effect.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect/Accounting reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-022 — Accounting recognition for procurement and supplier settlement

- **Status:** Resolved
- **Question:** Is inventory/AP recognized at GoodsReceipt, SupplierInvoice, or through an interim received-not-invoiced concept? How are variance and supplier settlement posted?
- **Decision:** MVP uses Goods-Received-Not-Invoiced (GRNI). Confirmed physical receipt/accounting acceptance posts Inventory / GRNI using the accepted PO/receipt cost evidence. SupplierInvoiceRecorded clears GRNI to Accounts Payable; any approved invoice-versus-receipt difference posts to Purchase Price Variance rather than rewriting receipt history. SupplierPaymentRecorded posts Accounts Payable / Cash. Each business fact is independently idempotent.
- **Rationale:** GRNI preserves the real distinction between receiving goods, receiving the supplier invoice, and paying the supplier. It supports honest timing and variance handling without pretending one event implies the others.
- **Decision gate:** Resolved; TASK-0044/TASK-0051–0052 require reconciliation before Ready.
- **Affected tasks:** TASK-0044, TASK-0051–0052.
- **Approved policy constraint:** GoodsReceiptRecorded, StockReceived, SupplierInvoiceRecorded, and SupplierPaymentRecorded remain distinct; no pair duplicates Inventory/AP/Cash effects.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect/Accounting reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-023 — Refund request approval, return, and accounting correction

- **Status:** Resolved
- **Question:** What approval must occur before refund effects are accepted, and how do approved refunds affect stock, revenue/COGS, journals, and provider-refund truth?
- **Decision:** MVP uses an explicit request-and-approval workflow. A refund begins as `RefundRequested` and must be reviewed in a dedicated merchant refund-approval experience before refund consequences are authorized. Approval records `RefundApproved`; rejection records `RefundRejected`. In MVP, approving the refund also means the merchant accepts the corresponding returned goods as restockable, so Inventory records the approved `StockReturned` quantity exactly once. Accounting creates linked reversal/compensating journal(s), never edits posted history: for already recognized fulfilled sales, the approved refund reverses the applicable revenue recognition (`Dr Sales Revenue / Cr Customer Deposits`) and the accepted `StockReturned` reverses the applicable COGS/inventory effect (`Dr Inventory / Cr COGS`) using the original issue-cost basis/provenance. Payment money movement remains owned by Payments: approval authorizes the refund operation, but `PaymentRefunded` exists only after verified provider evidence; when verified, Accounting clears the corresponding customer-deposit liability against Cash (`Dr Customer Deposits / Cr Cash`). Rejection produces no `StockReturned`, no refund accounting correction, and no payment-refund authorization. Non-restock refund semantics are outside the MVP policy established by this decision.
- **Rationale:** A dedicated approval gate prevents an unreviewed refund request from automatically mutating stock or financial history. Tying approval to an accepted restock plus explicit compensating journals gives the MVP one auditable recovery path while preserving immutable journals and the existing rule that only verified provider evidence proves money was actually refunded.
- **Decision gate:** Resolved; refund/return work still requires Domain/Technical/Backlog reconciliation before Ready.
- **Affected tasks:** TASK-0050, TASK-0063–0066, refund back-office approval experience, Inventory returns, Accounting reconciliation, Reporting.
- **Approved policy constraint:** `RefundRequested` alone has no stock/accounting/payment effect; `RefundApproved` authorizes one logical `StockReturned` plus linked compensating accounting effects; posted journals are never edited; `PaymentRefunded` still requires verified provider evidence; `RefundRejected` creates none of those effects.
- **Approved by:** CommerceOS human Product Owner
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** `docs/domains/commerce-operations.md`, `docs/02-business-domains.md`, `docs/domains/product-decision-reconciliation.md` in the same Domain Architect reconciliation pass.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-024 — Financial treatment of stock adjustments

- **Status:** Resolved
- **Question:** Which adjustment reasons create accounting effects, and which gain/expense accounts/policies apply?
- **Decision:** Every confirmed physical stock adjustment has an explicit reason. A quantity decrease posts Inventory Adjustment Loss/Expense against Inventory using the applicable Accounting weighted-average cost; a quantity increase posts Inventory against Inventory Adjustment Gain using the approved valuation basis for that adjustment. Reservation changes are not stock adjustments and create no accounting posting. Administrative metadata corrections create no quantity/value posting.
- **Rationale:** Explicit gain/loss treatment makes physical discrepancies financially visible without pretending every correction is a sale/purchase. Separating reservations avoids posting value for quantity that has not physically moved.
- **Decision gate:** Resolved; TASK-0029/TASK-0044/TASK-0051 require reconciliation before Ready.
- **Affected tasks:** TASK-0029, TASK-0044, TASK-0051.
- **Approved policy constraint:** accepted stock/value changes are auditable and idempotent; adjustment reasons are mandatory.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect/Accounting reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-038 — Chart of accounts and account lifecycle policy

- **Status:** Resolved
- **Question:** Is Accounting enabled automatically or opt-in, which account template/types/codes/control accounts are required, what may the merchant customize, are codes unique/reusable, and when may accounts be deactivated?
- **Decision:** Accounting is enabled automatically for every MVP Tenant with a minimal CommerceOS learning chart containing at least Cash, Customer Deposits, Sales Revenue, Inventory, COGS, Accounts Payable, GRNI, Purchase Price Variance, Inventory Adjustment Gain, and Inventory Adjustment Loss. Required control-account semantic roles are platform-defined. Merchants may add non-control accounts and edit display names where allowed, but account identity/code is tenant-unique and cannot be reused after it has been referenced by a posted journal. Posted references remain immutable. Non-required accounts may be deactivated, not hard-deleted; required control accounts cannot be deactivated while the corresponding capability is enabled.
- **Rationale:** A standard minimal chart makes event-driven accounting deterministic and testable without claiming jurisdictional compliance. Stable/non-reusable account identities protect posted history while allowing enough customization for learning and later extension.
- **Decision gate:** Resolved; TASK-0044–0052 require reconciliation before Ready.
- **Affected tasks:** TASK-0044–0052, financial reporting.
- **Approved policy constraint:** no account lifecycle action can rewrite or orphan posted journal history.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect/Accounting reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-039 — Journal effective date, posting date, and backdating

- **Status:** Resolved
- **Question:** Which accounting/effective date drives General Ledger and Trial Balance, how does it relate to source occurrence and posting time, is backdating allowed, and what period/open-date rules apply before formal period close exists?
- **Decision:** General Ledger and Trial Balance are bucketed by Journal EffectiveDate. For automated journals, EffectiveDate is the approved source business date interpreted in the Tenant business timezone; PostingTimestamp records when the journal was actually committed. Manual journals in MVP use the current Tenant business date only: user-selected past/future effective dates and formal backdating are not supported. Source occurrence timestamp, EffectiveDate, and PostingTimestamp are preserved separately.
- **Rationale:** Distinct dates keep delayed async processing from moving business activity into the wrong reporting day. Disallowing manual backdating avoids period-close/open-period complexity until the accounting model explicitly adds it.
- **Decision gate:** Resolved; TASK-0044–0047 and reporting tasks require reconciliation before Ready.
- **Affected tasks:** TASK-0044–0047, TASK-0050–0055.
- **Approved policy constraint:** infrastructure/server processing time never silently becomes business effective-date policy.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect/Accounting reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-025 — Submitted purchase-order amendment/cancellation

- **Status:** Resolved
- **Question:** What does Submitted mean, may a submitted PO be amended/cancelled, and what explicit correction evidence is required?
- **Decision:** `Submitted` freezes supplier, product lines, quantities, and commercial terms as an immutable PO snapshot. Submitted POs are not edited in place. A Submitted PO may be cancelled only while no confirmed GoodsReceipt, SupplierInvoice, or SupplierPayment evidence exists. Otherwise cancellation/amendment is not supported in MVP and any correction requires the later explicit return/correction workflow or a replacement PO as applicable.
- **Rationale:** Immutable submitted snapshots preserve supplier evidence and remove complex amendment-versioning. Restricting cancellation after downstream evidence prevents history from contradicting receipts/invoices/payments.
- **Decision gate:** Resolved; TASK-0041–0043 require reconciliation before Ready.
- **Affected tasks:** TASK-0041–0043.
- **Approved policy constraint:** submitted commercial snapshots are never silently edited.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-027 — Supplier and PO-line eligibility

- **Status:** Resolved
- **Question:** What minimum Supplier identity/status/uniqueness is required? May archived/unpublished Products be purchased? Are tax, freight, and line/order discounts excluded initially?
- **Decision:** Supplier has stable Tenant-owned identity, display name, and Active/Archived status; supplier name is not treated as a legal uniqueness key. New POs require an Active Supplier. Draft or Published Products may be purchased so stock can be acquired before storefront publication; Archived Products cannot be added to new POs. MVP excludes procurement tax, freight, and PO discount calculations.
- **Rationale:** Stable identity rather than name uniqueness avoids false duplicate assumptions. Allowing Draft Product purchasing supports normal pre-launch stocking, while Archived prevents new commercial commitments. Excluding tax/freight/discounts keeps procurement and accounting scope controlled.
- **Decision gate:** Resolved; TASK-0041–0043 require reconciliation before Ready.
- **Affected tasks:** TASK-0041–0043.
- **Approved policy constraint:** submitted PO references are tenant-local stable identities and their commercial snapshot is immutable.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-028 — Goods-receipt confirmation and correction

- **Status:** Resolved
- **Question:** Does PO “received” follow physical GoodsReceipt confirmation or successful Inventory application? How is an erroneous confirmed receipt corrected without destructive mutation?
- **Decision:** PO received quantity follows confirmed physical GoodsReceipt evidence, not the success state of downstream Inventory application. Inventory application is separately tracked as Pending/Applied/NeedsAttention. A confirmed GoodsReceipt is immutable; an error is corrected by a new explicit compensating receipt/correction record that references the original, after which derived received quantities use the net accepted evidence.
- **Rationale:** Physical receipt and system inventory application can fail independently. Preserving immutable receipt evidence avoids rewriting what the merchant actually confirmed while still allowing explicit corrections.
- **Decision gate:** Resolved; TASK-0042/TASK-0051/TASK-0084 require reconciliation before Ready.
- **Affected tasks:** TASK-0042, TASK-0051, TASK-0084.
- **Approved policy constraint:** Inventory application failure never erases or rewrites confirmed physical receipt evidence.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-029 — Supplier invoice and payment evidence

- **Status:** Resolved
- **Question:** Is there exactly one invoice/full payment per initial PO, may invoice precede receipt, what matching tolerance applies, and which amount/date/reference evidence is required?
- **Decision:** MVP supports exactly one SupplierInvoice and one full SupplierPayment per PO. SupplierInvoice may be recorded only after the PO is fully received under the MVP receipt policy. Invoice records supplier invoice reference, supplier invoice date, Money amount/currency, and the related PO. Exact match is required for automatic acceptance; any variance requires explicit merchant approval and is preserved as variance evidence for Accounting. SupplierPayment may be recorded only after the invoice and records payment date, Money amount, and external/reference text; it is merchant attestation, not bank execution.
- **Rationale:** One invoice/full payment keeps the initial AP flow small while still teaching receipt/invoice/payment separation and variance handling. Requiring explicit variance approval prevents silent financial drift.
- **Decision gate:** Resolved; TASK-0043/TASK-0051 require reconciliation before Ready.
- **Affected tasks:** TASK-0043, TASK-0051, Accounts Payable reporting.
- **Approved policy constraint:** CommerceOS never implies it executed supplier bank payment merely because payment evidence was recorded.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect/Accounting reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

## 5. Product Data Ingestion, Reporting, Notification, and Audit decisions

### PD-026 — Source governance, policy review, and operating authority

- **Status:** Resolved
- **Question:** Is DataSource policy platform-owned, tenant-owned, or shared with tenant overlays? Who may review/approve policy, activate/pause/disable a source, and what makes a review stale?
- **Decision:** Base DataSource policy and policy-review approval are platform-owned. Only authorized platform administrators may mark a source policy review Current and may globally enable/disable source operation. Tenant Owner/Admin may opt an approved globally-enabled source in/out for that Tenant but cannot override platform policy. A review becomes stale when a material source/API/terms/robots/authentication policy change is detected or an authorized platform reviewer explicitly marks it stale; MVP has no arbitrary time-based expiry. Acquisition requires both Current platform approval and tenant enablement.
- **Rationale:** Central policy ownership prevents each Tenant from independently deciding whether collection is permitted, while tenant opt-in preserves merchant control. Event/material-change staleness avoids pretending an arbitrary renewal interval proves policy safety.
- **Decision gate:** Resolved; TASK-0014–0017/TASK-0059–0062 require reconciliation before Ready.
- **Affected tasks:** TASK-0014–0017, TASK-0059–0062.
- **Approved policy constraint:** operating status never overrides stale/missing policy approval.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-040 — External mapping cardinality and ImportCandidate lifecycle

- **Status:** Resolved
- **Question:** May one external source product map to several canonical Products within a Tenant? What lifecycle applies to ImportCandidate and does a newer snapshot stale an older candidate?
- **Decision:** In MVP one external source-product identity may map to zero or one canonical Product within a Tenant. An ImportCandidate lifecycle is `Ready → Approved → Applied` or `Ready → Rejected`; `Ready` or `Approved` may become `Superseded` when a newer candidate for the same source identity is accepted for review before application. MVP uses no time-only `Expired` transition. Approved means merchant approval only; `Applied` occurs only after Catalog confirms the canonical change. Applied, Rejected, and Superseded candidates are historical/terminal.
- **Rationale:** One-to-one mapping keeps canonical identity predictable and avoids one source record multiplying into unrelated Products. Explicit Approved-versus-Applied semantics preserve bounded-context ownership. Superseding by newer evidence is more meaningful than arbitrary time expiry.
- **Decision gate:** Resolved; TASK-0018/TASK-0059–0062 require reconciliation before Ready.
- **Affected tasks:** TASK-0018, TASK-0059–0062.
- **Approved policy constraint:** source snapshots/candidates remain Ingestion-owned; mappings/canonical Product changes remain Catalog-owned.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-030 — Operational commerce KPI definitions

- **Status:** Resolved
- **Question:** What facts/formulas define order count, AOV, top products, failed-payment rate, and any non-ledger gross-sales metric?
- **Decision:** MVP operational dashboard uses: **Order count** = count of `OrderConfirmed` facts in the selected business-date window; **AOV** = sum of authoritative OrderTotal snapshots for those confirmed Orders divided by confirmed-order count; **Top products** = ranked sum of confirmed ordered whole-unit quantity by Product snapshot in the window; **Failed-payment rate** = terminal definitive failed PaymentAttempts divided by all terminal PaymentAttempts, excluding `OutcomeUnknown`; **Operational Gross Sales** = sum of confirmed OrderTotal snapshots, explicitly labeled operational gross sales and never accounting revenue. Cancellation/refund amounts are shown separately and do not retroactively rewrite the original operational event counts.
- **Rationale:** Event-defined numerators/denominators are reproducible and avoid status-at-query-time ambiguity. Separating operational gross sales from ledger revenue prevents dashboards from becoming unofficial accounting statements.
- **Decision gate:** Resolved; TASK-0054/TASK-0056 require reconciliation before Ready.
- **Affected tasks:** TASK-0054, TASK-0056.
- **Approved policy constraint:** every published KPI has named source facts, denominator/eligibility, business-date semantics, and freshness metadata.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect/Reporting reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-031 — Reporting business date and correction attribution

- **Status:** Resolved
- **Question:** Which Tenant timezone defines operational business day, and are refunds/corrections attributed to occurrence date or original transaction date?
- **Decision:** The Tenant Business Profile's explicit IANA timezone defines operational business-day boundaries. Operational corrections, cancellations, refunds, and similar compensating facts are attributed to the business date on which the correcting fact occurs and retain a reference to the original transaction; they do not rewrite the original event's historical date. Financial reports use Journal EffectiveDate under PD-039 rather than operational occurrence-date policy.
- **Rationale:** Tenant-local business days are understandable to merchants and avoid server-timezone leakage. Occurrence-date corrections preserve an append-oriented history and allow users to reconcile what happened on each day.
- **Decision gate:** Resolved; TASK-0053–0056 require reconciliation before Ready.
- **Affected tasks:** TASK-0053–0056.
- **Approved policy constraint:** event/effective/posting timestamps remain distinct and infrastructure timezone is never business policy.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect/Reporting reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-032 — Notification audience, read, and acknowledgement semantics

- **Status:** Resolved
- **Question:** Which roles/users receive each selected outcome? Is state per recipient or shared? Are Unread, Read, and Acknowledged distinct and who may transition them?
- **Decision:** Notification delivery/read state is per recipient, never one shared Tenant flag. `Unread → Read` is monotonic; actionable notifications may additionally become `Acknowledged`, which implies Read and is terminal for notification acknowledgement only. One recipient's action never changes another recipient's state. Owner/Admin receive tenant-level critical security, billing, accounting, and operational exceptions; Staff receive operational notifications for capabilities they are allowed to act on; Viewer receives no actionable notifications in MVP. Acknowledging a notification never resolves its source business exception.
- **Rationale:** Per-recipient state matches real team use and avoids one person's click hiding work from others. Role/capability audience rules limit noise and sensitive exposure. Separating acknowledgement from source resolution preserves domain ownership.
- **Decision gate:** Resolved; TASK-0056/TASK-0085 require reconciliation before Ready.
- **Affected tasks:** TASK-0056, TASK-0085.
- **Approved policy constraint:** notification state is not source-domain state.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-033 — Audit coverage, readers, and security-denial visibility

- **Status:** Resolved
- **Question:** Which privileged successes/rejections require Audit evidence, which tenant roles may read it, and what details of attempted cross-tenant targets may be tenant-visible?
- **Decision:** Audit records successful and rejected privileged mutations, membership/role/security administration, accounting posting/correction actions, subscription/admin actions, and security-significant tenant-isolation denials with actor, trusted Tenant, action, outcome, timestamp, correlation, and safe reason metadata. Tenant Audit is readable by Owner/Admin only in MVP. Tenant-visible denial evidence must not reveal another Tenant/entity's existence or identifiers; cross-tenant target details may be retained only in appropriately protected platform-security evidence when genuinely required for investigation.
- **Rationale:** Privileged mutation and denial evidence supports accountability and incident analysis, while limiting tenant visibility prevents Audit itself from becoming a cross-tenant information leak.
- **Decision gate:** Resolved; TASK-0009 and later privileged actions require reconciliation before Ready.
- **Affected tasks:** TASK-0009 and every later privileged domain action.
- **Approved policy constraint:** Audit is append-oriented evidence and never business-state authority; tenant-visible evidence does not disclose another Tenant's data/existence.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

## 6. Subscription & Billing decisions

### PD-043 — Subscription acquisition, trial, and tenant-without-subscription policy

- **Status:** Resolved
- **Question:** After merchant registration, is subscription acquisition an automatic trial, explicit plan selection, platform provisioning, or another path? Is trial supported and may a Tenant exist/operate without an Active subscription?
- **Decision:** Successful Tenant registration automatically creates a **30-day Trial subscription** with a dedicated Trial terms/EntitlementSet; it is not inferred from a marketing plan name. Trial requires no payment method. Tenant identity/data may continue to exist without an Active/Trial subscription, but ordinary merchant mutations, scheduled automation, and public commerce require effective subscription entitlements. At trial expiry without a paid/subsequent subscription, the Subscription ends, public commerce and ordinary mutations are disabled, while authenticated merchant read/history/export/recovery access remains available and no business data is deleted.
- **Rationale:** Automatic no-card trial keeps onboarding simple and lets the project exercise the subscription/entitlement domain immediately. A dedicated Trial entitlement avoids coupling trial semantics to a future marketing plan. Read/recovery after expiry protects merchant data and makes subscription state independent of Tenant identity.
- **Why material:** this defines first entitlement effectivity and onboarding/subscription separation.
- **Decision gate:** Resolved; subscription-aware onboarding tasks require reconciliation before Ready.
- **Affected tasks:** TASK-0006–0009 when subscription-coupled, future Subscription acquisition/onboarding tasks, TASK-0092 architecture.
- **Approved policy constraint:** Tenant creation and Subscription creation remain separate facts even when onboarding orchestrates both.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain Architect Subscription/Billing reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-044 — Plan catalog, versioning, accepted terms, and commercial package policy

- **Status:** Deferred for exact commercial catalog/pricing; structural policy approved
- **Question:** Which initial plans/prices/terms are offered, how are versions retired, may accepted versions be edited, and is Enterprise/custom pricing in scope?
- **Interim human direction:** `Starter`, `Growth`, and `Business` remain candidate marketing packages only until a deliberate pricing exercise approves exact prices and entitlements; `Enterprise`/custom pricing is out of MVP. Domain design must use stable Plan identity plus immutable accepted PlanVersion/terms. Once a PlanVersion has been accepted by any Subscription it is never edited in place; changes require a new version. Plan versions may be withdrawn from new purchase without rewriting existing subscriptions/history.
- **Rationale:** Architecture needs immutable version semantics now, but inventing commercial prices/limits provides no engineering value and would falsely turn hypotheses into product truth. Deferring only the sellable catalog preserves flexibility without blocking the domain model.
- **Decision gate:** exact plan-selection/pricing/entitlement-definition tasks remain non-Ready until this commercial sub-decision is resolved.
- **Affected tasks:** future plan/subscription/entitlement UI and pricing tasks.
- **Safe interim constraint:** marketing plan names/prices are never hard-coded into unrelated domains; accepted terms/history are immutable.
- **Approved by:** CommerceOS human Product Owner — delegated defer/structural direction
- **Approved on:** 2026-08-10

### PD-045 — Billing-cycle and subscription-period policy

- **Status:** Resolved
- **Question:** Does the product support monthly, annual, both, or another billing cycle? How are anchors and month-end behavior defined?
- **Decision:** Paid MVP subscriptions use a monthly billing cycle only; annual billing is out of scope. A subscription records an explicit billing anchor at activation. Each next period advances one calendar month from the anchor; when the equivalent day does not exist, the period boundary uses the last valid day of that month. Effective periods are explicit non-overlapping intervals and do not rely on reporting/server timezone assumptions. Trial uses its separate fixed 30-day period under PD-043.
- **Rationale:** Monthly-only removes annual discounts, mixed-cycle migration, and long-duration proration while still exercising recurring subscription behavior. Explicit anchors and month-end rules avoid hidden date arithmetic bugs.
- **Decision gate:** Resolved; recurring-period/renewal tasks require reconciliation before Ready.
- **Affected tasks:** future SubscriptionPeriod, renewal, metering, billing-history, cancellation, upgrade/downgrade tasks.
- **Approved policy constraint:** every entitlement/charge period has explicit start/end evidence; no cadence is inferred from plan names.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain/Technical Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-046 — CommerceOS SaaS currency, tax, invoice, and proration policy

- **Status:** Resolved for learning/MVP
- **Question:** Which currency may CommerceOS charge, what precision/rounding applies, are taxes included/added/ignored, what does invoice mean, and is proration supported?
- **Decision:** CommerceOS SaaS learning/MVP charges are VND-only, whole đồng, with explicit Money currency. No currency conversion, tax calculation, tax-inclusive/exclusive logic, legally compliant tax invoice, or proration is supported. CommerceOS may present a billing statement/PlatformCharge record for traceability, but it must not be labeled a statutory/tax invoice. Mid-period plan changes follow PD-047/PD-048 rather than prorating unused time.
- **Rationale:** Tax/legal invoicing and multi-currency are jurisdiction-specific product/compliance programs, not useful assumptions for a learning MVP. Explicitly excluding them keeps PlatformCharge semantics honest and makes later compliance work deliberate.
- **Decision gate:** Resolved for MVP; any real tax/invoice/currency feature requires a new product/compliance decision.
- **Affected tasks:** future SaaS billing/charge/history and plan-change tasks.
- **Approved policy constraint:** no UI/document may imply legal tax-invoice compliance in MVP.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain/Technical Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-047 — Upgrade effective-time and charge-precondition policy

- **Status:** Resolved
- **Question:** Is upgrade immediate or next-cycle, what charge condition is required, and how does proration interact?
- **Decision:** An upgrade is immediate only after the required new-plan PlatformCharge has a verified successful outcome. Because MVP has no proration, a successful mid-cycle upgrade starts a fresh monthly subscription period at the upgraded terms and grants the new EntitlementSet at that same effective boundary; unused old-period value is not credited in MVP. If charge outcome is Declined or Unknown, higher entitlements do not become effective and the existing subscription remains authoritative.
- **Rationale:** Immediate upgrades provide clear merchant value while verified-charge-first prevents free accidental entitlement escalation. Resetting the billing period is simpler and more deterministic than proration for the simulated-billing MVP.
- **Decision gate:** Resolved; upgrade execution tasks require reconciliation before Ready.
- **Affected tasks:** future plan-change, EntitlementSet, PlatformCharge, merchant subscription UI/history tasks.
- **Approved policy constraint:** `PlanChangeRequested` never grants higher entitlements; Unknown is not success.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain/Technical Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-048 — Downgrade timing and excess-resource remediation policy

- **Status:** Resolved
- **Question:** Is downgrade immediate or next-cycle, and what happens if current usage exceeds target limits?
- **Decision:** Downgrade is scheduled for the next renewal boundary, not immediate. Before effectivity, authoritative owning-domain usage is revalidated against target hard limits. If usage exceeds a target hard limit, the downgrade remains `BlockedByUsage/RemediationRequired`; no lower EntitlementSet becomes effective and CommerceOS continues the current plan/terms for the next period until a later eligible downgrade boundary. Merchant remediation must use normal owning-domain actions; CommerceOS never auto-deletes/disables resources to fit the lower plan. No temporary overage/grandfathered write expansion is granted by the downgrade itself.
- **Rationale:** Next-cycle downgrade avoids refund/proration complexity and gives merchants time to remediate. Keeping the current plan active when blocked is safer than either destructive enforcement or silently granting lower-price higher usage.
- **Decision gate:** Resolved; downgrade/remediation tasks require reconciliation before Ready.
- **Affected tasks:** future plan-change plus Merchant Access, Inventory, Ingestion and other entitlement-enforced tasks.
- **Approved policy constraint:** downgrade cannot become effective while authoritative current usage violates target hard limits.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain/Technical Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-049 — Cancellation, expiry, grace, delinquency, reactivation, suspension, and retention policy

- **Status:** Resolved for MVP
- **Question:** Is cancellation immediate or end-of-cycle? What happens at expiry/failed renewal, what access remains, can subscriptions reactivate, and how long is data retained?
- **Decision:** Merchant cancellation means **cancel renewal** and becomes effective at the current paid period end; entitlements remain unchanged until that boundary. A definitive renewal-charge failure enters `PastDue` with a 7-day grace period during which existing entitlements continue. Unknown billing outcome is not PastDue until resolved. If grace ends without successful renewal, Subscription becomes Ended: ordinary merchant mutations, scheduled automation, and public commerce are disabled, while authenticated merchant read/history/export/recovery access remains. Reactivation starts a new subscription period/terms; it does not rewrite the ended history. MVP performs no automatic Tenant/business-data deletion because a Subscription ends; data is retained until a future explicit retention/privacy policy supersedes this rule.
- **Rationale:** End-of-period cancellation and short grace avoid abrupt service loss and proration. Read/recovery access protects merchants while keeping paid operational capabilities gated. Indefinite MVP retention is safer than inventing legal deletion windows before privacy/closure policy exists.
- **Decision gate:** Resolved for MVP; any destructive retention/deletion feature requires a later explicit policy.
- **Affected tasks:** future lifecycle/recovery/support tasks and subscription-based ordinary-operation gating.
- **Approved policy constraint:** billing failure/cancellation never mutates TenantStatus, disables Memberships, or deletes business data by implication.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain/Technical Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-050 — Hard, soft, overage, unlimited, and enforcement-point policy

- **Status:** Resolved for MVP policy categories
- **Question:** For each entitlement/limit, is it hard, soft, overage, grace, or unlimited, and what operations remain while over limit?
- **Decision:** MVP uses explicit entitlement types rather than one generic limit rule. Capability flags such as scheduled ingestion/API access are hard gates at the owning command boundary while preserving read/history/recovery access. Counted resource limits such as MaxActiveStaff and MaxWarehouses are hard **growth/activation** limits: creation/activation that would exceed the limit is rejected, but existing over-limit resources caused by a pending downgrade are not destroyed and remain readable/manageable for remediation. Order-volume is soft-warning only under PD-051. Overage billing is not supported. `Unlimited` is an explicit entitlement value, never a missing record.
- **Rationale:** Different limits have different operational risk. Hard growth gates protect plan boundaries without destructive cleanup, while order-volume remains non-disruptive. Explicit Unlimited prevents absence/error states from becoming accidental permission.
- **Decision gate:** Resolved at category level; each new entitlement still must name its category/enforcement point before its implementing task is Ready.
- **Affected tasks:** future Subscription/Entitlement work and every entitlement-enforced domain task.
- **Approved policy constraint:** stale UI/Reporting projections and plan-name checks never authorize writes; read/recovery is not blocked merely because current usage exceeds a hard growth limit.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain/Technical Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-051 — Order-volume limit and shopper-checkout behavior

- **Status:** Resolved
- **Question:** How is order volume counted, what window applies, and may reaching a threshold reject shopper checkout?
- **Decision:** MVP order-volume usage counts idempotent `OrderConfirmed` facts within the current Subscription billing period. Reaching/exceeding the configured threshold is warning/operational follow-up only. It never rejects otherwise valid shopper checkout, never cancels an Order, and creates no automatic overage charge in MVP. Merchant and platform-admin projections may surface current usage and threshold crossing.
- **Rationale:** Subscription limits should not unexpectedly break the merchant's customer checkout flow. Counting confirmed Orders provides a stable accepted-business fact, while warning-only usage still exercises metering/entitlement architecture without revenue-impacting behavior.
- **Decision gate:** Resolved; metering/reporting tasks require reconciliation before Ready.
- **Affected tasks:** future UsageMeter/order-volume work, Sales checkout, Reporting/platform-admin usage views.
- **Approved policy constraint:** order-volume threshold cannot deny shopper checkout in MVP.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain/Technical Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-052 — SaaS billing-provider strategy for learning/MVP and later real operation

- **Status:** Resolved
- **Question:** Is learning/MVP billing domain-only, simulated, or connected to a real provider, and what is the later transition intent?
- **Decision:** MVP uses a **dedicated simulated SaaS billing-provider seam** for CommerceOS PlatformCharge behavior. It is separate from the merchant-order Mock Payment Provider/Payments context. No real merchant card/bank data is stored and no real money is charged. The simulation must support success, decline/no-commit, timeout/OutcomeUnknown, duplicate delivery, retry/idempotency, query/reconciliation, and out-of-order evidence so the domain is designed for a later real provider. Choosing a real provider is a future product/architecture/compliance decision behind the same provider boundary.
- **Rationale:** Simulation allows the project to learn billing lifecycle, ambiguity, idempotency, webhook/reconciliation, and provider abstraction without compliance/cost/vendor lock-in. A dedicated seam prevents SaaS billing from contaminating shopper Payment ownership.
- **Decision gate:** Resolved; TASK-0092 may design the provider boundary but must remain provider-agnostic.
- **Affected tasks:** TASK-0092, future PlatformCharge/billing attempt/webhook/reconciliation tasks.
- **Approved policy constraint:** no real card data, no real provider assumption, and no reuse of merchant-order Payments merely for convenience.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Technical Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

### PD-053 — Platform-admin subscription/billing support and override authority

- **Status:** Resolved for MVP
- **Question:** Which platform administrators may manually assign/change/cancel/reactivate plans, adjust billing evidence, or override entitlement/usage restrictions?
- **Decision:** MVP platform administration for Subscription & Billing is **read/support visibility only**. Platform administrators may inspect tenant subscription, entitlement, usage, PlatformCharge, and reconciliation projections when authorized, but may not manually comp/assign/change plans, cancel/reactivate subscriptions, mutate charge outcomes, or bypass entitlement limits. No hidden cross-tenant override exists. Any future mutation capability must be added as an explicit Subscription & Billing business command with its own Product Owner decision, authorization policy, tenant-notice/consent policy where applicable, and Audit evidence.
- **Rationale:** Read-only support provides operational visibility without creating a privileged backdoor around subscription truth, tenant isolation, or financial evidence. Override workflows are high-risk and can be introduced later when a concrete support need justifies them.
- **Decision gate:** Resolved for MVP; mutation/support-override tasks remain out of scope until a later explicit decision.
- **Affected tasks:** future platform-admin subscription/billing operations, Audit, support/recovery tooling.
- **Approved policy constraint:** platform-admin visibility never becomes authority to mutate tenant subscription/billing truth.
- **Approved by:** CommerceOS human Product Owner — delegated resolution instruction
- **Approved on:** 2026-08-10
- **Affected baseline documents updated:** Pending Domain/Technical Architect reconciliation.
- **Affected candidate tasks notified:** Pending Backlog Planner reconciliation.

## 7. Resolution template

When the human resolves an entry, replace its status and append the complete resolution record below. **Rationale is mandatory**; a Decision without a Rationale is not a complete product resolution.

```text
Decision:
Rationale (why this option, important trade-offs, and why deferred complexity is not needed now):
Approved by / resolution authority:
Approved on:
Affected baseline documents updated:
Affected candidate tasks notified:
```

If resolution authority is explicitly delegated to an agent for a decision pass, record that fact rather than implying the human individually authored every option.

If baseline/task propagation cannot be completed in the same change, record it explicitly as `Pending <responsible role> reconciliation` rather than claiming it is updated. The product decision itself may be Resolved while propagation is pending, but an affected candidate task does not become Ready until the responsible architecture/domain/backlog reconciliation has completed.

The Domain Architect, Technical Architect, and Backlog Planner determine downstream consequences. Resolution does not by itself make a candidate task Ready.