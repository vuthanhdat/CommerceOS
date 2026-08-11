# CommerceOS — Agent Development Workflow

## 1. Goal

The default workflow separates **intent, implementation, verification, and review** so AI output is constrained by repository evidence rather than confidence.

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

## 2. Builder workflow

### Step 1 — Resolve scope

Read the active task and relevant product/domain/architecture docs. Understand goal, acceptance criteria, in/out-of-scope boundaries, and invariants before editing.

### Step 2 — Inspect current implementation

Identify owning domain, existing contracts/patterns, closest tests, relevant ADRs, and reusable fixtures/tools. Prefer extending a coherent pattern over inventing a parallel one.

### Step 3 — Plan the smallest vertical change

Make the acceptance criteria true with the least architecture change necessary. If a material architectural mechanism is missing, return to Technical Architect/ADR rather than deciding it silently as Builder.

### Step 4 — Implement

Keep business rules in Domain/Application. Keep persistence, AWS SDK usage, LocalStack endpoints/configuration, transport, and emulator-specific concerns at Infrastructure/Delivery boundaries.

Do not bypass tenant, idempotency, accounting, inventory, event, or provider-ambiguity rules for expedience.

### Step 5 — Verify continuously

Run focused tests while developing, then the repository verification command before completion. Run task-declared LocalStack verification when infrastructure semantics are affected.

### Step 6 — Self-review

Review the diff against:

1. acceptance criteria and scope;
2. domain/module ownership;
3. tenant isolation/authorization;
4. failure/retry/idempotency/Unknown semantics;
5. accounting/inventory invariants;
6. observability;
7. infrastructure capability and LocalStack configuration boundaries;
8. bootstrap/reset/resource isolation when relevant;
9. known LocalStack limitations and unsupported behavior;
10. unnecessary complexity/dead code;
11. documentation/ADR requirements.

### Step 7 — Completion summary

Report what changed, verification evidence, acceptance criteria, architecture/security/runtime implications, LocalStack limitations/reset evidence where applicable, and intentional follow-up work.

## 3. Reviewer workflow

The reviewer assumes the Builder may have made a locally reasonable but systemically wrong decision.

Review product correctness, scope, invariants, distributed failure behavior, operability, security, architecture/runtime boundaries, test quality, and emulator limitations.

For distributed/external operations ask what happens on timeout, duplicate/out-of-order delivery, partial completion, retry, poison message, process restart, and reconciliation.

For infrastructure changes ask whether the capability is justified, LocalStack-specific details are confined to configuration/adapters, lifecycle is reproducible, and exact AWS compatibility is not overclaimed.

## 4. Human role

Human review should focus increasingly on product value, architecture trade-offs, learning goals, and sequencing rather than manually rediscovering structural rules the harness can enforce.

Any future return to real AWS is a human architecture decision under ADR-012, not a Builder convenience choice.

## 5. No silent guardrail bypass

If a check blocks implementation, understand the guardrail and fix the implementation when the rule is valid. If the rule is obsolete, change it explicitly with rationale and relevant architecture/harness documentation.

A green pipeline obtained by unjustifiably weakening the harness is a failed task.
