# CommerceOS — Phase H0 Exit Checklist

Phase H0 precedes the **Phase 0 — Repository & LocalStack foundation** under the current ADR-012 runtime decision.

Its purpose is to ensure AI-assisted development begins with repository-level context, constraints, verification, and feedback loops before business implementation expands.

## H0 deliverables

### Agent-readable context

- [x] Root `AGENTS.md` routes agents to product/architecture/task context.
- [x] Product definition, NFRs, domains, architecture, runtime, crawler, payment, and roadmap docs exist.
- [x] Architecture rules are explicit.
- [x] ADR process and template exist.

### Task discipline

- [x] Task specification process exists.
- [x] Task template exists.
- [x] In-scope / out-of-scope separation is mandatory.
- [x] Acceptance criteria are machine-testable where practical.
- [x] Definition of Done exists.

### Verification and guardrails

- [x] Repository-level harness check exists.
- [x] CI runs the same harness check.
- [x] Pull-request/task guidance covers task, tests, architecture, tenant/security, runtime, and harness impact.
- [x] Application build/lint/unit/architecture/IaC checks are connected to the verification entry point.
- [x] Architecture tests enforce initial dependency rules.
- [x] ADR-012 defines LocalStack-only infrastructure targeting and prohibits stale real-AWS execution gates.

### Agent workflow

- [x] Builder workflow is documented.
- [x] Reviewer workflow is documented.
- [x] Human review role is defined.
- [x] Guardrail bypass is prohibited.

### Harness improvement

- [x] Defects are classified as possible harness failures, not only implementation failures.
- [x] Criteria exist for when a defect should become a reusable guardrail.
- [x] Harness-audit questions are documented.

## H0 exit decision

H0 is operationally complete enough to begin Phase 0 when the checked repository-level mechanisms exist and `python3 scripts/harness_check.py` passes in CI.

Phase 0 TASK-0003 connected the concrete .NET/React/CDK toolchain and executable architecture tests to the same verification command. TASK-0094 now owns the LocalStack foundation lifecycle/bootstrap/reset proof after ADR-012.

## First validation task

Early implementation slices should remain live harness evaluations. Record:

1. which repository instructions the agent failed to discover;
2. which acceptance criteria were ambiguous;
3. which verification steps required manual intervention;
4. which review comments could have been automated;
5. which architecture/security/runtime rules should become tests;
6. which LocalStack limitations need explicit reusable documentation or test adapters.

The goal is not to freeze the harness. The goal is a stable feedback mechanism that improves with every milestone.
