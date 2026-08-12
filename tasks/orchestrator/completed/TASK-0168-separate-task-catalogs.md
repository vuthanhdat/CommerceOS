# TASK-0168 — Separate CommerceOS and Orchestrator task catalogs

Status: Completed
Specification maturity: Completed
Execution permission: NO — completed
Owner: Builder — Engineering / Harness
Created: 2026-08-12
Completed: 2026-08-12
Depends on: completed TASK-0090
Cloud verification: No

## Goal

Prevent Orchestrator/Harness development work from being scheduled, displayed, or persisted as
if it were CommerceOS product/runtime work.

## Business context

The mixed backlog lets tooling work appear beside product work in scheduling, status, and local
persistence. Operators cannot reliably tell which system a task changes, and tooling can compete
with product delivery despite having a different lifecycle and risk profile.

## Planning readiness

- Owning domain: Engineering / Harness.
- Product/domain decisions: N/A — repository tooling boundary only.
- Architecture decision: two named task catalogs with one shared dependency registry.
- Remaining blockers: None; the human explicitly requested physical separation.

## In scope

- Move task specs, completion records, and shards into `tasks/commerceos/` and
  `tasks/orchestrator/`.
- Add an explicit CLI catalog selector, defaulting to `commerceos`.
- Filter scheduling, planning, dashboard reads, completion bookkeeping, and cleanup by catalog.
- Isolate state and logs by catalog.
- Preserve shared completed dependency references through `tasks/BACKLOG.v2.yaml`.
- Add migration/usage documentation and regression tests.

## Out of scope

- Renumbering existing task IDs.
- Changing task maturity or acceptance semantics.
- Redesigning the full agent workflow/state machine.

## Acceptance criteria

### AC01 — Physical separation

All canonical shards and detailed task records are under exactly one named catalog directory.

### AC02 — Scheduling isolation

Loading `commerceos` exposes zero Orchestrator tasks; loading `orchestrator` exposes zero
CommerceOS tasks. Each catalog retains valid dependencies and Ready-frontier semantics.

### AC03 — Runtime isolation

Default state/log paths include the selected catalog and the dashboard reports only that catalog.

### AC04 — Completion stays in catalog

Finalization moves a task from its own `backlog/` to its own `completed/` and updates the shared
registry without crossing catalog boundaries.

### AC05 — Verification

Orchestrator tests and repository task validation pass with both catalogs.

## Architecture impact

Repository harness only. The shared registry remains the cross-catalog dependency authority;
catalog shards and artifacts become the scheduling boundary.

## Security and tenant impact

No tenant/authentication/authorization behavior changes. Catalog selection is validated against
an allow-list and cannot become an arbitrary filesystem path.

## Reliability and idempotency impact

Catalog selection is deterministic. Repeated validation and finalization stay within the selected
catalog. Existing legacy test fixtures remain supported.

## Observability impact

Dashboard status includes the selected catalog. SQLite state and logs use a catalog-specific
runtime root.

## Cost impact

No AWS or LocalStack cost impact. Local disk usage adds one small state/log directory per catalog.

## Test plan

- Unit-test catalog filtering and invalid catalog rejection.
- Unit-test catalog-specific completion destination.
- Validate both catalogs through the CLI.
- Run Orchestrator tests and repository harness.

## Completion summary

TASK-0168 is complete. CommerceOS product tasks and Orchestrator tooling tasks are now
physically separated into named catalogs, with catalog-aware scheduling, planning, dashboard,
state/log paths, completion bookkeeping, and cleanup. The shared registry remains the explicit
cross-catalog dependency authority.

### Acceptance criteria status

- AC01 — Satisfied: canonical shards and detailed task records are isolated under
  `tasks/commerceos/` or `tasks/orchestrator/`.
- AC02 — Satisfied: catalog loading and dependency/frontier validation are catalog-specific.
- AC03 — Satisfied: CLI, dashboard, state, and logs use the selected catalog.
- AC04 — Satisfied: completion destinations remain inside the selected catalog.
- AC05 — Satisfied: Orchestrator tests and repository harness verification pass.

### Verification evidence

- `python -m unittest discover -s tests/orchestrator -p "test_*.py"` — passed.
- `python scripts/harness_check.py` — passed.

### Scope and impact

- Documentation/task organization and Orchestrator harness behavior only.
- No CommerceOS product, tenant, security, domain, or LocalStack runtime behavior changed.
