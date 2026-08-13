# CommerceOS — Direct AI Development Workflow

## Goal

The default workflow is a direct conversation between the human and the AI. Repository evidence,
tests, and architecture rules constrain implementation; ordinary work does not require separate
Planner, Builder, or Reviewer agents.

```text
Human request ↔ AI
                  ↓
          Inspect relevant context
                  ↓
             Implement
                  ↓
       Focused + repository checks
                  ↓
          Human validation
```

## Workflow

1. Resolve scope from the current conversation and relevant repository files.
2. Inspect the nearest implementation, tests, domain rules, and accepted ADRs.
3. State any assumption that materially affects behavior or architecture.
4. Implement the smallest coherent change directly in the current workspace.
5. Verify continuously with focused tests and finish with proportionate repository checks.
6. Self-review tenant isolation, domain ownership, failure behavior, idempotency, accounting and
   inventory invariants, observability, and LocalStack boundaries where applicable.
7. Report the result, verification evidence, risks, and intentionally deferred work.

## Human decisions

Ask the human when a missing choice would materially change product meaning, architecture, security,
cost, or external behavior. Do not invent such decisions merely to keep implementation moving.

Any future return to real AWS remains a human architecture decision under ADR-012.

## No silent guardrail bypass

If a check blocks implementation, fix the implementation when the rule is valid. If the rule is
obsolete, change it explicitly with rationale and update the relevant documentation or test.
