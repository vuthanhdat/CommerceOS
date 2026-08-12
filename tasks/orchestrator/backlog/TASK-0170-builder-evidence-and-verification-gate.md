# TASK-0170 — Require Builder evidence before independent review

Status: Backlog
Specification maturity: Refined
Execution permission: NO — waits for TASK-0169
Owner: Builder — Engineering / Harness
Created: 2026-08-12
Depends on: TASK-0169
Cloud verification: No

## Goal

Make implementation evidence a Builder-owned output validated by the Orchestrator before review,
while keeping canonical completion bookkeeping exclusively Orchestrator-owned.

## Business context

Review repeatedly reports missing completion evidence because evidence production and lifecycle
completion have been conflated. The Builder must prove its implementation, but must never move
the task to `completed/` or mark lifecycle `Completed`.

## In scope

- Add a machine-readable Builder result manifest tied to task ID, task commit, and contract
  version.
- Require one verdict for every acceptance-criterion ID, changed-file inventory, verification
  command/result records, test totals, required-skip count, limitations, and follow-ups.
- Have the deterministic Verification Runner execute declared required commands and produce the
  authoritative command/test result record.
- Validate Builder evidence before dispatching Reviewer.
- Remove completion-summary/status/file-move checks from Reviewer inputs and prompts.
- Keep lifecycle mutation prohibited in Builder worktrees.

## Out of scope

- Independent correctness approval by Builder.
- Canonical completion bookkeeping.
- Finding-scoped repair enforcement, delivered by TASK-0172.

## Acceptance criteria

### AC01 — Acceptance-criterion coverage

The Builder manifest references every task AC exactly once: coverage must equal 100%, with zero
unknown and zero duplicate AC IDs.

### AC02 — Verification threshold

Every required verification command exits successfully; discovered required test cases have a
100% pass rate and zero unexplained required skips. Any lower result prevents review dispatch.

### AC03 — Evidence integrity

The manifest task ID and commit SHA match the reviewed worktree, and 100% of changed files are
listed. Stale or mismatched evidence is rejected.

### AC04 — Lifecycle separation

Builder attempts to set `Completed`, add canonical completion bookkeeping, or move a task into
`completed/` are restored/rejected in 100% of regression fixtures while implementation changes
remain inspectable.

### AC05 — Reviewer receives evidence, not evidence work

Reviewer input includes the validated Builder manifest and Verification report. Reviewer prompts
contain zero requirements to create/check completion summaries, lifecycle status, or completed
task paths.

## Architecture/security/runtime impact

Harness-only. Evidence is untrusted task output until schema, task ID, and commit binding pass.
No CommerceOS or LocalStack behavior changes.

## Quantified Definition of Done

- AC mapping: exactly 100%.
- Required verification/test pass rate: 100%.
- Unexplained required skips: 0.
- Changed-file inventory coverage: 100%.
- Reviewer completion-evidence checks: 0.
- All Orchestrator tests and repository harness pass.

## Test plan

- Valid/missing/duplicate/stale Builder manifest fixtures.
- Verification failure and skipped-required-test rejection.
- Premature lifecycle mutation restoration tests.
- Prompt snapshots proving completion evidence is not Reviewer scope.
- LocalStack verification: N/A.

