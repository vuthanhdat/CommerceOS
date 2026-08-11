# CommerceOS — Codex Multi-Agent & Git Worktree Operating Model

_Last reviewed: 2026-08-11._

## 1. Purpose

CommerceOS uses Codex under the repository Harness Engineering model. This document defines model selection, agent roles, concurrency, Git worktree isolation, local/LocalStack runtime isolation, review, integration, and cleanup.

The primary concurrency invariant is:

> **one writable task = one branch = one worktree**

The infrastructure invariant under ADR-012 is:

> **concurrent tasks must not silently share mutable LocalStack resources or ports**

## 2. Model policy

Planning roles use the stronger reasoning profile defined by `AGENTS.md`; Builder/routine implementation roles use the lower-cost execution profile defined there.

Escalate based on reasoning difficulty/risk, not task size alone. Architecture, security/tenant isolation, accounting, payment ambiguity/idempotency, concurrency, and difficult distributed-system design belong in planning artifacts before Builder execution.

## 3. Agent roles

### Planner / Architect

- clarify approved technical/domain intent;
- define acceptance criteria and invariants;
- identify infrastructure-verification need;
- create/update ADRs when required;
- do not implement business features.

### Builder

- work from one Ready task;
- modify only its task worktree;
- add/update tests/docs;
- run required verification;
- never invent missing architecture/domain semantics.

### Reviewer / Verification

- review independently;
- emphasize failure paths, tenant isolation, idempotency, concurrency, and architecture boundaries;
- do not silently repair the Builder branch unless explicitly assigned to do so.

## 4. Concurrency policy

Default maximum is two active Builder-style writable tasks, and only when contracts/resources are sufficiently independent.

Stable boundaries permit concurrency; available agent count does not.

## 5. Core worktree rule

Keep the main checkout as the integration/control checkout.

Recommended layout:

```text
~/src/
├── CommerceOS/
└── CommerceOS.worktrees/
    ├── TASK-0094-localstack-foundation/
    └── TASK-0096-subscription/
```

Do not let two writable agents share the same worktree or branch.

## 6. Manual worktree fallback

```bash
cd ~/src/CommerceOS
git fetch origin
git switch main
git pull --ff-only origin main
git status --short
mkdir -p ../CommerceOS.worktrees

git worktree add \
  ../CommerceOS.worktrees/TASK-0094-localstack-foundation \
  -b agent/TASK-0094-localstack-foundation \
  origin/main
```

Branch convention:

```text
agent/TASK-<id>-<short-name>
```

## 7. Task lifecycle

```text
main clean
   ↓
Ready TASK
   ↓
create task branch/worktree
   ↓
Builder
   ↓
local harness/tests
   ↓
LocalStack verification if required
   ↓
commit + push
   ↓
review / verification
   ↓
integrate serially
   ↓
remove worktree
```

No real AWS deployment step exists under ADR-012.

## 8. Updating long-running work

Update intentionally against current `origin/main`, then rerun `python3 scripts/harness_check.py` and task-specific verification.

Do not rebase while another process is writing to the same worktree.

## 9. Review isolation

Use a separate review thread or disposable detached worktree where execution is required. Reviewer findings return to the Builder/task owner.

## 10. Local runtime isolation

Each concurrent worktree uses an instance identifier, for example:

```text
COMMERCEOS_INSTANCE=0094
COMMERCEOS_INSTANCE=0096
```

The launcher/configuration derives distinct ports and resource prefixes from this identifier.

Shared package caches may be reused; mutable application/infrastructure state may not be shared accidentally.

## 11. LocalStack resource isolation

A task using LocalStack should derive isolation from its instance identifier where the service permits it:

- CDK stack names;
- DynamoDB table names;
- SQS/DLQ names;
- EventBridge bus/rule names;
- Step Functions state-machine/execution identities;
- S3 bucket/object prefixes;
- Lambda/API logical names;
- test tenant/data namespaces;
- ports and local provider endpoints.

A task declares an exclusive resource only when the capability cannot be safely namespaced or isolated.

## 12. LocalStack lifecycle per task

Infrastructure-sensitive task flow:

```text
worktree
   ↓
start/use isolated LocalStack profile
   ↓
CDK synth
   ↓
deploy/bootstrap selected resources
   ↓
integration/failure tests
   ↓
collect diagnostics
   ↓
reset/stop/remove task-owned state
```

The task must record any unsupported, partial, edition-dependent, or behaviorally different LocalStack feature. A real AWS preview is not the fallback.

## 13. Secrets/configuration

- never commit credentials/secrets;
- LocalStack synthetic credentials live in ignored/configured development settings;
- do not copy real production/business data into task environments;
- Domain/Application code must not depend on LocalStack endpoints or configuration.

## 14. Main checkout policy

Allowed: pull/fetch, inspect main, final verification, compare branches/PRs, create/remove worktrees, controlled integration.

Avoid: normal feature implementation directly on main, mixing task changes, uncommitted experiments on main, or using main as shared mutable runtime state while worktrees execute.

## 15. Prompt guidance

Builder prompts should require the task's declared verification and should say **LocalStack**, not AWS cloud deployment, when infrastructure semantics are in scope.

Technical Architect prompts should describe capabilities first and map them to LocalStack-supported AWS-style services only after the capability/contract is defined.

## 16. Decision table

| Work | Worktree? | Infrastructure target |
|---|---|---|
| Business/domain design | only if writing files | none normally |
| Architecture/ADR | yes if writing | LocalStack only when validation needed |
| Feature implementation | yes | LocalStack if task requires infrastructure semantics |
| Unit/architecture tests | same Builder worktree | none |
| CDK implementation | yes | synth + LocalStack verification as required |
| Routine review | separate thread/read-only | none normally |
| Failure verification | isolated task/review context | targeted LocalStack |
| Docs/refactor/cleanup | yes if writable | none |

## 17. Scaling rule

Add roles only for repeated coordination/expertise bottlenecks. Prefer clearer tasks, stronger architecture tests, reusable scripts, and better guardrails over unnecessary agent/process complexity.
