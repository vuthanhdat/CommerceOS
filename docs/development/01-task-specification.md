# CommerceOS — Task Specification

## 1. Why every non-trivial change needs a task

AI agents perform better when scope, business intent, constraints, and acceptance criteria are explicit and stored in the repository.

A task is the contract between product intent and implementation.

Tasks live under:

```text
tasks/
├── backlog/
├── active/
└── completed/
```

Git does not preserve empty folders, so they are created naturally as tasks move through the workflow.

Use `tasks/TASK-TEMPLATE.md`.

---

## 2. Task lifecycle

```text
backlog
   ↓
active
   ↓
implementation
   ↓
verification
   ↓
review
   ↓
completed
```

A task should move to `active` only when implementation is intended to begin.

Only one logical scope should be represented by a task. If implementation discovers unrelated work, record a follow-up task instead of silently expanding scope.

---

## 3. Required task sections

### Goal

One observable outcome, written from the system/user perspective.

Bad:

> Implement DynamoDB repository.

Better:

> Merchant staff can create a product owned by the authenticated tenant and retrieve it later.

### Business context

Explain why the capability exists and which domain rules matter.

### In scope

Explicit implementation boundary.

### Out of scope

Explicit anti-scope-creep boundary.

### Acceptance criteria

Prefer Given/When/Then or other deterministic statements that can become tests.

### Architecture impact

State whether domain boundaries, events, persistence, external contracts, workflows, or AWS infrastructure change.

### Security and tenant impact

Describe identity, authorization, tenant scoping, sensitive-data handling, and abuse concerns.

### Reliability/idempotency impact

Required for async workflows, external calls, payments, queues, events, retries, or other distributed operations.

### Observability impact

State required logs, metrics, traces, correlation IDs, or operational states.

### Cost impact

State whether the task changes request volume, storage, compute duration, workflow transitions, network transfer, or introduces an AWS service.

### Test plan

List unit/integration/architecture/IaC/manual verification expected.

---

## 4. Acceptance-criteria quality

Prefer criteria that a machine can verify.

Example:

```text
AC01
Given a user authenticated for Tenant A
when they create a product
then the persisted product is scoped to Tenant A.

AC02
A tenantId supplied in the request body cannot override the authenticated tenant.

AC03
Tenant B cannot retrieve Tenant A's product by guessing its productId.

AC04
Blank product name returns a validation error.

AC05
Repository verification passes.
```

Avoid:

- "works correctly";
- "secure";
- "good performance";
- "follows best practices".

Those statements are too vague unless converted into measurable criteria.

---

## 5. Task completion record

When moving a task to `completed`, append a short completion section containing:

- implementation summary;
- verification result;
- important architecture/security/cost decisions;
- follow-up tasks intentionally left out;
- harness improvement made, if any defect/friction revealed a reusable gap.
