# TASK-0180 - Add immediate Force Stop with resumable runtime recovery

Status: Backlog
Specification maturity: Ready
Owner: Builder - Engineering / Harness
Recommended implementation model: gpt-5.6-terra, medium reasoning, standard service tier
Created: 2026-08-13
Depends on: TASK-0179
Cloud verification: No

## Goal

Let a local operator immediately terminate a hung Orchestrator worker and its active agent process
tree without deleting task worktrees or losing the persisted execution state required by Resume.

## Business context

Graceful Stop intentionally drains active work and therefore cannot recover a hung Builder. Cleanup
only removes terminal-task worktrees and is not a runtime-control operation. Operators need an
explicit emergency action whose destructive boundary is the active process tree, not task data.

## Planning readiness

- Owning domain/bounded context: Engineering / Harness.
- State/error semantics: Force Stop terminates the registered worker process tree, preserves each
  task execution state/branch/worktree/evidence pointer, clears drain intent, and sets control state
  to `STOPPED`; Resume rehydrates those active runs through the existing resume path.
- Module ownership: local runtime process registration/control, CLI, dashboard controls, state store,
  operator documentation, and Orchestrator tests.
- Security boundary: only a repository/catalog-scoped registered worker whose PID still identifies
  an Orchestrator command may be terminated. A stale or unverifiable registration fails closed.
- Material ADRs: none; this refines the accepted local-only Orchestrator control contract.
- Remaining planning blockers: none.

## In scope

- Add a `force-stop` CLI command and a distinct dashboard Force Stop action/button.
- Persist enough worker identity to find a worker started from CLI or dashboard.
- Terminate the registered worker and descendant agent/tool processes immediately.
- Preserve active task state, branch, worktree, uncommitted files, and evidence.
- Atomically record `STOPPED` after termination and permit the existing Resume path to continue.
- Make dashboard-started workers separate child processes so the dashboard survives Force Stop.
- Add confirmation/error text that clearly distinguishes Force Stop from Stop and Cleanup.

## Out of scope

- Deleting worktrees, branches, commits, logs, evidence, or LocalStack state.
- Automatic timeout/watchdog policy.
- Resuming a partially executed external command from its instruction pointer; Resume may rerun the
  current bounded stage using the preserved worktree.
- Force stopping an unrelated or unverifiable operating-system process.

## Acceptance criteria

### AC01 - Immediate bounded termination

Given a registered active worker with descendant processes, when Force Stop is requested, then the
worker process tree is terminated and no new Orchestrator stage transition is accepted from it.

### AC02 - State and worktree preservation

Given an active task at Force Stop, its execution state, branch, worktree, attempts, and evidence
pointers remain unchanged, drain intent is cleared, control becomes `STOPPED`, and Cleanup is not run.

### AC03 - Resumable recovery

Given a force-stopped active task, when Resume is requested, a new worker starts and the existing
resume path rehydrates the active run from the preserved worktree.

### AC04 - Fail-closed process ownership

Given a missing, stale, malformed, or identity-mismatched worker registration, Force Stop reports a
bounded error and does not signal that process or mutate task state.

### AC05 - Operator controls and regression coverage

CLI help, dashboard labels/confirmation, documentation, and automated tests distinguish graceful
Stop, Force Stop, Cleanup, and Resume and cover their state-preservation contract.

## Architecture impact

No product/domain or LocalStack architecture changes. Runtime process identity remains ignored local
state under `.commerceos/orchestrator/<catalog>/`; no ADR is required.

## Security and tenant impact

No tenant data is touched. Process termination is loopback/local CLI only, requires the existing
dashboard control header/origin checks, validates repository/catalog/process identity, and fails
closed rather than targeting an arbitrary PID.

## Reliability and idempotency impact

Repeated Force Stop after a successful stop returns a stable not-running result. A crash between
process termination and state persistence is recoverable because a later Force Stop can reconcile a
dead registered worker to `STOPPED` only after validating the registration belongs to this runtime.

## Observability impact

Record worker registration and `FORCE_STOPPED`/rejected Force Stop events with bounded diagnostics;
never place prompts, secrets, or full command lines in the event detail.

## Test plan

- Unit tests for worker registration, identity rejection, process-tree termination abstraction, and
  atomic force-stop state preservation.
- Dashboard/CLI contract tests for Force Stop and Resume.
- Existing Orchestrator suite and full repository harness.
- LocalStack/infrastructure verification: No; local process-control harness only.

