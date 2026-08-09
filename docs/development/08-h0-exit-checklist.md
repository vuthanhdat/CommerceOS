# CommerceOS — Phase H0 Exit Checklist

Phase H0 precedes **Phase 0 — Repository & AWS foundation** in `docs/07-delivery-roadmap.md`.

Its purpose is to ensure that AI-assisted development begins with repository-level context, constraints, verification, and feedback loops rather than adding them after the codebase has already drifted.

## H0 deliverables

### Agent-readable context

- [x] Root `AGENTS.md` exists and routes agents to product/architecture/task context.
- [x] Product definition, NFRs, domains, architecture, cost, crawler, payment, and roadmap docs exist.
- [x] Architecture rules are explicit.
- [x] ADR process and template exist.

### Task discipline

- [x] Task specification process exists.
- [x] Task template exists.
- [x] In-scope / out-of-scope separation is mandatory.
- [x] Acceptance criteria are expected to be machine-testable where practical.
- [x] Definition of Done exists.

### Verification and guardrails

- [x] Repository-level harness check exists.
- [x] CI runs the same harness check.
- [x] Pull-request checklist references task, tests, architecture, tenant/security, cost, and harness impact.
- [ ] Application build/lint/unit/integration checks are connected to the single verification entry point — deferred until Phase 0 establishes the concrete application toolchain.
- [ ] Architecture tests are executable against code — deferred until the first domain assemblies/modules exist.

### Agent workflow

- [x] Builder workflow is documented.
- [x] Reviewer workflow is documented.
- [x] Human review role is defined.
- [x] Guardrail bypass is explicitly prohibited.

### Harness improvement

- [x] Defects are classified as possible harness failures, not only implementation failures.
- [x] Criteria exist for when a defect should become a reusable guardrail.
- [x] Harness-audit questions are documented.

## H0 exit decision

H0 is considered **operationally complete enough to begin Phase 0** when all checked items above are present and `python3 scripts/harness_check.py` passes in CI.

The unchecked application-specific items do not block H0 because the code/toolchain does not exist yet. They become Phase 0 deliverables and must be wired into the same verification command before Phase 1 business implementation is considered mature.

## First validation task

The first Phase 0/Phase 1 vertical slice should be treated as a live harness evaluation.

During that task record:

1. Which repository instructions the agent failed to discover.
2. Which acceptance criteria were ambiguous.
3. Which verification steps required manual intervention.
4. Which review comments could have been automated.
5. Which architecture/security rules should become tests.

The goal is not to freeze the H0 harness. The goal is to establish a stable feedback mechanism that improves with every milestone.
