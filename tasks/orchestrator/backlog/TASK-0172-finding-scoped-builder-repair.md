# TASK-0172 — Restrict Builder rework to accepted Reviewer findings

Status: Backlog
Specification maturity: Ready
Execution permission: YES
Owner: Builder — Engineering / Harness
Recommended implementation model: gpt-5.6-luna, medium reasoning, standard service tier
Created: 2026-08-12
Depends on: completed TASK-0171
Cloud verification: No

## Planning readiness

- Owning domain: Engineering / Harness; product/domain/tenant decisions are N/A.
- Contracts: `RepairPacket/v1` is Orchestrator-generated from validated open Builder findings;
  `RepairManifest/v1` is Builder output validated against packet, reviewed baseline, repaired
  commit, and Git repair delta.
- Allowed-path semantics: each finding's validated `affectedPaths` entries are repository-relative
  POSIX glob patterns (`*` does not cross `/`, `**` may); absolute/traversal patterns are invalid.
- Dependency expansion: lock/manifest/project files, task specs/indexes, and `docs/adr/**` are
  denied unless an open finding's pattern explicitly matches that path.
- Persistence/integration: JSON evidence artifacts only; existing SQLite stage references and
  Verification/Reviewer pipeline remain authoritative.
- Infrastructure, LocalStack, cost, security identity, and ADR decisions: N/A.
- Remaining planning blockers: None.

## Repair contracts

`RepairPacket/v1` contains task ID, reviewed baseline SHA, original ledger artifact/reference,
and only open `BUILDER/BUILDER_FIX` findings with stable IDs, allowed path globs, evidence refs,
and acceptance conditions.

`RepairManifest/v1` contains task ID, baseline SHA, repaired SHA, exactly one disposition
(`ADDRESSED` or `BLOCKED`) for every packet finding, and an exact mapping from every Git repair
delta path to one or more packet finding IDs. The Orchestrator rejects missing/duplicate/unknown
IDs, unmatched paths, unsafe globs, unauthorized dependency/governance paths, and stale commits
before deterministic verification or re-review.

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

## Architecture impact

Harness-only versioned evidence contracts; no product module, cross-domain boundary, persistence
technology, infrastructure capability, or ADR change.

## Security and tenant impact

All globs, paths, IDs, and manifests are untrusted and validated against the worktree-contained
Git delta. Authentication, authorization, tenant data, secrets, and customer data are N/A.

## Reliability and idempotency impact

Packet/manifest generation and validation are deterministic for the same ledger and commits.
Invalid or blocked repair evidence fails closed without verification, re-review, merge, or
lifecycle mutation.

## Observability impact

Repair packet/manifest artifact IDs, baseline/repaired SHA, finding dispositions, and explicit
scope failure reasons are retained in stage timeline evidence.

## Local runtime/resource impact

Repository-local JSON only; no LocalStack, external service, port, or persistent runtime.

## Cost impact

No external/cloud cost; approved coding profile remains unchanged.

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
