# TASK-0171 — Enforce a bounded read-only Reviewer contract

Status: Completed
Specification maturity: Completed
Execution permission: NO — completed
Owner: Builder — Engineering / Harness
Recommended implementation model: gpt-5.6-luna, medium reasoning, standard service tier
Created: 2026-08-12
Depends on: completed TASK-0170
Cloud verification: No

## Planning readiness

- Owning domain: Engineering / Harness; no CommerceOS product domain is touched.
- Contract ownership: `tools/commerceos_orchestrator/review_contract.py` owns
  `ReviewLedger/v1`; the Orchestrator validates it before accepting a verdict.
- Execution boundary: non-Windows uses Codex `read-only` in the task worktree. Windows uses
  Codex `read-only` from the primary repository root and an absolute sibling-worktree target,
  avoiding the documented restricted-runner spawn failure without granting write permission.
- Command boundary: Codex JSONL command records are inspected and reviews that launch the
  repository harness or full test suites are rejected. Read-only inspection remains allowed.
- Persistence: ledger JSON is repository-local evidence; SQLite stores references and stage
  state only. Business, tenant, infrastructure, LocalStack, and ADR decisions are N/A.
- Remaining planning blockers: None.

## Review ledger contract

`ReviewLedger/v1` contains `contractVersion`, `taskId`, `reviewedCommitSha`, `reviewRound`
(`INITIAL` or `REPAIR`), exactly one verdict per task AC, exactly one scope classification per
Git-derived changed file, findings, and final `verdict` (`PASS` or `FIX_REQUIRED`). AC verdicts
are `PASS` or `FAIL`; file classifications are `IN_SCOPE`, `OUT_OF_SCOPE`, `GENERATED`, or
`EVIDENCE`.

Each finding contains a stable `findingId`, `status` (`OPEN`, `RESOLVED`, or `FOLLOW_UP`),
`severity` (`HIGH`, `MEDIUM`, or `LOW`), exactly one owner and owner-consistent route, one or more
validated evidence references, one or more worktree-contained affected paths, and a measurable
acceptance condition. Owner/route pairs follow
`docs/development/17-review-scope-and-finding-ownership.md`.

On re-review every previous finding ID appears exactly once. Existing blocking findings may stay
`OPEN` or become `RESOLVED`. A new blocking finding is valid only when all affected paths belong
to the Git-derived repair delta; other new observations are `FOLLOW_UP`. `PASS` requires all ACs
to pass, no `OUT_OF_SCOPE` file, no open blocking finding, and a valid ledger.

## Goal

Make Reviewer a read-only decision stage that assesses scope, acceptance criteria, and risk from
validated evidence without rerunning the full test pipeline or performing lifecycle work.

## Business context

Reviewer currently has enough freedom to repeat test execution and rediscover Orchestrator-owned
completion bookkeeping. This wastes time and blurs the difference between mechanical
verification and independent review.

## In scope

- Run Reviewer with read-only repository permissions.
- Supply task spec, reviewed commit/diff, Builder manifest, Verification report, authoritative
  docs, and prior finding ledger when applicable.
- Define a versioned review ledger with stable finding ID, status, severity, owner, route,
  evidence reference, affected paths, and acceptance condition.
- Require explicit verdicts for every AC and every changed file's scope classification.
- Prevent Reviewer from running harness/full test suites; test execution belongs to the
  Verification Runner.
- Define first-review and bounded re-review rules.
- Persist the validated ledger as the sole review decision artifact and route from it instead of
  regex-parsed free text.

## Out of scope

- Editing implementation or task lifecycle artifacts.
- Repeating deterministic verification.
- Requiring a minimum number of findings; zero findings is valid when coverage is complete.

## Acceptance criteria

### AC01 — Read-only execution

100% of Reviewer runs use read-only workspace permissions. Write-attempt fixtures fail and leave
zero modified files.

### AC02 — No duplicate test stage

Reviewer launches zero repository harness/full-suite commands. Existing Verification results may
be inspected; a requested new executable check is routed to Verification or emitted as a finding.

### AC03 — Review coverage

Reviewer output contains verdicts for 100% of task ACs and scope classifications for 100% of
changed files, with zero unknown/duplicate IDs.

### AC04 — Ledger validity

Every blocking finding has exactly one valid owner, route, evidence reference, affected-path set,
and measurable acceptance condition. Malformed or owner/route-inconsistent findings fail closed.

### AC05 — Pass rule

`PASS` is accepted only when open blocking findings equal 0 and AC/file coverage equals 100%.
`FOLLOW_UP` count is unrestricted and non-blocking.

### AC06 — Bounded re-review

Re-review may change tracked finding status and report regressions introduced by the repair. New
unrelated observations are `FOLLOW_UP`; they cannot reopen implementation scope.

## Architecture impact

Harness-only internal versioned evidence contract. No product module, persistence technology,
cross-domain contract, infrastructure capability, or ADR change.

## Security and tenant impact

Reviewer receives untrusted evidence under a process-level read-only sandbox. Ledger paths and
evidence references must remain worktree-contained and match Orchestrator inventories.
Authentication, authorization, tenant scoping, secrets, and customer data are N/A.

## Reliability and idempotency impact

Validation for a task/commit is deterministic and side-effect free. Missing, duplicate, stale,
malformed, forbidden-command, or incomplete output fails closed before merge, lifecycle mutation,
or Builder repair dispatch.

## Observability impact

Timeline records validated ledger artifact ID, review round, verdict, finding counts, and explicit
protocol or command-policy failure reason.

## Local runtime/resource impact

No LocalStack or external cloud use. Reviewer remains Luna-medium-standard. Evidence is bounded
repository-local disk state; no persistent service or port is introduced.

## Cost impact

No external service or cloud cost. Reviewer remains on the repository-approved coding profile.

## Quantified Definition of Done

- Reviewer write operations: 0 successful.
- Reviewer full-suite/harness executions: 0.
- AC and changed-file review coverage: 100%.
- Invalid ledger lines accepted: 0.
- Open blocking findings allowed for PASS: 0.
- All Orchestrator tests and repository harness pass.

## Test plan

- Read-only runner and attempted-write fixtures.
- Reviewer command-policy fixtures.
- Ledger parser/owner-route/coverage tests.
- First-review versus re-review finding-set tests.
- Windows launch test proving primary-root execution with `read-only` and sibling-worktree target.
- Stale commit, traversal, unknown evidence, incomplete coverage, owner/route mismatch, and
  forbidden command fixtures.
- LocalStack verification: N/A.

## Completion summary

Implemented process-level read-only Reviewer execution, ReviewLedger/v1 exact AC/file coverage, structured finding ownership/routing, command-policy enforcement, write-attempt rollback, and bounded re-review continuity. Independent review resolved four findings and passed. 91 Orchestrator tests and full repository harness passed. No product, tenant, cloud, or LocalStack impact.
