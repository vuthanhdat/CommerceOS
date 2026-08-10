# TASK-0089 — Generate canonical Backlog V2 and reconcile legacy backlog

Status: Completed
Specification maturity: Completed
Owner: Backlog Planner
Created: 2026-08-09
Completed: 2026-08-10
Depends on: completed TASK-0087, TASK-0088, TASK-0091, TASK-0092 and final 2026-08-10 product/technical reconciliation

## Goal

Generate a clean-room canonical CommerceOS Backlog V2 from the approved current product/domain/architecture baseline, audit the prior generated backlog separately, reconcile the existing Phase 0 skeleton, and expose only the first safe Builder-executable dependency frontier.

No business feature implementation belongs to this task.

## Authority used

TASK-0089 used the current repository authority order rather than freezing itself to stale task wording:

- `AGENTS.md` and repository planning/Definition-of-Done rules;
- `docs/agents/backlog-planner.md`;
- approved product scope and current domain baselines produced/reconciled by TASK-0087/TASK-0091 and the 2026-08-10 product-decision pass;
- technical baseline/ADRs produced by TASK-0088/TASK-0092 and the final reconciliation of `PD-004`, `PD-023`, and `PD-044`;
- current Phase 0 code/test/CDK skeleton;
- only after clean-room V2 generation: legacy `TASK-0004`–`TASK-0086` for separate salvage/conflict mapping.

The current approved MVP scope has no unresolved/deferred `PD-*` gate. Narrow domain gaps remain explicit and local: Storefront Tenant addressing, Accounting moving-weighted-average cost-pool scope, and refund approval capability mapping.

## Outputs

### Canonical machine-readable backlog

Created:

- `tasks/BACKLOG.v2.yaml` — canonical schema/readiness policy/completed roots/current frontier;
- `tasks/backlog-v2/00-foundation-tenancy.yaml`;
- `tasks/backlog-v2/01-catalog-ingestion.yaml`;
- `tasks/backlog-v2/02-storefront-commerce.yaml`;
- `tasks/backlog-v2/03-procurement-subscription-accounting.yaml`;
- `tasks/backlog-v2/04-hardening-future.yaml`.

The graph contains **75 canonical nodes** (`TASK-0090`, `TASK-0093`–`TASK-0166`). It intentionally stores minimum executable planning metadata instead of expanding every distant task into a long specification.

Maturity at completion:

- **Ready: 1** — `TASK-0093`;
- **Refined: 2** — `TASK-0090`, `TASK-0094`;
- **Outline: 72**.

The metadata contract defines task ID, maturity, type/domain/title/goal, dependencies, gates, owner role, model class, cloud-verification requirement, and optional detailed spec path. Numeric order is explicitly non-authoritative for readiness.

### Human-readable backlog

Replaced `tasks/BACKLOG.md` with the V2 planning index and first-frontier explanation.

### Clean-room coverage audit

Created `tasks/planning/TASK-0089-coverage-matrix.md`.

Key corrections relative to V1 include:

- explicit SubscriptionBilling plan/Trial/Entitlement and later subscription lifecycle/SaaS billing/usage work;
- onboarding reconciled to ADR-009;
- named ADR-006 integration rather than generic event infrastructure;
- ADR-010 order workflow implemented directly rather than re-decided;
- ADR-011 refund choreography rather than a global returns workflow;
- explicit domain-decision nodes for the remaining narrow gaps;
- extraction kept conditional on measured evidence and an accepted decision.

### Legacy V1 audit

Created `tasks/planning/TASK-0089-legacy-v1-to-v2-mapping.md` and classified **83/83** V1 tasks:

- SALVAGEABLE: 54
- PARTIAL: 7
- DUPLICATE: 6
- OVERLAP: 6
- REPLACED: 5
- PREMATURE: 3
- CONFLICT: 2

Useful outcomes were preserved in V2; stale decomposition was not copied as authority.

Per the human instruction for this execution, original `TASK-0004` through `TASK-0086` files are moved from `tasks/backlog/` into `tasks/legacy/` as historical audit artifacts. Their original Git blobs/content are preserved rather than rewritten.

### Phase 0 skeleton reconciliation

Created `tasks/planning/TASK-0089-skeleton-reconciliation.md`.

The skeleton is broadly compatible with the approved modular-serverless-monolith target, but one current executable conflict was found:

- `tests/CommerceOS.ArchitectureTests/DependencyRulesTests.cs` currently requires every Application project reference to be only its own Domain project;
- accepted architecture permits an approved foreign producer-owned `*.Contracts` reference while continuing to forbid foreign Domain/Application/Infrastructure dependencies.

Created explicit remediation `TASK-0093 — Reconcile architecture-test contract rules` instead of changing the test inside this planning task.

Real AWS Phase 0 deployment proof is represented separately by Refined `TASK-0094`; OIDC/preview/dev delivery remains Outline `TASK-0095`. Their absence is missing capability, not a reason to pre-create target architecture resources.

## First Ready frontier

Only `TASK-0093` is Ready.

Rationale:

- its architecture meaning is already approved;
- the mismatch exists in current skeleton code;
- it is local-only, deterministic harness/test remediation;
- no product/domain/AWS/persistence decision is missing;
- it removes a blocker that would otherwise reject future correct Contracts-based dependencies.

`TASK-0090` remains Refined so its detailed Orchestrator specification can be checked against the newly frozen V2 metadata contract after integration rather than being allowed to create a competing schema.

`TASK-0094` remains Refined because it depends on TASK-0093 and needs explicit real-cloud authorization plus concrete account/region/Budget notification inputs.

All more distant capability tasks remain Outline.

## Acceptance criteria status

- Clean-room V2 generated from approved product/domain/architecture authority before legacy reconciliation: **PASS**.
- Canonical dependency DAG produced with machine-readable readiness/gate metadata: **PASS**.
- Numeric ordering is not treated as execution readiness: **PASS**.
- Distant tasks remain Outline rather than receiving premature maximum detail: **PASS**.
- Existing useful V1 outcomes preserved where valid: **PASS**.
- Legacy backlog audited separately, all 83 tasks classified/mapped: **PASS**.
- Missing Subscription & Billing capability added: **PASS**.
- Superseded/unsafe orchestration/event assumptions removed from canonical authority: **PASS**.
- Existing Phase 0 skeleton audited against approved architecture: **PASS WITH REMEDIATION**.
- Skeleton conflicts represented as explicit remediation task rather than hidden code change: **PASS**.
- Only the first safe dependency frontier marked Ready: **PASS** — `TASK-0093` only.
- Orchestrator-consumable task metadata contract established: **PASS**.
- `TASK-0004`–`TASK-0086` archived under `tasks/legacy/` per human instruction: **PASS**.
- No business feature implemented: **PASS**.

## Verification

Planning/documentary verification performed:

- canonical node count: 75;
- dependency references: all resolve to a canonical task or declared completed root;
- DAG cycle check: no cycle found;
- Ready-frontier count: exactly 1;
- legacy mapping coverage: 83/83;
- legacy classification total: 83;
- skeleton conflict traced from current executable test to accepted architecture rule.

`python3 scripts/harness_check.py`: **not executed in this connector-only planning session**. Repository harness status is therefore not represented as green by TASK-0089. The first runnable checkout after integration should run the harness; TASK-0093 specifically requires it on completion.

Cloud verification: N/A — TASK-0089 creates planning/documentation only and deploys no AWS resource.

## Architecture, security, reliability, and cost implications

- Architecture: no accepted architecture changed; V2 reflects ADR-003 through ADR-011 and current technical reconciliation.
- Security/Tenant: trusted Tenant authority remains server-side; no client/JWT/cache/foreign-persistence shortcut is introduced.
- Reliability: durable named integration/workflow work is sequenced behind owner capabilities; no generic asynchronous infrastructure is pre-created.
- Cost: `$0` runtime/AWS change from TASK-0089. Real cloud work remains an explicit gated task.

## Stop condition

**V2 BASELINE READY.**

Current Builder-executable frontier: **`TASK-0093` only**.
