# CommerceOS Requirement Index

_Last updated: 2026-08-13_

## Source policy

This index normalizes requirements from the existing `docs/` baseline. It does not replace source documents or invent new product policy. Where older source text conflicts with accepted ADR-012 or later domain reconciliation, the later authority is recorded in Notes.

## Requirements

| ID | Requirement | Primary source | Status / notes |
|---|---|---|---|
| REQ-FND-001 | LocalStack is the only infrastructure/runtime target; lifecycle must be deterministic and repository-owned. | `docs/adr/ADR-012-localstack-only-infrastructure-runtime.md` | Approved |
| REQ-FND-002 | CDK is the IaC source of truth; no manually-created required LocalStack resource. | `docs/development/12-infrastructure-as-code.md` | Approved |
| REQ-FND-003 | Local profiles/task instances isolate endpoints, ports, prefixes and disposable state. | `docs/architecture/localstack-runtime-and-lifecycle.md` | Approved |
| REQ-FND-004 | Harness/CI mechanically verifies build, architecture, contracts and selected LocalStack behavior. | `docs/development/00-engineering-harness.md`, `04-testing-strategy.md`, `11-ci-cd-pipeline.md` | Approved |
| REQ-SEC-001 | Tenant-owned operations derive Tenant authority from current authenticated Membership, never client `tenantId`. | `docs/domains/tenant-identity.md`, ADR-004 | Approved |
| REQ-SEC-002 | Merchant read authority and mutation authority are distinct; Suspended may read by role but cannot mutate. | `docs/architecture/first-frontier-contracts.md` | Approved |
| REQ-SEC-003 | Platform admin/support uses explicit privileged contexts; suspend/reactivate requires reason and Audit. | `docs/domains/tenant-identity.md` | Approved |
| REQ-TEN-001 | Verified-email self-service registration creates Active Tenant + initial Active Owner and must complete required Trial outcome. | `docs/domains/tenant-identity.md` | Approved |
| REQ-TEN-002 | One subject may belong to multiple Tenants; ambiguous membership requires intentional Tenant selection and server revalidation. | `docs/domains/tenant-identity.md` | Approved |
| REQ-TEN-003 | MVP Membership has exactly one Owner/Admin/Staff/Viewer role and preserves last Active Owner. | `docs/domains/tenant-identity.md` | Approved |
| REQ-TEN-004 | Invitations are Tenant/email bound, 7-day, single-use; resend rotates credential; Disabled members are not silently reactivated. | `docs/domains/tenant-identity.md` | Approved |
| REQ-TEN-005 | Tenant lifecycle is Active/Suspended only in MVP; no closure/deletion/privacy erasure. | `docs/domains/tenant-identity.md` | Approved; destructive lifecycle is future product work |
| REQ-SUB-001 | Onboarding starts a dedicated 30-day no-card Trial with approved Trial EntitlementSet. | `docs/domains/subscription-billing.md` | Approved |
| REQ-SUB-002 | Starter/Growth/Business PlanVersions are immutable accepted terms; runtime authority is EntitlementSet, never Plan name. | `docs/domains/subscription-billing.md` | Approved |
| REQ-SUB-003 | MaxActiveMemberships and MaxWarehouses are hard growth/activation limits enforced with owner-authoritative counts. | `docs/domains/subscription-billing.md` | Approved |
| REQ-SUB-004 | ScheduledProductIngestion is an entitlement gate; order-volume threshold is warning-only and never blocks checkout. | `docs/domains/subscription-billing.md` | Approved |
| REQ-SUB-005 | Upgrade becomes effective only after verified successful PlatformCharge and starts a fresh monthly period; no proration. | `docs/domains/subscription-billing.md` | Approved |
| REQ-SUB-006 | Downgrade is next-renewal and blocks on authoritative excess usage without deleting/disabling resources. | `docs/domains/subscription-billing.md` | Approved |
| REQ-SUB-007 | Definitive renewal failure -> PastDue 7-day grace -> Ended if unresolved; OutcomeUnknown is not failure. | `docs/domains/subscription-billing.md` | Approved |
| REQ-SUB-008 | SaaS billing uses a dedicated simulated provider seam separate from merchant-order Payments and supports idempotency/reconciliation. | `docs/domains/subscription-billing.md` | Approved |
| REQ-CAT-001 | Product lifecycle is Draft/Published/Unpublished/Archived; Archived terminal; Published edits update canonical/public state directly. | `docs/domains/catalog.md` | Approved |
| REQ-CAT-002 | SKU optional in Draft, required for first publish, Tenant-unique case-insensitively, immutable after first publication and never reused after Archive. | `docs/domains/catalog.md` | Approved |
| REQ-CAT-003 | Publication requires Name + SKU + valid VND Money; zero price allowed; stock/category/brand/media are not publication prerequisites. | `docs/domains/catalog.md` | Approved |
| REQ-CAT-004 | Category/Brand are flat optional Tenant references with normalized-name uniqueness and non-destructive retirement. | `docs/domains/catalog.md` | Approved; historical name-reuse semantics only if implementation needs them |
| REQ-CAT-005 | Public Product uses Tenant-scoped mutable slug and exposes approved public fields while excluding internal/source/advisory-cost data. | `docs/domains/catalog.md` | Approved |
| REQ-MED-001 | Public media comes from merchant uploads managed by CommerceOS; arbitrary external copy/hotlink is unsupported. | `docs/domains/catalog.md` | Approved |
| REQ-CAT-006 | One external source-product identity maps to at most one canonical Product per Tenant and application is explicit after merchant approval. | `docs/domains/catalog.md` | Approved |
| REQ-PDI-001 | Source policy is platform governed; Tenant enablement cannot override platform policy; scheduled operation also requires entitlement. | `docs/domains/commerce-operations.md`, `05-product-data-ingestion.md` | Approved |
| REQ-PDI-002 | External source snapshots are immutable/provenanced and never canonical merchant products. | `docs/05-product-data-ingestion.md` | Approved |
| REQ-PDI-003 | Manual supported-URL ingestion is the first acquisition flow; source-specific adapter details stay behind adapter contract. | `docs/05-product-data-ingestion.md` | Approved |
| REQ-PDI-004 | Crawler work uses bounded queue/backpressure, per-source rate/concurrency, bounded retry and DLQ; never bypass anti-bot restrictions. | `docs/05-product-data-ingestion.md` | Approved |
| REQ-PDI-005 | Raw payload retention is bounded; parser/version/normalized schema and failure reasons are testable from fixtures. | `docs/05-product-data-ingestion.md` | Approved |
| REQ-PDI-006 | ImportCandidate review/apply preserves merchant authority and provenance; source updates never silently mutate Product. | `docs/domains/catalog.md`, `05-product-data-ingestion.md` | Approved |
| REQ-PDI-007 | Scheduled refresh is later and rechecks source policy + current entitlement before dispatch. | `docs/architecture/first-frontier-contracts.md` | Approved |
| REQ-STO-001 | Public requests use PublicTenantContext and current Tenant status; Suspended disables storefront/checkout. | `docs/architecture/first-frontier-contracts.md` | Approved except Tenant public-address semantics |
| REQ-STO-002 | Storefront provides public listing/filter/search, product detail, availability and responsive cart/checkout experience. | `docs/00-product-definition.md` | Approved; implementation follows public projection boundaries |
| REQ-CHK-001 | Cart supports add/remove/update and remains transient/untrusted until authoritative checkout validation. | `docs/00-product-definition.md`, domain baseline | Approved |
| REQ-CHK-002 | Any authoritative price change requires shopper reconfirmation and no Order is placed for that attempt. | `docs/domains/commerce-operations.md` | Approved |
| REQ-SAL-001 | Equivalent checkout intent creates at most one immutable SalesOrder snapshot; incompatible idempotency reuse conflicts. | `docs/domains/commerce-operations.md`, ADR-007 | Approved |
| REQ-SAL-002 | Order happy path is OrderPlaced -> reserve -> full capture -> Confirmed -> Allocated -> whole fulfillment; no partial allocation/backorder. | `docs/domains/commerce-operations.md` | Approved |
| REQ-SAL-003 | Merchant may cancel before Fulfilled; cancellation does not claim Inventory/Payment effects completed. | `docs/domains/commerce-operations.md` | Approved |
| REQ-INV-001 | Inventory preserves OnHand>=0, Reserved>=0, Available=OnHand-Reserved>=0 under concurrency. | `docs/domains/commerce-operations.md` | Approved |
| REQ-INV-002 | Warehouse creation/reactivation enforces current MaxWarehouses against Inventory-owned active count. | `docs/architecture/persistence-access-patterns.md` | Approved |
| REQ-INV-003 | Reserve/release/issue/receive/return/adjust are source-idempotent and create immutable movement evidence. | `docs/domains/commerce-operations.md` | Approved |
| REQ-PAY-001 | One Payment obligation per Order has immutable attempts; full amount captured immediately; no new capture while prior outcome Unknown. | `docs/domains/commerce-operations.md` | Approved |
| REQ-PAY-002 | Mock merchant provider is external-like and supports deterministic success/decline/transient/timeout/delayed/duplicate scenarios. | `docs/06-mock-payment-provider.md` | Approved |
| REQ-PAY-003 | State-changing provider operations are idempotent; callbacks are signed, verified and deduplicated. | `docs/06-mock-payment-provider.md` | Approved |
| REQ-PAY-004 | Timeout/transport ambiguity remains OutcomeUnknown until provider inquiry/reconciliation establishes evidence. | `docs/06-mock-payment-provider.md` | Approved |
| REQ-ORC-001 | OrderPlaced -> reservation -> payment/reconciliation -> Confirmed -> Allocated uses the ADR-010 durable workflow scope. | ADR-010, `docs/architecture/integration-and-aws.md` | Approved |
| REQ-PROC-001 | Supplier and submitted PO are Tenant-owned; submitted commitment is immutable and cancellation is limited before downstream evidence. | `docs/domains/commerce-operations.md` | Approved |
| REQ-PROC-002 | Confirmed GoodsReceipt is immutable and corrections are compensating evidence; Inventory application is independently recoverable. | `docs/domains/commerce-operations.md` | Approved |
| REQ-PROC-003 | MVP has one SupplierInvoice and one full SupplierPayment per PO with variance approval and attested external payment. | `docs/domains/commerce-operations.md` | Approved |
| REQ-ACC-001 | Every Tenant gets required control-account semantic roles; non-control accounts can be added/deactivated under policy. | `docs/domains/commerce-operations.md` | Approved |
| REQ-ACC-002 | Posted journals are balanced, immutable, traceable and source-idempotent; corrections use reversal/compensating entries. | `docs/01-non-functional-requirements.md`, domain baseline | Approved |
| REQ-ACC-003 | PaymentCaptured, OrderFulfilled and StockIssued generate distinct approved sale/deposit/revenue/COGS postings. | `docs/domains/commerce-operations.md` | Approved |
| REQ-ACC-004 | Moving weighted-average inventory valuation is Accounting authority and uses immutable issue-cost provenance. | `docs/domains/commerce-operations.md` | Approved policy; cost-pool dimension remains unresolved |
| REQ-ACC-005 | Procurement and stock-adjustment facts generate approved GRNI/AP/PPV/gain-loss postings without foreign-table reads. | `docs/domains/commerce-operations.md` | Approved |
| REQ-REP-001 | Operational KPI formulas are defined from authoritative facts and Tenant business timezone. | `docs/domains/commerce-operations.md` | Approved |
| REQ-REP-002 | General ledger/trial balance use Journal EffectiveDate; Reporting projections are never transactional/entitlement authority. | `docs/domains/commerce-operations.md` | Approved |
| REQ-REF-001 | RefundRequested requires explicit merchant approve/reject; request alone has no stock/payment/accounting effect. | `docs/domains/commerce-operations.md` | Approved; exact approval role mapping remains open |
| REQ-REF-002 | RefundApproved authorizes exactly-once restockable StockReturned for approved eligible quantity. | `docs/domains/commerce-operations.md` | Approved |
| REQ-REF-003 | Payments refunds only approved intent; only verified provider evidence creates PaymentRefunded; Unknown is reconciled. | `docs/domains/commerce-operations.md` | Approved |
| REQ-REF-004 | RefundApproved, StockReturned and PaymentRefunded independently drive append-only Accounting corrections; no global RefundCompleted truth. | ADR-011, technical baseline | Approved |
| REQ-NOT-001 | Notification read/acknowledgement state is per recipient and never resolves source business exception. | `docs/domains/commerce-operations.md` | Approved |
| REQ-AUD-001 | Privileged/security actions and relevant rejections create append-oriented non-disclosing Audit evidence. | `docs/domains/tenant-identity.md`, commerce baseline | Approved |
| REQ-OBS-001 | Structured logs/correlation and actionable failure visibility cover APIs, queues, workflows, crawlers, providers and posting failures. | `docs/01-non-functional-requirements.md` | Approved; CloudWatch wording is capability mapping under ADR-012 |
| REQ-HARD-001 | At-least-once consumers are duplicate/out-of-order safe with bounded retry, DLQ and recovery/redrive identity preservation. | `docs/01-non-functional-requirements.md`, ADR-006 | Approved |
| REQ-HARD-002 | Architecture rules prohibit Domain/Application infrastructure leakage and cross-module persistence shortcuts, promoted to executable checks when practical. | `docs/development/03-architecture-rules.md` | Approved |
| REQ-HARD-003 | Infrastructure-sensitive tests use LocalStack where sufficiently supported and explicitly record emulator limitations. | `docs/development/10-testing-and-cloud-verification.md` | Approved |
| REQ-CRM-001 | Customer/CRM may own explicit Tenant customer profile/contact preferences; guest checkout does not auto-create or rewrite CRM history. | `docs/00-product-definition.md`, commerce baseline | Later scope |
| REQ-PRI-001 | Pricing/Promotion owns future offer/discount rules; MVP guest checkout has no manual authoritative discount. | `docs/00-product-definition.md`, commerce baseline | Later scope |

## Conflict / supersession register

1. Real-AWS account, IAM/OIDC, Budget/Free Tier and cloud-validation requirements in older documents are superseded by ADR-012. They do not generate current backlog tasks.
2. Older role lists (`Sales`, `Warehouse`, `Accountant`) in `00-product-definition.md` are superseded for MVP Membership authority by the resolved Owner/Admin/Staff/Viewer model.
3. Older roadmap references to missing `TASK-0093`–`TASK-0095` do not prove implementation status; current code must be inspected/verified.
4. `prompt_arrange_doc.md` currently declares `tasks/planning/` at its top-level target structure while some deeper examples still say `docs/planning/`; this planning output follows the current top-level target.

## Explicit open implementation questions

- OQ-001 — Resolved by PD-052: Tenant-owned globally unique `/{storefrontSlug}` with permanent retirement/no reuse and no MVP custom domains or redirects.
- OQ-002 — Accounting moving-weighted-average authoritative cost-pool dimension must be resolved before valuation persistence keys are finalized.
- OQ-003 — Exact refund-approval role/capability mapping must be approved before refund approval API/UI is Ready.
- OQ-004 — Category/Brand historical normalized-name reuse semantics are only needed if implementation requires reuse after rename/retirement.
