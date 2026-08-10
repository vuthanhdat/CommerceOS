# CommerceOS — Local Task Orchestrator

_Last reviewed: 2026-08-10._

## 1. Purpose

The Task Orchestrator is a **local-only execution engine** for the canonical Backlog V2. It automates routine Harness Engineering execution after the Backlog Planner has already marked work `Ready`.

It does not replace product, domain, technical-architecture, or backlog-planning authority. It consumes repository truth and fails closed when that truth is incomplete.

```text
Domain / Technical / Backlog planning
              ↓
       canonical Ready DAG
              ↓
      Task Orchestrator
              ↓
 Builder → verify → Reviewer → merge
              ↓
       authoritative main
```

## 2. Implementation shape

V1 is intentionally small and local:

- Python standard library only for orchestration code;
- SQLite for persisted local run/control state;
- Git branches/worktrees for writable task isolation;
- `codex exec` behind a centralized agent-runner adapter;
- `python3 scripts/harness_check.py` as the default deterministic verification command;
- `ThreadingHTTPServer` for the loopback-only monitoring/control dashboard;
- no Redis, RabbitMQ, Temporal, Kubernetes, remote worker fleet, network database, or AWS runtime service.

Transient files are written under `.commerceos/orchestrator/` by default and must remain untracked.

## 3. Commands

Run from the repository root:

```bash
python3 tools/orchestrator.py validate
python3 tools/orchestrator.py plan
python3 tools/orchestrator.py dry-run
python3 tools/orchestrator.py status

python3 tools/orchestrator.py run
python3 tools/orchestrator.py start
python3 tools/orchestrator.py stop
python3 tools/orchestrator.py resume
python3 tools/orchestrator.py cleanup
python3 tools/orchestrator.py ui
```

`start` runs the scheduler and dashboard together. The dashboard binds to `127.0.0.1:8765` by default. V1 rejects non-loopback dashboard binding.

## 4. Codex runner

The real agent runner invokes the repository-approved `codex` executable non-interactively through a single adapter. Only model routing is configurable; the executable itself is intentionally fixed so environment input cannot redirect privileged task execution to an arbitrary program:

```text
COMMERCEOS_CODEX_MODEL_DEFAULT
COMMERCEOS_CODEX_MODEL_STRONG
```

If model variables are absent, Codex's configured default model is used. Tests use `FakeAgentRunner` and consume no Codex quota. Deterministic verification also uses the fixed repository-owned `python3 scripts/harness_check.py` entrypoint rather than an environment-supplied command.

The Builder gets a writable task worktree. Reviewer runs read-only and must return an explicit pass/fix marker. Conflict Resolver is writable only in the serialized integration checkout and must return a safe-resolution marker; otherwise the task becomes Human Required. Builder/reviewer/conflict evidence is treated as untrusted data: diffs/conflict contents are inspected from Git rather than interpolated into privileged prompts, and prior verification/review feedback is written to ignored local evidence files that cannot override repository instructions.

## 5. Mechanical scheduling

A task is eligible only when canonical metadata proves:

- maturity is `Ready`;
- lifecycle is `Backlog`;
- every dependency is Completed or a declared completed root;
- gates are empty/satisfied;
- no active task owns an overlapping `exclusive_resources` value;
- Builder concurrency is below the V1 limit.

Numeric task order and Markdown detail never imply readiness.

The canonical maximum is two writable task pipelines and one serialized merge lane. Local `BLOCKED`/`HUMAN_REQUIRED` task state prevents automatic redispatch until explicit operator resume/retry.

## 6. Execution and completion

The automated happy path is:

```text
Ready
  ↓
claim + isolated worktree
  ↓
Builder
  ↓
deterministic verification
  ↓
independent Reviewer
  ↓
serialized merge queue
  ↓
sync latest origin/main
  ↓
merge / bounded safe conflict resolution
  ↓
post-integration verification
  ↓
completion bookkeeping
  ↓
verification again
  ↓
non-force push origin/main
  ↓
Completed
```

The Orchestrator refuses to complete a task from an empty/no-op Builder diff. An agent claim, local commit, or green task branch is not authoritative completion.

The manual PR/human-integration workflow documented in `14-codex-multi-agent-and-worktrees.md` remains valid for work not executed by this Orchestrator. Automated merge is an Orchestrator-controlled exception with stricter deterministic and independent-review gates; it does not grant a normal Builder permission to merge its own task.

## 7. Graceful Stop

`stop` and the dashboard Stop button share the same persisted control model.

When Stop is accepted:

1. persist stop intent;
2. claim no fresh Ready task;
3. mark every task already active as part of the drain set;
4. allow those task(s) to finish their bounded Builder/fix/verification/review/integration lifecycle;
5. do not dispatch tasks that become Ready during draining;
6. transition to `STOPPED` after all drain-set tasks reach Completed, Blocked, or Human Required.

If two Builders are active, both drain. Stop is not a hard process kill.

A stop request survives process restart. Starting without an explicit resume cannot silently consume newly Ready work while a persisted drain is in progress.

## 8. Recovery and blockers

Local SQLite state records attempts, execution state, blocker details, worktree/branch, drain intent, and a recent-event timeline.

Recoverable pre-merge states conservatively rerun deterministic Builder/verification work in the existing worktree. `MERGE_QUEUED`/`INTEGRATING` states return to the merge lane instead of being reset to fresh queued work.

The tool fails closed to `HUMAN_REQUIRED` for conditions such as:

- product/domain/architecture/security/cost decision required;
- cloud execution not authorized;
- repeated Builder/Reviewer repair exhaustion;
- unsafe semantic merge conflict;
- dirty/unclassifiable integration checkout;
- invalid canonical backlog;
- missing required external tooling.

Independent Ready lanes may continue when their metadata/resources prove independence.

## 9. Cloud safety

The Orchestrator itself creates no AWS resources.

A canonical task marked `cloud_verification: required` is not dispatched for real-cloud execution unless the operator explicitly starts the Orchestrator with `--allow-cloud`, in addition to the task's repository gates already being satisfied.

For `conditional` cloud verification, the task may proceed through local implementation while the Builder is explicitly told that real AWS execution is not authorized unless the run has `--allow-cloud`. The Orchestrator never infers permission to spend cloud credit.

## 10. Dashboard

The dashboard is a thin read/control client over Orchestrator Core. It shows:

- Orchestrator state and progress;
- Ready frontier;
- Builder/Reviewer/merge lane utilization;
- blocker count;
- workflow columns;
- task metadata, branch/worktree, attempts and blockers;
- DAG/progress summary;
- recent event timeline;
- graceful Stop and explicit Resume controls.

The browser UI does not calculate readiness, mutate canonical YAML, resolve dependencies, decide retries, or perform Git integration directly.

## 11. Verification

The repository harness runs the Orchestrator Python test suite before the application toolchain checks. The suite covers at least:

- YAML/backlog validation and cycles/missing dependencies;
- Ready scheduling and exclusive resources;
- persisted stop state and restart draining;
- Builder verification/fix and Reviewer/fix loops using fake agents;
- cloud fail-closed behavior;
- real local Git worktree/merge primitives against a temporary bare repository;
- loopback-only dashboard status/control and DOM rendering without dynamic `innerHTML`;
- canonical shard/spec path containment so backlog metadata cannot escape repository-owned planning directories;
- untrusted agent evidence isolation from privileged prompts;
- completion bookkeeping.

The real Codex executable is intentionally not required by tests.
