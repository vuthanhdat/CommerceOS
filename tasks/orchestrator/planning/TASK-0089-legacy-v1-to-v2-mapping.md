# TASK-0089 — Legacy Backlog V1 to Canonical V2 Mapping

_Status: completed audit, 2026-08-10._

## Rule

`TASK-0004` through `TASK-0086` were audited for useful outcome intent **after** the clean-room V2 DAG was generated from the approved product/domain/architecture baseline. They were not used as the V2 decomposition template.

Classification meanings:

- **SALVAGEABLE** — outcome remains useful and maps substantially to one V2 node;
- **PARTIAL** — useful intent remains but approved scope/boundaries materially changed;
- **OVERLAP** — one V1 task mixed responsibilities now separated in V2;
- **DUPLICATE** — outcome is absorbed into another canonical V2 slice;
- **REPLACED** — an accepted architecture/domain decision superseded the V1 task's decision/decomposition;
- **PREMATURE** — the task assumed a future architecture move before evidence/decision;
- **CONFLICT** — the V1 assumption conflicts with the approved baseline and must not be executed as written.

## Full 83-task mapping

| Legacy V1 | Classification | Canonical V2 | Reconciliation note |
|---|---|---|---|
| `TASK-0004` | SALVAGEABLE | `TASK-0094` | Preserve real-AWS foundation/Budget deploy-destroy outcome; keep cloud input/authorization gate explicit. |
| `TASK-0005` | SALVAGEABLE | `TASK-0095` | Preserve OIDC + conditional preview/dev delivery outcome. |
| `TASK-0006` | PARTIAL | `TASK-0098` | Registration is no longer tenant/profile alone; accepted onboarding includes initial Owner + Trial completion/recovery. |
| `TASK-0007` | REPLACED | `TASK-0097`, `TASK-0099` | Cognito identity proof and current Tenant authority are explicitly separate under ADR-004. |
| `TASK-0008` | SALVAGEABLE | `TASK-0100` | Preserve invitation/membership outcome, reconciled to verified email, one role, last-owner and entitlement limit. |
| `TASK-0009` | PARTIAL | `TASK-0101`, `TASK-0102` | Split durable Audit evidence from Tenant suspension/support trust paths. |
| `TASK-0010` | PARTIAL | `TASK-0105` | Preserve canonical Catalog/access-pattern outcome under finalized SKU/slug/reference rules. |
| `TASK-0011` | SALVAGEABLE | `TASK-0106` | Preserve tenant-scoped Catalog management. |
| `TASK-0012` | PARTIAL | `TASK-0104`, `TASK-0107` | Managed media is its own FilesMedia capability; publication follows finalized lifecycle/public projection policy. |
| `TASK-0013` | SALVAGEABLE | `TASK-0108` | Preserve Back Office Catalog experience. |
| `TASK-0014` | SALVAGEABLE | `TASK-0109` | Preserve source registry/policy gate. |
| `TASK-0015` | SALVAGEABLE | `TASK-0110` | Preserve queued ingestion/snapshot/candidate pipeline. |
| `TASK-0016` | SALVAGEABLE | `TASK-0111` | Preserve first approved source adapter. |
| `TASK-0017` | DUPLICATE | `TASK-0111` | Manual URL submission is one entry path into the first adapter/acquisition slice. |
| `TASK-0018` | SALVAGEABLE | `TASK-0112` | Preserve merchant review/mapping with explicit Catalog-owned apply. |
| `TASK-0019` | SALVAGEABLE | `TASK-0113` | Preserve crawler observability/DLQ/recovery. |
| `TASK-0020` | CONFLICT | `TASK-0117`, `TASK-0118` | V1 assumed Storefront tenant addressing that current domain baseline says is still an explicit gap. |
| `TASK-0021` | SALVAGEABLE | `TASK-0119` | Preserve public storefront Catalog experience. |
| `TASK-0022` | SALVAGEABLE | `TASK-0120` | Preserve CDN/static/media delivery outcome behind resolved public Tenant context. |
| `TASK-0023` | SALVAGEABLE | `TASK-0121` | Preserve cart/checkout entry and revalidation. |
| `TASK-0024` | SALVAGEABLE | `TASK-0122` | Preserve SalesOrder lifecycle/persistence under approved snapshots/revisions. |
| `TASK-0025` | OVERLAP | `TASK-0121`, `TASK-0122` | Checkout revalidation and durable idempotent order snapshot are separate responsibilities. |
| `TASK-0026` | SALVAGEABLE | `TASK-0123` | Preserve guest checkout/customer-profile outcome. |
| `TASK-0027` | SALVAGEABLE | `TASK-0131` | Preserve merchant order operations after workflow/payment/Inventory foundations. |
| `TASK-0028` | SALVAGEABLE | `TASK-0124` | Preserve Inventory foundation; add approved Warehouse entitlement guard. |
| `TASK-0029` | SALVAGEABLE | `TASK-0125` | Preserve receipt/adjustment operations. |
| `TASK-0030` | SALVAGEABLE | `TASK-0126` | Preserve reservation/release concurrency invariants. |
| `TASK-0031` | OVERLAP | `TASK-0129`, `TASK-0131` | Allocation participates in ADR-010 workflow; fulfillment/low-stock remains operational work. |
| `TASK-0032` | SALVAGEABLE | `TASK-0127` | Preserve separately deployed Mock Payment Provider. |
| `TASK-0033` | OVERLAP | `TASK-0127`, `TASK-0128` | Provider deterministic behavior and CommerceOS Payments authority are separate. |
| `TASK-0034` | REPLACED | `TASK-0128`, `TASK-0129` | Checkout/payment interaction now has an explicit Payments boundary plus ADR-010 workflow. |
| `TASK-0035` | OVERLAP | `TASK-0127`, `TASK-0130` | Failure scenarios belong to provider simulation; ambiguity/reconciliation belongs to Payments. |
| `TASK-0036` | SALVAGEABLE | `TASK-0130` | Preserve signed/deduplicated callback behavior within the consolidated reliability slice. |
| `TASK-0037` | SALVAGEABLE | `TASK-0130` | Preserve Payment Unknown/reconciliation. |
| `TASK-0038` | DUPLICATE | `TASK-0130`, `TASK-0131` | Recovery tooling is absorbed into Payments and merchant operations slices. |
| `TASK-0039` | REPLACED | `TASK-0129` | ADR-010 already selects Step Functions Standard; there is no remaining “decide orchestration” implementation task. |
| `TASK-0040` | REPLACED | `TASK-0129` | Observable state-machine intent is implemented specifically under ADR-010. |
| `TASK-0041` | SALVAGEABLE | `TASK-0132` | Preserve Supplier/PurchaseOrder management. |
| `TASK-0042` | SALVAGEABLE | `TASK-0133` | Preserve goods receipt -> Inventory integration through explicit ownership. |
| `TASK-0043` | SALVAGEABLE | `TASK-0134` | Preserve supplier invoice/payment operational evidence. |
| `TASK-0044` | PARTIAL | `TASK-0138`, `TASK-0139` | Accounting implementation must first resolve the remaining moving-weighted-average cost-pool scope. |
| `TASK-0045` | SALVAGEABLE | `TASK-0139` | Preserve balanced immutable traceable journal foundation. |
| `TASK-0046` | SALVAGEABLE | `TASK-0140` | Preserve reversal/manual-control outcome. |
| `TASK-0047` | DUPLICATE | `TASK-0140` | GL/trial balance stays in the same coherent query slice as journal controls. |
| `TASK-0048` | REPLACED | `TASK-0141` | ADR-006 prohibits generic event infrastructure without named producers/consumers; first routes arrive with Accounting consumers. |
| `TASK-0049` | DUPLICATE | `TASK-0141` | Accounting consumer mechanics are part of the named reliable integration slice. |
| `TASK-0050` | SALVAGEABLE | `TASK-0141` | Preserve automated Sales/Payment postings with current fact semantics. |
| `TASK-0051` | OVERLAP | `TASK-0141`, `TASK-0142` | Inventory and Procurement producer readiness differ; split their Accounting routes. |
| `TASK-0052` | SALVAGEABLE | `TASK-0145` | Preserve posting reconciliation/recovery. |
| `TASK-0053` | SALVAGEABLE | `TASK-0146` | Preserve event/fact-driven rebuildable Reporting projections. |
| `TASK-0054` | DUPLICATE | `TASK-0146` | Commerce/operations KPIs are the first projection outcomes. |
| `TASK-0055` | SALVAGEABLE | `TASK-0147` | Preserve financial projection/basic P&L outcome. |
| `TASK-0056` | SALVAGEABLE | `TASK-0148` | Preserve merchant dashboard and in-app Notification outcome. |
| `TASK-0057` | SALVAGEABLE | `TASK-0159` | Preserve scheduled promotions as later Pricing work. |
| `TASK-0058` | SALVAGEABLE | `TASK-0160` | Preserve advanced pricing/promotion future scope. |
| `TASK-0059` | SALVAGEABLE | `TASK-0115` | Preserve second approved source after first-source reliability. |
| `TASK-0060` | SALVAGEABLE | `TASK-0114` | Preserve scheduled refresh but add Subscription entitlement + source policy gates. |
| `TASK-0061` | SALVAGEABLE | `TASK-0115` | Preserve source-change/price/parser intelligence. |
| `TASK-0062` | SALVAGEABLE | `TASK-0116` | Preserve explicit decision gate for advanced source/discovery crawling. |
| `TASK-0063` | PARTIAL | `TASK-0143`, `TASK-0144` | Refund request/review is Sales-owned and approval authority remains an explicit domain decision. |
| `TASK-0064` | SALVAGEABLE | `TASK-0144` | Preserve idempotent provider refund execution inside approved choreography. |
| `TASK-0065` | OVERLAP | `TASK-0144` | Inventory/Accounting compensation is driven by distinct ADR-011 facts, not one shared transaction. |
| `TASK-0066` | CONFLICT | `TASK-0144` | ADR-011 explicitly chooses post-approval choreography, not a global returns Step Functions workflow. |
| `TASK-0067` | SALVAGEABLE | `TASK-0149` | Preserve future granular-permission evolution after MVP roles stabilize. |
| `TASK-0068` | SALVAGEABLE | `TASK-0150` | Preserve platform-admin operational controls under explicit support/admin trust paths. |
| `TASK-0069` | PARTIAL | `TASK-0151` | Preserve security/privacy audit but do not invent Tenant deletion/retention/privacy-erasure semantics. |
| `TASK-0070` | SALVAGEABLE | `TASK-0152` | Preserve API/resource hardening. |
| `TASK-0071` | SALVAGEABLE | `TASK-0153` | Preserve DLQ recovery/failure-injection hardening. |
| `TASK-0072` | SALVAGEABLE | `TASK-0154` | Preserve bounded performance/load verification. |
| `TASK-0073` | SALVAGEABLE | `TASK-0155` | Preserve backup/recovery/production-readiness evidence. |
| `TASK-0074` | SALVAGEABLE | `TASK-0156` | Preserve measured observability/cost baseline. |
| `TASK-0075` | SALVAGEABLE | `TASK-0157` | Preserve architecture audit before extraction. |
| `TASK-0076` | SALVAGEABLE | `TASK-0157` | Selective-extraction decision is part of the architecture audit outcome. |
| `TASK-0077` | PREMATURE | `TASK-0158` | PDI extraction is conditional on accepted evidence/ADR, not pre-approved. |
| `TASK-0078` | PREMATURE | `TASK-0158` | Accounting extraction is conditional on accepted evidence/ADR. |
| `TASK-0079` | PREMATURE | `TASK-0158` | Reporting extraction is conditional on accepted evidence/ADR. |
| `TASK-0080` | DUPLICATE | `TASK-0158` | Post-extraction validation is mandatory inside any approved extraction task family. |
| `TASK-0081` | SALVAGEABLE | `TASK-0161` | Preserve Product variants as future scope. |
| `TASK-0082` | SALVAGEABLE | `TASK-0162` | Preserve custom storefront domains as future scope. |
| `TASK-0083` | SALVAGEABLE | `TASK-0163` | Preserve shopper accounts/customer lifecycle as future scope. |
| `TASK-0084` | SALVAGEABLE | `TASK-0164` | Preserve partial goods receipts as future scope. |
| `TASK-0085` | SALVAGEABLE | `TASK-0165` | Preserve optional email Notification as future scope. |
| `TASK-0086` | SALVAGEABLE | `TASK-0166` | Preserve basic balance-sheet projection as future scope. |

## Classification totals

| Classification | Count |
|---|---:|
| SALVAGEABLE | 54 |
| PARTIAL | 7 |
| DUPLICATE | 6 |
| OVERLAP | 6 |
| REPLACED | 5 |
| PREMATURE | 3 |
| CONFLICT | 2 |
| **Total** | **83** |

## Material gaps/changes discovered by the audit

1. **Subscription & Billing was materially absent from V1.** V2 adds the initial catalog/Trial entitlement authority (`TASK-0096`) plus subscription lifecycle, PlatformCharge/mock SaaS billing, and usage work (`TASK-0135`–`TASK-0137`).
2. **Generic event publication is obsolete as a standalone prerequisite.** V2 introduces ADR-006 infrastructure only with named producer/consumer tasks such as `TASK-0141`.
3. **Checkout orchestration is already decided.** V1's decision task is superseded by ADR-010 and `TASK-0129`.
4. **Returns/refunds are not a global workflow.** V1's orchestration assumption conflicts with ADR-011; `TASK-0144` implements Sales approval plus independent choreography.
5. **Storefront public Tenant addressing remains a real domain gap.** V2 surfaces `TASK-0117` instead of silently carrying V1 slug assumptions.
6. **Accounting cost-pool scope and refund approval capability remain narrow explicit domain gaps.** They are represented as `TASK-0138` and `TASK-0143` rather than Builder guesses.

## Archive policy

The original V1 Markdown blobs are preserved under `tasks/legacy/` for auditability. They are **not executable backlog authority**. Current planning authority is `tasks/BACKLOG.v2.yaml`, its shards, and any current detailed V2 task specification.

**Legacy mapping audit: PASS — 83/83 V1 tasks classified and mapped.**
