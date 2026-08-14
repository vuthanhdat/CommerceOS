# CommerceOS Canonical Backlog

_Last updated: 2026-08-14_

Only the first safe dependency frontier is marked Ready. `Backlog` status means not started; readiness explains whether it can safely begin.

| Task | Feature | Priority | Requirement IDs | Depends on | Readiness |
|---|---|---|---|---|---|
| TASK-0100 Verify current LocalStack foundation baseline (Done) | F01 | P0 | REQ-FND-001..004 | N/A | Completed 2026-08-13 |
| TASK-0101 Close deterministic lifecycle/bootstrap/reset gaps (Done) | F01 | P0 | REQ-FND-001..003 | TASK-0100 | Completed 2026-08-13 |
| TASK-0102 Add selected LocalStack CI/infrastructure verification (Done) | F01 | P0 | REQ-FND-004, REQ-HARD-003 | TASK-0100 | Completed 2026-08-13 |
| TASK-0103 Expand architecture/configuration guardrails (Done) | F01 | P0 | REQ-HARD-002, REQ-FND-003 | TASK-0100 | Completed 2026-08-13 |
| TASK-0110 Establish Tenancy module and owned persistence (Done) | F02 | P0 | REQ-SEC-001, REQ-TEN-001..005 | TASK-0101, TASK-0103 | Completed 2026-08-13 |
| TASK-0111 Implement trusted Tenant discovery/read/mutation authority (Done) | F02 | P0 | REQ-SEC-001..002, REQ-TEN-002 | TASK-0110 | Completed 2026-08-13 |
| TASK-0112 Implement Membership, roles, limits and Invitation lifecycle (Done) | F02 | P0 | REQ-TEN-003..004, REQ-SUB-003 | TASK-0110, TASK-0121 | Completed 2026-08-14 |
| TASK-0113 Implement merchant onboarding and Trial bootstrap recovery (Done) | F02 | P0 | REQ-TEN-001, REQ-SUB-001 | TASK-0110, TASK-0120 | Completed 2026-08-14 |
| TASK-0114 Implement platform Tenant suspend/reactivate/support path | F02 | P1 | REQ-SEC-003, REQ-TEN-005, REQ-AUD-001 | TASK-0111 | **Done** |
| TASK-0120 Bootstrap immutable Trial and paid PlanVersion catalog (Done) | F03 | P0 | REQ-SUB-001..002 | TASK-0101 | Completed 2026-08-13 |
| TASK-0121 Implement current EntitlementSet evaluation authority (Done) | F03 | P0 | REQ-SUB-002..004 | TASK-0120 | Completed 2026-08-14 |
| TASK-0122 Enforce Membership/Warehouse hard limits at owner boundaries (Done) | F03 | P0 | REQ-SUB-003 | TASK-0121, TASK-0110, TASK-0161 | Completed 2026-08-14 |
| TASK-0123 Build SaaS billing provider seam and PlatformCharge evidence | F03 | P1 | REQ-SUB-008 | TASK-0120 | **Done** |
| TASK-0124 Implement paid activation, upgrade, downgrade, renewal/cancel/grace | F03 | P1 | REQ-SUB-005..008 | TASK-0121, TASK-0123 | **Done** |
| TASK-0125 Implement order-volume UsageMeter warning and support queries (Done) | F03 | P2 | REQ-SUB-004 | TASK-0121, TASK-0160 | Completed 2026-08-14 |
| TASK-0130 Establish Catalog module and Tenant-owned persistence (Done) | F04 | P0 | REQ-CAT-001..006 | TASK-0111, TASK-0121 | Completed 2026-08-14 |
| TASK-0131 Implement Product lifecycle, SKU, Money and slug rules (Done) | F04 | P0 | REQ-CAT-001..003, REQ-CAT-005 | TASK-0130 | Completed 2026-08-14 |
| TASK-0132 Implement Category, Brand and Product specifications (Done) | F04 | P1 | REQ-CAT-004..005 | TASK-0131 | Completed 2026-08-14 |
| TASK-0133 Implement Files/Media upload boundary and Product media association (Done) | F04 | P1 | REQ-MED-001 | TASK-0130 | Completed 2026-08-14 |
| TASK-0134 Implement external source mapping and explicit ImportCandidate apply (Done) | F04 | P1 | REQ-CAT-006, REQ-PDI-006 | TASK-0131 | Completed 2026-08-14 |
| TASK-0140 Implement PDI source registry and governance (Done) | F05 | P1 | REQ-PDI-001 | TASK-0121, TASK-0134 | Completed 2026-08-14 |
| TASK-0141 Implement one policy-approved adapter and manual URL ingestion (Done) | F05 | P1 | REQ-PDI-002..003 | TASK-0140 | Completed 2026-08-14 |
| TASK-0142 Implement crawler queue, raw snapshot, normalize, retry/rate/DLQ | F05 | P1 | REQ-PDI-002, REQ-PDI-004..005 | TASK-0141, TASK-0102 | Done |
| TASK-0143 Implement merchant ImportCandidate review and Catalog apply | F05 | P1 | REQ-PDI-006 | TASK-0142, TASK-0134 | Done |
| TASK-0144 Implement scheduled refresh with policy and entitlement recheck | F05 | P2 | REQ-PDI-007, REQ-SUB-004 | TASK-0143, TASK-0121 | Done |
| TASK-0150 Resolve Storefront Tenant public-address semantics (Done) | F06 | P0 | REQ-STO-001 | N/A | Completed 2026-08-14 |
| TASK-0151 Implement PublicTenantContext and storefront list/detail reads (Done) | F06 | P0 | REQ-STO-001..002, REQ-CAT-005 | TASK-0150, TASK-0131, TASK-0111 | Completed 2026-08-14 |
| TASK-0152 Implement transient cart experience (Done) | F06 | P1 | REQ-CHK-001 | TASK-0151 | Completed 2026-08-14 |
| TASK-0153 Implement authoritative checkout validation and repricing/reconfirm (Done) | F06 | P0 | REQ-CHK-002, REQ-CAT-003 | TASK-0152, TASK-0161 | Completed 2026-08-14 |
| TASK-0154 Implement idempotent guest order placement and immutable snapshot (Done) | F06 | P0 | REQ-SAL-001 | TASK-0153 | Completed 2026-08-14 |
| TASK-0160 Establish Sales module/order lifecycle persistence (Done) | F07 | P0 | REQ-SAL-001..003 | TASK-0154 | Completed 2026-08-14 |
| TASK-0161 Establish Inventory Warehouse/StockItem persistence and limits (Done) | F07 | P0 | REQ-INV-001..002 | TASK-0131, TASK-0121 | Completed 2026-08-14 |
| TASK-0162 Implement reserve/release/issue/receive/adjust stock operations (Done) | F07 | P0 | REQ-INV-001, REQ-INV-003 | TASK-0161 | Completed 2026-08-14 |
| TASK-0163 Establish Payments obligation/attempt persistence and contracts (Done) | F07 | P0 | REQ-PAY-001, REQ-PAY-004 | TASK-0160 | Completed 2026-08-14 |
| TASK-0164 Build merchant-order Mock Payment Provider deterministic scenarios (Done) | F07 | P0 | REQ-PAY-002..003 | TASK-0101 | Completed 2026-08-14 |
| TASK-0165 Implement provider evidence, signed callback dedupe and reconciliation (Done) | F07 | P0 | REQ-PAY-003..004 | TASK-0163, TASK-0164 | Completed 2026-08-14 |
| TASK-0166 Implement pre-fulfillment cancellation and independent release/refund requests (Done) | F07 | P1 | REQ-SAL-003, REQ-INV-003, REQ-PAY-004 | TASK-0160, TASK-0162, TASK-0165 | Completed 2026-08-14 |
| TASK-0170 Implement ADR-010 durable order payment/allocation workflow (Done) | F08 | P0 | REQ-ORC-001, REQ-SAL-002 | TASK-0160, TASK-0162, TASK-0165 | Completed 2026-08-14 |
| TASK-0171 Implement workflow recovery, Unknown and NeedsAttention semantics (Done) | F08 | P0 | REQ-ORC-001, REQ-PAY-004, REQ-HARD-001 | TASK-0170 | Completed 2026-08-14 |
| TASK-0172 Verify workflow branches/retry/wait/idempotency in LocalStack (Done) | F08 | P0 | REQ-HARD-003 | TASK-0171, TASK-0102 | Completed 2026-08-14 |
| TASK-0180 Implement Supplier and immutable submitted PurchaseOrder (Done) | F09 | P1 | REQ-PROC-001 | TASK-0131, TASK-0111 | Completed 2026-08-14 |
| TASK-0181 Implement GoodsReceipt and compensating correction evidence (Done) | F09 | P1 | REQ-PROC-002 | TASK-0180 | Completed 2026-08-14 |
| TASK-0182 Integrate GoodsReceipt to Inventory idempotently (Done) | F09 | P1 | REQ-PROC-002, REQ-INV-003, REQ-HARD-001 | TASK-0181, TASK-0162 | Completed 2026-08-14 |
| TASK-0183 Implement SupplierInvoice, variance approval and SupplierPayment evidence (Done) | F09 | P1 | REQ-PROC-003 | TASK-0181 | Completed 2026-08-14 |
| TASK-0190 Resolve moving-weighted-average authoritative cost-pool scope | F10 | P0 | REQ-ACC-004 | N/A | Done |
| TASK-0191 Establish Accounting module and required chart bootstrap (Done) | F10 | P0 | REQ-ACC-001, REQ-ACC-004 | TASK-0190, TASK-0113 | Completed 2026-08-14 |
| TASK-0192 Implement balanced immutable Journal posting and reversal (Done) | F10 | P0 | REQ-ACC-002 | TASK-0191 | Completed 2026-08-14 |
| TASK-0193 Implement sale/deposit/revenue/COGS fact consumers and valuation (Done) | F10 | P0 | REQ-ACC-003..004, REQ-HARD-001 | TASK-0192, TASK-0162, TASK-0165 | Completed 2026-08-14 |
| TASK-0194 Implement procurement and stock-adjustment Accounting consumers (Done) | F10 | P1 | REQ-ACC-005 | TASK-0192, TASK-0182, TASK-0183 | Completed 2026-08-14 |
| TASK-0195 Implement reliable fact routes plus General Ledger/Trial Balance (Done) | F10 | P0 | REQ-ACC-002, REQ-REP-002, REQ-HARD-001 | TASK-0193, TASK-0194, TASK-0102 | Completed 2026-08-14 |
| TASK-0200 Establish Reporting projections from producer-owned facts (Done) | F11 | P1 | REQ-REP-001..002 | TASK-0160, TASK-0195 | Completed 2026-08-14 |
| TASK-0201 Implement operational KPI projections and Tenant business-date semantics (Done) | F11 | P1 | REQ-REP-001 | TASK-0200 | Completed 2026-08-14 |
| TASK-0202 Implement financial/back-office reporting views (Done) | F11 | P1 | REQ-REP-002 | TASK-0200, TASK-0195 | Completed 2026-08-14 |
| TASK-0210 Resolve exact merchant refund-approval role/capability mapping | F12 | P0 | REQ-REF-001 | N/A | Done |
| TASK-0211 Implement Sales refund request and terminal approve/reject review (Done) | F12 | P0 | REQ-REF-001 | TASK-0210, TASK-0160 | Completed 2026-08-14 |
| TASK-0212 Implement RefundApproved reliable choreography/fan-out (Done) | F12 | P0 | REQ-REF-002..004, REQ-HARD-001 | TASK-0211, TASK-0102 | Completed 2026-08-14 |
| TASK-0213 Implement Inventory return and Payments refund/reconciliation effects (Done) | F12 | P0 | REQ-REF-002..003 | TASK-0212, TASK-0162, TASK-0165 | Completed 2026-08-14 |
| TASK-0214 Implement refund Accounting corrections and progress projection (Done) | F12 | P0 | REQ-REF-004 | TASK-0213, TASK-0195 | Completed 2026-08-14 |
| TASK-0220 Establish Audit append/query module (Done) | F13 | P1 | REQ-AUD-001 | TASK-0110 | Completed 2026-08-14 |
| TASK-0221 Integrate privileged/security action Audit delivery (Done) | F13 | P1 | REQ-AUD-001, REQ-HARD-001 | TASK-0220, TASK-0114 | Completed 2026-08-14 |
| TASK-0222 Implement per-recipient Notification state and critical-event consumers (Done) | F13 | P2 | REQ-NOT-001 | TASK-0200, TASK-0214 | Completed 2026-08-14 |
| TASK-0230 Standardize structured logs, correlation and operational metrics (Done) | F14 | P1 | REQ-OBS-001 | TASK-0172, TASK-0222 | Completed 2026-08-14 |
| TASK-0231 Build DLQ/recovery/redrive/operator diagnostic tooling (Done) | F14 | P1 | REQ-HARD-001, REQ-OBS-001 | TASK-0230 | Completed 2026-08-14 |
| TASK-0232 Run tenant-isolation, authorization and failure-oriented hardening campaign (Done) | F14 | P1 | REQ-SEC-001..003, REQ-HARD-003 | TASK-0231 | Completed 2026-08-14 |
| TASK-0233 Expand executable architecture/harness guardrails and unified checks (Done) | F14 | P1 | REQ-HARD-002..003, REQ-FND-004 | TASK-0232 | Completed 2026-08-14 |
| TASK-0234 Perform milestone architecture audit and selective-extraction assessment (Done) | F14 | P2 | REQ-HARD-002 | TASK-0233 | Completed 2026-08-14 |
| TASK-0240 Implement explicit Customer/CRM profiles without guest auto-linking (Done) | F15 | P3 | REQ-CRM-001 | TASK-0200 | Completed 2026-08-14 |
| TASK-0241 Resolve first Pricing/Promotion product and domain semantics (Done) | F15 | P3 | REQ-PRI-001 | N/A | Completed 2026-08-14 |
| TASK-0242 Design Pricing authority contracts, persistence and checkout integration (Done) | F15 | P3 | REQ-PRI-001 | TASK-0241, TASK-0234 | Completed 2026-08-14 |
| TASK-0243 Implement scheduled Product promotional-price slice (Done) | F15 | P3 | REQ-PRI-001 | TASK-0242, TASK-0131, TASK-0153, TASK-0154, TASK-0195 | Completed 2026-08-14 |

## Ready frontier

- `TASK-0125`, `TASK-0141`, `TASK-0172`, `TASK-0182`, `TASK-0183`, and `TASK-0221` are complete.
- `TASK-0142`, `TASK-0143`, and `TASK-0144` are complete. The PDI source policy must remain current at operation time.
- `TASK-0190` is complete: Accounting cost pools are `Tenant + Product`; Warehouse is not a valuation dimension.
- `TASK-0191` through `TASK-0202` are complete. Accounting and Reporting baselines are now implemented through their current planned slices.
- `TASK-0211` and `TASK-0212` are complete.
- `TASK-0241` is complete: PD-054 and `docs/domains/pricing-promotion.md` close the first-slice Pricing product/domain ambiguity.
- `TASK-0242` and `TASK-0243` are complete. The first Pricing slice is implemented with a dedicated module/table, authoritative storefront checkout decision and immutable Sales pricing provenance.
- Other remaining tasks retain the readiness recorded in their task rows; completing the Pricing design gate does not reclassify unrelated work.
