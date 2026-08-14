# CommerceOS Dependency Map

_Last updated: 2026-08-14_

## Feature dependency graph

```mermaid
flowchart TD
  F01["F01 Foundation & Harness"] --> F02["F02 Tenant & Merchant Access"]
  F01 --> F03["F03 Subscription & Billing"]
  F02 --> F03
  F02 --> F04["F04 Catalog & Files/Media"]
  F03 --> F04
  F04 --> F05["F05 Product Data Ingestion"]
  F02 --> F06["F06 Storefront, Cart & Checkout"]
  F03 --> F06
  F04 --> F06
  F06 --> F07["F07 Sales, Inventory & Payments"]
  F03 --> F07
  F07 --> F08["F08 Durable Order Orchestration"]
  F04 --> F09["F09 Procurement"]
  F07 --> F09
  F07 --> F10["F10 Accounting"]
  F09 --> F10
  F10 --> F11["F11 Reporting"]
  F07 --> F11
  F07 --> F12["F12 Returns & Refunds"]
  F10 --> F12
  F02 --> F13["F13 Notification & Audit"]
  F07 --> F13
  F10 --> F13
  F05 --> F13
  F08 --> F14["F14 Platform Hardening"]
  F11 --> F14
  F12 --> F14
  F13 --> F14
  F14 --> F15["F15 Later CRM & Pricing"]
  F14 --> F16["F16 UI Delivery & API Surfaces"]
  F15 --> F16
```

## Task dependency graph — core capability path

```mermaid
flowchart TD
  T0100["TASK-0100 Verify foundation baseline"] --> T0101["TASK-0101 Deterministic lifecycle gaps"]
  T0100 --> T0102["TASK-0102 LocalStack CI verification"]
  T0100 --> T0103["TASK-0103 Architecture/config guardrails"]
  T0101 --> T0110["TASK-0110 Tenancy module + persistence"]
  T0110 --> T0111["TASK-0111 Trusted tenant authority"]
  T0110 --> T0112["TASK-0112 Membership + invitation"]
  T0111 --> T0113["TASK-0113 Onboarding + Trial recovery"]
  T0120["TASK-0120 Subscription catalog + Trial terms"] --> T0113
  T0120 --> T0121["TASK-0121 Entitlement authority"]
  T0121 --> T0122["TASK-0122 Resource limits"]
  T0120 --> T0123["TASK-0123 SaaS billing provider + PlatformCharge"]
  T0123 --> T0124["TASK-0124 Paid lifecycle"]
  T0124 --> T0125["TASK-0125 Usage warnings/support queries"]
  T0121 --> T0130["TASK-0130 Catalog module"]
  T0130 --> T0131["TASK-0131 Product lifecycle"]
  T0131 --> T0132["TASK-0132 Category/Brand/spec"]
  T0131 --> T0133["TASK-0133 Files/Media"]
  T0131 --> T0134["TASK-0134 Source mapping/import apply"]
  T0134 --> T0140["TASK-0140 PDI source governance"]
  T0140 --> T0141["TASK-0141 Adapter/manual ingest"]
  T0141 --> T0142["TASK-0142 Queue/snapshot/normalize"]
  T0142 --> T0143["TASK-0143 Review/apply"]
  T0143 --> T0144["TASK-0144 Scheduled refresh"]
  T0111 --> T0150["TASK-0150 Resolve storefront tenant addressing"]
  T0150 --> T0151["TASK-0151 Public tenant/storefront reads"]
  T0131 --> T0151
  T0151 --> T0152["TASK-0152 Cart"]
  T0152 --> T0153["TASK-0153 Checkout validation"]
  T0153 --> T0154["TASK-0154 Idempotent order placement"]
  T0154 --> T0160["TASK-0160 Sales order module"]
  T0121 --> T0161["TASK-0161 Inventory foundation"]
  T0161 --> T0162["TASK-0162 Stock operations"]
  T0160 --> T0163["TASK-0163 Payments module"]
  T0163 --> T0164["TASK-0164 Mock provider"]
  T0164 --> T0165["TASK-0165 Evidence/webhook/reconciliation"]
  T0162 --> T0166["TASK-0166 Cancellation/release integration"]
  T0165 --> T0166
  T0160 --> T0170["TASK-0170 Durable order workflow"]
  T0162 --> T0170
  T0165 --> T0170
  T0170 --> T0171["TASK-0171 Workflow failure recovery"]
  T0171 --> T0172["TASK-0172 LocalStack workflow verification"]
  T0131 --> T0180["TASK-0180 Supplier + PO"]
  T0180 --> T0181["TASK-0181 Goods receipt"]
  T0181 --> T0182["TASK-0182 Inventory integration"]
  T0181 --> T0183["TASK-0183 Invoice/payment evidence"]
  T0190["TASK-0190 Resolve valuation cost pool"] --> T0191["TASK-0191 Accounting foundation"]
  T0191 --> T0192["TASK-0192 Journal posting/reversal"]
  T0165 --> T0193["TASK-0193 Sales accounting consumers"]
  T0162 --> T0193
  T0192 --> T0193
  T0183 --> T0194["TASK-0194 Procurement/adjustment accounting"]
  T0192 --> T0194
  T0193 --> T0195["TASK-0195 GL/trial balance + reliable routes"]
  T0194 --> T0195
  T0195 --> T0200["TASK-0200 Reporting projections"]
  T0160 --> T0200
  T0200 --> T0201["TASK-0201 Operational KPIs"]
  T0195 --> T0202["TASK-0202 Financial/back-office reports"]
  T0210["TASK-0210 Resolve refund approval capability"] --> T0211["TASK-0211 Refund request/review"]
  T0160 --> T0211
  T0211 --> T0212["TASK-0212 RefundApproved choreography"]
  T0162 --> T0212
  T0165 --> T0212
  T0195 --> T0212
  T0212 --> T0213["TASK-0213 Inventory/payment refund effects"]
  T0213 --> T0214["TASK-0214 Accounting/progress projection"]
  T0114["TASK-0114 Tenant admin lifecycle + Audit intents"] --> T0220["TASK-0220 Audit module"]
  T0220 --> T0221["TASK-0221 Audit integrations"]
  T0200 --> T0222["TASK-0222 Notifications"]
  T0214 --> T0222
  T0172 --> T0230["TASK-0230 Observability/correlation"]
  T0222 --> T0230
  T0230 --> T0231["TASK-0231 DLQ/recovery tooling"]
  T0231 --> T0232["TASK-0232 Security/tenant campaign"]
  T0232 --> T0233["TASK-0233 Harness/architecture expansion"]
  T0233 --> T0234["TASK-0234 Architecture audit/extraction assessment"]
  T0200 --> T0240["TASK-0240 Explicit Customer/CRM profiles"]
  T0241["TASK-0241 Pricing product/domain semantics"] --> T0242["TASK-0242 Pricing technical design"]
  T0234 --> T0242
  T0242 --> T0243["TASK-0243 Scheduled Product promotional price"]
  T0131 --> T0243
  T0153 --> T0243
  T0154 --> T0243
  T0195 --> T0243
```

Core capability tasks through TASK-0243 are complete at the currently planned slices. F16 adds delivery/read-model/API/UI work rather than reopening those business semantics.

## F16 task dependency graph — UI delivery

```mermaid
flowchart TD
  T0234["TASK-0234 Architecture audit — Done"] --> T0244["TASK-0244 UI foundation — Ready"]

  T0151["TASK-0151 Public Storefront reads — Done"] --> T0246["TASK-0246 Storefront HTTP delivery — Ready"]
  T0243["TASK-0243 Pricing implementation — Done"] --> T0246
  T0244 --> T0247["TASK-0247 Storefront UI"]
  T0246 --> T0247

  T0244 --> T0245["TASK-0245 Merchant session/current Tenant"]
  T0111["TASK-0111 Tenant authority — Done"] --> T0245
  T0112["TASK-0112 Membership — Done"] --> T0245

  T0245 --> T0248["TASK-0248 Catalog API/read models"]
  T0244 --> T0249["TASK-0249 Catalog UI"]
  T0248 --> T0249

  T0244 --> T0250["TASK-0250 PDI delivery + UI"]
  T0245 --> T0250

  T0245 --> T0251["TASK-0251 Sales/refund API/read models"]
  T0244 --> T0252["TASK-0252 Sales/refund UI"]
  T0251 --> T0252

  T0245 --> T0253["TASK-0253 Inventory API/read models"]
  T0244 --> T0254["TASK-0254 Inventory UI"]
  T0253 --> T0254

  T0245 --> T0255["TASK-0255 Procurement delivery completion"]
  T0244 --> T0256["TASK-0256 Procurement UI"]
  T0255 --> T0256

  T0245 --> T0257["TASK-0257 Accounting/Reporting HTTP"]
  T0244 --> T0258["TASK-0258 Dashboard/Accounting/Reports UI"]
  T0257 --> T0258

  T0245 --> T0259["TASK-0259 Customer/Pricing HTTP"]
  T0240["TASK-0240 Customer — Done"] --> T0259
  T0243 --> T0259
  T0244 --> T0260["TASK-0260 Customer/Promotion UI"]
  T0259 --> T0260

  T0245 --> T0261["TASK-0261 Settings/Team/Sub/Notif/Audit HTTP"]
  T0244 --> T0262["TASK-0262 Settings/Operations UI"]
  T0261 --> T0262

  T0244 --> T0263["TASK-0263 Platform Admin API/UI"]
  T0114["TASK-0114 Platform Tenant lifecycle — Done"] --> T0263
  T0231["TASK-0231 Operator diagnostics — Done"] --> T0263

  T0247 --> T0264["TASK-0264 Cross-app E2E hardening"]
  T0249 --> T0264
  T0250 --> T0264
  T0252 --> T0264
  T0254 --> T0264
  T0256 --> T0264
  T0258 --> T0264
  T0260 --> T0264
  T0262 --> T0264
  T0263 --> T0264
```

## F16 blocker table

| Blocker | Affected tasks | Resolution |
|---|---|---|
| Shared frontend routing/API/error/test conventions are not established | Most F16 UI tasks | `TASK-0244`; first Ready frontier |
| Merchant HTTP requests do not yet have a normal authenticated current-Tenant delivery context | Merchant Backoffice APIs/screens | `TASK-0245`; must derive authority from current Membership, never client `tenantId` |
| Public Storefront Application capabilities are not exposed as complete UI-oriented HTTP DTOs | SF-01..SF-05 | `TASK-0246`; independent first Ready frontier |
| Merchant Catalog list/detail and reference-list queries are incomplete | BO-10..BO-12 | `TASK-0248` |
| PDI source/enrollment/candidate list/run-status reads are incomplete | BO-13 | `TASK-0250` |
| Order detail/refund list and cross-domain operational summaries need explicit read models | BO-20..BO-22 | `TASK-0251`; no foreign-table joins |
| Warehouse/stock/movement lists and explicit adjustment-increase command are incomplete | BO-30..BO-33 | `TASK-0253` |
| Procurement Draft PO/create/update/cancel and several list/detail read surfaces are incomplete | BO-40..BO-43 | `TASK-0255`; do not invent richer PO semantics |
| Accounting/Reporting are application-ready but mostly lack HTTP/read DTO delivery | BO-01, BO-50..BO-60 | `TASK-0257` |
| Customer/Pricing are implemented but lack merchant UI delivery/list projections | BO-61..BO-62 | `TASK-0259` |
| Business/Storefront settings, Team lists, Subscription reads, Notification/Audit HTTP delivery are incomplete | BO-70..BO-75 | `TASK-0261` |
| Platform Tenant list/support dashboard contracts are incomplete | PA-01..PA-04 | `TASK-0263` |
| Safe anonymous Order history/status lookup is not approved | Any future shopper order-history screen | Not part of F16; SF-05 uses immediate placement response. A later anonymous lookup needs an opaque guest access capability/product decision. |

## Parallelism guidance

F16 begins with exactly two independent Ready tasks: `TASK-0244` and `TASK-0246`. This matches the repository's default two-Builder concurrency ceiling if automated parallel execution is used.

After `TASK-0244`, `TASK-0245` may start while `TASK-0246` finishes or `TASK-0247` begins. After `TASK-0245`, the merchant-domain API/read-model tasks (`TASK-0248`, `0251`, `0253`, `0255`, `0257`, `0259`, `0261`) are logically independent, but automated execution should still respect the default concurrency ceiling and avoid simultaneous edits to shared API composition/frontend shell files unless worktrees/ownership are isolated.

`TASK-0264` is the final integration/hardening gate and should not be used to bypass missing owner-domain contracts discovered earlier.