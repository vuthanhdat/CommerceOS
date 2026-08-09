# CommerceOS Delivery Backlog

This index is the **candidate delivery backlog** derived from the product definition and roadmap. The generated task files are useful decomposition and dependency hypotheses, but they are **not automatically implementation-ready specifications**.

Unless a task is explicitly refined through `docs/development/15-planning-factory-and-task-maturity.md`, treat it as:

```text
Specification maturity: Outline
Execution permission: NO
```

A Builder may execute only a task whose `Specification maturity` has been explicitly changed to `Ready` after domain, architecture, dependency, security, reliability, cost, and verification prerequisites are resolved.

## Planning recovery gate

Before new business implementation proceeds, run these planning tasks in dependency order:

1. `TASK-0087` — reconcile business-domain baseline (Domain Architect, strong reasoning model).
2. `TASK-0088` — reconcile technical architecture baseline (Technical Architect, strong reasoning model).
3. `TASK-0089` — reconcile backlog readiness and the existing Phase 0 skeleton (Backlog Planner, Luna unless escalation is needed).

The existing Phase 0 codebase skeleton is retained as foundation scaffolding; it is not evidence that business-domain or persistence decisions are already finalized.

Do **not** rewrite all 83 tasks to maximum detail up front. After TASK-0089, refine only the first dependency frontier and immediate prerequisites to `Ready`; later tasks should remain Outline until earlier implementation teaches us more.

## Planning rules

- Task order is dependency-driven; numeric order is the recommended default, not permission to bypass an unmet dependency.
- `Backlog` is a location/status, not a readiness signal.
- Milestones A–E retain the meanings defined in `docs/07-delivery-roadmap.md`.
- Conditional tasks require their execution gate to be satisfied before activation.
- Later product capabilities are explicitly captured but remain unscheduled until a milestone review prioritizes them.
- Architecture/security/payment/accounting/concurrency decisions must be resolved in their decision task or ADR before routine implementation proceeds.
- At most two Builder-style coding tasks run in parallel, and only when their dependencies/contracts are stable and their writable worktrees are isolated.
- Agents communicate through repository artifacts, ADRs/contracts, task metadata, PR findings, and CI evidence rather than relying on private cross-thread conversation.

## Summary

| Scope | Tasks |
|---|---:|
| Phase 0 | 2 |
| Phase 1 | 4 |
| Phase 2 | 4 |
| Phase 3 | 6 |
| Phase 4 | 3 |
| Phase 5 | 5 |
| Phase 6 | 4 |
| Phase 7 | 3 |
| Phase 8 | 4 |
| Phase 9 | 2 |
| Phase 10 | 3 |
| Phase 11 | 4 |
| Phase 12 | 5 |
| Phase 13 | 4 |
| Later product capability | 8 |
| Phase 14 | 4 |
| Phase 15 | 4 |
| Phase 16 | 8 |
| Phase 17 | 1 |
| Phase 18 | 5 |
| Planning recovery | 3 |
| **Total backlog** | **86** |

## Task map

### Planning recovery — execute before new business implementation

| Task | Outcome | Owner | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0087](completed/TASK-0087-domain-baseline-reconciliation.md) | Reconcile implementation-useful business-domain baseline | Domain Architect | product/domain docs | Completed |
| [TASK-0088](backlog/TASK-0088-technical-architecture-baseline-reconciliation.md) | Reconcile technical architecture baseline | Technical Architect | TASK-0087 | — |
| [TASK-0089](backlog/TASK-0089-backlog-readiness-and-skeleton-reconciliation.md) | Reconcile candidate backlog, readiness, and Phase 0 skeleton | Backlog Planner | TASK-0087, TASK-0088 | — |

### Phase 0

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0004](backlog/TASK-0004-aws-dev-foundation-cost-guardrails.md) | Deploy the AWS dev foundation and cost guardrails | Foundation | TASK-0003 | Planning gate + readiness required |
| [TASK-0005](backlog/TASK-0005-oidc-ci-cd-preview-delivery.md) | Establish OIDC CI/CD and ephemeral preview delivery | Foundation | TASK-0004 | Planning gate + readiness required |

### Phase 1

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0006](backlog/TASK-0006-tenant-registration-business-profile.md) | Deliver tenant registration and business profiles | Milestone A | TASK-0004 | Planning gate + readiness required |
| [TASK-0007](backlog/TASK-0007-cognito-auth-trusted-tenant-context.md) | Integrate Cognito authentication and trusted tenant context | Milestone A | TASK-0006 | Planning gate + readiness required |
| [TASK-0008](backlog/TASK-0008-staff-invitations-memberships-rbac.md) | Manage staff invitations, memberships, and tenant roles | Milestone A | TASK-0007 | Planning gate + readiness required |
| [TASK-0009](backlog/TASK-0009-tenant-isolation-privileged-audit.md) | Enforce tenant isolation and privileged audit guardrails | Milestone A | TASK-0006, TASK-0007, TASK-0008 | Planning gate + readiness required |

### Phase 2

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0010](backlog/TASK-0010-catalog-product-model-access-patterns.md) | Establish the canonical product model and access patterns | Milestone A | TASK-0009 | Planning gate + readiness required |
| [TASK-0011](backlog/TASK-0011-tenant-scoped-catalog-management.md) | Deliver tenant-scoped catalog management | Milestone A | TASK-0010 | Planning gate + readiness required |
| [TASK-0012](backlog/TASK-0012-product-publication-media-references.md) | Deliver product publication and media references | Milestone A | TASK-0011 | Planning gate + readiness required |
| [TASK-0013](backlog/TASK-0013-backoffice-catalog-experience.md) | Deliver the back-office catalog experience | Milestone A | TASK-0011, TASK-0012 | Planning gate + readiness required |

### Phase 3

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0014](backlog/TASK-0014-product-source-registry-policy-gate.md) | Establish the product-source registry and policy gate | Milestone A | TASK-0009 | Planning gate + readiness required |
| [TASK-0015](backlog/TASK-0015-queued-ingestion-snapshot-pipeline.md) | Build the queued ingestion and snapshot pipeline | Milestone A | TASK-0004, TASK-0014 | Planning gate + readiness required |
| [TASK-0016](backlog/TASK-0016-first-policy-approved-source-adapter.md) | Implement the first policy-approved source adapter | Milestone A | TASK-0014, TASK-0015 | Policy review + planning readiness required |
| [TASK-0017](backlog/TASK-0017-manual-product-url-import.md) | Deliver manual product URL import | Milestone A | TASK-0015, TASK-0016 | Planning gate + readiness required |
| [TASK-0018](backlog/TASK-0018-merchant-import-review-catalog-mapping.md) | Deliver merchant import review and catalog mapping | Milestone A | TASK-0013, TASK-0017 | Planning gate + readiness required |
| [TASK-0019](backlog/TASK-0019-crawler-observability-dlq-recovery.md) | Make crawler failures observable and recoverable | Milestone A | TASK-0015, TASK-0016, TASK-0017 | Planning gate + readiness required |

### Phase 4

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0020](backlog/TASK-0020-storefront-tenancy-public-catalog.md) | Expose tenant storefront configuration and public catalog contracts | Milestone A | TASK-0006, TASK-0012 | Planning gate + readiness required |
| [TASK-0021](backlog/TASK-0021-public-storefront-catalog-experience.md) | Deliver the public storefront catalog experience | Milestone A | TASK-0020 | Planning gate + readiness required |
| [TASK-0022](backlog/TASK-0022-storefront-cloud-delivery-caching.md) | Deploy the storefront with CDN caching and image delivery | Milestone A | TASK-0005, TASK-0021 | Planning gate + readiness required |

### Phase 5

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0023](backlog/TASK-0023-cart-checkout-entry.md) | Deliver customer cart behavior and checkout entry | Milestone A | TASK-0021 | Planning gate + readiness required |
| [TASK-0024](backlog/TASK-0024-sales-order-lifecycle-persistence.md) | Establish sales-order lifecycle and persistence | Milestone A | TASK-0009, TASK-0011 | Planning gate + readiness required |
| [TASK-0025](backlog/TASK-0025-idempotent-checkout-price-snapshot.md) | Create orders idempotently with price and discount snapshots | Milestone A | TASK-0023, TASK-0024 | Planning gate + readiness required |
| [TASK-0026](backlog/TASK-0026-guest-checkout-customer-profiles.md) | Support guest checkout and tenant customer profiles | Milestone A | TASK-0024, TASK-0025 | Planning gate + readiness required |
| [TASK-0027](backlog/TASK-0027-backoffice-order-operations.md) | Deliver back-office order operations | Milestone A | TASK-0024, TASK-0025 | Planning gate + readiness required |

### Phase 6

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0028](backlog/TASK-0028-inventory-stock-movement-foundation.md) | Establish single-warehouse inventory and movement history | Milestone A | TASK-0009, TASK-0011 | Planning gate + readiness required |
| [TASK-0029](backlog/TASK-0029-inventory-receipt-adjustment.md) | Receive and adjust stock with auditability | Milestone A | TASK-0028 | Planning gate + readiness required |
| [TASK-0030](backlog/TASK-0030-inventory-reservation-release-concurrency.md) | Reserve and release stock safely under concurrency | Milestone A | TASK-0028, TASK-0029 | Planning gate + readiness required |
| [TASK-0031](backlog/TASK-0031-order-allocation-fulfillment-low-stock.md) | Allocate and fulfill orders with low-stock visibility | Milestone A | TASK-0024, TASK-0030 | Planning gate + readiness required |

### Phase 7

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0032](backlog/TASK-0032-mock-payment-core-provider.md) | Deploy the core Mock Payment Provider | Milestone A | TASK-0005 | Planning gate + readiness required |
| [TASK-0033](backlog/TASK-0033-payment-success-decline-contracts.md) | Prove payment success, decline, and idempotency contracts | Milestone A | TASK-0032 | Planning gate + readiness required |
| [TASK-0034](backlog/TASK-0034-checkout-payment-integration.md) | Integrate checkout with the payment boundary | Milestone A | TASK-0025, TASK-0030, TASK-0033 | Planning gate + readiness required |

### Phase 8

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0035](backlog/TASK-0035-payment-failure-ambiguity-scenarios.md) | Add deterministic payment failure and ambiguity scenarios | Milestone B | TASK-0033, TASK-0034 | Planning gate + readiness required |
| [TASK-0036](backlog/TASK-0036-signed-payment-webhooks.md) | Deliver signed, retryable, deduplicated payment webhooks | Milestone B | TASK-0032, TASK-0035 | Planning gate + readiness required |
| [TASK-0037](backlog/TASK-0037-payment-unknown-reconciliation.md) | Resolve PaymentUnknown through retry and reconciliation | Milestone B | TASK-0035, TASK-0036 | Planning gate + readiness required |
| [TASK-0038](backlog/TASK-0038-payment-operations-recovery.md) | Deliver payment operations and recovery tooling | Milestone B | TASK-0037 | Planning gate + readiness required |

### Phase 9

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0039](backlog/TASK-0039-checkout-orchestration-decision.md) | Decide checkout orchestration from measured complexity | Milestone B | TASK-0034, TASK-0035, TASK-0036, TASK-0037 | Produces an ADR; implementation depends on its accepted decision. |
| [TASK-0040](backlog/TASK-0040-observable-checkout-state-machine.md) | Implement an observable checkout state machine | Milestone B | TASK-0039 | Run only if TASK-0039 accepts Step Functions or another explicit orchestration mechanism. |

### Phase 10

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0041](backlog/TASK-0041-supplier-purchase-order-management.md) | Manage suppliers and purchase orders | Milestone C | TASK-0009, TASK-0011 | Planning gate + readiness required |
| [TASK-0042](backlog/TASK-0042-goods-receipt-inventory-integration.md) | Receive purchased goods into inventory | Milestone C | TASK-0029, TASK-0041 | Planning gate + readiness required |
| [TASK-0043](backlog/TASK-0043-supplier-invoice-payment-operations.md) | Track supplier invoices, payments, and procurement operations | Milestone C | TASK-0041, TASK-0042 | Planning gate + readiness required |

### Phase 11

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0044](backlog/TASK-0044-accounting-policy-chart-of-accounts.md) | Define accounting policy and chart of accounts | Milestone C | TASK-0009 | Produces an ADR or policy record before automatic posting rules are implemented. |
| [TASK-0045](backlog/TASK-0045-journal-posting-immutability.md) | Post balanced, immutable, traceable journals | Milestone C | TASK-0044 | Planning gate + readiness required |
| [TASK-0046](backlog/TASK-0046-journal-reversal-manual-controls.md) | Reverse journals and control manual accounting actions | Milestone C | TASK-0045 | Planning gate + readiness required |
| [TASK-0047](backlog/TASK-0047-general-ledger-trial-balance.md) | Deliver general ledger and trial balance | Milestone C | TASK-0045 | Planning gate + readiness required |

### Phase 12

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0048](backlog/TASK-0048-domain-event-publication-foundation.md) | Establish reliable domain-event publication | Milestone D | TASK-0024, TASK-0031, TASK-0034, TASK-0043, TASK-0045 | Requires an ADR for the publication/atomicity mechanism and public event contracts. |
| [TASK-0049](backlog/TASK-0049-accounting-event-worker.md) | Consume accounting events idempotently | Milestone D | TASK-0045, TASK-0048 | Planning gate + readiness required |
| [TASK-0050](backlog/TASK-0050-sales-payment-accounting-postings.md) | Post sales and payment events automatically | Milestone C / D | TASK-0034, TASK-0049 | Planning gate + readiness required |
| [TASK-0051](backlog/TASK-0051-inventory-procurement-accounting-postings.md) | Post inventory and procurement events automatically | Milestone C / D | TASK-0031, TASK-0043, TASK-0049 | Planning gate + readiness required |
| [TASK-0052](backlog/TASK-0052-accounting-posting-reconciliation.md) | Reconcile missing or failed accounting postings | Milestone D | TASK-0050, TASK-0051 | Planning gate + readiness required |

### Phase 13

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0053](backlog/TASK-0053-reporting-projection-foundation.md) | Establish event-driven reporting projections | Milestone C | TASK-0048 | Planning gate + readiness required |
| [TASK-0054](backlog/TASK-0054-commerce-operations-kpis.md) | Deliver commerce and operations KPIs | Milestone C | TASK-0024, TASK-0031, TASK-0053 | Planning gate + readiness required |
| [TASK-0055](backlog/TASK-0055-financial-projections-pnl.md) | Deliver financial projections and basic P&L | Milestone C | TASK-0047, TASK-0050, TASK-0051, TASK-0053 | Planning gate + readiness required |
| [TASK-0056](backlog/TASK-0056-merchant-dashboard-notifications.md) | Deliver merchant dashboards and in-app notifications | Milestone C / D | TASK-0019, TASK-0038, TASK-0052, TASK-0054, TASK-0055 | Planning gate + readiness required |

### Later product capability

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0057](backlog/TASK-0057-scheduled-promotions.md) | Deliver authorized scheduled promotions | Unscheduled | TASK-0025, TASK-0048 | Planning gate + readiness required |
| [TASK-0058](backlog/TASK-0058-advanced-pricing-promotions.md) | Add coupons, price lists, segments, and campaign pricing | Unscheduled | TASK-0026, TASK-0057 | Planning gate + readiness required |
| [TASK-0081](backlog/TASK-0081-product-variants.md) | Add tenant-safe product variants | Unscheduled | TASK-0013, TASK-0031 | Planning gate + readiness required |
| [TASK-0082](backlog/TASK-0082-custom-storefront-domains.md) | Support custom storefront domains | Unscheduled | TASK-0022, TASK-0070 | Requires DNS/certificate ownership validation and current cost review. |
| [TASK-0083](backlog/TASK-0083-shopper-accounts-customer-lifecycle.md) | Add shopper accounts, customer notes, and data lifecycle controls | Unscheduled | TASK-0026, TASK-0069 | Planning gate + readiness required |
| [TASK-0084](backlog/TASK-0084-partial-goods-receipts.md) | Support partial goods receipts | Unscheduled | TASK-0042 | Planning gate + readiness required |
| [TASK-0085](backlog/TASK-0085-email-notifications.md) | Deliver optional email notifications | Unscheduled | TASK-0056, TASK-0069 | Requires provider/service and cost decision before implementation. |
| [TASK-0086](backlog/TASK-0086-balance-sheet-projection.md) | Deliver a basic balance-sheet projection | Unscheduled | TASK-0047, TASK-0055 | Planning gate + readiness required |

### Phase 14

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0059](backlog/TASK-0059-second-product-source-adapter.md) | Add a second policy-approved product source | Milestone D | TASK-0019 | Policy review + planning readiness required |
| [TASK-0060](backlog/TASK-0060-scheduled-source-refresh-safety.md) | Refresh mapped sources safely on a schedule | Milestone D | TASK-0059 | Planning gate + readiness required |
| [TASK-0061](backlog/TASK-0061-source-change-price-parser-intelligence.md) | Track source changes, price history, and parser health | Milestone D | TASK-0060 | Planning gate + readiness required |
| [TASK-0062](backlog/TASK-0062-advanced-source-decision-gates.md) | Decide advanced source integrations and discovery crawling | Milestone D | TASK-0061 | Amazon and discovery work proceed only through separately accepted policy/license decisions. |

### Phase 15

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0063](backlog/TASK-0063-return-request-validation.md) | Validate and record return requests | Milestone D | TASK-0024, TASK-0034 | Planning gate + readiness required |
| [TASK-0064](backlog/TASK-0064-idempotent-payment-refunds.md) | Refund mock payments idempotently | Milestone D | TASK-0032, TASK-0063 | Planning gate + readiness required |
| [TASK-0065](backlog/TASK-0065-return-inventory-accounting-compensation.md) | Compensate inventory and accounting for returns | Milestone D | TASK-0031, TASK-0051, TASK-0064 | Planning gate + readiness required |
| [TASK-0066](backlog/TASK-0066-returns-workflow-operations.md) | Orchestrate and operate the returns workflow | Milestone D | TASK-0040, TASK-0063, TASK-0064, TASK-0065 | Step Functions is used only if the accepted orchestration decision justifies it. |

### Phase 16

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0067](backlog/TASK-0067-granular-permission-authorization.md) | Move authorization from coarse roles to granular permissions | Milestone E | TASK-0008, TASK-0066 | Planning gate + readiness required |
| [TASK-0068](backlog/TASK-0068-platform-admin-operations.md) | Deliver platform-admin tenant and operations controls | Milestone E | TASK-0019, TASK-0038, TASK-0052, TASK-0056, TASK-0067 | Planning gate + readiness required |
| [TASK-0069](backlog/TASK-0069-tenant-security-privacy-audit.md) | Audit tenant isolation, privacy, and sensitive operations | Milestone E | TASK-0026, TASK-0067, TASK-0068 | Planning gate + readiness required |
| [TASK-0070](backlog/TASK-0070-api-edge-resource-hardening.md) | Harden API edge and resource consumption limits | Milestone E | TASK-0069 | WAF is introduced only if review justifies its cost and an ADR is accepted when material. |
| [TASK-0071](backlog/TASK-0071-dlq-recovery-failure-injection.md) | Exercise DLQ recovery and failure injection | Milestone E | TASK-0019, TASK-0038, TASK-0052, TASK-0066 | Planning gate + readiness required |
| [TASK-0072](backlog/TASK-0072-performance-load-verification.md) | Verify performance and cost-bounded scaling | Milestone E | TASK-0070, TASK-0071 | Requires an approved test budget before cloud load execution. |
| [TASK-0073](backlog/TASK-0073-backup-recovery-production-readiness.md) | Prove backup, recovery, and production delivery readiness | Milestone E | TASK-0069, TASK-0070, TASK-0071 | Planning gate + readiness required |
| [TASK-0074](backlog/TASK-0074-observability-cost-baseline.md) | Replace cost assumptions with operational measurements | Milestone E | TASK-0068, TASK-0072, TASK-0073 | Planning gate + readiness required |

### Phase 17

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0075](backlog/TASK-0075-architecture-audit-remediation-backlog.md) | Audit the architecture and generate remediation work | Milestone E | TASK-0074 | Architecture changes are outputs as new tasks/ADRs, not silently implemented here. |

### Phase 18

| Task | Outcome | Milestone | Depends on | Gate |
|---|---|---|---|---|
| [TASK-0076](backlog/TASK-0076-selective-extraction-decision.md) | Decide whether selective extraction is justified | Milestone E | TASK-0075 | Produces accepted/rejected ADR decisions for each candidate. |
| [TASK-0077](backlog/TASK-0077-extract-product-data-ingestion.md) | Extract Product Data Ingestion when justified | Milestone E | TASK-0076 | Conditional on an accepted extraction ADR. |
| [TASK-0078](backlog/TASK-0078-extract-accounting.md) | Extract Accounting when justified | Milestone E | TASK-0076 | Conditional on an accepted extraction ADR. |
| [TASK-0079](backlog/TASK-0079-extract-reporting.md) | Extract Reporting when justified | Milestone E | TASK-0076 | Conditional on an accepted extraction ADR. |
| [TASK-0080](backlog/TASK-0080-post-extraction-validation.md) | Validate architecture, reliability, and cost after extraction | Milestone E | TASK-0077, TASK-0078, TASK-0079 | Run against only the extraction work actually approved and completed. |

## Parallelization guide

Safe parallelism must be re-checked when tasks become Ready. Typical candidates after their shared contracts stabilize are:

- infrastructure delivery and a business-design task only if they do not share undecided architecture;
- storefront UI work and crawler operations after their API/event contracts are fixed;
- reporting projections by independent metric family after TASK-0053;
- Phase 16 security, resilience, and observability reviews only when they do not edit the same shared infrastructure surfaces.

Do not parallelize tenant-context design, inventory concurrency, payment ambiguity, accounting policy, event-publication semantics, or extraction decisions before their governing task/ADR is accepted.
