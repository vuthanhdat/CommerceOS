# TASK-0089 — Reconcile backlog readiness and Phase 0 skeleton

Status: Backlog
Specification maturity: Ready
Owner: Backlog Planner
Created: 2026-08-09
Depends on: TASK-0087, TASK-0088, current `tasks/BACKLOG.md`, Phase 0 skeleton/ADR-002

## Goal

Reconcile the generated candidate backlog and existing Phase 0 codebase skeleton against the approved domain and technical architecture baselines, then expose only the first safe implementation frontier as Ready work.

## Business context

CommerceOS already contains an 83-task generated backlog and a Phase 0 skeleton. Much of this work is useful, but it was created before the planning maturity model existed. Deleting it would waste useful decomposition; treating it all as implementation-ready would push unresolved design choices into Builder code.

## Planning readiness

- Owning domain/bounded context: Engineering/Backlog governance
- Domain invariants required by this task: TASK-0087
- Aggregate/entity/value-object decisions resolved? Sufficient for first frontier — TASK-0087
- State/error semantics resolved? Sufficient for first frontier — TASK-0087
- Cross-domain ownership/contracts resolved? Business baseline from TASK-0087 and technical baseline from TASK-0088
- Module/layer ownership resolved? TASK-0088
- Sync/async interaction decision resolved? Sufficient for first frontier — TASK-0088
- Transaction/consistency boundary resolved? Sufficient for first frontier — TASK-0088
- Persistence ownership/access patterns resolved? Sufficient for first frontier — TASK-0088
- Material ADRs accepted? Required near-term ADRs from TASK-0088
- Remaining planning blockers: any task-specific unknowns discovered during reconciliation

## In scope

- treat all existing business implementation tasks as Outline unless the Ready gate is explicitly proven;
- audit the generated dependency graph for contradictions, missing prerequisites, duplicates, or tasks that mix design and implementation improperly;
- preserve useful candidate tasks rather than renumbering/recreating them unnecessarily;
- refine only the first dependency frontier and immediate prerequisites to implementation-ready detail;
- add/update `Specification maturity`, dependency, planning-readiness, and blocker information for tasks selected for near-term execution;
- classify later tasks as Outline and defer implementation-level detail where earlier learning can change the design;
- audit the Phase 0 skeleton and ADR-002 against TASK-0087/TASK-0088;
- if the skeleton conflicts with accepted baselines, create explicit remediation tasks rather than silently rewriting unrelated foundation code;
- recommend at most two independent Ready tasks for initial execution.

## Out of scope

- implementing business features;
- rewriting all 83 tasks to maximum detail;
- deleting the backlog merely because it was AI-generated;
- changing product/domain/architecture decisions owned by TASK-0087/TASK-0088;
- deploying AWS resources.

## Acceptance criteria

### AC01 — Backlog is no longer falsely implementation-ready

Given the generated backlog exists
when reconciliation completes
then its governance clearly distinguishes Outline/Refined/Ready tasks and no task is eligible for a Builder solely because it has a detailed-looking specification.

### AC02 — First frontier is explicit

Given the approved domain and technical architecture baselines
when the task graph is reconciled
then the first safe implementation frontier and its satisfied dependencies are explicit, with each selected task passing the Ready gate.

### AC03 — Later work remains intentionally provisional

Given distant tasks depend on future learning
when reconciliation completes
then they remain Outline rather than encoding speculative implementation decisions as commitments.

### AC04 — Skeleton is audited, not blindly trusted or discarded

Given the current Phase 0 skeleton and ADR-002
when compared with the approved baselines
then compatible foundation choices are retained and every material conflict is captured as a separate remediation/ADR task.

### AC05 — Parallel work is bounded

Given multiple tasks may be Ready
when execution candidates are recommended
then at most two Builder tasks are proposed in parallel and only when their contracts/write surfaces are sufficiently independent.

## Architecture impact

- Owning domain: Engineering/Repository governance
- Domains touched: all indirectly
- Persistence impact: none directly
- Events/contracts impact: task metadata may reference accepted contracts; no runtime contract change
- AWS/IaC impact: no deployment; skeleton/CDK structure is audited only
- ADR required? No by default; conflicts discovered may require follow-up ADRs

## Security and tenant impact

- Authentication: no runtime change
- Authorization: no runtime change
- Tenant scoping: task readiness must preserve accepted tenant model
- Sensitive data/secrets: none
- Abuse/rate-limit considerations: N/A

## Reliability and idempotency impact

- Retry behavior: N/A — planning task
- Timeout semantics: N/A
- Duplicate-delivery behavior: N/A
- Idempotency key/strategy: ensure relevant implementation tasks reference resolved strategy before Ready
- DLQ/recovery/reconciliation: ensure relevant implementation tasks reference resolved strategy before Ready

## Observability impact

- Logs: N/A
- Metrics: N/A
- Traces/correlation: N/A
- Operational states/errors: N/A

## Cost impact

- Request/compute impact: none
- Storage impact: repository metadata/docs only
- Network impact: none
- New AWS resources/services: none
- Free Tier allowance relevant to this task: no AWS usage; future Ready tasks must respect guardrails
- Expected monthly cost change or `negligible` with rationale: negligible
- Estimated one-off cloud-test/load-test cost, if any: none

## Test plan

- Unit: N/A
- Integration: N/A
- Architecture: verify first-frontier tasks reference accepted architecture decisions
- Contract: verify task dependencies/contracts are coherent
- IaC: audit skeleton/ADR compatibility only
- E2E/manual: human review of recommended Ready frontier
- **Cloud verification required?** No — planning/reconciliation task
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A
