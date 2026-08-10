# TASK-0089 — Generate canonical Backlog V2 and reconcile legacy backlog

Status: Backlog
Specification maturity: Ready
Owner: Backlog Planner
Recommended model: Strong reasoning model for this initial full-project synthesis; Luna is appropriate only for later incremental frontier maintenance.
Created: 2026-08-09
Updated: 2026-08-10
Depends on: completed/approved TASK-0087, completed/approved TASK-0088, current product/NFR/roadmap documents, accepted domain/architecture baselines and ADRs

## Goal

Generate a new canonical CommerceOS task graph from the approved product, domain, and technical architecture sources of truth **without using the existing AI-generated backlog as the decomposition source**. Only after the independent V2 draft is complete, compare it with the legacy backlog to identify missing coverage, duplicate/overlapping work, useful work to salvage, premature tasks, dependency mistakes, and skeleton remediation.

The final result must expose a complete project-level V2 DAG, preserve distant work as Outline, and refine only the first safe implementation frontier to Ready.

## Business context

CommerceOS already contains an AI-generated backlog of roughly 83 candidate tasks and a Phase 0 codebase skeleton. The old backlog contains useful ideas, but it was generated before the domain and technical baselines were sufficiently mature. Repairing each old task in place would anchor the new Planner to the original decomposition and could preserve incorrect boundaries simply because they already exist.

TASK-0089 therefore uses a clean-room planning pass:

```text
Product / NFR / Roadmap
          ↓
Approved Domain Baseline (TASK-0087)
          ↓
Approved Technical Architecture (TASK-0088 + ADRs)
          ↓
Independent Backlog V2 DAG
          ↓
Freeze V2 draft
          ↓
Read legacy backlog
          ↓
Coverage / gap / duplicate / salvage mapping
          ↓
First Ready Frontier
```

The legacy backlog is evidence and reference material, not planning authority.

## Source-of-truth priority

When sources disagree, use this precedence and surface unresolved conflicts instead of guessing:

1. explicit human-approved product decisions and current product definition;
2. NFRs and accepted roadmap constraints;
3. TASK-0087 business/domain baseline and product-decision register;
4. TASK-0088 technical architecture baseline and accepted ADRs;
5. current codebase/skeleton as implementation reality to audit, not design authority;
6. legacy generated backlog only after the independent V2 draft has been produced.

A legacy task must never override an accepted product/domain/architecture decision merely because it is more detailed.

## Planning readiness

- Owning domain/bounded context: Engineering / Backlog governance
- Domain baseline required: TASK-0087 completed and approved
- Technical architecture required: TASK-0088 completed and approved
- Aggregate/entity/value-object decisions: sufficient for the first implementation frontier according to TASK-0087
- State/error semantics: sufficient for the first implementation frontier according to TASK-0087/TASK-0088
- Cross-domain ownership/contracts: use TASK-0087/TASK-0088; unresolved items remain planning blockers
- Module/layer ownership: TASK-0088
- Sync/async interaction policy: TASK-0088 and accepted ADRs
- Transaction/consistency boundaries: TASK-0088 and accepted ADRs
- Persistence ownership/access-pattern strategy: TASK-0088 and accepted ADRs
- Material ADRs: all first-frontier ADRs required by TASK-0088 must be accepted before a dependent implementation task becomes Ready
- Remaining planning blockers: any unresolved PD/ADR/task-specific decision discovered during V2 generation

Do not start TASK-0089 while TASK-0088 is still materially incomplete. A partial 0088 commit may be used for inspection only; it does not satisfy this dependency until the architecture task is completed/approved.

## In scope

### Pass A — Generate Backlog V2 independently

Before reading legacy task bodies as decomposition input:

- read the authoritative product, NFR, roadmap, TASK-0087 domain baseline, TASK-0088 architecture baseline, accepted ADRs, and current skeleton;
- derive the complete project-level capability/work graph from those sources;
- create a new candidate V2 DAG covering product capabilities, domain work, architecture/foundation work, security, reliability, observability, cost, testing, cloud verification, operations, and later evolution checkpoints;
- define for every V2 task at minimum: ID/provisional ID, title, observable goal, task type, owning domain, dependencies, important gates, maturity, and recommended agent/model class;
- mark distant tasks as `Outline` rather than inventing premature implementation details;
- refine only near-term prerequisites and the first dependency frontier to implementation-ready detail;
- explicitly encode design/decision tasks where implementation must not choose policy or architecture itself;
- identify safe parallelism and conflict/exclusivity groups where useful;
- freeze the independent V2 draft before consulting legacy task bodies for reconciliation.

### Pass B — Coverage and legacy reconciliation

After the V2 draft is frozen:

- compare the V2 graph against the legacy generated backlog;
- build forward traceability from Product → Domain → Architecture → V2 Task to find missing task coverage;
- build reverse traceability from legacy Task → Architecture/Domain/Product reason to find unnecessary or unjustified work;
- classify legacy/V2 relationships using at least:
  - `COVERED`
  - `PARTIAL`
  - `MISSING`
  - `DUPLICATE`
  - `OVERLAP`
  - `PREMATURE`
  - `CONFLICT`
  - `SALVAGEABLE`
  - `REPLACED`
- create new V2 candidate tasks for genuinely missing capabilities;
- split V2 tasks that mix independently verifiable outcomes;
- merge or remove duplicate V2 work when justified;
- record which legacy tasks are replaced, split, merged, deferred, salvaged, or no longer justified;
- do not silently copy implementation assumptions from V1 into V2.

### Pass C — Skeleton reconciliation

- audit the existing Phase 0 skeleton and ADR-002 against the approved TASK-0087/TASK-0088 baselines;
- retain compatible scaffolding;
- where code/tooling/folder/IaC choices conflict materially with the approved baseline, create explicit remediation tasks instead of silently changing foundation code inside TASK-0089;
- distinguish harmless scaffolding from accidental architecture commitments.

### Pass D — Execution frontier

- determine the first safe dependency frontier from V2;
- refine only the first frontier and its immediate prerequisites to `Specification maturity: Ready`;
- leave later tasks `Outline` or `Refined` as appropriate;
- recommend at most two independent Builder-style tasks for parallel execution;
- document why those tasks are safe to execute concurrently, including shared contracts/write surfaces/cloud resources;
- if no implementation task is safely Ready, return the required planning/decision task instead of forcing a Builder task.

## Expected outputs

TASK-0089 should produce repository artifacts equivalent to:

1. **Canonical V2 task graph** — complete project-level DAG with maturity/dependency/gate metadata.
2. **Coverage matrix** — Product/Domain/Architecture concerns mapped to V2 tasks, exposing `MISSING`/`PARTIAL` coverage.
3. **Legacy V1 → V2 mapping** — every relevant old task classified as replaced/salvaged/split/merged/deferred/conflicting/unjustified.
4. **Skeleton reconciliation report** — compatible foundation choices plus explicit remediation tasks for conflicts.
5. **Ready Frontier #1** — at most two Builder tasks, or a documented reason why implementation must remain blocked.

The exact storage paths may be selected consistently with repository conventions, but V1 should remain available for audit/history until a later explicit promotion/archive step. Do not destructively overwrite or delete the legacy backlog during the independent V2 generation pass.

## Out of scope

- implementing business features;
- modifying TASK-0087 domain decisions;
- modifying TASK-0088 architecture decisions;
- completing or rewriting TASK-0088;
- deploying AWS resources;
- blindly repairing all 83 legacy tasks in place;
- copying legacy task decomposition into V2 before the independent V2 draft is frozen;
- writing maximum implementation detail for the entire project;
- deleting legacy backlog/history during this task;
- making unresolved business/security/accounting/payment/concurrency decisions by assumption.

## Acceptance criteria

### AC01 — V2 is independently derived

Given approved product/domain/architecture baselines
when the Planner generates the first V2 draft
then the decomposition is derived from those authoritative sources and is completed/frozen before legacy task bodies are used for reconciliation.

### AC02 — Complete project-level DAG exists

Given the CommerceOS product scope and accepted architecture
when V2 generation completes
then every known capability and required engineering concern is represented in a coherent dependency graph at Outline-or-better maturity, without requiring all distant tasks to contain implementation detail.

### AC03 — Missing work is detectable

Given Product → Domain → Architecture sources of truth
when forward traceability is evaluated
then missing and partial task coverage is explicitly identified and genuinely missing V2 tasks are created rather than hidden behind status changes to existing tasks.

### AC04 — Excess/duplicate/premature work is detectable

Given the frozen V2 graph and legacy V1 backlog
when reverse traceability and comparison are performed
then duplicate, overlapping, premature, conflicting, unjustified, and salvageable legacy work is explicitly classified.

### AC05 — V1 does not anchor V2 decomposition

Given detailed-looking legacy tasks exist
when V2 differs in task boundaries, dependencies, or architecture ownership
then V2 follows the approved product/domain/architecture model rather than preserving V1 structure for convenience.

### AC06 — Skeleton is audited, not blindly trusted or discarded

Given the current Phase 0 skeleton and ADR-002
when compared with TASK-0087/TASK-0088
then compatible foundation choices are retained and every material conflict becomes an explicit remediation/ADR task rather than an unrelated implementation change.

### AC07 — First Ready Frontier is explicit

Given the V2 dependency graph and all accepted planning gates
when reconciliation completes
then only tasks that pass the Ready gate are marked Ready and the first implementation frontier is explicit.

### AC08 — Parallel work is bounded and justified

Given multiple V2 tasks may be Ready
when initial execution candidates are recommended
then at most two Builder-style tasks are proposed in parallel and their dependency, contract, write-surface, local-state, and cloud-resource independence is documented.

### AC09 — Legacy history remains traceable

Given the old backlog contains useful historical decomposition
when TASK-0089 completes
then V1 remains inspectable and a mapping exists from relevant V1 tasks to their V2 disposition.

## Architecture impact

- Owning domain: Engineering / Repository governance
- Domains touched: all indirectly through planning coverage
- Persistence impact: none directly
- Events/contracts impact: planning metadata references accepted contracts; no runtime contract is changed by this task
- AWS/IaC impact: no deployment; current skeleton/CDK choices are audited only
- ADR required? No by default; discovered architecture conflicts must become follow-up ADR/remediation work rather than being silently resolved here

## Security and tenant impact

- Authentication: no runtime change
- Authorization: V2 coverage must include the accepted trusted-authority/authorization model from TASK-0088
- Tenant scoping: V2 task coverage must preserve TASK-0087/TASK-0088 tenant isolation requirements and associated verification work
- Sensitive data/secrets: none
- Abuse/rate-limit considerations: ensure relevant edge/integration tasks exist in V2 where required by NFRs

## Reliability and idempotency impact

- Retry behavior: no runtime behavior changed; V2 must include required retry/failure work for affected capabilities
- Timeout semantics: ensure payment/external-integration ambiguity has explicit task coverage where applicable
- Duplicate-delivery behavior: ensure at-least-once consumers and deduplication requirements map to explicit V2 work
- Idempotency key/strategy: an implementation task cannot become Ready until the relevant strategy is resolved
- DLQ/recovery/reconciliation: coverage matrix must expose missing recovery/reconciliation work rather than assuming it is implicit

## Observability impact

- Logs: no runtime change
- Metrics: no runtime change
- Traces/correlation: V2 must include required correlation/diagnostic coverage according to accepted architecture/NFRs
- Operational states/errors: task graph must cover operational recovery/status behavior where required

## Cost impact

- Request/compute impact: none
- Storage impact: repository planning artifacts only
- Network impact: none
- New AWS resources/services: none
- Free Tier allowance relevant to this task: no AWS usage; V2 tasks that introduce services must retain cost/Free Tier gates
- Expected monthly cost change or `negligible` with rationale: negligible
- Estimated one-off cloud-test/load-test cost, if any: none

## Test plan

- Unit: N/A
- Integration: N/A
- Architecture: verify V2 near-term tasks reference accepted TASK-0088 architecture/ADRs and do not require Builder invention
- Contract: verify dependencies and producer/consumer contract ownership are coherent for the first frontier
- Coverage: verify forward Product → Domain → Architecture → V2 Task traceability and reverse legacy-task justification mapping
- Graph: verify there are no accidental dependency cycles and every Ready task has completed/accepted prerequisites
- IaC: audit skeleton/ADR compatibility only; no deploy
- E2E/manual: human review of V2 decomposition, V1→V2 mapping, missing/duplicate findings, and Ready Frontier #1
- **Cloud verification required?** No — planning/reconciliation task only
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

## Stop conditions

TASK-0089 may finish with one of:

- `V2 BASELINE READY` — independent V2 DAG, coverage/mapping, skeleton audit, and Ready Frontier #1 are complete;
- `PLANNING BLOCKED` — an unresolved product/domain/architecture decision prevents coherent V2 decomposition; name the decision and owning role;
- `ARCHITECTURE BASELINE INCOMPLETE` — TASK-0088 is not sufficiently completed/approved to safely generate V2; do not compensate by guessing from legacy tasks.
