# Review scope, finding ownership, and routing

This is the shared contract for Builder, Reviewer, and the Task Orchestrator. Role documents
may add responsibilities, but they must not contradict this contract.

## Authority and precedence

Review uses this order of authority:

1. human-approved product or architecture decisions;
2. accepted ADRs and Domain/Technical Architect artifacts;
3. the Ready task and its acceptance criteria;
4. the Definition of Done;
5. this contract and role contracts;
6. Builder evidence and agent suggestions.

Reviewer findings must cite the applicable source. A Reviewer may identify a contradiction
between authoritative artifacts, but must not resolve it by instructing the Builder to guess.

## Shared responsibilities

The executable role handoff contract is `commerceos.orchestrator.stage/v1`. The role matrix below
maps to the versioned `builder`, `verification`, `reviewer`, `repair_builder`, `integration`, and
`finalization` stages; planning entry and convergence use the versioned `planning` stage. Agent
text is evidence only until the matching stage output validates.

| Role | Owns | Does not own |
| --- | --- | --- |
| Builder | implementation, tests, task-scoped documentation, verification evidence | product/architecture decisions, independent approval, lifecycle bookkeeping |
| Reviewer | independent validation, finding classification, evidence and scope judgment | changing code, redefining scope, deciding architecture |
| Domain Architect | business semantics, domain ownership, invariants, domain decisions | implementation repair or technical runtime design |
| Technical Architect | contracts, module boundaries, persistence/integration/runtime design, ADRs | business semantics or implementation repair |
| Backlog Planner | task maturity, dependencies, readiness, planning convergence | implementation approval or architecture decisions |
| Orchestrator | dispatch, routing, retry policy, merge, completion bookkeeping | product/domain/architecture decisions |
| Human | unresolved product or high-consequence architecture decisions | routine Builder/Reviewer repair |

## Finding ownership and routing

Every blocking finding has one owner and one route. The Orchestrator must not send every finding
back to the Builder.

| Finding owner | First route | Next route after resolution | Blocking state |
| --- | --- | --- | --- |
| `BUILDER` | Builder repair | Verification -> Reviewer | `FIX_REQUIRED` |
| `DOMAIN_ARCHITECT` | Backlog Planner | Domain Architect -> Backlog Planner readiness reconciliation -> Builder | `PLANNING_REQUIRED` |
| `TECHNICAL_ARCHITECT` | Backlog Planner | Technical Architect -> Backlog Planner readiness reconciliation -> Builder | `PLANNING_REQUIRED` |
| `BACKLOG_PLANNER` | Backlog Planner | Builder when the task is Ready | `PLANNING_REQUIRED` |
| `ORCHESTRATOR` | Orchestrator handler | Resume the interrupted pipeline | `ORCHESTRATOR_ACTION_REQUIRED` |
| `HUMAN` | Human decision gate | Backlog Planner or Builder, as recorded by the decision | `HUMAN_REQUIRED` |

The planning root is always **Backlog Planner**. The Orchestrator must not call Domain or
Technical Architect directly for a review finding because the Planner owns task selection,
planning convergence, and the final Ready gate. If Technical Architect discovers a missing
business decision, the route is `Technical Architect -> Domain Architect -> Backlog Planner`,
then back to the implementation pipeline.

## Review protocol

The executable decision is `ReviewLedger/v1`, validated against the reviewed commit, Ready-task
AC inventory, Git changed-file inventory, accepted evidence references, and (for re-review) the
previous ledger plus repair delta. Free text is audit context only.

The Reviewer emits one machine-readable line for every finding:

```text
FINDING F-001 STATUS: OPEN OWNER: BUILDER ROUTE: BUILDER_FIX TITLE: short title
```

Allowed owners are `BUILDER`, `DOMAIN_ARCHITECT`, `TECHNICAL_ARCHITECT`, `BACKLOG_PLANNER`,
`ORCHESTRATOR`, and `HUMAN`. Allowed routes are `BUILDER_FIX`, `PLANNING_REQUIRED`,
`ORCHESTRATOR_ACTION_REQUIRED`, and `HUMAN_REQUIRED`.

`FOLLOW_UP` findings are non-blocking and must not enter a repair loop. Completion bookkeeping
(the selected catalog's `completed/` directory, canonical lifecycle indexes, and the completion summary written after
integration) is Orchestrator-owned and is never a Builder finding.

Reviewer execution is process-level read-only. Reviewer inspects validated Verification evidence
and must not run `scripts/harness_check.py` or repository full test suites. A needed executable
check is represented as a routed finding. On Windows the read-only Codex process starts from the
primary checkout and inspects the absolute sibling worktree because the restricted runner cannot
spawn from sibling worktrees.

Open Builder findings produce `RepairPacket/v1`. The repair Builder may change only paths matched
by those findings and must emit `RepairManifest/v1` mapping every repair-delta file to a stable
finding ID. Unknown IDs, unmatched paths, blocked dispositions, and opportunistic scope changes
fail before Verification or re-review.
