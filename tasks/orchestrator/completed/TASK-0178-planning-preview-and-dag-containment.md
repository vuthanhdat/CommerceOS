# TASK-0178 - Expose planning candidates and contain the dashboard DAG

Status: Completed
Specification maturity: Completed
Execution permission: NO — completed
Owner: Builder - Engineering / Harness
Recommended implementation model: gpt-5.6-terra, medium reasoning, standard service tier
Created: 2026-08-12
Completed: 2026-08-12
Depends on: TASK-0177
Cloud verification: No

## Goal

Make the dashboard Plan command identify the nearest Refined/Outline planning candidate and keep
the dependency DAG horizontally contained within its own panel.

## Business context

After TASK-0094 completed, TASK-0095 correctly remained Refined. The dashboard Plan button returned
only an empty Builder dispatch list, making it appear that the planner could not see TASK-0095.
The DAG also used intrinsic max-content width that expanded the whole page beyond the viewport.

## Planning readiness

- Owning domain/bounded context: Engineering / Harness.
- Domain invariants: planning maturity and Builder dispatch gates remain unchanged.
- State/error semantics: Plan remains read-only; Run/Start performs planning work.
- Module/layer ownership: planning facade, runtime controller/CLI reporting, state reset, dashboard UI.
- Persistence: existing local state database only; no schema change.
- Infrastructure/LocalStack: N/A.
- Material ADRs: ADR-013 remains authoritative; no new ADR required.
- Remaining planning blockers: None.

## In scope

- Include the nearest planning candidate in Plan command output when no Ready Builder task exists.
- Explain that Run/Start launches the planning factory; Plan remains a read-only preview.
- Clear stale HUMAN_REQUIRED control state when retryable blockers are explicitly reset and none remain.
- Contain DAG horizontal scrolling inside the DAG panel at desktop and narrow widths.
- Add controller, planning, state, HTML/CSS contract, and browser regression coverage.

## Out of scope

- Making Refined/Outline tasks directly dispatchable to Builder.
- Automatically running an agent from the read-only Plan command.
- Changing task dependency/maturity semantics or redesigning the dashboard.

## Acceptance criteria

### AC01 - Planning candidate visibility

Given no Ready task and a dependency-satisfied Refined task, when Plan is invoked, then output names
that task as `planning_candidate` while `dispatchable` remains empty.

### AC02 - Correct operator guidance

The Plan response and dashboard explain that Run or Start launches Backlog Planner for the candidate.

### AC03 - Control-state consistency

Explicit retry reset removes retryable terminal runs and leaves control state non-blocking when no
blocked run remains.

### AC04 - DAG containment

The dashboard document does not overflow horizontally; wide DAG stages scroll only inside the DAG
viewport, and task titles wrap within nodes.

## Architecture impact

- Read-only command reporting gains planning preview metadata.
- No canonical task mutation occurs from Plan.
- No ADR required.

## Security and tenant impact

- Loopback and same-origin controls remain unchanged.
- No tenant data, credentials, cloud authorization, or shell input is introduced.

## Reliability and idempotency impact

- Plan remains repeatable and side-effect free.
- Retry reset becomes internally consistent with its cleared blocker set.

## Observability impact

- Operators can see the selected planning candidate and next action before starting agents.

## Local runtime/resource impact

- No LocalStack or hosted resources are changed.

## Cost impact

- Plan consumes no provider quota; agents start only through Run/Start.
- Cost-model update required? No.

## Test plan

- Unit: planning preview and stale control-state reset.
- Integration: dashboard Plan API response.
- UI contract/browser: contained DAG at desktop and narrow viewport with no page-level overflow.
- Full repository harness.

## Completion summary

### What changed

- Plan now reports the nearest dependency-satisfied Refined/Outline planning candidate while
  preserving an empty Builder dispatch list.
- Dashboard guidance directs the operator to Run or Start to launch Backlog Planner.
- Retry reset clears stale HUMAN_REQUIRED control state when no retryable blocker remains.
- The DAG keeps intrinsic stage width inside a dedicated horizontal scroller and wraps task titles.

### Verification

- `py -3 -m unittest discover -s tests/orchestrator -p 'test_*.py'`: PASS (139 tests).
- `py -3 scripts/harness_check.py`: PASS.
- Browser QA: TASK-0095 planning preview PASS; desktop document 1265/1265 with DAG 1191/5762;
  480px document 465/465 with DAG 357/5762.

### Acceptance criteria status

- AC01-AC04: satisfied.

### Architecture/security/runtime notes

- Plan remains read-only and does not dispatch a Builder or mutate canonical maturity.
- Run/Start remains the only route that starts planning agents.
- No tenant, LocalStack, cloud authorization, or provider permission boundary changed.

### Harness improvement

- Added regressions for planning preview, controller output, stale state reset, DAG containment,
  and title wrapping.

### Follow-up tasks

- None identified.
