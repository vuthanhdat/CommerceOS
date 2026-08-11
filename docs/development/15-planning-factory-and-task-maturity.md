# CommerceOS — Planning Factory & Task Maturity

_Last reviewed: 2026-08-11. ADR-012 is authoritative for infrastructure/runtime planning._

## 1. Why this exists

A generated backlog is not automatically an implementation contract. A task can describe a plausible outcome while still depending on business/domain/architecture decisions that have not been made.

CommerceOS therefore separates backlog generation from implementation readiness.

> A task may exist long before it is safe to implement.

## 2. Specification maturity

```text
Outline -> Refined -> Ready -> Active -> Completed
```

### Outline

Purpose and rough outcome are known, but important design dependencies may still be unresolved. Must not be assigned to a Builder.

### Refined

Business behavior and major constraints are understood, but one or more implementation decisions/contracts/dependencies are still pending. Must not be assigned to a Builder.

### Ready

The task is an implementation contract. A Builder may take it only when the Ready gate below passes.

### Active

A Ready task has an assigned branch/worktree and Builder.

### Completed

Acceptance criteria, verification, review, and Definition of Done are satisfied.

## 3. Ready gate

Before a task becomes `Ready`, the Backlog Planner verifies that all relevant items are resolved or explicitly N/A.

### Business/domain

- owning domain/bounded context is explicit;
- business outcome is unambiguous;
- required aggregate/entity/value-object ownership is defined;
- touched invariants are documented;
- required state transitions/error semantics are documented;
- cross-domain facts and ownership are explicit.

### Architecture

- module/layer ownership is explicit;
- synchronous vs asynchronous interactions are decided;
- public/internal contracts are defined or versioned;
- transaction/consistency boundary is defined where relevant;
- persistence ownership/access patterns are defined where relevant;
- required infrastructure **capability** is explicit before selecting a service mapping;
- AWS-style service mappings follow accepted architecture/ADR and target LocalStack under ADR-012;
- LocalStack endpoint/credentials/region/account placeholders/ports/resource prefixes remain configuration concerns;
- known LocalStack support/edition/behavior limitations that materially affect the task are understood;
- new material architecture choices have an accepted ADR.

### Security/reliability/infrastructure verification

- tenant/authentication/authorization expectations are explicit;
- idempotency/retry/timeout/duplicate-delivery behavior is explicit where relevant;
- LocalStack/infrastructure verification need is explicit;
- bootstrap/reset/cleanup expectations are explicit when infrastructure state is involved;
- unsupported/different LocalStack behavior has a planned nearest-reliable-layer verification path;
- no real AWS account, IAM/OIDC, Budget/credit, cloud authorization, preview/staging, or cloud-cost gate is required under ADR-012.

### Execution

- dependencies are completed or otherwise satisfied;
- acceptance criteria are machine-testable where practical;
- out-of-scope boundaries prevent scope creep;
- exclusive local/LocalStack resources are declared where isolation cannot safely be achieved;
- no unresolved decision remains that would force the Builder to choose product or architecture behavior.

If any material item is missing, the task stays `Outline` or `Refined` and is routed back to the appropriate planning role.

## 4. Planning Factory

```text
Product intent / existing docs
          ↓
DOMAIN ARCHITECT
business model, invariants, ownership
          ↓
TECHNICAL ARCHITECT
module/contracts/persistence/integration/runtime/ADR
          ↓
BACKLOG PLANNER
reconcile task graph + maturity + dependencies
          ↓
READY TASKS
          ↓
BUILDER
```

Only near-term tasks require implementation-level detail. Later tasks remain Outline until their dependencies and lessons from earlier phases are known.

## 5. Agent communication protocol

Agents communicate through repository artifacts:

```text
Domain Architect
  -> docs/domains/* / decision register

Technical Architect
  -> docs/architecture/* / docs/adr/* / contracts

Backlog Planner
  -> tasks/BACKLOG* / task files / dependency + maturity metadata

Builder
  -> branch/worktree + commits + completion summary

Reviewer / Verification
  -> findings + test evidence
```

The repository and GitHub history are the shared planning record.

## 6. Builder stop rule

A Builder must not silently resolve an architectural or business ambiguity.

If implementation requires a decision not encoded in the Ready task or accepted source documents, stop with:

```text
BLOCKED — PLANNING DECISION REQUIRED
```

State the missing decision, why it affects implementation, which planning role owns it, and the affected task/contract/ADR.

An emulator limitation is not automatically a planning decision. If architecture already defines the capability and the task defines the limitation-handling strategy, record/test the limitation according to ADR-012. If satisfying the task would require changing the capability/contract/business semantics, route back to Technical Architect or Domain Architect instead of inventing a workaround.

## 7. Existing task reconciliation

Process tasks by dependency frontier:

1. retain existing task nodes unless obsolete;
2. refresh domain and technical baselines;
3. identify the first dependency frontier;
4. refine only those tasks and immediate prerequisites;
5. remove stale real-AWS gates before a task becomes Ready;
6. mark individual tasks Ready;
7. implement and learn;
8. revisit later Outline tasks as architecture/domain knowledge improves.

## 8. Human role

The human approves high-consequence product/architecture baselines and decides when a task/phase is worth executing. Routine Builder implementation should not require cloud-account permissions or repeated infrastructure decisions once the task is Ready.
