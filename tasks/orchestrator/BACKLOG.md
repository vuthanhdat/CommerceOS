# CommerceOS Task Orchestrator — Tooling Backlog

This catalog contains work whose deliverable is the repository-owned Task Orchestrator,
planning/review harness, agent workflow, or operator UI. It is isolated from CommerceOS
product/runtime work.

## Catalog boundary

- Canonical shard: `tasks/orchestrator/backlog-v2/00-harness.yaml`.
- Detailed open specs: `tasks/orchestrator/backlog/`.
- Completion records: `tasks/orchestrator/completed/`.
- Run with `python tools/orchestrator.py --catalog orchestrator <command>`.
- Default state/log root: `.commerceos/orchestrator/orchestrator/`.

Recently completed:

- `TASK-0178` — Expose planning candidates and contain the dashboard DAG (`Completed`).

- `TASK-0177` — Configure Codex sandbox by agent role (`Completed`).

- `TASK-0176` — Add dashboard command controls and agent settings (`Completed`).

- `TASK-0175` — Switch the coding execution profile from Luna to Terra (`Completed`).

- `TASK-0174` — Expose and verify the strict Orchestrator workflow end to end (`Completed`).

- `TASK-0173` — Make completion an Orchestrator-owned verified transaction (`Completed`).

- `TASK-0172` — Restrict Builder rework to accepted Reviewer findings (`Completed`).
- `TASK-0171` — Enforce a bounded read-only Reviewer contract (`Completed`).
- `TASK-0170` — Require Builder evidence before independent review (`Completed`).
- `TASK-0169` — Define machine-checkable agent stage contracts and workflow states (`Completed`).
- `TASK-0167` — Stream Codex agent activity and pin role-based model profiles (`Completed`).
- `TASK-0168` — Separate CommerceOS and Orchestrator task catalogs (`Completed`).

## Ready frontier

- `TASK-0179` — Make worktree creation resilient to transient origin fetch failures (`Ready`).



## Strict workflow boundary program

```text
TASK-0169 stage contracts/state model
       ↓
TASK-0170 Builder evidence + Verification gate
       ↓
TASK-0171 read-only Reviewer ledger
       ├───────────────┐
       ↓               ↓
TASK-0172 scoped       TASK-0173 completion transaction
Builder repair         owned by Orchestrator
       └───────┬───────┘
               ↓
TASK-0174 status observability + end-to-end contract
```

TASK-0172 and TASK-0173 are completed; TASK-0174 passed its planning gate.
