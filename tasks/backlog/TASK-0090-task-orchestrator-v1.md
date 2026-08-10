# TASK-0090 — Build Task Orchestrator V1

Status: Backlog
Specification maturity: Refined
Execution permission: NO — blocked until TASK-0089 completes and the canonical Backlog V2 task-metadata/DAG contract is approved
Owner: Engineering / Harness
Recommended implementation model: Luna after TASK-0089 resolves the metadata contract; escalate only if implementation exposes a new architecture decision
Created: 2026-08-10
Updated: 2026-08-10
Depends on: TASK-0089

## Goal

Build a deterministic, **local-only CommerceOS Task Orchestrator V1** that consumes the canonical Backlog V2 task graph, computes the current safe Ready frontier, dispatches a small number of Codex agents into isolated Git worktrees, coordinates Builder → verification → Reviewer → fix loops, serializes integration through a single merge lane, resolves routine merge conflicts with AI when safe, and automatically merges verified work into `main` without requiring a human for the normal path.

Human involvement is an **exception path**, not the default merge gate. The Orchestrator must stop only when a task requires a product/domain/architecture/security/cost decision, an unsafe cloud action, an unresolved semantic merge conflict, repeated agent-loop exhaustion, or another condition explicitly requiring judgment.

```text
Planner / canonical task DAG
           ↓
      Orchestrator
           │
     compute Ready frontier
           │
      ┌────┴────┐
      ▼         ▼
 Builder A   Builder B       max 2 writable Builders
      │         │
      ▼         ▼
   Harness   Harness
      │         │
      ▼         ▼
   Reviewer   Reviewer
      │         │
      └────┬────┘
           ▼
      VERIFIED TASKS
           │
           ▼
       MERGE QUEUE             concurrency = 1
           │
     sync latest main
           │
           ▼
       conflict?
       /       \
     no         yes
     │           │
     │     Conflict Resolver AI
     │           │
     └─────┬─────┘
           ▼
   post-integration harness
           │
       pass / fail
        /       \
      pass      unresolved
       │           │
  AUTO MERGE    HUMAN_REQUIRED
       │
       ▼
  push origin/main
       │
       ▼
      DONE
```

## Business context

CommerceOS uses AI-generated planning plus Harness Engineering. Once TASK-0089 establishes a canonical Backlog V2, task dependencies, maturity, role, model class, concurrency constraints, verification expectations, and human gates should be machine-readable enough that routine execution no longer requires the human to manually open Codex threads, review task order, merge branches, or advance the DAG.

The intended operating environment is deliberately narrow:

- one developer machine;
- one CommerceOS repository;
- one Orchestrator process;
- a small number of concurrent agents;
- maximum two writable Builder tasks by default;
- one serialized merge lane;
- no distributed scheduler or remote worker fleet;
- local Git worktrees and local process state;
- GitHub remains the remote repository/system-of-record, but V1 does not require a separate always-on service.

Because there is only one Orchestrator process, V1 does not need distributed locking, leader election, Redis, a message broker, Temporal, Kubernetes, or another orchestration platform.

The orchestration layer must remain intentionally boring and deterministic. AI is used for planning, implementation, review, verification, and bounded conflict resolution; the scheduler itself must not invent dependencies, reinterpret architecture, or decide that an Outline/blocked task is safe to run.

## Planning readiness

Before this task may move to `Ready`, TASK-0089 must define or explicitly approve the machine-readable contract consumed by the Orchestrator, including at minimum:

- canonical task identifier;
- specification maturity / execution eligibility;
- task lifecycle state;
- dependency identifiers;
- execution role (`builder`, `reviewer`, `verification`, or equivalent);
- model class/routing hint without coupling task files to one permanent model name;
- concurrency/exclusive-resource information sufficient to prevent unsafe parallel execution;
- cloud-verification requirement and human/cloud approval gate where relevant;
- task path / authoritative specification location;
- definition of the first Ready frontier;
- completion state semantics used to unlock dependent tasks;
- merge/integration expectations needed to determine when a task is truly `DONE`.

If TASK-0089 chooses a metadata representation different from examples in this task, **TASK-0089 wins**. The Orchestrator consumes the approved contract rather than creating a competing task schema.

## In scope

### 1. Deterministic DAG loader and validator

- read the canonical Backlog V2 metadata produced/approved by TASK-0089;
- validate task IDs and dependency references;
- detect missing dependencies;
- detect dependency cycles;
- reject duplicate canonical task IDs;
- reject execution of tasks whose maturity/execution gate is not Ready;
- produce a clear diagnostic report rather than guessing through invalid metadata.

### 2. Ready-frontier scheduler

Compute eligible work mechanically from repository state and task metadata.

A task may be dispatched only when all required conditions are satisfied:

- task is explicitly execution-ready;
- all dependencies are `DONE`/accepted according to the canonical lifecycle contract;
- no unresolved human/product/architecture/security/cost gate blocks it;
- its concurrency/exclusive-resource constraints do not conflict with currently active work;
- Builder concurrency remains within the project default of **maximum two active writable Builder tasks**.

The scheduler must never infer readiness from numeric task order or from a detailed-looking Markdown body.

Recommended lane limits for V1:

```text
Builder lane    max 2 concurrent writable tasks
Reviewer lane   max 2 concurrent read-only reviews
Merge lane      exactly 1 integration at a time
```

### 3. Local orchestrator state and resumability

Maintain enough local machine state to safely resume after process restart without treating transient runtime state as repository architecture authority.

The implementation must:

- distinguish repository task state from local process/run state;
- prevent the same task from being dispatched twice by one orchestrator instance/run state;
- detect stale/incomplete claims after interruption;
- support inspection/status without changing task state;
- support safe resume/recovery;
- preserve attempt counts and bounded loop counters;
- keep generated local state/logs out of Git unless an explicit evidence artifact belongs in the task.

A simple local filesystem or SQLite-backed state store is sufficient. V1 must not introduce a network database merely for orchestration.

### 4. Worktree and branch isolation

For every writable Builder task:

- create or reuse a task-specific branch/worktree according to `docs/development/14-codex-multi-agent-and-worktrees.md`;
- never let a Builder modify the primary `main` checkout;
- ensure task worktrees cannot silently share declared task-specific mutable local resources;
- detect an existing dirty/conflicting worktree and classify it instead of overwriting work;
- make worktree creation/resume/cleanup idempotent;
- clean/prune finished worktrees only after their task is safely integrated.

### 5. Codex runner abstraction

Provide a runner boundary that launches Codex non-interactively from the task worktree using repository-owned role instructions.

The runner constructs context from authoritative repository artifacts, including:

- `AGENTS.md`;
- the task specification;
- the relevant role document under `docs/agents/`;
- task-selected domain/architecture/ADR references where provided by the canonical task contract.

The implementation must support a **fake/test runner** so tests do not consume Codex usage.

A `--dry-run` mode must show which tasks, roles, worktrees, commands, model classes, merge actions, and gates would be used without launching Codex or modifying Git state.

### 6. Role and model routing

Use canonical task metadata to route routine work.

Expected policy:

- Builder implementation defaults to the project's cheap/default implementation model class (currently Luna policy);
- Reviewer defaults to the normal review model class;
- routine merge-conflict resolution defaults to the normal implementation/review class;
- strong reasoning is used only when metadata or an escalation rule explicitly requires it;
- model routing configuration is centralized so task files do not need rewriting when model names change.

The Orchestrator must not escalate model strength merely because a task is large.

### 7. Builder execution and harness verification

After a Builder run:

- capture a structured run result/status;
- run repository verification required by the task/DoD;
- if deterministic verification fails, transition to a bounded fix loop instead of advancing to review;
- preserve diagnostics for the subsequent Builder fix run;
- never weaken tests or guardrails automatically to obtain a green result.

### 8. Independent review and bounded fix loop

After a Builder produces a locally verified diff:

- Reviewer receives the task + accepted domain/architecture context + diff/worktree state;
- Reviewer must not modify the Builder branch;
- actionable findings are persisted in inspectable run state/artifacts;
- blocking findings transition the task to `FIX_REQUIRED` and redispatch the Builder with those findings;
- a clean review advances the task to `VERIFIED` / `MERGE_QUEUED`;
- automatic Builder ↔ Reviewer iteration is bounded;
- after the configured retry/iteration limit, transition to `HUMAN_REQUIRED` rather than looping forever.

### 9. Serialized automatic merge queue

Verified tasks enter a **single serialized merge queue**. Only one task may integrate with `main` at a time, even when multiple Builders/Reviewers run concurrently.

For each queued task, the merge worker must:

1. acquire the local merge-lane lock;
2. fetch/sync the latest `origin/main` and verify the local integration checkout is in a known-clean state;
3. integrate the task branch against the latest `main` using the chosen repository Git strategy;
4. if integration is clean, run post-integration verification before finalizing the merge;
5. if Git reports a conflict, enter the conflict-resolution flow below;
6. if verification passes, automatically merge the task into `main`;
7. push `main` to `origin/main` without force-push;
8. mark the task `DONE` only after the authoritative `main` contains the verified task result;
9. unlock newly eligible dependent tasks;
10. clean the finished task worktree when safe.

The merge lane must never integrate two task branches concurrently.

If `origin/main` moved unexpectedly, the merge worker must resync/re-evaluate rather than force-push or overwrite remote history.

### 10. AI-assisted conflict resolution

A merge conflict is **not automatically a human gate**.

Conflict handling uses an escalation ladder:

```text
Git integration attempt
        ↓
clean? ── yes ──► post-integration verification
  │
  no
  ↓
Conflict Resolver Agent
  │
  ├─ preserve both accepted task outcomes where compatible
  ├─ do not invent product/domain/architecture decisions
  ├─ run focused tests + harness
  └─ produce structured resolution result
        ↓
resolved safely?
   ├─ yes ──► post-integration verification ──► auto merge
   └─ no
        ↓
HUMAN_REQUIRED / ARCHITECTURE_DECISION_REQUIRED
```

The Conflict Resolver Agent may handle routine textual/structural integration such as compatible changes to registration/composition files, dependency manifests, documentation indexes, generated project membership, or nearby implementation edits.

It must **not silently choose** between incompatible accepted contracts, domain invariants, state models, persistence ownership rules, security boundaries, or architecture decisions.

A Git-clean merge is still subject to post-integration tests because semantic conflicts can exist without textual conflict.

### 11. Post-integration semantic verification

Every candidate automatic merge must be verified against the latest `main` state after integration/rebase/merge preparation.

At minimum:

- run the relevant task verification/harness;
- run architecture/contract/integration tests implicated by the changed surfaces;
- reject the merge if the combined state violates accepted invariants even when Git reported no textual conflict;
- use bounded automated repair attempts for clearly implementation-level failures;
- escalate when repair requires a new decision or the retry limit is exhausted.

`git merge` success alone is never sufficient evidence for `DONE`.

### 12. Automatic merge and push policy

The **happy path is fully automatic**:

```text
READY
  ↓
Builder
  ↓
Harness
  ↓
Reviewer PASS
  ↓
MERGE_QUEUED
  ↓
sync latest main
  ↓
integrate
  ↓
post-integration harness PASS
  ↓
AUTO MERGE
  ↓
push origin/main
  ↓
DONE
  ↓
unlock next Ready frontier
```

V1 must not require a human to approve routine merges that satisfy every configured gate.

The Orchestrator must never:

- force-push `main`;
- bypass required deterministic verification;
- mark a task `DONE` merely because an agent claimed success;
- overwrite newer remote `main` history;
- resolve an accepted architecture/product/security decision by choosing arbitrarily.

### 13. Human/strong-reasoning escalation gates

Stop the affected lane and surface a structured blocker when any of these occur:

- `BUSINESS_DECISION_REQUIRED`;
- `DOMAIN_DECISION_REQUIRED`;
- `ARCHITECTURE_DECISION_REQUIRED`;
- `SECURITY_DECISION_REQUIRED`;
- `COST_APPROVAL_REQUIRED`;
- material AWS/cloud execution not already approved by task metadata;
- merge conflict whose safe resolution requires changing an accepted contract/invariant/architecture decision;
- semantic integration failure that remains after bounded AI repair attempts;
- dependency/backlog conflict the deterministic scheduler cannot resolve;
- repeated Builder/Reviewer/Conflict-Resolver loop exhaustion;
- dirty/manual Git state that cannot be safely classified or recovered.

Independent Ready lanes may continue when metadata proves they are unaffected.

A strong-reasoning Architect/Reviewer agent may be used to **analyze** an escalation and propose a resolution. If that proposal changes a product/domain/architecture/security/cost decision requiring human authority, the system remains blocked until the decision is explicitly approved.

### 14. Cloud and cost safety

Default orchestration is local and the Orchestrator itself must cost `$0` in AWS usage.

If a future Ready task requires real AWS verification:

- enter that path only if canonical task metadata explicitly requires cloud verification;
- require the approved cloud-execution policy/gate from TASK-0089/repository governance;
- respect Free Tier/credit guardrails and teardown requirements;
- never launch an unapproved cloud load test, crawler run, or long-lived preview deployment merely to keep the automation loop moving.

### 15. CLI / developer experience

Expose an ergonomic repository-local interface, preferably through existing CommerceOS tooling or a documented adjacent command.

The final command surface must support capabilities equivalent to:

```text
status       inspect DAG, active runs, blockers, merge queue, and Ready frontier
validate     validate canonical task metadata/DAG
plan         show tasks that would dispatch next
dry-run      simulate Builder/Review/Merge actions with no side effects
run          run the automated scheduler until completion or a blocking gate
resume       recover safely after interruption
cleanup      inspect/remove finished task worktrees safely
```

`run` should continue advancing the DAG automatically rather than requiring one invocation per task.

## Out of scope

- changing product/domain architecture;
- generating canonical Backlog V2 (owned by TASK-0089);
- replacing Backlog Planner reasoning with scheduler logic;
- using an LLM to decide DAG eligibility/dependency satisfaction;
- more than two concurrent writable Builders by default;
- multiple concurrent merge workers;
- distributed orchestration, leader election, remote worker fleets, or multi-host state;
- Kubernetes, Temporal, Redis, RabbitMQ, database servers, or always-on orchestration infrastructure;
- a web UI or separate server-hosted orchestration service;
- autonomous approval of product/domain/architecture/security/cost changes;
- force-pushing or rewriting `main` history;
- automatically spending AWS credit without the repository-approved cloud gate;
- changing ChatGPT/Codex product-level quota or account configuration.

## Acceptance criteria

### AC01 — Invalid DAG fails closed

Given canonical task metadata contains a missing dependency, duplicate task ID, dependency cycle, or invalid execution state
when the Orchestrator validates the graph
then it exits non-zero with actionable diagnostics and dispatches no Codex task.

### AC02 — Ready frontier is computed mechanically

Given a valid DAG containing Ready, Outline, blocked, active, and completed tasks
when `plan`/equivalent runs
then only tasks whose dependencies and gates are satisfied appear in the dispatchable Ready frontier.

### AC03 — Parallel build/review is bounded

Given more than two tasks are otherwise Ready
when the Orchestrator runs
then at most two writable Builder tasks are active concurrently, declared exclusive-resource conflicts prevent unsafe co-scheduling, and review concurrency remains bounded.

### AC04 — Worktree isolation

Given two independent Ready Builder tasks are dispatched
when execution starts
then each uses its own task branch/worktree and neither writes feature code in the primary `main` checkout.

### AC05 — Dry-run is side-effect free

Given valid Ready tasks
when dry-run executes
then the Orchestrator reports intended task/role/model/worktree/merge/gate actions without launching Codex, creating worktrees, mutating task state, merging Git history, pushing `main`, or invoking AWS.

### AC06 — Codex is testable without Codex usage

Given repository tests run
when Orchestrator behavior is tested
then fake runners simulate Builder success/failure, blocker, Reviewer findings, conflict resolution, merge verification, and interruption without calling real Codex.

### AC07 — Failed harness returns to bounded fix flow

Given a Builder completes but required local verification fails
when the Orchestrator evaluates the result
then the task does not advance to review/merge and receives actionable failure context for bounded automated repair.

### AC08 — Independent review is enforced

Given a Builder diff passes required local verification
when the task enters review
then review runs with independent Reviewer instructions/context and does not modify the Builder branch.

### AC09 — Review loop cannot run forever

Given Reviewer reports blocking findings repeatedly
when the configured automatic fix/review limit is exhausted
then the task transitions to a structured human/escalation state instead of looping forever.

### AC10 — Merge lane is serialized

Given multiple tasks become `VERIFIED`
when they enter the merge queue
then exactly one task integrates with `main` at a time and every later merge candidate is evaluated against the `main` produced by earlier successful integrations.

### AC11 — Routine merge is fully automatic

Given a task passes Builder verification and independent review
when its merge-queue turn starts and it integrates cleanly with latest `main`
and post-integration verification passes
then the Orchestrator merges it into `main`, pushes `origin/main` without force, marks it `DONE`, and unlocks dependent Ready tasks without human intervention.

### AC12 — Routine textual conflict can be resolved by AI

Given a verified task conflicts textually with newer `main`
when the conflict does not require a new product/domain/architecture/security/cost decision
then the Conflict Resolver Agent may resolve the conflict, run required verification, and allow the automatic merge path to continue.

### AC13 — Decision conflicts escalate instead of guessing

Given merge/integration exposes incompatible accepted contracts, invariants, architecture decisions, or security rules
when AI cannot reconcile them without choosing a new decision
then the task transitions to the appropriate structured human/architectural blocker and `main` is not modified by that candidate merge.

### AC14 — Semantic conflicts are caught after clean Git integration

Given Git integration succeeds without textual conflict but the combined code violates a contract, architecture rule, invariant, or test
when post-integration verification runs
then the merge is rejected and enters bounded repair/escalation rather than being marked `DONE`.

### AC15 — Interrupted runs are resumable

Given the Orchestrator stops while tasks are claimed, reviewing, resolving conflicts, or queued for merge
when `status`/`resume` executes later
then it detects actual branch/worktree/main/run state, avoids duplicate dispatch/merge, and resumes or reports a safe recovery action.

### AC16 — Remote main is never overwritten

Given `origin/main` advances while a task waits in the merge queue
when that task reaches integration
then the Orchestrator resynchronizes and re-verifies against the new head; it never force-pushes or discards remote commits.

### AC17 — Human decision gates fail closed

Given a task requires a business, domain, architecture, security, cost, or otherwise unapproved decision
when encountered
then the affected lane transitions to an explicit blocker and is not automatically continued merely to keep the DAG moving.

### AC18 — AWS remains governed

Given a task does not explicitly require approved cloud verification
when orchestration runs
then no AWS deployment/API execution is initiated by the Orchestrator.

Given a task does require cloud verification
when the repository-approved cloud gate is not satisfied
then the task stops at that gate rather than spending AWS credit.

### AC19 — Repository harness covers orchestrator invariants

Given TASK-0090 is implemented
when `python3 scripts/harness_check.py` runs
then Orchestrator tests/validation cover DAG calculation, bounded concurrency, worktree isolation, review loops, merge serialization, AI conflict escalation, auto-merge safety, resume behavior, and human gates.

## Architecture impact

- Owning domain: Engineering / Repository Harness
- Domains touched: all implementation domains indirectly through task dispatch/integration only
- Persistence impact: no business persistence; local orchestration state only
- Events/contracts impact: consumes canonical machine-readable task contract defined/approved by TASK-0089
- AWS/IaC impact: none required for the Orchestrator itself
- Git impact: Orchestrator becomes the normal automated integration path for eligible task branches; `main` writes are serialized through one merge lane
- ADR required? No by default — repository-local engineering tooling. Create an ADR only if implementation proposes a persistent service, distributed workers, external orchestrator, paid infrastructure, or another material architecture change.

## Security and tenant impact

- Authentication: no CommerceOS runtime authentication change
- Authorization: no tenant runtime authorization change
- Tenant scoping: Orchestrator must not weaken task-level tenant/security requirements
- Sensitive data/secrets: never embed AWS credentials, GitHub tokens, Codex secrets, or model credentials in task files/logs; inherit approved local/CLI authentication mechanisms
- Git safety: never force-push `main`; never auto-resolve a security/authority conflict by silently weakening the stricter rule
- Abuse/rate-limit considerations: bounded agent concurrency and bounded fix/review/conflict loops protect Codex usage; cloud execution remains governed

## Reliability and idempotency impact

- Retry behavior: Builder, Reviewer, conflict-resolution, integration, and push retries are bounded and resumable
- Timeout semantics: runner timeout/interruption is not proof that worktree/main was unchanged; recovery inspects actual Git/run state
- Duplicate-delivery behavior: duplicate task dispatch and duplicate merge of the same canonical task must be prevented by local state + Git state inspection
- Idempotency key/strategy: canonical task ID + run/claim identity; merged commit/task provenance must make completed integration detectable after restart
- Merge atomicity: one local merge lane serializes `main` integration; task is not `DONE` until authoritative `main` contains the verified result
- Remote divergence: non-fast-forward push never triggers force; resync and re-evaluate instead
- DLQ/recovery/reconciliation: no queue/DLQ in V1; explicit stale-claim/run/merge recovery is required

## Observability impact

- Logs: structured local logs include task ID, run ID, role, lifecycle transition, worktree/branch, merge-queue position, conflict category, integration commit, push result, and blocker category without secrets
- Metrics: no external metrics required in V1
- Traces/correlation: task ID/run ID are sufficient local correlation keys
- Audit: preserve an inspectable execution history for Builder results, verification, Reviewer findings, conflict resolution, merge verification, final merge/push, and escalations
- Operational states/errors: every stop/failure maps to an explicit inspectable lifecycle/blocker state rather than disappearing into agent prose

## Cost impact

- Request/compute impact: local CPU/process usage plus Codex usage for dispatched agents
- Storage impact: local worktrees/log/state/audit files and normal Git history
- Network impact: Git/Codex network usage; GitHub push traffic
- New AWS resources/services: none
- Free Tier allowance relevant to this task: Orchestrator implementation/CI should require no AWS use
- Expected monthly AWS cost change or `negligible` with rationale: `$0` expected from the Orchestrator itself
- Estimated one-off cloud-test/load-test cost, if any: none for Orchestrator verification
- Codex usage guardrail: default maximum two writable Builders plus bounded review/fix/conflict attempts

## Test plan

- Unit: DAG parsing/validation, cycle/missing-dependency detection, Ready-frontier calculation, lifecycle transitions, concurrency/exclusive-resource scheduling, merge-queue serialization, model/role routing, retry limits, human/cloud gates, stale-run recovery
- Integration: temporary Git repository/worktree lifecycle using fake Codex runners; simulate two parallel Builders, concurrent reviews, serialized merge, remote-main advancement, textual conflict resolution, semantic verification failure, retry exhaustion, interruption/resume, cleanup
- Architecture: scheduler remains deterministic and contains no business-domain decision logic; AI runner, Git integration, conflict resolver, state store, and remote push sit behind testable boundaries
- Contract: canonical TASK-0089 metadata schema fixtures validate backward/forward failure behavior
- IaC: N/A
- E2E/manual: on a synthetic task DAG, run dry-run then a fake lifecycle from Ready → Builder → harness → Reviewer → MERGE_QUEUED → integrate → verify → AUTO_MERGE → DONE; also exercise one conflict that AI resolves and one decision conflict that stops at `HUMAN_REQUIRED`
- **Cloud verification required?** No — Orchestrator V1 is local engineering tooling and AWS execution must remain mocked/gated
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

## Implementation notes

Keep V1 deliberately small and local. A single Python process integrated with existing repository tooling is preferred over an agent framework or distributed orchestration platform.

Recommended internal separation (names illustrative, not mandatory):

```text
tools/orchestrator/
  task_graph.py       parse/validate canonical task metadata
  scheduler.py        deterministic Ready-frontier/concurrency logic
  state.py            local claim/run lifecycle and resume
  worktrees.py        Git branch/worktree operations
  runner.py           Codex/fake Builder/Reviewer runner boundary
  review.py           review/fix transition handling
  conflicts.py        AI conflict-resolution boundary and escalation
  merge_queue.py      serialized latest-main integration/verification/push
  audit.py            structured local execution history
  cli.py              status/validate/plan/run/resume/cleanup
```

Suggested lifecycle:

```text
OUTLINE
  ↓ Planner
READY
  ↓ Orchestrator claim
RUNNING
  ↓ Builder + harness
REVIEW
  ├─ FIX_REQUIRED ──► RUNNING
  └─ VERIFIED
        ↓
   MERGE_QUEUED
        ↓
   INTEGRATING
      ├─ CONFLICT_RESOLUTION ──► INTEGRATING
      ├─ HUMAN_REQUIRED
      └─ MERGE_VERIFY
              ├─ repair/retry ──► INTEGRATING
              ├─ HUMAN_REQUIRED
              └─ MERGED
                    ↓ push origin/main
                   DONE
```

The Orchestrator should be deterministic enough that scheduler and merge-queue behavior are exhaustively testable without an LLM. AI should enter only at well-defined worker/review/conflict-resolution boundaries.

## Completion summary

Fill before moving to `tasks/completed/`.

### What changed

- ...

### Verification

- `python3 scripts/harness_check.py`: PASS/FAIL
- Orchestrator unit/integration tests:
- dry-run evidence:
- fake auto-merge/conflict-resolution evidence:
- real Codex smoke run: PASS/FAIL/N/A
- cloud verification: N/A

### Acceptance criteria status

- AC01: PASS/FAIL
- AC02: PASS/FAIL
- AC03: PASS/FAIL
- AC04: PASS/FAIL
- AC05: PASS/FAIL
- AC06: PASS/FAIL
- AC07: PASS/FAIL
- AC08: PASS/FAIL
- AC09: PASS/FAIL
- AC10: PASS/FAIL
- AC11: PASS/FAIL
- AC12: PASS/FAIL
- AC13: PASS/FAIL
- AC14: PASS/FAIL
- AC15: PASS/FAIL
- AC16: PASS/FAIL
- AC17: PASS/FAIL
- AC18: PASS/FAIL
- AC19: PASS/FAIL
