# CommerceOS — Planning Factory & Task Maturity

_Last reviewed: 2026-08-09._

## 1. Why this exists

CommerceOS currently has a useful large candidate backlog, but a generated backlog is not automatically an implementation contract.

A task can describe a plausible outcome while still depending on business/domain/architecture decisions that have not been made yet. Allowing a Builder to implement such a task would make the Builder silently become the architect.

CommerceOS therefore separates **backlog generation** from **implementation readiness**.

> A task may exist long before it is safe to implement.

## 2. Recovery rule for the existing backlog

The current implementation backlog under `tasks/backlog/` is retained. Do not delete or renumber it merely because it was generated early.

Until individually refined, existing backlog tasks are treated as:

```text
Specification maturity: Outline
Execution permission: NO
```

They are candidate work items and dependency hypotheses, not final implementation instructions.

The Phase 0 codebase skeleton already present in the repository is also retained as **foundation scaffolding only**. It does not prove that business-domain boundaries, aggregates, contracts, DynamoDB access patterns, or workflow choices are implementation-ready.

## 3. Specification maturity

Every task uses one of these maturity states.

### Outline

Purpose and rough outcome are known, but important design dependencies may still be unresolved.

May contain:
- candidate scope;
- likely dependencies;
- rough acceptance criteria;
- architecture hypotheses.

Must not be assigned to a Builder.

### Refined

Business behavior and major constraints are understood, but one or more implementation decisions/contracts are still pending.

Must not be assigned to a Builder unless the remaining uncertainty is explicitly out of scope and cannot affect the implementation.

### Ready

The task is an implementation contract.

A Builder may take it only when the Ready gate below passes.

### Active

A Ready task has an assigned branch/worktree and Builder.

### Completed

Acceptance criteria, verification, review, and Definition of Done are satisfied.

## 4. Ready gate

Before a task becomes `Ready`, the Backlog Planner verifies that all items relevant to the task are resolved or explicitly N/A:

### Business/domain

- owning domain/bounded context is explicit;
- business outcome is unambiguous;
- aggregate/entity/value-object ownership needed by the task is defined;
- business invariants touched by the task are documented;
- state transitions/error semantics needed by the task are documented;
- cross-domain facts and ownership are explicit.

### Architecture

- module/layer ownership is explicit;
- synchronous vs asynchronous interactions needed by the task are decided;
- public/internal contracts needed by the task are defined or versioned;
- transaction/consistency boundary is defined where relevant;
- persistence ownership and required access patterns are defined where relevant;
- AWS service use follows accepted architecture/ADR rather than being invented by the Builder;
- new material architecture choices have an accepted ADR.

### Security/reliability/cost

- tenant/authentication/authorization expectations are explicit;
- idempotency/retry/timeout/duplicate-delivery behavior is explicit where relevant;
- cloud verification need and teardown are explicit;
- Free Tier/credit impact is understood.

### Execution

- dependencies are completed or otherwise satisfied;
- acceptance criteria are machine-testable where practical;
- out-of-scope boundaries prevent scope creep;
- no unresolved decision remains that would force the Builder to choose product or architecture behavior.

If any material item is missing, the task stays `Outline` or `Refined` and is routed back to the appropriate planning role.

## 5. Planning Factory

CommerceOS uses three planning roles before implementation.

```text
Product intent / existing docs
          ↓
DOMAIN ARCHITECT
business model, invariants, ownership
          ↓
TECHNICAL ARCHITECT
module/contracts/persistence/integration/ADR
          ↓
BACKLOG PLANNER
reconcile task graph + maturity + dependencies
          ↓
READY TASKS
          ↓
BUILDER
```

The first planning pass may cover the whole product at medium depth, but only near-term tasks need implementation-level detail. Later tasks can remain Outline until their dependencies and lessons from earlier phases are known.

## 6. Agent communication protocol

Agents do not rely on private cross-thread conversation as the source of truth.

They communicate through repository artifacts:

```text
Domain Architect
  → docs/domains/* / domain sections / decision notes

Technical Architect
  → docs/architecture/* / docs/adr/* / contracts

Backlog Planner
  → tasks/BACKLOG.md / task files / dependency + maturity metadata

Builder
  → branch/worktree + commits + task completion summary + PR

Reviewer / Verification
  → PR findings + CI/test evidence

Builder
  → fixes in the original task branch
```

The repository and GitHub PR are the shared blackboard/message bus.

## 7. Builder stop rule

A Builder must not silently resolve an architectural or business ambiguity.

If implementation requires a decision not encoded in the Ready task or accepted source documents, stop with:

```text
BLOCKED — PLANNING DECISION REQUIRED
```

State:
- the missing decision;
- why it affects implementation;
- which role should resolve it (Domain Architect or Technical Architect);
- affected task/contract/ADR.

## 8. Existing task reconciliation

The generated backlog is useful raw material. Reconciliation should not rewrite all 83 tasks in one expensive pass.

Process tasks by dependency frontier:

1. keep all existing tasks as Outline candidates;
2. establish/refresh domain and technical architecture baselines;
3. identify the first dependency frontier;
4. refine only those tasks and their immediate prerequisites;
5. mark individual tasks Ready;
6. implement and learn;
7. periodically revisit later Outline tasks as architecture and domain knowledge improve.

This preserves planning work without pretending distant implementation details are already known.

## 9. Initial execution freeze

Until the planning baseline is reconciled, no new business implementation task in `tasks/backlog/` should be activated solely because its numeric predecessor completed.

The exception is an explicitly human-approved foundation/recovery task whose scope cannot encode unresolved business design.

## 10. Human role

The human approves high-consequence product/architecture baselines and decides when a task/phase is worth executing.

The human should not need to micromanage routine Builder implementation once a task is Ready.
