# CommerceOS Product Roadmap

_Last updated: 2026-08-13_

## Product Goal

**PG-001 — Operable multi-tenant commerce SaaS learning platform**

Enable a merchant to onboard, manage an authoritative catalog, sell through a public storefront, reserve/issue inventory, simulate payment and failure recovery, procure stock, generate traceable double-entry accounting, and operate the system through observable LocalStack-based serverless infrastructure without cross-domain persistence shortcuts.

## Epics and Features

| Epic | Feature | Outcome | Roadmap relationship |
|---|---|---|---|
| E01 Engineering foundation | F01 LocalStack foundation & harness | reproducible local infrastructure and mechanical guardrails | Phase 0 / H0 continuation |
| E02 Merchant platform | F02 Tenant & Merchant Access | trusted multi-tenant onboarding, staff and lifecycle authority | Phase 1 |
| E02 Merchant platform | F03 Subscription & Billing | Trial/paid plan lifecycle, entitlements, limits and simulated SaaS billing | cross-cutting after onboarding |
| E03 Merchandising | F04 Catalog & Files/Media | merchant-owned canonical products and managed public media | Phase 2 |
| E03 Merchandising | F05 Product Data Ingestion | governed external-source snapshots and reviewed import | Phases 3/14 |
| E04 Sell something | F06 Storefront, cart & checkout | public browse/cart and authoritative idempotent order placement | Phases 4/5 |
| E04 Sell something | F07 Sales, Inventory & merchant Payments | order truth, stock invariants and payment evidence | Phases 6–8 |
| E04 Sell something | F08 Durable order orchestration | interruption-safe OrderPlaced -> allocation workflow | Phase 9 |
| E05 Run the business | F09 Procurement | supplier, PO, receipt, invoice and payment evidence | Phase 10 |
| E05 Run the business | F10 Accounting | chart, immutable journals, valuation and automatic postings | Phases 11/12 |
| E05 Run the business | F11 Reporting | operational and financial projections from owned facts | Phase 13 |
| E06 Exceptions | F12 Returns & refunds | reviewed restockable refund choreography and accounting corrections | Phase 15 |
| E06 Exceptions | F13 Notification & Audit | per-recipient operational notifications and privileged evidence | supporting/cross-cutting |
| E07 Production-minded learning | F14 Platform hardening | failure recovery, observability, security and architecture guardrails | Phases 16/17 |
| E08 Later product scope | F15 Customer CRM & Pricing/Promotion | explicitly documented later capabilities without contaminating MVP | post-MVP |

## Milestones

### Milestone A — Sell something

F01 -> F02/F03 -> F04 -> F06 -> F07.

Observable outcome: an eligible Trial merchant can publish a product, expose it publicly, place an order, reserve stock and complete a deterministic mock payment path.

### Milestone B — Survive failure

F07 -> F08 plus payment reconciliation/failure tests.

Observable outcome: timeout, duplicate delivery and ambiguous provider state do not create duplicate payment/order/stock effects.

### Milestone C — Run the business

F09 -> F10 -> F11.

Observable outcome: procurement and sales facts generate traceable inventory/accounting effects and useful business projections.

### Milestone D — Event-driven effects and exceptions

F05 scheduled intelligence, F12 refunds, F13 notification/audit, F10 automatic accounting.

Observable outcome: reliable facts fan out through owner-controlled idempotent consumers with DLQ/recovery visibility.

### Milestone E — Production-minded engineering

F14 hardening and architecture audit; extraction only when measured pressure justifies it.

## Current frontier

The repository documents Phase-0 lifecycle tooling but current `main` does not contain the historical V2 task files. Therefore the first backlog action is **TASK-0100: verify the current foundation against ADR-012 and establish reproducible baseline evidence**. It is a verification/reconciliation task, not permission to rewrite working foundation code.

Only tasks whose dependencies and material decisions are closed are marked `Ready` in `BACKLOG.md`.
