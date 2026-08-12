# CommerceOS Agent Instructions

CommerceOS is developed with a **Harness Engineering** workflow. Agents work from repository context, satisfy machine-checkable constraints, and improve the harness when recurring failure modes are discovered.

## 1. Before implementation

Read, in this order:

1. `README.md`
2. `docs/development/15-planning-factory-and-task-maturity.md`
3. the active task under `tasks/commerceos/active/` or `tasks/orchestrator/active/`, or the task explicitly named by the human
4. the role contract under `docs/agents/`
5. relevant product/domain documents
6. `docs/development/03-architecture-rules.md`
7. `docs/development/14-codex-multi-agent-and-worktrees.md` when using parallel agents/worktrees
8. relevant ADRs under `docs/adr/`, especially ADR-012 for runtime/infrastructure work
9. domain-local `AGENTS.md` files if present

Do not implement from the task title alone.

### Planning gate

A file under either catalog's `backlog/` directory is not evidence that it is implementation-ready.

```text
Outline -> Refined -> Ready -> Active -> Completed
```

Only a `Ready` task may be assigned to a Builder. If a Builder encounters an Outline/Refined task or an unresolved business/architecture decision, stop with `BLOCKED — PLANNING DECISION REQUIRED` instead of guessing.

Planning flow:

```text
Domain Architect
      ↓
Technical Architect
      ↓
Backlog Planner
      ↓
Ready task
      ↓
Builder
```

Agents communicate through repository artifacts, tasks, ADRs/contracts, review findings, and verification evidence rather than private conversational memory.

## 2. Core product invariants

### Multi-tenancy

- Tenant-owned data is always scoped by trusted Tenant context.
- Never trust a client-supplied `tenantId` for authorization.
- Cross-tenant access is denied by design and covered by tests.

### Domain boundaries

- A domain owns its business rules and persistence model.
- One domain never directly reads/writes another domain persistence as an integration shortcut.
- Cross-domain integration uses explicit application contracts, commands, queries, or domain/integration events as documented.
- Domain code must not depend directly on AWS SDKs, LocalStack packages, HTTP frameworks, or persistence implementations.
- Application code must not depend on LocalStack-specific endpoints/credentials/configuration.

### Accounting

- Posted journals are immutable.
- Corrections use reversal/correction entries.
- Every posted journal balances.
- Event-driven accounting consumers are idempotent and traceable to source fact/transaction.

### Inventory

- Inventory invariants are concurrency-safe.
- `Available = OnHand - Reserved` unless a later ADR changes the model.
- Never rely on unprotected read-then-write logic for stock correctness.

### Payments

- Only internal Mock Payment Provider behavior is used until an explicit later decision.
- Payment retries require idempotency.
- Timeout does not imply failure; ambiguous outcomes require query/reconciliation.
- Never store real card data or secrets in fixtures.

### Events and async consumers

- Consumers assume at-least-once delivery and are idempotent when effects can repeat.
- Integration events carry `eventId`, `eventType`, `eventVersion`, `tenantId` when applicable, `aggregateId`, `occurredAt`, `correlationId`, and `causationId` when applicable.
- Do not publish vague technical events when a meaningful business fact exists.

### Product-data ingestion

- External source snapshots are not the merchant canonical catalog.
- Crawling/adapters respect source policy, robots/terms review, rate limits, kill switches, and parser fixtures.
- Prefer official APIs where available and permitted.

### Infrastructure runtime — ADR-012

- **LocalStack is the only infrastructure/runtime target** for development, staging-like validation, integration testing, and deployment exercises.
- Do not require or provision a real AWS account unless a later accepted ADR explicitly supersedes ADR-012.
- AWS-style services may be used where LocalStack supports the learning scenario.
- Define architecture capabilities first; service names are implementation mappings.
- Endpoints, synthetic credentials, region, account-id placeholders, ports, resource prefixes, reset policy, and LocalStack feature/edition switches are configuration concerns.
- No LocalStack-specific implementation detail belongs in Domain/Application contracts.
- Unsupported, partial, behaviorally different, or edition-dependent LocalStack features must be documented explicitly; never silently claim AWS compatibility.
- No AWS account/IAM/OIDC/Budget/Free Tier/cloud-preview/cloud-staging gate is valid under the current architecture.

## 3. Codex model and worktree policy

CommerceOS uses role-based Codex profiles so planning quality and routine execution cost/throughput are predictable.

- Planning roles — Domain Architect, Technical Architect, Backlog Planner — use `gpt-5.6-sol`, reasoning `medium`, Standard service tier.
- Builder and routine Reviewer/Verification/Conflict Resolver work use `gpt-5.6-terra`, reasoning `medium`, Standard service tier.
- Do not enable Fast/priority or different models unless the human explicitly changes the assignment or repository policy.

Parallel writable work follows:

> **one writable task = one branch = one worktree**

The primary `main` checkout is integration/control. Default concurrency is at most two active Builder-style coding tasks and only when boundaries/resources are sufficiently independent.

LocalStack resources/ports must be isolated by task instance where concurrent work runs.

See `docs/development/14-codex-multi-agent-and-worktrees.md`.

## 4. Specialized agent roles

- `domain-architect.md` — business/domain ownership and invariants.
- `technical-architect.md` — modules/contracts/persistence/integration/runtime/ADR decisions.
- `backlog-planner.md` — task graph, maturity, dependency reconciliation.
- `builder.md` — implement one Ready task.
- `reviewer.md` — independent implementation review.
- `verification.md` — failure-oriented verification.

A role writes only artifact types allowed by its role contract unless the human explicitly changes the assignment.

## 5. Task discipline

Every non-trivial change is tied to a task specification using `tasks/TASK-TEMPLATE.md`.

Respect specification maturity, dependencies, goal, business context, scope, acceptance criteria, architecture/security/test impact, and infrastructure-verification requirements.

Do not expand scope because an adjacent improvement looks useful. Record follow-up work separately.

Before a task becomes Ready, apply `docs/development/15-planning-factory-and-task-maturity.md`. If a Builder would need to make a material product/domain/architecture decision, the task is not Ready.

## 6. Definition of Done

A task is not complete merely because code compiles.

Follow `docs/development/02-definition-of-done.md`.

At minimum:

- acceptance criteria are satisfied;
- relevant tests are added/updated;
- tenant/security impact is checked;
- failure/idempotency behavior is considered for distributed operations;
- architecture boundaries still hold;
- documentation/ADR is updated when required;
- repository verification passes;
- required LocalStack bootstrap/integration/reset evidence is complete when the task declares infrastructure verification;
- known emulator limitations are recorded.

## 7. Verification

Run:

```bash
python3 scripts/harness_check.py
```

This remains the stable repository entry point for build, lint, unit, integration, architecture, IaC, frontend, and security checks as the implementation grows.

Never bypass/delete a failing guardrail simply to make the task pass. Fix the product or explicitly change the rule through the documented architecture/harness process.

## 8. Architecture decisions

Use `docs/adr/ADR-000-template.md` when a task changes a significant architectural decision, including:

- new infrastructure capability/service mapping;
- new persistence technology or access-model strategy;
- new cross-domain integration mechanism;
- new public/external contract;
- changed tenant-isolation model;
- changed accounting integrity model;
- material reliability/security/runtime trade-off;
- runtime/deployment model changes.

Any future return to real AWS requires an explicit human architecture decision that supersedes ADR-012.

## 9. Harness improvement rule

When a defect or repeated agent mistake is found:

1. fix the immediate defect;
2. ask why the harness allowed it and improve at least one of instruction, test, architecture rule, template, linter/check, fixture, or documentation when practical.

Do not convert every one-off mistake into a permanent rule. Prefer harness changes for repeated, severe, architecture-drifting, security/financial, or mechanically detectable failure classes.

## 10. Completion summary

When finishing work, report briefly:

- what changed;
- acceptance criteria satisfied;
- verification run/result;
- architecture/security implications;
- LocalStack verification/reset/limitations when applicable;
- follow-up items that remain out of scope.
