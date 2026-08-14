# F16 — UI Delivery & API Surfaces

## Feature goal
Turn the implemented CommerceOS domain/application capabilities into usable Storefront, Merchant Backoffice and Platform Administration experiences without weakening tenant authority, domain ownership, pricing/checkout authority, accounting immutability or LocalStack-only runtime rules.

This plan is screen-first but backend-grounded. A screen is not considered implementation-ready merely because domain logic exists: the required read model, command/query contract and HTTP delivery surface must also exist.

## Source requirements
Existing approved requirements only. No new product semantics are introduced by this feature.

Primary requirement families: REQ-SEC-001..003, REQ-TEN-001..005, REQ-SUB-001..008, REQ-CAT-001..006, REQ-MED-001, REQ-PDI-001..007, REQ-STO-001..002, REQ-CHK-001..002, REQ-SAL-001..003, REQ-INV-001..003, REQ-PROC-001..003, REQ-ACC-001..005, REQ-REP-001..002, REQ-REF-001..004, REQ-NOT-001, REQ-AUD-001, REQ-CRM-001, REQ-PRI-001, REQ-OBS-001 and REQ-HARD-002..003.

## Current delivery baseline

- `apps/storefront` and `apps/backoffice` exist as React/Vite foundations.
- `CommerceOS.Api` currently exposes `/health` and merchant onboarding; most module capabilities exist only at Application/Contracts/Infrastructure layers.
- Public storefront list/detail, checkout validation and order placement application services already exist.
- Merchant Catalog, Sales, Inventory, Procurement, Accounting, Reporting, Customer, Pricing, Notification, Audit, Tenancy and Subscription/Billing capabilities exist to different degrees, but many lack UI-oriented list/detail projections or HTTP endpoints.
- Merchant HTTP delivery must derive trusted Tenant context from authenticated/current Membership. Client-supplied `tenantId` is never authorization.
- Storefront anonymous delivery uses trusted `PublicTenantContext` derived from storefront slug and current Tenant state.
- LocalStack remains the only infrastructure/runtime target.

## Delivery principles

1. UI never reads module persistence directly or invents cross-domain joins.
2. HTTP endpoints adapt trusted identity/public routing into application contracts; they do not move business rules into controllers/endpoints.
3. List/detail screens use owner-produced or explicitly composed read models rather than leaking domain entities by default.
4. Mutations carry revision/idempotency information where the underlying application contract requires it.
5. Loading, empty, forbidden, not-found, conflict, stale-revision, validation and service-unavailable states are first-class UI states.
6. Owner/Admin/Staff/Viewer visibility is capability-driven. Do not restore superseded Sales/Warehouse/Accountant membership roles in navigation.
7. Browser cart price, discount, timestamp and total remain untrusted; authoritative checkout and Pricing rules remain server-owned.
8. Storefront and Backoffice may be developed independently where dependencies allow, while preserving the repository default two-Builder concurrency ceiling if agent automation is used.

## API readiness vocabulary

- `HTTP` — an HTTP endpoint is already mapped.
- `Backend-ready` — application/domain capability exists; add delivery endpoint/DTO/composition.
- `Partial` — some application capability exists but a UI-required query/read model/command is missing.
- `Missing` — capability must be added before the screen can be production-complete.

## Screen inventory and API contract plan

### Storefront

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| SF-01 Product listing | `/{storefrontSlug}` | Store display name; search; product image/name/SKU; base/effective price; promotion indicator/end; availability; category/brand; add to cart; cursor pagination | Public list service exists. Add `GET /api/v1/storefronts/{slug}` and `GET /api/v1/storefronts/{slug}/products`; enrich public projection with approved media/classification/availability/Pricing fields. |
| SF-02 Product detail | `/{storefrontSlug}/products/{productSlug}` | Gallery; name/SKU; base/effective price; promotion; availability; category/brand; specifications; quantity; add to cart | Public detail service exists but current DTO is too small. Add enriched detail DTO and `GET /api/v1/storefronts/{slug}/products/{productSlug}`. |
| SF-03 Cart | `/{storefrontSlug}/cart` | Lines; estimated unit/line totals; quantity update/remove/clear; estimated subtotal; checkout CTA | Browser-local/transient; no authoritative cart persistence/API required. |
| SF-04 Checkout | `/{storefrontSlug}/checkout` | Guest name/email/phone/address; authoritative line prices; total; reconfirmation when price changes | Application validation/place services exist. Add `POST /api/v1/storefronts/{slug}/checkout/validate` and `POST /api/v1/storefronts/{slug}/orders`; preserve idempotency and PD-011 reconfirmation. |
| SF-05 Order confirmation | `/{storefrontSlug}/order-confirmation/{orderId}` | Order id/status, accepted immutable lines/total, guest snapshot, processing message | Placement response exists; safe anonymous post-checkout read authority is not yet defined. First implementation may render the accepted placement response without creating an insecure anonymous order lookup. Any later lookup requires an opaque guest access capability/product decision. |

### Merchant Backoffice — shared shell

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| BO-00 App shell / tenant selection | `/app/*` | Current subject, memberships, selected Tenant, Tenant name/status, role/capabilities, navigation, sign-out/local identity behavior | Partial. Add authenticated/current-subject and membership/tenant-selection delivery boundary; selected Tenant must be server-revalidated. |
| BO-01 Dashboard | `/app` | Orders, order value, AOV, failed-payment rate, cancelled/refunded amount, top products, projection freshness, critical notifications | Reporting queries exist. Add HTTP KPI/freshness endpoints and notification summary. |

### Catalog

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| BO-10 Products | `/app/catalog/products` | Image, name, SKU, base/effective price where useful, category, brand, lifecycle status, revision; search/filter; create/edit/publish/unpublish/archive | Product mutations exist; merchant list/detail queries are missing/partial. Add tenant-scoped product list/detail read model + HTTP mutations. |
| BO-11 Product editor | `/app/catalog/products/new`, `/app/catalog/products/{id}` | Name, SKU, slug, base price, status, category, brand, specifications, media; save/publish/unpublish/archive | General mutation/media services exist. Complete explicit assign references/specifications commands if needed and expose HTTP. |
| BO-12 Categories & Brands | `/app/catalog/references` | Category/Brand name/status/revision; create/retire | Create/retire exists; list queries missing. |

### Product Data Ingestion

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| BO-13 Product import | `/app/catalog/import` | Source registry/enrollment eligibility; manual URL request; candidate queue/detail; proposed fields/provenance; approve/reject/apply; run state | Manual ingest and candidate get/review/apply exist; source/enrollment/candidate list and run-status queries are partial/missing. |

### Sales & Refunds

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| BO-20 Orders | `/app/sales/orders` | Order id, customer snapshot summary, status, total, created time, cursor/filter | Sales list exists; UI projection should include stable created/observed time and safe summaries. |
| BO-21 Order detail | `/app/sales/orders/{id}` | Immutable lines/guest/total/status/revision plus payment, inventory/workflow/refund summaries; cancel/request-refund actions | Sales get/cancel exist. Cross-domain operational status needs explicit composition/read model, never foreign-table reads. |
| BO-22 Refunds | `/app/sales/refunds` | Refund id/order/payment/amount/lines/status/requested/decided metadata; request/approve/reject | Commands exist; refund list/detail queries are missing. |

### Inventory

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| BO-30 Stock | `/app/inventory/stock` | Product, warehouse, OnHand, Reserved, Available; filter; adjustment action | Point lookup/availability exists; stock list projection missing. |
| BO-31 Warehouses | `/app/inventory/warehouses` | Name/status/revision; create/disable/reactivate | Commands exist; list query missing. |
| BO-32 Movements | `/app/inventory/movements` | Time, product, warehouse, type, quantity, source, correlation id | Movements are evidence but list/query surface is missing. |
| BO-33 Adjust stock | Dialog from Stock | Product, warehouse, increase/decrease, quantity, source/reason | Decrease exists; domain supports increase but explicit application command may need adding. |

### Procurement

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| BO-40 Suppliers | `/app/purchasing/suppliers` | Display name/status/revision; create/archive | Commands exist; list query missing. Do not invent phone/email/address fields. |
| BO-41 Purchase Orders | `/app/purchasing/orders`, `/app/purchasing/orders/{id}` | Supplier; lines with product/name/SKU snapshot, quantity, unit price, totals, status/revision; create/edit draft, submit, cancel | Submit/get exists; list/create/update/cancel UI contracts need completion. |
| BO-42 Goods receipts | `/app/purchasing/orders/{poId}/receipts` | PO, lines, warehouse, status, corrections; create/confirm/correct | Confirm/correct exists; create/list/detail delivery surfaces need completion. |
| BO-43 Supplier invoices | `/app/purchasing/invoices` | Reference, PO, date, expected/actual amount, variance, status/payment evidence; record/approve variance/record payment | Commands exist; list/detail/payment query surfaces missing. |

### Accounting & Reporting

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| BO-50 Chart of Accounts | `/app/accounting/accounts` | Code/name/role/status/control flag; bootstrap/add non-control/deactivate | Commands/get exist; list query missing. |
| BO-51 Journals | `/app/accounting/journals`, `/app/accounting/journals/{id}` | Effective date, posting time, source, reversal, lines/debit/credit; post permitted manual journal/reverse | List/get/post/reverse application capabilities exist; expose HTTP. |
| BO-52 General Ledger | `/app/accounting/general-ledger` | Date filters; journal/account/debit/credit rows; cursor | Query exists; expose HTTP/read DTO. |
| BO-53 Trial Balance | `/app/accounting/trial-balance` | Through date; account/debit/credit/balance | Query exists; expose HTTP. |
| BO-60 Reports | `/app/reports` | Operational KPIs, financial view, refund progress and projection freshness | Reporting application queries exist; expose HTTP and freshness/checkpoint metadata. |

### Customer & Pricing

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| BO-61 Customers | `/app/customers`, `/app/customers/{id}` | Display name/email/phone/preferences/revision; search/list/create/update; optional explicit order association only if existing policy/contracts support it | Customer list/get/create/update now exist. Expose trusted merchant HTTP API; do not auto-link guest orders. |
| BO-62 Promotions | `/app/pricing/promotions` | Product, final promotional VND price, effective from/until, derived temporal state, cancellation, whether currently beneficial; schedule/cancel | Pricing schedule/cancel/effective-price exist; add merchant list/detail/schedule projection and HTTP. Preserve immutable terms/no overlap/Owner-Admin mutation. |

### Merchant administration & operations

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| BO-70 Business settings | `/app/settings/business` | DisplayName, TimeZoneIana, Tenant status/revision; update approved profile fields | Onboarding writes profile; normal merchant profile get/update delivery is partial/missing. |
| BO-71 Storefront settings | `/app/settings/storefront` | Storefront slug/public URL/status; configure approved slug semantics | Tenant model/read routing exists; merchant configuration contract/API is partial/missing. |
| BO-72 Team | `/app/settings/team` | Members and pending invitations with role/status/revision/expiry; invite/resend/revoke/change role/status | Mutations exist; member/invitation list queries need delivery surface. |
| BO-73 Subscription | `/app/settings/subscription` | Current plan/version/condition/period/anchor/cancel renewal/pending downgrade/entitlements; available plans; activate/upgrade/downgrade/cancel renewal | Catalog and paid lifecycle services exist; expose trusted merchant HTTP API/read model. |
| BO-74 Notifications | `/app/notifications` | Summary/source/time/read/ack state; mark read/acknowledge | Application list/read/ack exists; expose HTTP. |
| BO-75 Audit | `/app/settings/audit` | Time/action/entity/source/correlation/safe evidence fields; date paging/filter where supported | Tenant audit query exists and is Owner/Admin protected; expose HTTP without sensitive payload leakage. |

### Platform administration

| Screen | Route | Required information / actions | Delivery status and required APIs |
|---|---|---|---|
| PA-01 Platform dashboard | `/platform` | Health, operational failure summaries, DLQ/workflow/crawler signals | `/health` exists; aggregate support summaries are partial and must use explicit platform support contracts. |
| PA-02 Tenants | `/platform/tenants` | Tenant id/name/status/plan/support summary; search/list | Platform tenant get exists; tenant list query missing. |
| PA-03 Tenant detail | `/platform/tenants/{tenantId}` | Tenant profile/status/subscription/support metadata; suspend/reactivate with reason | Platform get/suspend/reactivate exists; add HTTP composition. |
| PA-04 Security audit | `/platform/audit` | Platform security audit evidence | Query exists behind explicit platform security-audit authority; expose HTTP. |

## Shared frontend foundation

Both apps should standardize:

- router and route-level layouts;
- API base URL/configuration and typed request wrappers;
- RFC 7807/ProblemDetails mapping;
- cursor pagination helpers;
- idempotency-key helpers for retried browser mutations where required;
- revision-conflict handling and stale-data refresh;
- loading/empty/error/forbidden/not-found/service-unavailable states;
- tenant/public routing context;
- capability guards for navigation/actions;
- form validation and server-error mapping;
- accessible dialogs/tables/forms/navigation;
- responsive behavior;
- unit/component tests and selected browser E2E tests.

Do not add a speculative global state framework or generated API client unless the implementation task demonstrates a concrete need and records the choice.

## Task sequence

- `TASK-0244` — UI foundation and shared delivery conventions.
- `TASK-0245` — authenticated merchant session/current-Tenant delivery boundary and Backoffice shell.
- `TASK-0246` — public Storefront HTTP/read-model delivery.
- `TASK-0247` — Storefront browse/cart/checkout/order-confirmation UI.
- `TASK-0248` — merchant Catalog read models and HTTP delivery.
- `TASK-0249` — Catalog/Product/Category/Brand Backoffice UI.
- `TASK-0250` — PDI delivery gaps plus Product Import UI.
- `TASK-0251` — Sales/refund read models and HTTP delivery.
- `TASK-0252` — Orders/Order detail/Refunds UI.
- `TASK-0253` — Inventory read models/commands and HTTP delivery.
- `TASK-0254` — Stock/Warehouse/Movement UI.
- `TASK-0255` — Procurement UI-required application/query/HTTP completion.
- `TASK-0256` — Supplier/PO/Receipt/Invoice UI.
- `TASK-0257` — Accounting/Reporting HTTP/read-model delivery.
- `TASK-0258` — Dashboard/Accounting/Reporting UI.
- `TASK-0259` — Customer and Pricing merchant HTTP/read-model delivery.
- `TASK-0260` — Customer and Promotion UI.
- `TASK-0261` — Tenant/Team/Subscription/Notification/Audit merchant HTTP delivery.
- `TASK-0262` — Settings/Team/Subscription/Notification/Audit UI.
- `TASK-0263` — Platform Admin support API and UI.
- `TASK-0264` — cross-app E2E, accessibility, responsive, authorization and LocalStack delivery hardening.

## Parallelism / first frontier

The first safe frontier is deliberately small:

- `TASK-0244` can start immediately because both frontend apps already exist and the task does not require new product semantics.
- `TASK-0246` can start in parallel because public Storefront authority is independent of merchant authenticated-session delivery and the underlying Storefront/Catalog/Inventory/Pricing/checkout implementations are complete.
- Merchant feature screens wait for `TASK-0245` so they do not invent insecure tenant selection or test-header coupling.

## Definition of Done

F16 is complete when the approved Storefront, merchant Backoffice and Platform Admin journeys are implemented against explicit HTTP/application contracts; UI state does not become business authority; tenant/public authorization boundaries are preserved; critical workflows have browser-level regression coverage; responsive/accessibility/error states are addressed; and the repository harness plus selected LocalStack end-to-end verification pass without bypassing guardrails.