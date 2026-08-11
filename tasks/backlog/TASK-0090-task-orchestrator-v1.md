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

Build a deterministic, **local-only CommerceOS Task Orchestrator V1** that consumes canonical Backlog V2 metadata, computes the safe Ready frontier, dispatches at most two writable Codex Builder task pipelines into isolated Git worktrees, coordinates deterministic verification and independent Reviewer/fix loops, serializes integration through one merge lane, and advances the DAG until no safe work remains or an explicit human gate is reached.

V1 also provides a loopback-only monitoring/control dashboard. A prominent **Stop** action has graceful-drain semantics: fresh Ready work stops immediately while every task already Active at stop-request time may finish its bounded Builder/fix/verify/review/merge lifecycle before the Orchestrator becomes `STOPPED`.

## Business context

CommerceOS uses Harness Engineering: expensive product/domain/architecture reasoning is encoded into repository artifacts first, then routine Builder execution should be automatable. TASK-0089 established the canonical Backlog V2 schema, dependencies, maturity, lifecycle semantics, exclusive resources, cloud gates, and integration policy. This task turns that machine-readable planning contract into a local execution engine.

The Orchestrator is not a new product/business domain. It must not replace the Backlog Planner or infer business semantics. It may surface structured blockers such as `PLANNING_REQUIRED`, `DOMAIN_DECISION_REQUIRED`, or `ARCHITECTURE_DECISION_REQUIRED`, but V1 does not automatically invent/refine/reorder tasks.

Normal operation should be:

```text
canonical Ready DAG
       ↓
Task Orchestrator
       ↓
Builder A / Builder B
       ↓
verification
       ↓
independent Reviewer
       ↓
serialized verified merge
       ↓
authoritative main
       ↓
reload DAG
```

## In scope

### 1. Canonical backlog reader and validator

- Read `tasks/BACKLOG.v2.yaml` plus declared `tasks/backlog-v2/*.yaml` shards.
- Validate task row shape, ids, maturity/lifecycle, spec paths, dependencies, gates, execution metadata, and declared Ready frontier.
- Reject duplicate IDs, missing dependencies, dependency cycles, malformed Ready tasks, and a Ready frontier that disagrees with mechanical eligibility.
- Fail closed with actionable diagnostics; never repair or reinterpret planning semantics with an LLM.

### 2. Ready-frontier scheduler

Dispatch only when canonical metadata proves:

- maturity `Ready`;
- lifecycle `Backlog`;
- all dependencies Completed/declared completed roots;
- gates empty/satisfied;
- no conflicting active `exclusive_resources`;
- writable concurrency below the configured maximum.

Defaults:

```text
Builder/task pipeline    max 2
Reviewer                 bounded by task pipelines (max 2)
Merge lane               exactly 1
```

Numeric task order is never readiness evidence. A local `BLOCKED` or `HUMAN_REQUIRED` run must not be auto-claimed again until an explicit operator retry/resume.

### 3. Persisted local run/control state

- Persist process state, per-task execution state, attempt/fix counters, branch/worktree, blocker details, graceful-stop drain membership, and event timeline.
- Separate local runtime state from canonical task planning state.
- Survive process restart without duplicate dispatch.
- Recover interrupted active work conservatively.
- Preserve `STOP_REQUESTED`/drain intent across restart.
- Use local filesystem/SQLite only; transient state/logs remain outside Git.

Orchestrator control states include:

```text
IDLE
RUNNING
STOP_REQUESTED
STOPPING
STOPPED
HUMAN_REQUIRED
```

Per-task states include:

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

### 4. Git worktree/branch isolation

- One writable task = one task branch = one sibling worktree.
- Builder never writes feature changes in primary `main` checkout.
- Create/reuse worktrees idempotently and refuse mismatched/unsafe state.
- Do not silently overwrite dirty worktrees.
- Commit inspectable Builder changes before review.
- Refuse no-op/empty implementation evidence rather than silently completing a task.
- Clean worktrees only after safe integration or explicit cleanup.

### 5. Codex runner abstraction

- Launch Codex non-interactively from the task worktree through one adapter.
- Build prompts from repository-owned `AGENTS.md`, role instructions, detailed task spec, architecture rules, worktree policy, Definition of Done, and task-referenced artifacts.
- Centralize executable/model routing configuration rather than embedding permanent model names in task files.
- Capture structured exit result and log path.
- Provide a deterministic fake agent runner for tests so tests consume no Codex quota.
- Reviewer runs read-only and returns an explicit pass/fix result.

### 6. Builder → verification → Reviewer → fix loop

- Run Builder against the isolated worktree.
- Run deterministic task/repository verification afterward.
- A verification failure returns diagnostics to a bounded Builder repair loop.
- A green Builder result is reviewed independently.
- Blocking Reviewer findings return to a bounded Builder fix loop.
- Retry exhaustion becomes persistent `HUMAN_REQUIRED`; never loop forever or weaken guardrails.

### 7. Serialized automatic integration

For each verified task:

1. acquire one process-local merge-lane lock;
2. require a clean primary integration checkout;
3. fetch/sync latest `origin/main` with fast-forward-only semantics;
4. integrate the task branch;
5. use bounded AI conflict resolution only for safe implementation-level textual/structural conflicts;
6. escalate semantic/product/domain/architecture/security conflicts;
7. run deterministic verification against combined latest-main state;
8. update canonical completion bookkeeping only after integrated code is green;
9. verify bookkeeping too;
10. push `main` without force;
11. consider the task Completed only after authoritative `main` contains verified implementation + required completion record;
12. reload the DAG.

A push race or other unpushed integration failure must roll the local integration checkout back to current `origin/main` rather than leaving a hidden divergent control checkout.

### 8. Graceful Stop

When Stop is accepted:

1. persist stop intent atomically;
2. immediately stop claiming fresh Ready tasks;
3. mark every currently Active task as part of the drain set;
4. allow those task(s) to continue through bounded Builder/fix, verification, review, merge, and post-integration verification;
5. let each drain task reach `COMPLETED`, `BLOCKED`, or `HUMAN_REQUIRED`;
6. do not start tasks newly unlocked by those completions;
7. transition to `STOPPED` only after the drain set is terminal;
8. recover the same intent after process restart.

With two active Builders, both active tasks drain. Stop is not an emergency kill and must not abandon a task mid-write or a partially integrated `main`.

### 9. Local dashboard

Provide a dashboard bound to loopback only. Show at minimum:

- Orchestrator state and completed/total progress;
- Ready frontier;
- Builder/Reviewer utilization;
- merge queue;
- blocker count;
- Ready / Active / Review / Merge / Blocked workflow views;
- task id/title/spec, dependencies/gates, branch/worktree, model class, attempts, blocker detail;
- DAG/progress representation;
- recent event timeline;
- Stop and Resume controls.

The dashboard is a thin read/control surface over Orchestrator Core. It must not calculate readiness, modify canonical YAML directly, resolve dependencies, or perform Git integration itself.

### 10. CLI

Expose equivalent local commands:

```text
status
validate
plan
dry-run
run
stop
resume
cleanup
ui
start
```

`start` runs scheduler + local dashboard. CLI and UI share the same persisted run/control model.

### 11. Cloud safety

The Orchestrator itself uses no AWS resource. For future tasks:

- canonical cloud gates remain authoritative;
- a task with `cloud_verification: required` additionally needs explicit runtime operator consent before real cloud execution;
- a Builder is explicitly told whether real cloud execution is authorized for the current run;
- no AWS spend is inferred merely to keep automation moving.

## Out of scope

- changing CommerceOS product/domain/technical architecture;
- creating or semantically refining Backlog V2 tasks;
- replacing Backlog Planner reasoning;
- LLM-based DAG eligibility/dependency decisions;
- automatic Planner-agent loop in V1;
- more than two writable task pipelines by default;
- multiple merge workers;
- distributed scheduler/leader election/remote worker fleet;
- Redis, RabbitMQ, Temporal, Kubernetes, network DB, or hosted orchestration infrastructure;
- internet-exposed/multi-user dashboard;
- autonomous approval of product/domain/architecture/security/cost decisions;
- force-push/rewrite of `main`;
- unapproved AWS spending;
- emergency hard-kill UI;
- changing ChatGPT/Codex account/quota/subscription configuration.

## Acceptance criteria

### AC01 — Invalid DAG fails closed
Malformed metadata, duplicate task IDs, missing dependencies, cycles, unsupported states, or invalid declared frontier cause validation failure and no Codex dispatch.

### AC02 — Ready frontier is mechanical
Only canonical Ready/Backlog work with satisfied dependencies/gates and no exclusive-resource conflict can be dispatched.

### AC03 — Parallel work is bounded
No more than two writable task pipelines operate concurrently and merge concurrency is exactly one.

### AC04 — Worktree isolation
Writable tasks use isolated task branches/worktrees; the primary checkout remains the integration/control checkout.

### AC05 — Dry-run is side-effect free
Dry-run reports intended dispatch/agent/model/worktree/cloud/verification/merge actions without launching Codex, mutating state/Git, pushing, or invoking AWS.

### AC06 — Codex execution is testable without quota
A fake runner exercises orchestration behavior without invoking Codex.

### AC07 — Verification failure enters bounded repair
Failed deterministic verification cannot reach review/merge; diagnostics return to Builder until bounded retry exhausts.

### AC08 — Independent review is mandatory
Builder changes cannot enter merge queue before verification + independent read-only Reviewer pass.

### AC09 — Integration is serialized and reverified on latest main
Latest main is synchronized, only one integration runs, no force-push occurs, and combined state must pass verification.

### AC10 — Completion is authoritative-main based
Local success/commit is not completion. Completion bookkeeping and verified result must be present on authoritative main.

### AC11 — Retry exhaustion fails safely
Exhausted Builder/Reviewer/conflict loops become persistent inspectable Human Required/Blocked states.

### AC12 — Dashboard shows system state
Dashboard exposes progress, lanes, task detail, logs/timeline and blockers without becoming a second scheduler.

### AC13 — Dashboard and CLI share control state
Status/Stop/Resume observe one persisted state model.

### AC14 — Stop rejects fresh dispatch immediately
No fresh Ready task may be claimed after Stop acceptance.

### AC15 — Stop drains already-active tasks
All tasks Active at stop-request time may finish their bounded lifecycle to a safe terminal state before `STOPPED`.

### AC16 — Stop does not consume newly unlocked work
Tasks becoming Ready during drain remain undispatched.

### AC17 — Stop survives restart
Persisted drain intent is recovered and fresh work remains blocked until explicit resume/start.

### AC18 — UI remains local-only
Non-loopback dashboard binding is rejected by default/V1 implementation.

### AC19 — Cloud execution remains fail-closed
Real AWS execution requires canonical permission plus explicit runtime authorization where required.

### AC20 — Repository harness and Orchestrator tests pass
Tests cover DAG validation, scheduler/exclusive resources, resumability, fake-agent execution, verification/review fix loops, Git worktree/integration primitives, graceful Stop/restart, dashboard control, cloud fail-closed behavior, and completion bookkeeping. `python3 scripts/harness_check.py` must pass before authoritative completion.

## Architecture impact

This is local engineering/harness tooling, not a CommerceOS bounded context or deployment unit. Implementation responsibilities remain separated across backlog validation, scheduler, run-state store, workspace manager, agent runner, verification, integration coordinator, control service/read model, and dashboard. No AWS service, business module, domain contract, persistence ownership, or tenant model changes.

The implementation uses Python standard-library local tooling and SQLite. This is consistent with the repository's existing Python harness/launcher and does not require an ADR because it does not change the CommerceOS application runtime/deployment architecture.

## Security and tenant impact

No merchant/Tenant data is introduced. The dashboard is local-loopback only and V1 rejects non-loopback binding. Real Codex Builder write scope is the task worktree; Reviewer runs read-only. No credentials are written to repository state. Cloud execution is explicitly fail-closed.

## Reliability and idempotency impact

SQLite persists control/run state. Claiming is transactional within the one-process V1 model. Stop intent and drain membership survive restart. Worktree creation/reuse and cleanup are idempotent-oriented. Merge is serialized; remote-main races fail rather than force-push. Human-required local runs are not auto-redispatched until explicit retry/resume.

## Observability impact

Local event/state records plus per-agent/per-verification logs make task attempts, lane, blockers, merge status, and stop behavior inspectable through CLI/dashboard without attaching a debugger. No CloudWatch or remote telemetry is introduced.

## Cost impact

- New AWS services/resources: none.
- Expected AWS runtime cost for Orchestrator: `$0`.
- Automated tests use fake agents and consume no Codex quota.
- Real Codex runs consume the user's configured Codex allowance/API usage according to their environment; the tool does not alter account/subscription settings.

## Test plan

- Python unit tests for YAML subset parsing and backlog validation.
- Missing-dependency/cycle/frontier mismatch failure tests.
- Scheduler tests for Ready eligibility, local Human Required suppression, concurrency, and exclusive resources.
- SQLite tests for transactional claim, persisted Stop/drain, and explicit retry reset.
- Fake Builder/verification/Reviewer pipeline tests including bounded fix loops and no-op fail-closed behavior.
- Local Git tests using temporary repository + bare origin for real worktree, commit, merge and non-force push primitives.
- Service-level graceful Stop tests with two active tasks and a third Ready task.
- Restart test proving persisted Stop drains old active work but does not start fresh work.
- Dashboard HTTP tests for status/Stop plus loopback-only binding.
- Full repository `python3 scripts/harness_check.py` before completion.

## Ready-gate result

Final Backlog Planner recheck completed after TASK-0089 froze the canonical Backlog V2 contract and the human approved the dashboard/graceful-Stop revision.

- TASK-0089 Completed.
- Required metadata/execution semantics exist.
- Product/domain/technical baselines do not block this engineering tool.
- Orchestrator itself requires no cloud verification.
- Planner authority remains external.
- Stop semantics are explicit/testable.
- Dashboard is separated from scheduling/merge semantics.

**Ready gate: satisfied. TASK READY.**
