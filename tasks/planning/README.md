# CommerceOS Planning Backlog

_Last updated: 2026-08-13_

This folder is the canonical implementation-planning layer derived from the current `docs/` baseline and `prompt_arrange_doc.md`.

## Authority and traceability

Use this order when wording conflicts:

1. `docs/domains/product-decisions.md` for approved product policy.
2. `docs/02-business-domains.md` and detailed `docs/domains/*` for business meaning.
3. accepted ADRs and `docs/architecture/*` for implementation architecture.
4. this planning layer for sequencing and implementation scope.

Every implementation task must trace as:

`Task -> Feature plan -> Requirement ID -> source document`.

ADR-012 is authoritative: LocalStack is the only infrastructure/runtime target. No task may require a real AWS account, IAM/OIDC deployment, AWS Budget/credit controls, real-cloud staging, or real-cloud validation unless a later accepted ADR supersedes ADR-012.

## Structure

- `PRODUCT_ROADMAP.md` — Product Goal, Epics, Features and milestone sequence.
- `REQUIREMENT_INDEX.md` — stable normalized requirements and source mapping.
- `DEPENDENCY_MAP.md` — Epic/Feature/Task dependency graph and blockers.
- `BACKLOG.md` — canonical ordered task inventory and readiness.
- `plans/<feature>.md` — feature implementation plans.
- `tasks/<feature>/TASK-XXXX.md` — independently executable task specifications.

## Status and readiness

Task status uses `Backlog`, `In Progress`, `Blocked`, `Done`.

Readiness uses only:

- `Ready`
- `Blocked`
- `Needs clarification`
- `Needs design`
- `Needs code inspection`

A task is not Ready while a material product/architecture question remains unresolved.

## Starting a feature

1. Read its feature plan.
2. Read the linked requirement sources.
3. Check `DEPENDENCY_MAP.md`.
4. Select a task whose readiness is `Ready`.
5. Inspect actual source code before assuming an implementation exists or naming concrete files.

## Starting a task

1. Read the task file and its `Depends on` field.
2. Read `AGENTS.md`, relevant domain/architecture docs and ADRs.
3. Check Git status/diff in the working copy.
4. Verify task assumptions against code.
5. Stay inside scope; surface material missing decisions rather than guessing.
6. Run focused verification and `python3 scripts/harness_check.py`.
7. Record the Completion report.
8. Update the task, feature plan, backlog and dependency map when state/dependencies change.

## Completion discipline

Before marking Done, update:

- task status and acceptance criteria;
- Completion report and verification evidence;
- feature-plan progress;
- `BACKLOG.md`;
- `DEPENDENCY_MAP.md` if dependencies changed;
- `REQUIREMENT_INDEX.md` only when implementation/verification status can be proven.

## Planning reconciliation notes

- The historical task files referenced by older roadmap prose (`TASK-0093`–`TASK-0095` and earlier V2 task numbers) are not present on current `main`; they are not silently recreated. This backlog starts at `TASK-0100`.
- `prompt_arrange_doc.md` now declares `tasks/planning/` as the output root. Older examples inside that prompt that still say `docs/planning/` are treated as stale path examples.
- Older real-AWS wording in top-level NFR/roadmap history is superseded for runtime purposes by ADR-012; the underlying business/reliability/security intent is preserved.
- Current approved MVP `PD-001`–`PD-053` decisions are resolved. Remaining implementation blockers are narrower domain/contract gaps explicitly recorded in the technical baseline, especially Accounting moving-weighted-average cost-pool scope and refund-approval capability mapping.
