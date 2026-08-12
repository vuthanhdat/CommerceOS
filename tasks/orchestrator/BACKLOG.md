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

## Current task

- `TASK-0167` — Stream Codex agent activity and pin role-based model profiles (`Active`).

Recently completed:

- `TASK-0168` — Separate CommerceOS and Orchestrator task catalogs (`Completed`).

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

All six tasks are `Refined`. TASK-0169 becomes eligible for Ready promotion after TASK-0167 is
completed; each later task follows its declared dependency gate.
