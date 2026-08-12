# TASK-0173 — Make completion an Orchestrator-owned verified transaction

Status: Backlog
Specification maturity: Refined
Execution permission: NO — waits for TASK-0170 and TASK-0171
Owner: Builder — Engineering / Harness
Created: 2026-08-12
Depends on: TASK-0170, TASK-0171
Cloud verification: No

## Goal

Make the actor that marks a task `Completed` solely responsible for creating the completion
summary, moving the canonical task file, updating registry/shard/index state, verifying the final
snapshot, and pushing it atomically.

## Business context

Builder must not complete lifecycle bookkeeping, and Reviewer must not complain that it is
missing. Completion happens only after review, merge, and post-integration verification, under
the serialized Orchestrator integration lane.

## In scope

- Define one completion transaction owned by Orchestrator.
- Build completion summary from validated Builder, Verification, Reviewer, integration, and
  finalization evidence.
- Move the task from its selected catalog's `backlog/` to that catalog's `completed/`.
- Update status, maturity, execution permission, completion date, shard path, lifecycle metadata,
  ready frontier, completed dependency registry when required, and catalog index.
- Verify the final canonical snapshot before non-force push.
- Roll back all unpushed integration/finalization changes on failure and support idempotent
  recovery when implementation is already on remote main.

## Out of scope

- Builder or Reviewer lifecycle writes.
- Force-pushing or rewriting remote main history.

## Acceptance criteria

### AC01 — Sole ownership

Only the Orchestrator finalizer writes lifecycle completion artifacts in production code. Static
and pipeline tests find zero Builder/Reviewer completion writes.

### AC02 — Entry gate

Finalization starts only after review has 0 open blocking findings, pre-merge required tests pass
100%, merge succeeds on latest main, and post-integration required tests pass 100%.

### AC03 — Canonical consistency

After finalization, exactly one canonical detailed task file exists in the selected catalog's
`completed/`; backlog copies equal 0; status/maturity/lifecycle/path/index references agree in
100% of catalog fixtures.

### AC04 — Verified push

Post-bookkeeping validation and required verification pass 100% before push. A single failed
check produces zero pushed completion commits.

### AC05 — Atomic recovery

Failure injection at each finalization step leaves either the pre-finalization state or one fully
valid completed state. Re-running recovery produces no duplicate completion record or duplicate
registry entry.

## Architecture/security/runtime impact

Harness-only, serialized Git integration. No LocalStack or tenant impact.

## Quantified Definition of Done

- Builder/Reviewer completion writes: 0.
- Finalization prerequisite satisfaction: 100%.
- Canonical task copies after success: exactly 1 completed, 0 backlog.
- Pushes after failed final verification: 0.
- Recovery duplicate artifacts/entries: 0.
- All Orchestrator tests and repository harness pass.

## Test plan

- Both catalog completion-destination fixtures.
- Failure injection before/after each bookkeeping step and before push.
- Already-on-remote-main recovery and repeat-run idempotency tests.
- Static role/prompt checks for lifecycle write ownership.
- LocalStack verification: N/A.

