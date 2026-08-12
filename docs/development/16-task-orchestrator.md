# CommerceOS — Local Task Orchestrator

_Last reviewed: 2026-08-11._

## 1. Purpose

The Task Orchestrator is a **local-only execution engine** for the canonical Backlog V2. It automates routine Harness Engineering execution after planning artifacts are repository-backed, and it may invoke a bounded Planning Factory when the Ready frontier is empty.

It does not replace product, domain, technical-architecture, or backlog-planning authority. It consumes repository truth, writes planning results back as repository artifacts, and fails closed when a human decision is required.

```text
canonical backlog / accepted baselines
              ↓
       Task Orchestrator
              ↓
     Ready work exists?
       /             \
     yes              no
      ↓                ↓
 Builder pipeline   Backlog Planner
      ↓                ↓
 authoritative main  architect(s) only if requested
                       ↓
                  Backlog Planner Ready gate
                       ↓
                    Ready task
```

## 2. Implementation shape

The local implementation intentionally stays small:

- Python standard library only for orchestration code;
- SQLite for persisted local run/control state;
- Git branches/worktrees for writable task/planning isolation;
- `codex exec --json` behind centralized runner adapters;
- `python scripts/harness_check.py` through the exact interpreter that launched the Orchestrator as the deterministic verification entrypoint;
- `ThreadingHTTPServer` for the loopback-only monitoring/control dashboard;
- Server-Sent Events (SSE) for one-way live Codex activity streaming;
- no Redis, RabbitMQ, Temporal, Kubernetes, remote worker fleet, network database, or AWS runtime service.

Transient files are written under `.commerceos/orchestrator/<catalog>/` by default and must
remain untracked. CommerceOS and Orchestrator-tooling runs never share state or logs implicitly.

## 3. Executable stage contract

The authoritative workflow contract is `commerceos.orchestrator.stage/v1`, implemented in
`tools/commerceos_orchestrator/stage_contracts.py`. Every accepted state transition passes
through the persisted state store; undeclared transitions fail closed to `HUMAN_REQUIRED` and
produce a rejected-transition event.

| Stage | Actor | Versioned input | Versioned output | Success exit |
| --- | --- | --- | --- | --- |
| `planning` | Backlog Planner | `PlanningInput` | `PlanningOutput` | `PLANNING_COMPLETED` |
| `builder` | Builder | `BuilderInput` | `BuilderOutput` | `PRE_REVIEW_VERIFICATION` |
| `verification` | Verification Runner | `VerificationInput` | `VerificationOutput` | `FIRST_REVIEW` or `RE_REVIEW` |
| `reviewer` | Reviewer | `ReviewerInput` | `ReviewerOutput` | `MERGE_QUEUED` or accepted repair route |
| `repair_builder` | Repair Builder | `RepairBuilderInput` | `RepairBuilderOutput` | `REPAIR_VERIFICATION` |
| `integration` | Orchestrator | `IntegrationInput` | `IntegrationOutput` | `FINALIZING` |
| `finalization` | Orchestrator | `FinalizationInput` | `FinalizationOutput` | `COMPLETED` |

Persisted implementation states distinguish `INITIAL_BUILD`, `PRE_REVIEW_VERIFICATION`,
`FIRST_REVIEW`, `REPAIR_REQUIRED`, `REPAIR_BUILD`, `REPAIR_VERIFICATION`, `RE_REVIEW`,
`MERGE_QUEUED`, `INTEGRATING`, and `FINALIZING`. A review state cannot transition directly back
to initial build. Every timeline transition records the contract version and artifact chain.

## 4. Commands

Run from the repository root:

```bash
python tools/orchestrator.py validate
python tools/orchestrator.py plan
python tools/orchestrator.py dry-run
python tools/orchestrator.py status

python tools/orchestrator.py run
python tools/orchestrator.py start
python tools/orchestrator.py stop
python tools/orchestrator.py resume
python tools/orchestrator.py cleanup
python tools/orchestrator.py ui
```

Commands operate on the `commerceos` catalog by default. Use the tooling catalog explicitly:

```bash
python tools/orchestrator.py --catalog orchestrator validate
python tools/orchestrator.py --catalog orchestrator start
```

Catalog contents are physically separated under `tasks/commerceos/` and
`tasks/orchestrator/`; see `tasks/README.md`.

The former `.commerceos/orchestrator/state.db` and sibling logs are legacy mixed-catalog
diagnostics. They are not automatically deleted or imported; new runs use the selected
catalog-specific directory so historical evidence remains recoverable.

`plan` remains a **mechanical DAG/scheduler preview**; it does not consume an LLM. `dry-run` shows normal dispatchable work and, when none exists, the next dependency-satisfied Outline/Refined planning candidate plus its Sol profile. `run` and `start` execute Ready work first and invoke the Planning Factory only when normal scheduling becomes idle.

`start` runs the scheduler and dashboard together. The dashboard binds to `127.0.0.1:8765` by default. Non-loopback binding is rejected.

## 5. Codex execution profiles

The real agent runners invoke the repository-approved `codex` executable non-interactively. Interactive Codex TUI model/Fast selections are not inherited by autonomous CommerceOS execution.

Human-facing **Standard** maps to Codex's default/non-Fast service tier at the CLI configuration boundary.

```text
Planning roles
  Backlog Planner
  Domain Architect
  Technical Architect
      → gpt-5.6-sol / medium / Standard

Execution roles
  Builder
  Reviewer
  verification-oriented agent work
  Conflict Resolver
      → gpt-5.6-luna / medium / Standard
```

The Orchestrator never enables Fast/priority service implicitly. Planning agents receive no real-cloud authorization. Tests use fake agent/planning runners and consume no Codex quota.

## 6. Live Codex observability

Codex automation uses `codex exec --json` with a streaming `subprocess.Popen` adapter. stdout JSONL is consumed while Codex is running, stderr is drained concurrently, and both are retained in audit logs.

```text
codex exec --json
      ↓
  stdout JSONL ─────→ per-task live JSONL
      ↓                     ↓
 audit log              loopback SSE
                             ↓
                         dashboard
```

Task detail can show live role/model/activity events for Planner, architects, Builder and Reviewer. Agent output is untrusted display data and is rendered through safe text DOM APIs; the dashboard does not inject agent HTML.

## 7. Mechanical scheduling

A task is Builder-dispatchable only when canonical metadata proves:

- maturity is `Ready`;
- lifecycle is `Backlog`;
- every dependency is Completed or a declared completed root;
- gates are empty/satisfied;
- no active task owns an overlapping `exclusive_resources` value;
- Builder concurrency is below the configured V1 limit.

Numeric task order and Markdown detail never imply readiness.

The canonical maximum is two writable implementation pipelines and one serialized merge lane. Local `BLOCKED`/`HUMAN_REQUIRED` task state prevents automatic redispatch until explicit operator resume/retry.

## 8. Planning Factory

Planning is serial and runs only after normal Ready scheduling has no dispatchable work.

The nearest planning candidate is selected deterministically from tasks that are:

- `Outline` or `Refined`;
- lifecycle `Backlog`;
- dependency-satisfied.

Unresolved gates do **not** become satisfied merely because planning starts; they are preserved for the Planner/human decision path.

Every planning cycle enters through **Backlog Planner**:

```text
Backlog Planner — Sol/medium/Standard
       ↓
  ┌────┼───────────────────────────────┐
  │    │                               │
READY  DOMAIN_REFINEMENT_REQUIRED      TECHNICAL_REFINEMENT_REQUIRED
  │    │                               │
  │  Domain Architect                  Technical Architect
  │     Sol/medium/Standard              Sol/medium/Standard
  │    │                               │
  └────┴──────────────→ Backlog Planner ←┘
                         ↓
                    final Ready gate
```

The Planner may also request both architects. Domain reconciliation runs before technical reconciliation when both are needed. If Technical Architect exposes a missing business/domain decision, control returns through Domain Architect and then back to Backlog Planner.

Architects may update only their allowed repository artifacts and **cannot mark the candidate Ready**. The Backlog Planner is the final readiness authority for this automated planning loop.

Recognized Planner outcomes are:

```text
PLANNING_RESULT: READY
PLANNING_RESULT: DOMAIN_REFINEMENT_REQUIRED
PLANNING_RESULT: TECHNICAL_REFINEMENT_REQUIRED
PLANNING_RESULT: DOMAIN_AND_TECHNICAL_REFINEMENT_REQUIRED
PLANNING_RESULT: HUMAN_REQUIRED
```

Planning is bounded. Missing protocol markers, repeated non-convergence, human product/architecture decisions, or unsafe semantic ambiguity become Human Required rather than being guessed.

## 9. Planning artifact integration

Planning roles communicate through the task worktree and repository artifacts, not private cross-agent memory.

Safe planning artifacts are verified and integrated through the latest-main serialized checkout. This integration **does not perform task completion bookkeeping** because refining a task is not completing its implementation.

For `PLANNING_RESULT: READY`, the candidate is accepted only if canonical validation proves it is now:

- maturity `Ready`;
- lifecycle `Backlog`;
- ungated;
- dependency-satisfied;
- represented by a valid detailed Ready spec and consistent canonical `ready_frontier`.

After planning artifacts are pushed to authoritative `main`, the scheduler reloads the DAG and the normal Luna Builder pipeline may claim the newly Ready task.

## 10. Implementation execution and completion

### Builder evidence and verification gate

Before Reviewer dispatch, the Builder emits `BuilderResultManifest/v1` from its final agent
message. The manifest is bound to the exact task ID and commit SHA, maps every task AC exactly
once, inventories every Git-changed file, and declares the trusted `task-verification` command ID.
The Orchestrator validates and stores the normalized manifest under the ignored per-task evidence
directory.

The deterministic Verification Runner executes the Orchestrator-owned command mapping and emits
`VerificationReport/v1` with command results, log artifacts, test totals, and commit binding. A
required failure, required skip, stale commit, missing/duplicate AC, or changed-file mismatch
prevents Reviewer dispatch. Reviewer receives paths to the validated manifest and report; it does
not create evidence or inspect completion bookkeeping.

The manifest may declare additional commands with stable `additional-*` IDs. They are executed
without a shell only when their argv matches the repository allow-list (bounded Python test/script,
`dotnet test`, or npm test/run forms). Every required and accepted additional command receives one
argv/exit/log-bound report row. Test totals are derived from recognized test-runner output; zero
parseable required tests fails closed rather than fabricating a pass.

The automated implementation happy path is:

```text
Ready
  ↓
claim + isolated worktree
  ↓
Builder — Luna/medium/Standard
  ↓
deterministic verification
  ↓
independent Reviewer — Luna/medium/Standard
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

The Orchestrator refuses to complete a task from an empty/no-op Builder diff. An agent claim, local commit, green task branch, or planning result is not authoritative implementation completion.

The manual PR/human-integration workflow documented in `14-codex-multi-agent-and-worktrees.md` remains valid for work not executed by this Orchestrator.

## 11. Graceful Stop

`stop` and the dashboard Stop button share the same persisted control model.

When Stop is accepted:

1. persist stop intent;
2. claim no fresh Ready task or planning candidate;
3. allow work already active at stop request, including the current bounded planning lifecycle, to drain safely;
4. do not dispatch a newly Ready Builder after the drain;
5. transition to `STOPPED` after active drain work reaches a safe terminal state.

Stop is not a hard process kill. A stop request survives process restart.

## 12. Recovery and blockers

Local SQLite state records attempts, execution state, blocker details, worktree/branch, drain intent, and a recent-event timeline.

The tool fails closed to `HUMAN_REQUIRED` for conditions such as:

- product/domain/architecture/security/cost decision required;
- cloud execution not authorized;
- planning protocol/non-convergence failure;
- repeated Builder/Reviewer repair exhaustion;
- unsafe semantic merge conflict;
- dirty/unclassifiable integration checkout;
- invalid canonical backlog;
- missing required external tooling.

Explicit `resume` clears retryable local Blocked/Human Required claims; it does not silently change canonical gates or accepted semantics.

Review findings use the shared contract in `17-review-scope-and-finding-ownership.md`. Builder
findings return to the Builder repair loop. Domain, Technical, and Backlog Planner findings route
first to the Backlog Planner, which owns planning convergence and the final Ready gate; the
Orchestrator must not call an Architect directly. Orchestrator-owned findings go to the
Orchestrator handler, and human-owned findings stop at the human decision gate.

## 13. Cloud safety

The Orchestrator itself creates no AWS resources.

A canonical implementation task marked `cloud_verification: required` is not dispatched for real-cloud execution unless the operator explicitly starts with `--allow-cloud`, in addition to repository gates being satisfied.

Planning roles never receive real-cloud authorization. They may refine cloud tasks and record required account/region/budget/human gates, but they may not spend cloud credit or treat planning as execution consent.

## 14. Dashboard

The dashboard is a thin read/control client over Orchestrator Core. It shows:

- Orchestrator state and progress;
- Ready frontier;
- Builder/Reviewer/merge lane utilization;
- blocker count;
- workflow columns;
- task metadata, branch/worktree, attempts and blockers;
- DAG/progress summary;
- recent state/planning events;
- live Codex activity and retained log metadata;
- graceful Stop and explicit Resume controls.

The browser UI does not calculate readiness, mutate canonical YAML, decide planning/architect routing, resolve dependencies, choose retries, or perform Git integration directly.

## 15. Verification

The repository harness runs the Orchestrator Python test suite before application toolchain checks. Coverage includes:

- YAML/backlog validation and cycles/missing dependencies;
- Ready scheduling and exclusive resources;
- persisted stop state and restart draining;
- Builder verification/fix and Reviewer/fix loops using fake agents;
- cloud fail-closed behavior;
- real local Git worktree/merge primitives against a temporary bare repository;
- loopback-only dashboard status/control, SSE and DOM rendering without dynamic `innerHTML`;
- canonical shard/spec path containment;
- untrusted agent evidence isolation from privileged prompts;
- live JSONL publication and audit retention;
- Sol/Luna profile command construction;
- Planner-first candidate selection and on-demand Domain/Technical routing using fake planning agents;
- completion bookkeeping.

The real Codex executable is intentionally not required by tests.

Reviewer decisions use `ReviewLedger/v1`: exact AC/file coverage, stable structured findings,
and a fail-closed PASS rule. Reviewer processes are read-only and full-suite commands are rejected;
the validated ledger artifact, not free text, drives routing and merge eligibility.
