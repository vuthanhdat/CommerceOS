# CommerceOS — Harness Improvement Loop

## 1. Principle

A recurring defect or agent failure is evidence that the repository may be missing context, a guardrail, a tool, or a test.

Fixing the immediate code is necessary but may not be sufficient.

Use this loop:

```text
Observed defect / repeated friction
        ↓
Fix immediate issue
        ↓
Classify why harness allowed it
        ↓
Add reusable prevention/detection
        ↓
Verify the new guardrail
```

---

## 2. Failure classification

### Missing knowledge

The correct business/architecture rule was not documented or discoverable.

Possible improvement:

- product/domain doc;
- ADR;
- domain README;
- nested `AGENTS.md`.

### Ambiguous instruction

The rule exists but is too vague to guide implementation.

Possible improvement:

- rewrite instruction into explicit invariant;
- add examples/anti-examples;
- link it from the task/agent router.

### Missing test

The system behavior was wrong but no test represented the expected behavior.

Possible improvement:

- regression test;
- reusable test fixture;
- mandatory scenario family.

### Missing structural guardrail

A prohibited dependency/pattern repeatedly appears.

Possible improvement:

- architecture test;
- static analysis rule;
- repository check;
- CI policy.

### Missing tooling

The agent cannot reliably reproduce or inspect the problem.

Possible improvement:

- deterministic local setup;
- fixture generator;
- log/trace helper;
- failure-injection tool;
- one-command verification.

### Missing observability

The implementation failed correctly/incorrectly but the state could not be diagnosed.

Possible improvement:

- structured log;
- metric;
- trace/correlation ID;
- operational status/read model;
- DLQ/reconciliation tooling.

### Task specification failure

The task allowed multiple incompatible interpretations or scope creep.

Possible improvement:

- stronger acceptance criteria;
- explicit out-of-scope;
- architecture/security/cost questions in template.

---

## 3. Harness change criteria

Not every typo deserves a new global rule.

Promote a failure into a harness improvement when at least one is true:

- the failure risks tenant/security/financial correctness;
- the failure can silently corrupt state;
- the failure is likely to recur across domains/tasks;
- multiple agents/humans have made the same mistake;
- manual review repeatedly checks the same mechanical condition;
- detecting it automatically is cheap and reliable.

---

## 4. Examples

### Cross-tenant read defect

```text
Defect
Tenant B reads Tenant A product

Immediate fix
Scope repository/API by authenticated tenant

Harness improvement
Cross-tenant integration fixture + repository contract rule
```

### Duplicate accounting posting

```text
Defect
EventBridge redelivery creates duplicate journal

Immediate fix
Idempotent accounting handler

Harness improvement
Mandatory duplicate-event test + source-event uniqueness invariant
```

### Unsafe payment retry

```text
Defect
Timeout triggers a second payment attempt

Immediate fix
Query/reconcile unknown payment state + idempotency key

Harness improvement
Mock-provider timeout-after-commit fixture + payment review checklist
```

### Architecture drift

```text
Defect
Accounting handler queries Sales table directly

Immediate fix
Carry required event contract / read model

Harness improvement
Cross-domain dependency architecture test + explicit rule
```

---

## 5. Harness audit cadence

Perform a lightweight harness audit:

- after a meaningful production-like defect;
- after a repeated review comment class;
- after each major milestone;
- before increasing agent autonomy.

Questions:

1. Which mistakes repeated?
2. Which review checks are still manual but mechanical?
3. Which docs are agents failing to discover?
4. Which tests are flaky or too slow to be useful feedback?
5. Which architecture rules remain prose-only despite repeated violations?
6. Which tools/fixtures would shorten diagnosis?
7. Can any guardrail be simplified rather than merely adding more instructions?

The harness should become clearer and more executable over time, not simply larger.
