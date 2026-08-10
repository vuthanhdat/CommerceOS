# TASK-0090 — Build Task Orchestrator V1

Status: Backlog
Specification maturity: Refined
Execution permission: NO — blocked until TASK-0089 completes and the canonical Backlog V2 task-metadata/DAG contract is approved
Owner: Engineering / Harness
Recommended implementation model: Luna after TASK-0089 resolves the metadata contract; escalate only if the implementation exposes a new architecture decision
Created: 2026-08-10
Depends on: TASK-0089

## Goal

Build a deterministic local **CommerceOS Task Orchestrator V1** that consumes the canonical Backlog V2 task graph, finds the current safe Ready frontier, claims and dispatches eligible tasks into isolated Git worktrees, launches the correct Codex role non-interactively, runs repository verification, coordinates review/fix transitions, and stops at explicit human decision/merge gates.

The Orchestrator must remove the need for a human to manually inspect the backlog and start each routine Builder task while preserving human control over product/architecture decisions, material cloud/cost decisions, and final merge approval.

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
      └────┬────┘
           ▼
        Review
           │
    ┌──────┴──────┐
    ▼             ▼
Fix required   Verified
    │             │
    └── Builder   ▼
              MERGE_READY
                   │
                Human gate
```

## Business context

CommerceOS uses AI-generated planning plus Harness Engineering. Once TASK-0089 establishes a canonical Backlog V2, task dependencies, maturity, role, model class, concurrency constraints, and verification expectations should be machine-readable enough that routine dispatch no longer requires the human to open Codex threads one by one.

The orchestration layer must remain intentionally boring and deterministic. AI is used for planning, implementation, review, and verification; the scheduler itself must not invent dependencies, reinterpret architecture, or decide that an Outline/blocked task is safe to run.

The repository remains the system of record. Private conversation between agents is not required for coordination.

## Planning readiness

Before this task may move to `Ready`, TASK-0089 must define or explicitly approve the machine-readable contract needed by the Orchestrator, including at minimum:

- canonical task identifier;
- specification maturity / execution eligibility;
- task lifecycle state;
- dependency identifiers;
- execution role (`builder`, `reviewer`, `verification`, or equivalent);
- model class/routing hint without coupling task files to one permanent model name;
- concurrency/exclusive-resource information sufficient to prevent unsafe parallel execution;
- cloud-verification requirement and human/cloud approval gate where relevant;
- task path / authoritative specification location;
- definition of the first Ready frontier.

If TASK-0089 chooses a metadata representation different from examples in this task, **TASK-0089 wins**. The Orchestrator must consume the approved contract rather than create a competing task schema.

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

A task may be dispatched only when all required conditions are satisfied, including:

- task is explicitly execution-ready;
- all dependencies are completed/accepted according to the approved lifecycle contract;
- no unresolved human/product/architecture/security/cost gate blocks it;
- its concurrency/exclusive-resource constraints do not conflict with currently active work;
- Builder concurrency remains within the project default of **maximum two active writable Builder tasks**.

The scheduler must never infer readiness from numeric task order or from a detailed-looking Markdown body.

### 3. Local orchestrator state and resumability

Maintain enough local machine state to safely resume after process restart without treating transient runtime state as repository architecture authority.

The implementation must:

- distinguish repository task state from local process/run state;
- prevent the same task from being dispatched twice by one orchestrator instance/run state;
- detect stale/incomplete claims after interruption;
- support inspection/status without changing task state;
- support a safe resume/recovery path;
- keep generated local state/logs out of Git unless an explicit evidence artifact belongs in the task/PR.

The exact local state format is an implementation detail, but it must be deterministic, inspectable, and tested.

### 4. Worktree and branch isolation

For every writable Builder task:

- create or reuse a task-specific branch/worktree according to `docs/development/14-codex-multi-agent-and-worktrees.md`;
- never let a Builder modify the primary `main` checkout;
- ensure task worktrees cannot silently share task-specific mutable local resources when the approved metadata/config identifies an isolation requirement;
- detect an existing dirty/conflicting worktree and stop with a useful diagnostic instead of overwriting work;
- provide cleanup/prune support after a task is finished and merged by the human workflow.

### 5. Codex runner abstraction

Provide a runner boundary that can launch Codex non-interactively from the task worktree using repository-owned role instructions.

The runner must construct context from authoritative repository artifacts, including:

- `AGENTS.md`;
- the task specification;
- the relevant role document under `docs/agents/`;
- task-selected domain/architecture/ADR references where provided by the canonical task contract.

The implementation must support a **fake/test runner** so CI and unit tests do not consume Codex usage.

A `--dry-run` mode must show which tasks, roles, worktrees, commands, model classes, and gates would be used without launching Codex or modifying Git state.

### 6. Role and model routing

Use the canonical task metadata to route routine work to the correct role.

Expected policy:

- Builder implementation defaults to the project's cheap/default implementation model class (currently Luna policy);
- Reviewer defaults to the normal review model class;
- strong reasoning is used only when the task metadata/approved gate explicitly requires it;
- model routing configuration is centralized so task files do not need to be rewritten when model names change.

The Orchestrator must not escalate model strength merely because a task is large.

### 7. Builder execution and harness verification

After a Builder run:

- capture a structured run result/status;
- run the repository verification command required by the task/DoD;
- if local deterministic verification fails, classify the task as needing fix rather than advancing it to review;
- preserve diagnostics sufficient for the subsequent Builder fix run;
- never weaken tests/guardrails automatically to obtain a green result.

The Orchestrator itself must not perform an AWS deployment merely because a task exists.

### 8. Review and fix loop

V1 must support an independent review transition after a Builder produces a verifiable diff.

- Reviewer receives task + accepted domain/architecture context + diff/worktree state;
- Reviewer must not modify the Builder branch;
- actionable findings are persisted in an inspectable run artifact/state;
- tasks with blocking findings transition to a fix-required state and may be redispatched to the Builder with the findings;
- a clean review advances the task to a verified/merge-ready state according to the approved lifecycle contract;
- prevent infinite automatic fix/review loops with a bounded retry/iteration limit and escalate to human when exceeded.

A detached/read-only review worktree may be used where local review execution requires repository checkout isolation.

### 9. Human gates

The Orchestrator must stop the affected lane and surface a clear human-action state when any of these are encountered:

- `BUSINESS_DECISION_REQUIRED`;
- `ARCHITECTURE_DECISION_REQUIRED`;
- `SECURITY_DECISION_REQUIRED`;
- `COST_APPROVAL_REQUIRED`;
- material AWS/cloud execution not already approved by the task;
- dependency/backlog conflict that the deterministic scheduler cannot resolve;
- repeated Builder/Reviewer loop exhaustion;
- branch/worktree conflict requiring human judgment.

Independent Ready lanes may continue when their metadata proves they are unaffected.

### 10. No autonomous merge in V1

V1 stops at a clear `MERGE_READY` (or canonical equivalent) state.

It must **not**:

- merge to `main` automatically;
- force-push shared branches;
- bypass required review/CI;
- mark a task Completed merely because an agent claimed success;
- rewrite domain/architecture decisions to unblock execution.

Final merge remains a human-controlled gate in V1.

### 11. Cloud and cost safety

Default orchestration is local and must cost `$0` in AWS usage.

If a future Ready task requires real AWS verification:

- the Orchestrator may only enter that path if the canonical task metadata explicitly requires cloud verification;
- V1 must require an explicit human/cloud-execution opt-in (for example a command flag or equivalent approved control) before launching any action that can create AWS cost;
- Free Tier/credit guardrails and teardown requirements remain authoritative;
- no unattended cloud load test, crawler run, or long-lived preview deployment is permitted by default.

### 12. CLI / developer experience

Expose an ergonomic repository-local interface, preferably through the existing CommerceOS tooling entry point or a clearly documented adjacent command.

The final command surface must support at least equivalent capabilities to:

```text
status       inspect DAG, active claims, blockers, and current Ready frontier
validate     validate canonical task metadata/DAG
plan         show which tasks would be dispatched next
dry-run      simulate dispatch with no Codex/Git mutation
run          dispatch eligible work within configured concurrency limits
resume       resume safely after interruption
cleanup      inspect/remove finished task worktrees safely
```

Exact command spelling may follow existing repository tooling conventions.

## Out of scope

- changing the product/domain architecture;
- generating the canonical Backlog V2 (owned by TASK-0089);
- replacing Backlog Planner reasoning with the scheduler;
- using an LLM to decide DAG eligibility or dependency satisfaction;
- more than two concurrent writable Builders by default;
- Kubernetes, Temporal, Redis, database-backed orchestration, message brokers, or other always-on orchestration infrastructure;
- server-hosted orchestration service;
- autonomous merge to `main`;
- autonomous architecture/product/security/cost approval;
- automatically deploying AWS without explicit task metadata and human cloud opt-in;
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

### AC03 — Parallelism is bounded and conflict-aware

Given more than two tasks are otherwise Ready
when the Orchestrator runs
then at most two writable Builder tasks are active concurrently, and declared concurrency/exclusive-resource conflicts prevent unsafe co-scheduling.

### AC04 — Worktree isolation

Given two independent Ready Builder tasks are dispatched
when execution starts
then each uses its own task branch/worktree and neither writes feature code in the primary `main` checkout.

### AC05 — Dry-run is side-effect free

Given valid Ready tasks
when dry-run mode executes
then the Orchestrator reports intended task/role/model/worktree/command/gate actions without launching Codex, creating worktrees, modifying task state, or invoking AWS.

### AC06 — Codex is testable without Codex usage

Given repository CI/unit tests run
when Orchestrator behavior is tested
then a fake runner can simulate success, failure, blocker, reviewer findings, and interrupted execution without calling real Codex.

### AC07 — Failed harness returns to fix flow

Given a Builder run completes but required local repository verification fails
when the Orchestrator evaluates the result
then the task does not advance to review/merge-ready and receives actionable failure context for a bounded fix attempt.

### AC08 — Independent review is enforced

Given a Builder diff passes required local verification
when the task enters review
then review runs with independent Reviewer instructions/context and does not modify the Builder branch.

### AC09 — Review findings loop safely

Given Reviewer reports blocking findings
when the Orchestrator processes them
then the task returns to a Builder fix state with findings attached, re-verifies after fixes, and escalates to human instead of looping forever after the configured retry limit.

### AC10 — Human decision gates fail closed

Given a task or agent result requires a business, architecture, security, cost, or unapproved cloud decision
when encountered
then the affected task transitions to an explicit human-blocked state and is not automatically continued by the Orchestrator.

### AC11 — No autonomous merge

Given a task passes Builder, harness, and Reviewer stages
when V1 completes its automated lifecycle
then it stops at `MERGE_READY` (or approved equivalent) and does not merge or force-push `main`.

### AC12 — Interrupted runs are resumable

Given the Orchestrator process stops while tasks are claimed/running
when status/resume is executed later
then it detects existing worktree/run state, avoids duplicate dispatch, and either resumes or reports a safe recovery action.

### AC13 — AWS remains opt-in

Given a task does not explicitly require approved cloud verification
when orchestration runs
then no AWS deployment/API execution is initiated by the Orchestrator.

Given a task does require real cloud verification
when cloud execution has not been explicitly opted into by the human
then the task stops at the cloud gate rather than spending AWS credit.

### AC14 — Repository harness covers orchestrator invariants

Given TASK-0090 is implemented
when `python3 scripts/harness_check.py` runs
then Orchestrator tests/validation are included in the repository verification path and regressions in core DAG/concurrency/human-gate behavior fail CI.

## Architecture impact

- Owning domain: Engineering / Repository Harness
- Domains touched: all implementation domains indirectly through task dispatch only
- Persistence impact: no business persistence; local orchestration state only
- Events/contracts impact: consumes the canonical machine-readable task contract defined/approved by TASK-0089
- AWS/IaC impact: none required for the Orchestrator itself
- ADR required? No by default — this is repository-local engineering tooling. Create an ADR only if implementation proposes a persistent service, external orchestrator, new paid infrastructure, or another material architecture change.

## Security and tenant impact

- Authentication: no CommerceOS runtime authentication change
- Authorization: no tenant runtime authorization change
- Tenant scoping: Orchestrator must not weaken task-level tenant/security requirements
- Sensitive data/secrets: never embed AWS credentials, GitHub tokens, Codex secrets, or model credentials in task files/logs; inherit approved local/CLI authentication mechanisms
- Abuse/rate-limit considerations: bounded Builder concurrency and bounded retry/review loops protect Codex usage; cloud execution is opt-in

## Reliability and idempotency impact

- Retry behavior: local task dispatch/review retry is bounded and resumable
- Timeout semantics: runner timeout/interruption must not be treated as proof that the task did or did not modify its worktree; recovery inspects actual state
- Duplicate-delivery behavior: duplicate task dispatch for one claimed task must be prevented by local orchestration state
- Idempotency key/strategy: canonical task ID + run/claim identity forms the orchestration identity; exact implementation may vary
- DLQ/recovery/reconciliation: no queue/DLQ in V1; explicit stale-claim/status/recovery flow is required

## Observability impact

- Logs: structured local logs include task ID, run ID, role, lifecycle transition, worktree/branch, command/result, and blocker category without secrets
- Metrics: no external metrics required in V1
- Traces/correlation: task ID/run ID are sufficient local correlation keys
- Operational states/errors: every stop/failure must map to an explicit inspectable lifecycle/blocker state rather than disappearing into agent prose

## Cost impact

- Request/compute impact: local CPU/process usage plus Codex usage for dispatched agents
- Storage impact: small local worktrees/log/state files; existing Git history/PRs
- Network impact: Git/Codex network usage when real runner is enabled
- New AWS resources/services: none
- Free Tier allowance relevant to this task: Orchestrator implementation/CI should require no AWS use
- Expected monthly AWS cost change or `negligible` with rationale: `$0` expected from the Orchestrator itself
- Estimated one-off cloud-test/load-test cost, if any: none for Orchestrator verification
- Codex usage guardrail: default maximum two writable Builders plus bounded review/fix attempts

## Test plan

- Unit: DAG parsing/validation, cycle/missing-dependency detection, Ready-frontier calculation, lifecycle transitions, concurrency/exclusive-resource scheduling, model/role routing, retry limits, human/cloud gates, stale-claim recovery
- Integration: temporary Git repository/worktree lifecycle using fake Codex runner; simulate two parallel Builders, one blocked task, review failure/fix/pass, interruption/resume, cleanup
- Architecture: scheduler remains deterministic and does not import/contain business-domain decision logic; runner and Git operations sit behind testable boundaries
- Contract: canonical TASK-0089 metadata schema fixtures validate backward/forward failure behavior
- IaC: N/A
- E2E/manual: on a small synthetic task DAG, run dry-run then a fake-run lifecycle from Ready → Builder → harness → Reviewer → MERGE_READY; optionally perform one explicitly approved real Codex smoke run after all fake tests pass
- **Cloud verification required?** No — Orchestrator V1 is local engineering tooling and AWS execution must be mocked/gated
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

## Implementation notes

Keep the first implementation deliberately small. A local Python process integrated with existing repository tooling is preferred over introducing an agent framework or distributed orchestration platform.

Recommended internal separation (names are illustrative, not mandatory):

```text
tools/orchestrator/
  task_graph.py       parse/validate canonical task metadata
  scheduler.py        deterministic Ready-frontier/concurrency logic
  state.py            local claim/run lifecycle and resume
  worktrees.py        Git branch/worktree operations
  runner.py           Codex/fake runner boundary
  review.py           review/fix transition handling
  cli.py              status/validate/plan/run/resume/cleanup
```

The Orchestrator should be deterministic enough that its scheduler behavior is exhaustively testable without an LLM.

## Completion summary

Fill before moving to `tasks/completed/`.

### What changed

- ...

### Verification

- `python3 scripts/harness_check.py`: PASS/FAIL
- Orchestrator unit/integration tests:
- dry-run evidence:
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
