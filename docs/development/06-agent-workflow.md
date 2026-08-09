# CommerceOS — Agent Development Workflow

## 1. Goal

The default workflow separates **intent, implementation, verification, and review** so that AI output is constrained by repository evidence rather than confidence.

```text
Human/product intent
        ↓
Task specification
        ↓
Builder agent
        ↓
Mechanical verification
        ↓
Self-review
        ↓
Independent review / CI
        ↓
Human product validation
        ↓
Merge
```

---

## 2. Builder workflow

### Step 1 — Resolve scope

Read the active task and relevant product/domain/architecture docs.

Restate internally:

- goal;
- acceptance criteria;
- in-scope/out-of-scope;
- invariants at risk.

Do not start by searching for files to edit before understanding the task.

### Step 2 — Inspect current implementation

Identify:

- owning domain;
- existing contracts/patterns;
- tests closest to the behavior;
- relevant ADRs;
- existing reusable fixtures/tools.

Prefer extending an existing coherent pattern over inventing a parallel one.

### Step 3 — Plan the smallest vertical change

The implementation should make the acceptance criteria true with the least architecture change necessary.

If a new architectural mechanism is required, trigger the ADR process.

### Step 4 — Implement

Keep business rules in the owning domain/application layer and AWS/framework concerns at infrastructure/delivery boundaries.

Do not bypass tenant, idempotency, accounting, inventory, or event rules for expedience.

### Step 5 — Verify continuously

Run focused tests while developing, then run the repository verification command before completion.

### Step 6 — Self-review

Review the diff against:

1. acceptance criteria;
2. out-of-scope boundary;
3. domain ownership/dependency rules;
4. tenant isolation and authorization;
5. failure/retry/idempotency semantics;
6. accounting/inventory invariants when applicable;
7. observability;
8. cost/infrastructure impact;
9. unnecessary complexity/dead code;
10. documentation/ADR requirements.

### Step 7 — Completion summary

Report:

- what changed;
- tests/checks run;
- acceptance criteria status;
- architecture/security/cost implications;
- intentional follow-up work.

---

## 3. Reviewer workflow

The reviewer should assume the builder may have made a locally reasonable but systemically wrong decision.

Review in this order:

### A. Product correctness

Does the diff satisfy the task's business goal and acceptance criteria?

### B. Scope correctness

Did the builder expand the task or introduce infrastructure/refactoring not required?

### C. Invariants

Check tenant isolation, accounting, inventory, payment, event, and domain-boundary invariants relevant to the change.

### D. Failure behavior

For distributed/external operations ask:

- what happens on timeout?
- duplicate delivery?
- partial completion?
- retry?
- poison message?
- process restart?
- reconciliation?

### E. Operability

Can failures be diagnosed? Are logs/metrics/statuses meaningful?

### F. Cost and security

Did resource/request/storage/security posture change unexpectedly?

### G. Test quality

Would the tests still pass if the implementation contained the most plausible regression?

---

## 4. Human role

Human review should increasingly focus on:

- whether we are building the right product capability;
- whether trade-offs match learning/product goals;
- whether a new architecture decision is justified;
- whether scope and sequencing remain sensible.

Humans should not have to manually rediscover every structural rule if the harness can enforce it mechanically.

---

## 5. No silent guardrail bypass

If a check blocks implementation:

- do not remove/disable the check by default;
- understand why it exists;
- fix implementation when the rule is valid;
- if the rule is obsolete, change it explicitly with rationale and relevant ADR/harness documentation.

A green pipeline obtained by weakening the harness without justification is a failed task.
