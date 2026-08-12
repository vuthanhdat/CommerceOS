# TASK-0171 — Enforce a bounded read-only Reviewer contract

Status: Backlog
Specification maturity: Refined
Execution permission: NO — waits for TASK-0170
Owner: Builder — Engineering / Harness
Created: 2026-08-12
Depends on: TASK-0170
Cloud verification: No

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

## Architecture/security/runtime impact

Harness-only. Read-only execution reduces accidental mutation risk. No product/tenant/LocalStack
impact.

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
- LocalStack verification: N/A.

