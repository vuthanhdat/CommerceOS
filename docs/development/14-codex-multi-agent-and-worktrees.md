# CommerceOS — Codex Multi-Agent & Git Worktree Operating Model

_Last reviewed: 2026-08-09._

## 1. Purpose

CommerceOS uses Codex as the primary AI development environment under the repository Harness Engineering model.

This document defines:

- model-selection policy;
- agent roles;
- concurrency limits;
- Git worktree isolation;
- branch naming;
- local/cloud environment isolation;
- review and integration flow;
- cleanup rules.

The objective is to maximize useful Codex work within a ChatGPT Plus usage budget while keeping repository state, architecture, AWS cost, and concurrent changes controlled.

OpenAI documents that the Codex app supports multiple agents in separate threads and has built-in worktree support so agents can work on isolated copies of the same repository. CommerceOS adopts that isolation model as the default when parallel work is useful.

References:

- https://openai.com/index/introducing-the-codex-app/
- https://openai.com/codex/

---

## 2. Model policy — Luna first

The default CommerceOS policy is:

> **Use Luna for normal engineering work. Escalate to a stronger reasoning model only when the task genuinely needs deeper business/domain/architecture reasoning.**

The user has observed that Luna consumes substantially less included Codex usage in normal development. The harness should therefore improve task clarity and verification so inexpensive model usage is sufficient for most implementation work.

### Default: Luna

Use the Codex model option labeled **Luna** in the user's environment for:

- implementation from an already-defined task;
- routine CRUD/application code;
- test implementation;
- fixture creation;
- straightforward refactoring;
- documentation updates;
- CDK implementation when the architecture decision is already made;
- fixing deterministic CI/lint/test failures;
- frontend implementation from an established contract;
- repetitive migration/renaming/cleanup;
- simple reviewer checks when no difficult architectural reasoning is involved.

### Escalate to a stronger reasoning model

Use a higher-capability model for work such as:

- product/business capability design;
- bounded-context/domain design;
- defining business invariants;
- architecture decisions and ADRs;
- multi-tenancy/security-model design;
- difficult distributed-system reasoning;
- payment timeout/idempotency/reconciliation design;
- accounting posting semantics;
- concurrency/consistency design;
- major DynamoDB access-model decisions;
- deciding whether to introduce EventBridge/SQS/Step Functions/new AWS services;
- difficult production/root-cause debugging;
- high-risk architecture/security review;
- harness audits that require synthesis across many repeated failures.

### Escalation rule

Do not choose a stronger model merely because a task is large.

Escalate when the **reasoning difficulty or consequence of a wrong decision** is high.

Preferred flow:

```text
High-reasoning model
  define WHAT / WHY / invariants / architecture
              ↓
          TASK + ADR
              ↓
            Luna
  implement / test / document / iterate
              ↓
           Harness
              ↓
     Luna reviewer normally
              ↓
High-reasoning reviewer only when risk warrants it
```

This separates expensive design reasoning from lower-cost execution.

---

## 3. Agent roles

Initial logical roles:

### Planner / Architect

Primary model: **strong reasoning model when business/domain/architecture design is involved**.

Responsibilities:

- clarify business outcome;
- define task scope;
- write acceptance criteria;
- identify invariants;
- determine cloud-verification need;
- identify cost/security/tenant impact;
- create ADRs when required.

The Planner normally does not implement feature code.

### Builder

Primary model: **Luna**.

Responsibilities:

- read `AGENTS.md`, task, relevant docs, and ADRs;
- implement exactly the approved scope;
- write/update tests;
- update documentation where required;
- run repository verification;
- produce a completion summary.

### Reviewer

Primary model: **Luna** for normal reviews.

Escalate to a stronger reasoning model for security-, architecture-, accounting-, payment-, concurrency-, or distributed-system-critical changes.

The Reviewer does not silently fix the Builder's branch. It reports findings; the Builder performs fixes so responsibility remains clear.

### Verification Agent — later phases

Primary model: **Luna**.

Focuses on adversarial/failure scenarios rather than code style:

- duplicate delivery;
- timeout ambiguity;
- concurrency conflicts;
- cross-tenant access;
- retry storms;
- DLQ behavior;
- out-of-order events;
- failure injection.

### Harness Auditor — later phases

Default: Luna for mechanical scans; stronger reasoning model for periodic architecture/harness synthesis.

Looks for repeated defects and converts them into reusable instructions, tests, linters, templates, fixtures, or CDK constructs.

---

## 4. Concurrency policy

Having multiple logical roles does not mean all roles run simultaneously.

Default ChatGPT Plus policy:

- maximum **2 active Builder-style coding tasks in parallel**;
- Planner and Reviewer are normally sequential around a Builder task;
- parallel Builders are allowed only when task boundaries/contracts are sufficiently independent;
- do not run multiple expensive reasoning agents concurrently without a clear benefit.

Good parallel pair:

```text
TASK-0041 Catalog API
TASK-0042 Storefront UI using already-stable API contract
```

Bad parallel pair:

```text
Builder A changes checkout contract
Builder B changes inventory reservation contract
Builder C changes shared event envelope
```

Concurrency is allowed by stable boundaries, not by available agent count.

---

## 5. Core worktree rule

> **One writable task = one branch = one worktree.**

The primary repository checkout is the integration/control checkout and should remain clean.

Recommended layout:

```text
~/src/
├── CommerceOS/                         # primary checkout; main/integration
└── CommerceOS.worktrees/
    ├── TASK-0003-toolchain/
    ├── TASK-0010-catalog-api/
    └── TASK-0011-storefront-ui/
```

Do not place worktrees inside `CommerceOS/` itself.

Do not let two writable agents share the same worktree.

Do not check out the same branch into multiple worktrees.

---

## 6. Preferred setup — Codex built-in worktrees

When using the Codex desktop app, prefer its built-in worktree isolation for parallel agent threads.

Operating rules:

1. Open `CommerceOS` as one Codex project.
2. Keep the main/local checkout clean and synchronized with `origin/main`.
3. Start one thread per task, not one thread per vague topic.
4. When a thread will modify code, run it in an isolated worktree.
5. Name/associate the work with the task ID, e.g. `TASK-0010 Catalog API`.
6. Do not make a second Builder modify the same task/worktree concurrently.
7. Review the thread diff before integrating.
8. Prefer PR-based integration for non-trivial changes.
9. Remove/discard the worktree after the task is merged or intentionally abandoned.

Codex's built-in support is the preferred user experience; the manual commands below are the fallback and the conceptual source for how isolation works.

---

## 7. Manual Git worktree setup — fallback / debugging

Assume the main checkout is:

```bash
cd ~/src/CommerceOS
```

Synchronize main first:

```bash
git fetch origin
git switch main
git pull --ff-only origin main
git status --short
```

The working tree should be clean before creating parallel task worktrees.

Create the sibling container directory once:

```bash
mkdir -p ../CommerceOS.worktrees
```

Create a task branch + worktree:

```bash
git worktree add \
  ../CommerceOS.worktrees/TASK-0010-catalog-api \
  -b agent/TASK-0010-catalog-api \
  origin/main
```

Open Codex/IDE/terminal against:

```text
../CommerceOS.worktrees/TASK-0010-catalog-api
```

A second independent Builder task:

```bash
git worktree add \
  ../CommerceOS.worktrees/TASK-0011-storefront-ui \
  -b agent/TASK-0011-storefront-ui \
  origin/main
```

Inspect active worktrees:

```bash
git worktree list
```

Branch convention:

```text
agent/TASK-<id>-<short-name>
```

Examples:

```text
agent/TASK-0010-catalog-api
agent/TASK-0011-storefront-ui
agent/TASK-0032-inventory-reservation
```

---

## 8. Worktree task lifecycle

```text
main clean
   ↓
TASK spec approved
   ↓
create task branch/worktree
   ↓
Builder (Luna)
   ↓
local harness/tests
   ↓
cloud verification if task says Required
   ↓
commit + push
   ↓
PR
   ↓
Reviewer
   ↓
Builder fixes in same task worktree
   ↓
CI green
   ↓
human merge
   ↓
remove worktree
```

A Builder does not merge its own task merely because tests pass.

---

## 9. Updating a long-running task worktree

If `main` advances while a task is still open, update intentionally rather than continuously rebasing every few minutes.

Inside the task worktree:

```bash
git fetch origin
git rebase origin/main
```

or use the repository's later agreed merge strategy if an ADR/process changes this policy.

After updating:

```bash
python3 scripts/harness_check.py
```

and rerun implementation-specific verification.

Do not rebase a branch while another agent/process is concurrently writing to the same worktree.

---

## 10. Review isolation

Reviewer should be logically independent from Builder.

Preferred options:

### Option A — Codex review thread

Reviewer inspects the PR/diff in a separate Codex thread without editing Builder state.

### Option B — read-only local inspection

Reviewer inspects the Builder worktree/diff but does not commit changes.

### Option C — separate review worktree when necessary

For complex local execution, create a temporary review worktree from the Builder branch:

```bash
git fetch origin
git worktree add --detach \
  ../CommerceOS.worktrees/review-TASK-0010 \
  origin/agent/TASK-0010-catalog-api
```

Detached review worktrees are disposable and must not become an alternative implementation branch.

Reviewer findings return to the Builder.

---

## 11. Local runtime isolation between worktrees

Git isolation is insufficient if two tasks start services on identical ports or share mutable local state.

Each concurrent worktree should have a task instance identifier, for example:

```text
COMMERCEOS_INSTANCE=0010
COMMERCEOS_INSTANCE=0011
```

The concrete launcher added in Phase 0 should derive local ports/resource names from this identifier.

Example target convention:

```text
TASK-0010
frontend     15170
api          15171
mock-payment 15172
DynamoDB     15173

TASK-0011
frontend     15270
api          15271
mock-payment 15272
DynamoDB     15273
```

Exact ports may change when tooling is implemented; the invariant is that two concurrent worktrees do not silently share mutable local infrastructure.

Developer secrets/config:

- use ignored local environment files;
- never commit AWS credentials/secrets;
- do not copy production data into a task worktree;
- package caches such as NuGet/npm caches may be shared globally because they are immutable/cache-like rather than application state.

---

## 12. AWS preview isolation

A cloud-sensitive task must not share mutable preview resources with another concurrent task.

Preview name derives from PR/task identity, for example:

```text
commerceos-pr-41
commerceos-task-0032
```

Tags include:

```text
Project=CommerceOS
Environment=preview
Task=TASK-0032
Owner=<developer>
Ephemeral=true
```

Cloud verification flow:

```text
worktree
   ↓
CDK synth/diff
   ↓
small preview stack
   ↓
cloud integration tests
   ↓
collect result
   ↓
CDK destroy
```

The task specification must state cloud-test cost and teardown behavior as required by the Free Tier/credit guardrails.

Never point two concurrently changing Builder branches at the same mutable staging resources merely to save setup effort.

---

## 13. Integrating and cleaning up

After PR merge, return to primary checkout:

```bash
cd ~/src/CommerceOS
git switch main
git pull --ff-only origin main
```

Remove the completed worktree:

```bash
git worktree remove ../CommerceOS.worktrees/TASK-0010-catalog-api
```

Delete the local task branch if merged:

```bash
git branch -d agent/TASK-0010-catalog-api
```

Prune stale worktree metadata occasionally:

```bash
git worktree prune
```

If a worktree contains uncommitted work, do not force-remove it until the human has explicitly decided that the work is disposable.

---

## 14. Main checkout policy

The main checkout is an integration/control plane, not another Builder workspace.

Allowed:

- pull/fetch;
- inspect current main;
- run final verification;
- compare branches/PRs;
- create/remove worktrees;
- emergency human-controlled maintenance.

Avoid:

- letting an agent implement a normal feature directly on `main`;
- mixing two task changes in the main checkout;
- keeping uncommitted experimental code on main;
- using main as a shared mutable runtime while parallel worktrees are executing tests.

---

## 15. Prompt templates

### Planner / Architect — stronger reasoning model

```text
Read AGENTS.md and the relevant CommerceOS product/domain/architecture documents.
Design the requested business capability and create/update its TASK specification.
Define invariants, acceptance criteria, architecture/security/cost impact, and cloud-verification requirements.
Do not implement feature code.
```

### Builder — Luna

```text
Implement TASK-XXXX according to AGENTS.md and the task specification.
Work only inside this task worktree.
Do not expand scope.
Add/update required tests and documentation.
Run the required CommerceOS verification before finishing.
Do not deploy AWS unless the task explicitly requires cloud verification.
```

### Reviewer — Luna normally

```text
Independently review TASK-XXXX and its diff against acceptance criteria, AGENTS.md, architecture rules, Definition of Done, tenant/security rules, reliability/idempotency, cost, and test quality.
Report findings by severity.
Do not modify the Builder branch.
```

For architecture/security/payment/accounting/concurrency-critical changes, run the Reviewer prompt using a stronger reasoning model.

---

## 16. Decision table

| Work | Default model | Worktree? | AWS? |
|---|---|---|---|
| Business/domain design | Strong reasoning | only if writing files | No normally |
| Architecture/ADR | Strong reasoning | yes if writing | Only for validation when required |
| Feature implementation | Luna | Yes | only if task requires |
| Unit/architecture tests | Luna | Same Builder worktree | No |
| CDK implementation from accepted design | Luna | Yes | synth locally; deploy conditionally |
| Routine review | Luna | Separate thread/read-only | No normally |
| High-risk architecture/security review | Strong reasoning | Separate thread/read-only | As required |
| Failure verification | Luna | isolated task/review context | Often targeted AWS preview |
| Docs/refactor/cleanup | Luna | Yes if writable | No |
| Harness architecture audit | Strong reasoning when synthesizing | separate task | No normally |

---

## 17. Scaling rule

Do not add an agent role merely because Codex supports more parallel agents.

Create a new role only when there is a repeated coordination/expertise bottleneck that cannot be solved more cheaply by:

- a better task specification;
- domain-local documentation;
- an architecture test;
- a reusable script/skill;
- a stronger repository guardrail.

Similarly, do not escalate model strength when a clearer task/harness can make Luna sufficient.

The desired long-term behavior is:

> **Use expensive reasoning to make good decisions once; encode those decisions in the repository; use Luna repeatedly to execute them safely.**
