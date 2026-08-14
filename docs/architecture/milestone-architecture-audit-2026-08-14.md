# Milestone architecture audit — 2026-08-14

## Method

Reviewed the module project graph, DynamoDB adapters, CDK resource topology, targeted tests, hardening matrix, and runtime tooling. The default deployment shape remains a modular serverless monolith.

## Findings

| Severity | Finding | Evidence | Action |
|---|---|---|---|
| High | No direct foreign persistence dependency was found in module application projects. | Architecture project-reference tests; each DynamoDB adapter implements an owner-local port. | Keep executable reference guardrail. |
| High | Refund effects are independent and use producer facts; no global `RefundCompleted` state exists. | Refund consumers, Accounting source claims, Reporting partial-progress projection. | Keep support view explicitly non-authoritative. |
| Medium | Refund/provider and notification operational paths now have table/queue ownership but direct-host consumers remain the deterministic test layer. | FoundationStack routes and LocalStack limitation record. | Add LocalStack worker delivery test only when a concrete worker host is introduced. |
| Medium | Application objects are intentionally compact but `AccountingFactConsumer` and `StorefrontCheckoutService` are growing integration seams. | Current application source and test coverage. | No extraction now; split only when a new producer contract or independent scaling pressure appears. |
| Low | `npm ci` can be blocked by a locked native binding in an already-running local web toolchain. | Harness failure `EPERM` against rolldown binary. | Treat as workstation/process contention; do not weaken the harness. |

## Extraction assessment

No measured independently scaling or reliability-isolation pressure justifies extracting a deployment service. The crawler, provider simulation, notification and accounting consumers are named asynchronous paths, but their ownership contracts and LocalStack queues are sufficient at current volume. Any extraction requires a new ADR with throughput, failure-isolation and operating-cost evidence.

## Follow-up

Pricing remains a module-level bounded context and its design is recorded separately. No behavior change was made by this audit.
