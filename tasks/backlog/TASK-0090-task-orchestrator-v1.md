# TASK-0090 — Build Task Orchestrator V1

Status: Backlog
Specification maturity: Ready
Execution permission: YES
Owner: Builder — Engineering / Harness
Recommended implementation model: default implementation model; escalate only when implementation exposes a new architecture/security decision
Created: 2026-08-10
Updated: 2026-08-10
Depends on: completed TASK-0089
Cloud verification: No for the Orchestrator itself; future cloud-requiring tasks remain separately gated by canonical task metadata
Exclusive resources when Active: `repository-task-state`, `git-worktrees`, `main-merge-lane`

## Goal

Build a deterministic, **local-only CommerceOS Task Orchestrator V1** that consumes the canonical Backlog V2 task graph, computes the safe Ready frontier, dispatches a small number of Codex agents into isolated Git worktrees, coordinates Builder → deterministic verification → Reviewer → bounded fix loops, serializes verified integration through one merge lane, and automatically advances the DAG until no safe work remains or an explicit gate requires human judgment.

V1 also provides a **local monitoring/control dashboard** so the human maintainer can see what the agent system is doing without following multiple terminal sessions. The dashboard is a thin observability/control surface over Orchestrator Core; it must not contain scheduling, readiness, dependency, or merge semantics.

The normal path should require only starting the Orchestrator and observing it. Human involvement is an exception path for product/domain/architecture/security/cost/cloud decisions, unsafe or ambiguous merge/integration failures, retry exhaustion, or explicit operator stop.

```text
Canonical Backlog V2
        │
        ▼
┌───────────────────────────────┐
│       Orchestrator Core       │
│                               │
│ DAG / Scheduler / Run State   │
│ Worktrees / Agent Runner      │
│ Verification / Review         │
│ Merge Queue / Recovery        │
└──────────────┬────────────────┘
               │
        ┌──────┴──────┐
        ▼             ▼
    Builder A      Builder B
        │             │
        ▼             ▼
   Verification   Verification
        │             │
        ▼             ▼
     Reviewer       Reviewer
        └──────┬──────┘
               ▼
         serial merge lane
               │
               ▼
              main
               │
               ▼
          reload DAG

Local Dashboard ──read/control──► Orchestrator Core
```

## Authority and planning boundary

TASK-0089 is complete and the canonical metadata/DAG contract exists in:

- `tasks/BACKLOG.v2.yaml`;
- `tasks/backlog-v2/*.yaml`;
- detailed Ready task specifications referenced by `spec_path`.

The Orchestrator **consumes** this contract. It does not invent a competing task schema and does not replace Backlog Planner reasoning.

The Orchestrator may surface `PLANNING_REQUIRED`, `DOMAIN_DECISION_REQUIRED`, `ARCHITECTURE_DECISION_REQUIRED`, or similar structured blockers, but it must not create/refine/reorder tasks by inference. Future automation may invoke a Backlog Planner agent through a separate boundary; that planner loop is not required for V1.

## Operating assumptions

V1 is deliberately narrow:

- one developer machine;
- one CommerceOS repository;
- one Orchestrator process;
- maximum two concurrent writable Builder tasks by default;
- bounded Reviewer concurrency;
- exactly one serialized merge lane;
- local Git branches/worktrees;
- local run state and logs;
- GitHub remains the remote repository/system of record;
- no distributed scheduler, remote worker fleet, Redis, RabbitMQ, Temporal, Kubernetes, or always-on cloud orchestration service.

The Orchestrator itself must have no AWS runtime cost.

## Core state model

Repository task lifecycle remains authoritative according to `tasks/BACKLOG.v2.yaml`.

The Orchestrator additionally owns transient/local execution states such as:

```text
IDLE
RUNNING
STOP_REQUESTED
STOPPING
STOPPED
HUMAN_REQUIRED
```

and per-task execution states such as:

```text
QUEUED
BUILDING
VERIFYING
FIX_REQUIRED
REVIEWING
MERGE_QUEUED
INTEGRATING
COMPLETED
BLOCKED
HUMAN_REQUIRED
```

Local runtime state must never silently override canonical task maturity, dependencies, gates, or accepted repository completion state.

## In scope

### 1. Deterministic DAG loader and validator

- read canonical Backlog V2 metadata;
- validate task IDs, required metadata, dependency references, lifecycle/maturity values, gates, and spec references;
- reject duplicate canonical task IDs;
- detect missing dependencies and dependency cycles;
- reject dispatch of Refined/Outline/blocked work;
- fail closed with actionable diagnostics rather than guessing.

### 2. Ready-frontier scheduler

A task is dispatchable only when canonical metadata proves all required conditions:

- maturity is `Ready`;
- lifecycle is `Backlog`;
- all dependencies are completed according to canonical completion semantics;
- no unresolved human/product/domain/architecture/security/cost/cloud gate remains;
- no declared exclusive-resource conflict exists with active work;
- writable Builder concurrency stays within the configured limit.

Numeric task order and a detailed-looking Markdown body are never readiness evidence.

Default lane limits:

```text
Builder lane    max 2 concurrent writable tasks
Reviewer lane   max 2 concurrent read-only reviews
Merge lane      exactly 1 integration at a time
```

### 3. Local run state and resumability

Persist enough local state to inspect, resume, and recover safely after interruption.

The implementation must:

- separate repository task state from local process state;
- prevent duplicate dispatch within one Orchestrator state store;
- preserve attempt counters, loop counters, task timelines, blocker details, and current lane;
- detect stale/incomplete claims after restart;
- support status inspection without mutation;
- support deterministic resume/recovery;
- keep transient state/logs out of Git unless a task explicitly requires evidence in the repository.

A local filesystem or SQLite-backed store is sufficient. No network database is required.

### 4. Worktree and branch isolation

For every writable Builder task:

- create/reuse a task-specific branch/worktree according to repository multi-agent guidance;
- never allow Builder feature changes in the primary `main` checkout;
- prevent unsafe parallel use of declared exclusive resources;
- detect dirty/conflicting worktrees instead of overwriting them;
- make create/resume/cleanup idempotent;
- prune finished worktrees only after safe integration or explicit operator cleanup.

### 5. Codex runner abstraction

Provide a runner boundary that launches Codex non-interactively from the task worktree using repository-owned instructions.

Context includes, as applicable:

- `AGENTS.md`;
- the detailed task specification;
- relevant role instructions under `docs/agents/`;
- task-selected domain/architecture/ADR artifacts.

Requirements:

- support a fake/test runner so automated tests consume no Codex quota;
- capture structured run result, stdout/stderr/log references, exit state, and attempt identity;
- centralize model-class routing rather than hard-coding permanent model names into task specs.

### 6. Builder execution and deterministic verification

After Builder execution:

- capture structured output;
- run verification required by the task/Definition of Done;
- if deterministic verification fails, enter a bounded Builder fix loop with preserved diagnostics;
- never weaken tests, architecture rules, or guardrails merely to obtain green status.

### 7. Independent Reviewer and bounded fix loop

After local verification passes:

- Reviewer receives task context plus the produced diff/result;
- Reviewer must not silently modify the Builder branch as part of review;
- actionable findings are persisted;
- blocking findings transition to `FIX_REQUIRED` and redispatch the Builder with review evidence;
- a clean review moves the task to `MERGE_QUEUED`;
- Builder ↔ Reviewer iteration is bounded;
- exhausted loops become `HUMAN_REQUIRED` rather than infinite retry.

### 8. Serialized automatic merge queue

Only one verified task may integrate with `main` at a time.

For each queued task, the merge worker must:

1. acquire the merge-lane lock;
2. sync against the latest `origin/main`;
3. integrate the task branch using the repository-approved Git strategy;
4. handle a clean integration or enter bounded conflict resolution;
5. run post-integration verification against the combined latest-main state;
6. push `main` without force-push only after verification succeeds;
7. mark the task Completed only when the verified result is present on authoritative `main` and the required completion record exists;
8. reload the DAG and unlock newly eligible work;
9. clean the task worktree only when safe.

A successful textual Git merge is not proof of semantic correctness.

### 9. Bounded AI-assisted merge conflict resolution

Routine textual/structural conflicts may be sent to a Conflict Resolver agent when the resolution does not require changing accepted product/domain/architecture/security semantics.

The resolver may handle compatible edits such as composition/registration files, manifests, documentation indexes, project membership, or nearby implementation changes.

It must escalate instead of choosing arbitrarily when a conflict requires changing an accepted contract, domain invariant, persistence owner, security boundary, architecture decision, or product meaning.

Every AI-resolved integration still requires deterministic post-integration verification.

### 10. Human/strong-reasoning escalation

Surface structured blockers for at least:

- `PLANNING_REQUIRED`;
- `BUSINESS_DECISION_REQUIRED`;
- `DOMAIN_DECISION_REQUIRED`;
- `ARCHITECTURE_DECISION_REQUIRED`;
- `SECURITY_DECISION_REQUIRED`;
- `COST_APPROVAL_REQUIRED`;
- cloud execution not explicitly authorized by canonical metadata/policy;
- semantic merge/integration failure after bounded repair;
- dependency/backlog inconsistency;
- Builder/Reviewer/Conflict-Resolver retry exhaustion;
- dirty/manual Git state that cannot be classified safely.

Independent lanes may continue only when the scheduler can prove they are unaffected.

### 11. Cloud-task safety

The Orchestrator itself performs local orchestration only.

For a future Ready task requiring real AWS verification:

- canonical metadata must explicitly require/allow cloud verification;
- the required human/cloud authorization gate must already be satisfied;
- repository cost and teardown guardrails remain mandatory;
- the Orchestrator must never infer permission to spend AWS credit merely because execution would otherwise be blocked.

### 12. Local monitoring and control dashboard

Provide a local dashboard, served only on the developer machine, for observability and basic operator control.

The dashboard must expose at minimum:

- Orchestrator state: `IDLE`, `RUNNING`, `STOP_REQUESTED`, `STOPPING`, `STOPPED`, `HUMAN_REQUIRED`;
- canonical backlog totals and completed/total progress;
- Ready frontier;
- active Builder/Reviewer counts;
- merge queue and blockers;
- recent activity timeline;
- task id/title/spec, maturity/lifecycle and execution lane;
- dependencies/gates;
- branch/worktree;
- agent role/model class;
- attempt/fix-loop count;
- verification/review/merge status;
- structured blocker reason;
- relevant logs/timeline;
- a readable DAG/progress visualization distinguishing Outline/Refined, Ready, Active, Reviewing/Merging, Completed, and Blocked/Human Required.

The UI must not implement readiness calculation, dependency resolution, retry policy, task completion semantics, or merge decisions. Those live in Orchestrator Core and are exposed through an explicit local read/control boundary.

The UI is local-only. Authentication, internet exposure, multi-user tenancy, and remote orchestration control are out of scope for V1.

### 13. Graceful Stop — finish current active task(s), then stop

The dashboard must provide a prominent **Stop** action with graceful-drain semantics.

When the operator clicks **Stop** or invokes the equivalent CLI command:

1. atomically persist `STOP_REQUESTED`;
2. immediately stop dispatching any new Ready task;
3. claim no new Builder task after the stop request is accepted;
4. allow every task already Active at stop-request time to continue through its normal bounded lifecycle until it reaches a safe terminal point: Completed on authoritative `main`, Blocked, or Human Required;
5. continue required verification, Reviewer/fix-loop work, merge processing, and post-integration verification for those already-active tasks because these steps finish current work rather than start new backlog work;
6. transition to `STOPPED` only after all tasks active at stop-request time reach a safe terminal point and their required merge-lane work is drained;
7. leave tasks newly unlocked by those completions undispatched;
8. persist stop intent across process restart so recovery cannot accidentally claim fresh Ready work until explicit operator resume/start.

With the default two-Builder configuration, if two tasks are already Active when Stop is requested, **both currently active tasks finish**. The Orchestrator does not arbitrarily kill one.

```text
RUNNING
   │
   │ Stop
   ▼
STOP_REQUESTED
   │
   ├── reject new task dispatch
   │
   └── drain tasks already Active
           │
           ├── Builder/fix
           ├── verification
           ├── review
           ├── integration/merge when safe
           └── Completed | Blocked | Human Required
                     │
                     ▼
                  STOPPED
```

Stop is **not** an emergency process kill and must not abandon a worktree mid-write, skip verification, or leave a partially integrated `main` merely to stop quickly.

A hard-abort/emergency-kill control is not required for V1. External process termination is handled by normal resume/recovery semantics.

### 14. CLI / developer experience

Expose an ergonomic local interface with capabilities equivalent to:

```text
status       inspect DAG, active work, blockers, stop state, merge queue, Ready frontier
validate     validate canonical task metadata/DAG
plan         show tasks that would dispatch next
dry-run      simulate dispatch/review/merge/control actions without side effects
run          run automatically until no work, a blocking gate, or graceful Stop completion
stop         request the same graceful drain used by the dashboard Stop button
resume       recover safely after interruption or restart scheduling after explicit operator action
cleanup      inspect/remove finished task worktrees safely
ui           start/open the local monitoring dashboard
start        start the Orchestrator and local dashboard together
```

`run`/`start` continue advancing the DAG automatically rather than requiring one invocation per task. CLI and dashboard use the same Orchestrator Core semantics.

## Out of scope

- changing product/domain/technical architecture;
- generating or semantically refining canonical Backlog V2;
- replacing Backlog Planner reasoning with scheduler logic;
- using an LLM to decide DAG eligibility/dependency satisfaction;
- automatically invoking a Planner agent to invent new task semantics in V1;
- more than two concurrent writable Builders by default;
- multiple concurrent merge workers;
- distributed orchestration, leader election, remote worker fleets, or multi-host state;
- Kubernetes, Temporal, Redis, RabbitMQ, database servers, or always-on orchestration infrastructure;
- internet-exposed or multi-user web UI;
- a server-hosted orchestration service;
- autonomous approval of product/domain/architecture/security/cost changes;
- force-pushing or rewriting `main` history;
- automatically spending AWS credit without repository-approved cloud gates;
- an emergency hard-kill UI that intentionally abandons in-flight task state;
- changing ChatGPT/Codex account, quota, or subscription configuration.

## Acceptance criteria

### AC01 — Invalid DAG fails closed
Invalid/missing dependencies, cycles, duplicate task IDs, malformed execution state, or unsupported canonical metadata cause validation failure with actionable diagnostics and no Codex dispatch.

### AC02 — Ready frontier is mechanical
Only canonical Ready/Backlog tasks whose dependencies/gates/exclusive-resource constraints are satisfied are dispatchable. Numeric ordering and Markdown detail do not grant eligibility.

### AC03 — Parallel work is bounded
At most two writable Builder tasks and the configured bounded Reviewer count may run concurrently; declared exclusive-resource conflicts prevent unsafe co-scheduling; merge concurrency is exactly one.

### AC04 — Worktree isolation
Independent writable tasks use isolated branches/worktrees and do not modify feature code from the primary `main` checkout.

### AC05 — Dry-run is side-effect free
Dry-run reports intended tasks, roles, model classes, worktrees, gates, control state, verification, and merge actions without launching Codex, mutating Git/run state, pushing, or invoking AWS.

### AC06 — Codex execution is testable without quota
The runner has a fake/test implementation sufficient to exercise scheduler, state transitions, verification/review loops, Stop behavior, and merge orchestration without invoking Codex.

### AC07 — Verification failures enter bounded repair
A Builder result that fails deterministic verification cannot advance to review/merge; diagnostics are preserved and bounded repair is attempted before Human Required.

### AC08 — Independent review is mandatory
A Builder-produced change cannot enter the merge queue until required deterministic verification and independent review pass according to policy.

### AC09 — Integration is serialized and verified on latest main
Only one task integrates at a time, integration uses latest `main`, no force-push occurs, and post-integration verification must pass before authoritative completion.

### AC10 — Completion unlocks DAG only after authoritative merge
An agent claim, local commit, or locally green branch does not count as Completed. Dependents unlock only after the verified result and required completion record are on authoritative `main`.

### AC11 — Retry exhaustion fails safely
Builder/Reviewer/Conflict-Resolver loop exhaustion produces a persistent inspectable Human Required/Blocked state instead of infinite retry or silent acceptance.

### AC12 — Dashboard shows current system state
The local dashboard reflects Orchestrator Core state for Ready, Active, Reviewing, Merge Queued/Integrating, Completed, and Blocked/Human Required tasks; it exposes progress, lane utilization, task detail, logs/timeline, and blockers without duplicating scheduler logic in the UI.

### AC13 — Dashboard and CLI share one control model
Starting/stopping/status operations through CLI and UI observe the same persisted Orchestrator state and cannot create contradictory scheduler state.

### AC14 — Stop rejects new dispatch immediately
Given one or more tasks are Active and additional Ready work exists, when Stop is accepted, `STOP_REQUESTED` is persisted and no new Ready task is dispatched afterward.

### AC15 — Stop drains already-active tasks safely
Tasks already Active when Stop was requested may continue through required Builder/fix, verification, review, merge, and post-integration verification until each reaches Completed, Blocked, or Human Required; only then may the Orchestrator become `STOPPED`.

### AC16 — Stop does not consume newly unlocked work
If draining an active task makes dependent tasks Ready, those newly Ready tasks remain undispatched while stopping/stopped.

### AC17 — Stop survives restart
If the process restarts after Stop was requested but before draining completes, persisted stop intent is recovered; already-active work may be recovered/finished but fresh Ready work cannot be claimed until explicit operator resume/start.

### AC18 — UI remains local-only
The dashboard binds to a local developer-machine interface by default and does not introduce internet exposure, cloud hosting, user management, or a remote-control service.

### AC19 — Cloud execution remains fail-closed
A task requiring AWS/cloud verification cannot be dispatched into real cloud execution unless canonical metadata and required explicit authorization gates permit it.

### AC20 — Repository harness and Orchestrator tests pass
Unit/integration tests cover DAG validation, scheduler eligibility, exclusive resources, resumability, fake-agent execution, review/fix loops, serialized integration, Stop draining, and UI/control-state behavior; repository harness/architecture checks required by the implementation pass.

## Required implementation boundaries

The implementation must preserve explicit responsibilities equivalent to:

```text
BacklogReader / DagValidator
Scheduler
RunStateStore
WorkspaceManager
AgentRunner
VerificationRunner
ReviewCoordinator
MergeQueue / IntegrationCoordinator
OrchestratorControlService
DashboardReadModel
LocalDashboard
```

Names may differ, but task semantics, Git mutation, agent execution, UI state, and scheduling must not collapse into one god object. The dashboard depends on stable read/control interfaces rather than direct access to Git worktrees, scheduler internals, or canonical YAML mutation.

## Ready-gate result

Final Backlog Planner recheck completed after the TASK-0089 Backlog V2 contract was frozen and after the local-dashboard/graceful-stop requirement revision was approved.

- TASK-0089 is Completed.
- Canonical task metadata and execution semantics required by this tool are defined.
- Product/domain/technical baselines do not block the engineering tool.
- No cloud execution is required to implement or verify the Orchestrator itself.
- The Orchestrator consumes, but does not redefine, Backlog Planner authority.
- Graceful Stop semantics are explicit and testable.
- Local dashboard responsibility is separated from scheduler/merge semantics.

**Ready gate: satisfied. TASK READY.**
