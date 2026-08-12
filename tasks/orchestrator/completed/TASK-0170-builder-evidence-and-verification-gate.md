# TASK-0170 — Require Builder evidence before independent review

Status: Completed
Specification maturity: Completed
Execution permission: NO — completed
Owner: Builder — Engineering / Harness
Recommended implementation model: gpt-5.6-luna, medium reasoning, standard service tier
Created: 2026-08-12
Depends on: completed TASK-0169
Cloud verification: No

## Planning readiness

- Owning domain: Engineering / Harness.
- Product/domain/tenant decisions: N/A.
- Technical boundary: extend `commerceos.orchestrator.stage/v1` with repository-local JSON
  evidence artifacts; SQLite stores references, not duplicated evidence bodies.
- Required command authority: trusted Orchestrator policy plus the Ready task test plan. Builder
  may declare additional commands but cannot remove or downgrade required commands.
- Remaining blockers: None.

## Evidence contract

`BuilderResultManifest/v1` contains `contractVersion`, `taskId`, `taskCommitSha`, exactly one
verdict per task AC ID, the Git-derived changed-file inventory, declared additional verification
commands, limitations, and follow-ups. Each AC verdict is `SATISFIED` or `BLOCKED` and references
one or more evidence IDs.

`VerificationReport/v1` contains `contractVersion`, `taskId`, `taskCommitSha`, the trusted required
command set, one command result per required/additional command (`argv`, exit code, log artifact),
aggregate discovered/passed/failed/skipped-required test totals, and a final success predicate.
The Orchestrator derives changed files from Git and required commands from trusted policy/task
metadata, then rejects any manifest/report mismatch before Reviewer dispatch.

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

## Architecture impact

Harness-only extension of the accepted stage contract. Evidence is stored as versioned JSON
artifacts and referenced by the existing stage timeline; no new service or persistence boundary.

## Security and tenant impact

Builder evidence is untrusted until schema, task ID, commit SHA, Git changed-file inventory, and
trusted required-command set match. Paths remain worktree-contained. No tenant behavior changes.

## Reliability and idempotency impact

Repeated validation of the same task/commit is deterministic. Missing, duplicate, stale, skipped,
or failed required evidence blocks Reviewer dispatch without mutating lifecycle state.

## Observability impact

Validated manifest/report artifact IDs and command logs remain inspectable from the task timeline.

## Cost impact

Fake-runner tests consume no Codex quota. No AWS, external cloud, or LocalStack resource impact.

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

## Completion summary

Implemented BuilderResultManifest/v1 and VerificationReport/v1 with exact AC, changed-file, command, log, and commit binding. The trusted Verification Runner executes bounded required/additional commands and gates Reviewer plus all post-integration/finalization stages. Builder lifecycle mutations are restored from the trusted baseline. Added 83 Orchestrator regression tests; independent re-review passed; python scripts/harness_check.py passed. No product, tenant, cloud, or LocalStack impact.
