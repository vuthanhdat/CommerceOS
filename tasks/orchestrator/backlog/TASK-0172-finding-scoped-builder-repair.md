# TASK-0172 — Restrict Builder rework to accepted Reviewer findings

Status: Backlog
Specification maturity: Refined
Execution permission: NO — waits for TASK-0171
Owner: Builder — Engineering / Harness
Created: 2026-08-12
Depends on: TASK-0171
Cloud verification: No

## Goal

Ensure a Builder rework round changes only paths and behavior explicitly authorized by open
Builder-owned findings, with every repair traceable to a stable finding ID.

## Business context

Returning free-form Reviewer comments to Builder allows opportunistic refactoring and unrelated
changes. A bounded repair loop needs an enforceable repair packet, not a general invitation to
improve the branch.

## In scope

- Generate a repair packet containing only open `OWNER: BUILDER` findings.
- Require each finding to declare allowed path globs and a measurable resolution condition.
- Start repair from the exact reviewed commit and bind the repair result to that baseline.
- Require a repair manifest mapping each changed file to one or more open finding IDs.
- Reject files outside finding allow-lists, unknown finding IDs, new dependencies, or task/ADR
  changes unless the finding explicitly authorizes them.
- Re-run deterministic verification after repair, then send the same ledger to Reviewer.

## Out of scope

- Implementing follow-up findings.
- Resolving planning-, Orchestrator-, or human-owned findings in Builder.
- General refactoring during repair.

## Acceptance criteria

### AC01 — Repair packet purity

100% of findings sent to Repair Builder are `OPEN`, `OWNER: BUILDER`, and route `BUILDER_FIX`.
All other owners stop or route before Builder dispatch.

### AC02 — File-scope enforcement

100% of files changed after the reviewed baseline match at least one finding's allowed paths and
are mapped to that finding in the repair manifest. One unmatched file rejects the repair.

### AC03 — No opportunistic dependency/scope expansion

New package/project dependencies, task semantics, ADRs, and unrelated docs are rejected unless
explicitly allowed by an open finding. Regression fixtures accept 0 unauthorized expansions.

### AC04 — Finding closure evidence

Every repair attempt reports all supplied finding IDs exactly once as `ADDRESSED` or `BLOCKED`
and attaches changed paths plus verification evidence. Missing or duplicate IDs fail closed.

### AC05 — Re-review continuity

The Reviewer receives the original ledger, reviewed baseline SHA, repaired SHA, repair manifest,
and verification report. Stable finding IDs are preserved across 100% of repair-round tests.

## Architecture/security/runtime impact

Harness-only. Scope enforcement must treat path globs and agent manifests as untrusted input and
resolve them within the task worktree.

## Quantified Definition of Done

- Builder repair findings with wrong owner/route dispatched: 0.
- Repair changed-file-to-finding coverage: 100%.
- Unauthorized changed files/dependencies accepted: 0.
- Supplied finding disposition coverage: 100%.
- Stable finding-ID retention: 100%.
- All Orchestrator tests and repository harness pass.

## Test plan

- Mixed-owner finding packet tests.
- Allowed/disallowed path and path-traversal fixtures.
- Unauthorized dependency/task/ADR mutation fixtures.
- Multi-round ledger continuity and bounded retry tests.
- LocalStack verification: N/A.

