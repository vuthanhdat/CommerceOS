# CommerceOS Agent Instructions

This repository is developed with a **Harness Engineering** workflow. Agents are expected to work from repository context, satisfy machine-checkable constraints, and improve the harness when recurring failure modes are discovered.

## 1. Before implementation

Read, in this order:

1. `README.md`
2. the active task under `tasks/active/` (or the task explicitly named by the user)
3. relevant product/domain documents under `docs/`
4. `docs/development/03-architecture-rules.md`
5. `docs/development/14-codex-multi-agent-and-worktrees.md` when using Codex or parallel agents
6. relevant ADRs under `docs/adr/`
7. domain-local `AGENTS.md` files if present

Do not implement from the task title alone.

## 2. Core product invariants

These rules are mandatory unless an accepted ADR explicitly changes them.

### Multi-tenancy

- Tenant-owned data must always be scoped by trusted tenant context.
- Never trust a client-supplied `tenantId` for authorization.
- Cross-tenant access must be denied by design and covered by tests.

### Domain boundaries

- A domain owns its business rules and persistence model.
- One domain must not directly read or write another domain's persistence tables/items as an integration shortcut.
- Cross-domain integration must use explicit application contracts, commands, queries, or domain events as documented.
- Domain code must not depend directly on AWS SDKs, HTTP frameworks, or persistence implementations.

### Accounting

- Posted journals are immutable.
- Corrections use reversal/correction entries rather than mutation.
- Every posted journal must balance: total debit == total credit.
- Event-driven accounting consumers must be idempotent and traceable to their source event/transaction.

### Inventory

- Inventory invariants must be concurrency-safe.
- `Available = OnHand - Reserved` unless a later ADR changes the model.
- Never rely on read-then-write logic where a conditional write/transaction is required to protect stock correctness.

### Payments

- Only the internal Mock Payment Provider is used until an explicit later decision.
- Payment retries require idempotency.
- Timeout is not assumed to mean payment failure; ambiguous outcomes require query/reconciliation behavior.
- Never store real card data or secrets in repository fixtures.

### Events and async consumers

- Consumers must assume at-least-once delivery and therefore be idempotent where side effects are possible.
- Domain events must carry: `eventId`, `eventType`, `eventVersion`, `tenantId` when applicable, `aggregateId`, `occurredAt`, `correlationId`, and `causationId` when applicable.
- Do not publish vague technical events when a meaningful business event exists.

### Product-data ingestion

- External source snapshots are not the merchant's canonical catalog.
- Crawling/adapters must respect source-specific policy, robots/terms review, rate limits, kill switches, and parser fixtures.
- Prefer official APIs where available and permitted.

### AWS and cost

- Do not add a new AWS managed service without documenting why it is needed.
- Material architecture additions require an ADR and cost impact review.
- Avoid always-on infrastructure when a serverless/pay-per-use alternative satisfies the requirement.
- No NAT Gateway, ALB, EC2, or always-on relational database may be introduced casually.

## 3. Codex model and worktree policy

CommerceOS uses a **Luna-first** Codex policy.

- Use Luna by default for implementation, tests, routine refactoring, documentation, straightforward CDK work, and normal review.
- Escalate to a stronger reasoning model for business/domain design, architecture/ADR decisions, security/tenant model design, accounting semantics, payment ambiguity/idempotency, difficult concurrency/distributed-system reasoning, or high-risk review.
- Do not escalate simply because a task is large; escalate when reasoning difficulty or consequence of a wrong decision is high.
- Prefer using expensive reasoning to define the decision/task/invariants once, then let Luna execute the encoded decision repeatedly.

Parallel writable work follows:

> **one writable task = one branch = one worktree**

The primary `main` checkout is the integration/control checkout. Normal agents do not implement feature code directly on `main`.

Default concurrency is at most two active Builder-style coding tasks, and only when task boundaries/contracts are sufficiently independent.

See `docs/development/14-codex-multi-agent-and-worktrees.md` for worktree creation, review isolation, local port/resource isolation, AWS preview isolation, prompts, and cleanup.

## 4. Task discipline

Every non-trivial change should be tied to a task specification using `tasks/TASK-TEMPLATE.md`.

Respect:

- Goal
- Business context
- In scope
- Out of scope
- Acceptance criteria
- Architecture/security/cost/test impact

Do not expand scope because an adjacent improvement looks useful. Record follow-up work separately.

## 5. Definition of Done

A task is not complete merely because code compiles.

Follow `docs/development/02-definition-of-done.md`.

At minimum:

- acceptance criteria are satisfied;
- relevant tests are added/updated;
- tenant and security impact is checked;
- failure/idempotency behavior is considered for distributed operations;
- architecture boundaries still hold;
- documentation/ADR is updated when required;
- repository verification passes.

## 6. Verification

Run the repository verification command before declaring completion:

```bash
python3 scripts/harness_check.py
```

As implementation is added, this command will become the single entry point that orchestrates build, lint, unit, integration, architecture, IaC, and security checks.

Never bypass or delete a failing guardrail simply to make the task pass. Fix the product or explicitly change the rule through the documented architecture/harness process.

## 7. Architecture decisions

Use `docs/adr/ADR-000-template.md` when a task changes a significant architectural decision, including:

- new AWS managed service;
- new persistence technology;
- new cross-domain integration mechanism;
- new public/external contract;
- changed tenant-isolation model;
- changed accounting integrity model;
- material cost/reliability/security trade-off.

## 8. Harness improvement rule

When a defect or repeated agent mistake is found, do both:

1. fix the immediate product defect;
2. ask why the harness allowed the defect and improve at least one of: instruction, test, architecture rule, template, linter/check, fixture, or documentation when practical.

The goal is that recurring classes of mistakes become progressively harder to reintroduce.

## 9. Completion summary

When finishing work, report briefly:

- what changed;
- which acceptance criteria were satisfied;
- verification run and result;
- architecture/security/cost implications;
- follow-up items that remain out of scope.
