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
```

## Task dependency graph — critical path

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
  T0241["TASK-0241 Pricing product/domain semantics — Done"] --> T0242["TASK-0242 Pricing technical design"]
  T0234 --> T0242
  T0242 --> T0243["TASK-0243 Scheduled Product promotional price — Done"]
  T0131 --> T0243
  T0153 --> T0243
  T0154 --> T0243
  T0195 --> T0243
```

TASK-0241, TASK-0242 and TASK-0243 are complete. PD-054 plus `docs/domains/pricing-promotion.md` define the first Pricing slice; TASK-0242 records its technical design and TASK-0243 implements it without expanding the approved promotion semantics.

## Blocker table

| Blocker | Affected tasks | Resolution |
|---|---|---|
| OQ-001 Storefront Tenant public-address semantics | TASK-0150–0154 and public catalog integration | Resolved by PD-052; implement Tenant-owned globally unique `/{storefrontSlug}` binding |
| OQ-002 Accounting weighted-average cost-pool dimension | TASK-0191–0195 and refund COGS reversal | Resolved by PD-021: trusted Tenant + Product valuation pool; Warehouse is not a valuation dimension |
| OQ-003 Refund approval role/capability mapping | TASK-0211–0214 | Resolved by PD-023: Owner/Admin/Staff request; Owner/Admin approve/reject; Viewer neither |
| External source policy changes over time | TASK-0141+ | implementation-time current policy/robots/terms review; disable source if unsafe |
| Pricing first-slice lifecycle/time/base-price/public-display/history semantics | TASK-0242–0243 | **Resolved by PD-054 and `docs/domains/pricing-promotion.md` in TASK-0241**; Technical Architecture/Builder must preserve this policy rather than reinterpret it |
| Future Pricing capabilities beyond the first slice | future work | Require an explicit product decision; TASK-0243 does not introduce coupons, stacking, segment, price-list or discount-accounting semantics |

## Parallelism guidance

After TASK-0100 closes foundation uncertainty, independent work may proceed where dependencies permit. Examples: Subscription catalog bootstrap and Tenancy module work can progress in parallel when their shared contract is stable; Catalog and provider simulation can also progress independently after required foundation boundaries exist. Infrastructure-sensitive parallel tasks must use distinct LocalStack task-instance ports/resource prefixes.

TASK-0241 -> TASK-0242 -> TASK-0243 is complete. Future Pricing work must preserve the module boundary and request a new product decision before adding new promotion semantics.
