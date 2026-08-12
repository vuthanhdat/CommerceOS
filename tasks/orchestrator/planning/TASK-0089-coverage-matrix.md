# TASK-0089 — Backlog V2 Coverage Matrix

_Status: completed planning audit, 2026-08-10._

## Purpose

This matrix checks that canonical Backlog V2 covers the approved CommerceOS product/domain/architecture baseline without treating the legacy V1 decomposition as the source of truth.

Coverage means a capability, architecture pressure, explicit decision gap, or hardening obligation has a canonical V2 owner/task family. It does **not** mean the task is Ready.

## Coverage matrix

| Approved area / obligation | Canonical V2 coverage | Audit result |
|---|---|---|
| Phase 0 executable architecture guardrails | `TASK-0093` | Covered; first safe remediation frontier |
| Real AWS dev foundation and cost guardrails | `TASK-0094` | Covered; Refined/cloud-gated |
| Keyless OIDC CI/CD + preview/dev delivery | `TASK-0095` | Covered; Outline behind cloud foundation |
| Local Task Orchestrator | `TASK-0090` | Covered; existing useful task preserved as Refined |
| Subscription plan catalog, Trial terms, EntitlementSet authority | `TASK-0096` | Covered; capability missing from legacy V1 is explicit in V2 |
| Merchant authentication identity edge | `TASK-0097` | Covered |
| Tenant registration + initial Owner + mandatory Trial outcome/recovery | `TASK-0098` | Covered; reconciled to ADR-009 rather than tenant-only creation |
| Multi-Tenant membership selection and trusted read/mutation context | `TASK-0099` | Covered |
| Invitations, memberships, Owner/Admin/Staff/Viewer, last-owner and active-member limit | `TASK-0100` | Covered |
| Privileged/security Audit evidence | `TASK-0101` | Covered |
| Active/Suspended Tenant lifecycle and platform support read path | `TASK-0102` | Covered |
| Tenant/staff Back Office experience | `TASK-0103` | Covered |
| Merchant-managed media / FilesMedia | `TASK-0104`, `TASK-0107` | Covered; no external hotlink assumption |
| Catalog Product/Category/Brand/SKU/slug persistence and invariants | `TASK-0105`–`TASK-0107` | Covered |
| Back Office Catalog experience | `TASK-0108` | Covered |
| Product source policy/runtime profile | `TASK-0109` | Covered |
| Queued crawler acquisition/raw snapshot/normalized candidate pipeline | `TASK-0110` | Covered |
| First approved source, manual URL import, merchant review/apply | `TASK-0111`, `TASK-0112` | Covered |
| Crawler observability/DLQ/redrive/reconciliation | `TASK-0113` | Covered |
| Scheduled ingestion + Subscription entitlement/source-policy gate | `TASK-0114` | Covered |
| Source-change intelligence / second source / future discovery decision | `TASK-0115`, `TASK-0116` | Covered |
| Storefront Tenant public-address ownership/lifecycle/uniqueness | `TASK-0117` | Explicit domain-decision task; not guessed |
| Public Tenant context + public Catalog contract | `TASK-0118` | Covered behind address decision |
| Public storefront experience + S3/CloudFront delivery | `TASK-0119`, `TASK-0120` | Covered |
| Cart/checkout entry and owner revalidation | `TASK-0121` | Covered |
| SalesOrder lifecycle, price snapshot and idempotent placement | `TASK-0122` | Covered |
| Guest customer/contact profile | `TASK-0123` | Covered |
| Warehouse/stock/movement + MaxWarehouses authority | `TASK-0124` | Covered |
| Stock receipt/adjustment/reservation/release concurrency | `TASK-0125`, `TASK-0126` | Covered |
| Mock Payment Provider external-like test system | `TASK-0127` | Covered; no real shopper payment provider |
| Payments capture/query/idempotency boundary | `TASK-0128` | Covered |
| Durable OrderPlaced -> reserve -> payment/reconcile -> confirm -> allocate workflow | `TASK-0129` | Covered directly from accepted ADR-010; no new orchestration decision task |
| Payment timeout/Unknown, callbacks, reconciliation and operations | `TASK-0130` | Covered |
| Fulfillment / low-stock / merchant order operations | `TASK-0131` | Covered |
| Supplier, PO, goods receipt, invoice/payment evidence | `TASK-0132`–`TASK-0134` | Covered |
| Subscription upgrade/downgrade/cancel/expiry hard-limit remediation | `TASK-0135` | Covered |
| CommerceOS SaaS Billing Mock Provider / PlatformCharge | `TASK-0136` | Covered; separate from shopper Payments |
| Order-volume usage meter/warnings | `TASK-0137` | Covered; warning-only current policy |
| Accounting moving-weighted-average cost-pool scope | `TASK-0138` | Explicit domain-decision task; not guessed |
| Accounting chart/valuation/journal/reversal/GL/trial balance | `TASK-0139`, `TASK-0140` | Covered |
| Reliable named Sales/Payments/Inventory -> Accounting routes | `TASK-0141` | Covered; ADR-006 pattern introduced with consumers rather than generic bus task |
| Procurement -> Accounting postings | `TASK-0142` | Covered |
| Refund approval capability mapping | `TASK-0143` | Explicit domain-decision task; not guessed |
| Refund request/review + Inventory/Payments/Accounting choreography | `TASK-0144` | Covered from ADR-011; no global RefundCompleted workflow |
| Accounting posting reconciliation | `TASK-0145` | Covered |
| Reporting projections/KPIs/financial views | `TASK-0146`, `TASK-0147` | Covered |
| Merchant dashboard + in-app Notification | `TASK-0148` | Covered |
| Later granular permission model | `TASK-0149` | Covered as distant Outline |
| Platform-admin operational controls | `TASK-0150` | Covered |
| Tenant isolation/privacy/security audit | `TASK-0151` | Covered without inventing deletion/erasure policy |
| API edge/resource hardening | `TASK-0152` | Covered |
| DLQ/failure injection | `TASK-0153` | Covered |
| Performance/load/cost-bounded scaling | `TASK-0154` | Covered |
| Backup/recovery/staging/production readiness | `TASK-0155` | Covered |
| Observability/cost measurements | `TASK-0156` | Covered |
| Architecture audit / selective extraction decision | `TASK-0157` | Covered |
| Module extraction after evidence/ADR only | `TASK-0158` | Covered conditionally; no pre-approved microservice split |
| Scheduled/advanced pricing | `TASK-0159`, `TASK-0160` | Covered as future Outline |
| Product variants | `TASK-0161` | Covered as future Outline |
| Custom storefront domains | `TASK-0162` | Covered as future Outline |
| Shopper accounts/customer lifecycle | `TASK-0163` | Covered as future Outline |
| Partial goods receipts | `TASK-0164` | Covered as future Outline |
| Optional email Notification | `TASK-0165` | Covered as future Outline |
| Balance-sheet projection | `TASK-0166` | Covered as future Outline |

## Negative-scope checks

The V2 audit also checked for architectural/product drift:

- no real shopper payment processor is introduced;
- CommerceOS SaaS billing is not merged into merchant-order Payments;
- no Tenant closure, hard deletion, timed retention, or privacy erasure behavior is invented from the MVP suspension decision;
- no generic EventBridge/SQS/Step Functions foundation is provisioned without a named producer/consumer/workflow;
- no domain becomes a microservice or one-stack-per-domain by default;
- no module may read/write a foreign module's persistence as a shortcut;
- no client/JWT/cache/Reporting plan, tenant, role, or entitlement value becomes current authority;
- no accounting journal mutation/rewrite is introduced;
- no unsafe inventory read-then-write mutation is accepted;
- no source snapshot becomes canonical Catalog truth automatically.

## Result

The clean-room V2 task graph covers the approved baseline and represents the remaining narrow domain gaps as explicit decision tasks. Coverage does not justify promoting distant nodes: only `TASK-0093` is Ready in the first frontier.

**Coverage audit: PASS.**
