# CommerceOS — H0 Engineering Harness

## 1. Purpose

CommerceOS uses **Harness Engineering** to make AI-assisted development progressively more reliable.

The repository itself should provide enough context, constraints, tools, tests, and feedback that an agent can understand a task, implement it, verify it, and explain the result without depending on fragile conversational memory.

The target development loop is:

```text
Human intent
   ↓
Task specification
   ↓
Agent reads repository context
   ↓
Implementation
   ↓
Mechanical verification
   ↓
Independent/self review
   ↓
Human product validation
   ↓
Merge
   ↓
Failure analysis → harness improvement
```

The repository, not the agent's confidence, is the primary quality mechanism.

---

## 2. Harness layers

### Layer 1 — Knowledge

Source-of-truth documentation:

- product definition;
- non-functional requirements;
- business domains;
- architecture;
- cost model;
- ADRs;
- domain-specific documentation.

### Layer 2 — Instructions

- root `AGENTS.md` as repository constitution/router;
- nested `AGENTS.md` files when a domain needs additional local rules.

### Layer 3 — Task specification

Every non-trivial change has an explicit task containing:

- goal;
- business context;
- scope/out-of-scope;
- acceptance criteria;
- architecture/security/cost impact;
- test plan.

### Layer 4 — Tooling

A small number of predictable commands should let humans and agents verify the repository.

H0 starts with:

```bash
python3 scripts/harness_check.py
```

The command will expand as code is introduced.

### Layer 5 — Guardrails

Rules that are important enough should move from prose into executable checks where practical:

- architecture dependency tests;
- tenant-isolation integration tests;
- accounting invariants;
- event-contract checks;
- idempotency tests;
- IaC validation;
- security/static analysis.

### Layer 6 — Evaluation

Tests verify code. Evals verify that the development harness guides an agent toward correct system decisions.

Examples later:

- cross-tenant product lookup must derive tenant from trusted context;
- editing a posted journal should result in reversal/correction design;
- payment timeout retry must consider idempotency and unknown outcome;
- new AWS service addition should trigger ADR/cost analysis.

### Layer 7 — Feedback loop

A defect is also a harness signal.

For meaningful defects ask:

1. What failed?
2. Why was the implementation able to pass existing checks?
3. Was context missing, ambiguous, or not machine enforced?
4. What reusable guardrail prevents the same class of defect?

---

## 3. H0 deliverables

H0 is complete when the repository contains and uses:

- `AGENTS.md`;
- task template and task lifecycle;
- Definition of Done;
- architecture rules;
- testing strategy;
- ADR policy/template;
- agent execution/review workflow;
- harness-improvement process;
- repository verification command;
- CI harness verification;
- PR checklist;
- delivery roadmap updated so H0 precedes AWS/business implementation.

---

## 4. Maturity model

### H1 — Agent-readable

The agent can discover product context, architecture rules, and task scope from the repository.

### H2 — Agent-verifiable

The repository exposes a deterministic verification command covering build/test/lint and structural checks.

### H3 — Agent-guarded

Critical invariants are mechanically enforced where possible.

### H4 — Agent-reviewable

The direct-working agent self-reviews against repository checks, and the human may request an
independent review when risk or uncertainty justifies it.

### H5 — Agent-driven

An agent can take a well-defined task from specification through implementation, verification, review preparation, documentation, and PR creation with minimal human intervention.

CommerceOS does **not** attempt to start at H5. The harness grows with the product.

---

## 5. Operating principle

For every feature:

```text
Business requirement
       ↓
Task specification
       ↓
Implementation
       ↓
Verification
       ↓
Review
       ↓
Observed failure or friction?
       ↓ yes
Harness improvement
```

We should prefer improving reusable repository mechanisms over repeatedly writing longer prompts for the same problem.
