# TASK-XXXX — <Short task title>

Status: Backlog
Specification maturity: Outline
Owner: <human/agent>
Created: YYYY-MM-DD
Depends on: <TASK-XXXX, ADR-XXX, domain/architecture baseline, or N/A>

> `Backlog` does not mean implementation-ready. A Builder may take this task only when `Specification maturity: Ready` and all dependencies/gates are satisfied.

## Goal

Describe one observable outcome from the user/system perspective.

## Business context

Why is this capability needed? Which business/domain rules matter?

## Planning readiness

- Owning domain/bounded context:
- Domain invariants required by this task:
- Aggregate/entity/value-object decisions resolved? Yes/No/N/A — references:
- State/error semantics resolved? Yes/No/N/A — references:
- Cross-domain ownership/contracts resolved? Yes/No/N/A — references:
- Module/layer ownership resolved? Yes/No/N/A — references:
- Sync/async interaction decision resolved? Yes/No/N/A — references:
- Transaction/consistency boundary resolved? Yes/No/N/A — references:
- Persistence ownership/access patterns resolved? Yes/No/N/A — references:
- Material ADRs accepted? Yes/No/N/A — references:
- Remaining planning blockers:

Use `docs/development/15-planning-factory-and-task-maturity.md` before changing maturity to `Ready`.

## In scope

- ...

## Out of scope

- ...

## Acceptance criteria

### AC01 — <name>

Given ...
When ...
Then ...

### AC02 — <name>

Given ...
When ...
Then ...

## Architecture impact

- Owning domain:
- Domains touched:
- Persistence impact:
- Events/contracts impact:
- AWS/IaC impact:
- ADR required? Yes/No — why:

## Security and tenant impact

- Authentication:
- Authorization:
- Tenant scoping:
- Sensitive data/secrets:
- Abuse/rate-limit considerations:

## Reliability and idempotency impact

- Retry behavior:
- Timeout semantics:
- Duplicate-delivery behavior:
- Idempotency key/strategy:
- DLQ/recovery/reconciliation:

Use `N/A` with a short reason when not applicable.

## Observability impact

- Logs:
- Metrics:
- Traces/correlation:
- Operational states/errors:

## Cost impact

- Request/compute impact:
- Storage impact:
- Network impact:
- New AWS resources/services:
- Free Tier allowance relevant to this task:
- Expected monthly cost change or `negligible` with rationale:
- Estimated one-off cloud-test/load-test cost, if any:

## Test plan

- Unit:
- Integration:
- Architecture:
- Contract:
- IaC:
- E2E/manual:
- **Cloud verification required?** Yes/No — why:
- AWS environment/stack(s) required:
- Preview/staging teardown plan:

A task that changes IAM, API Gateway integration, Lambda packaging/runtime configuration, Cognito, DynamoDB infrastructure/access behavior, SQS/EventBridge/Step Functions semantics, S3 policies/events/lifecycle, or material CDK resources should normally require selected real-AWS verification before it is considered release-ready.

Do not deploy a full AWS preview merely because a task exists. Cloud verification must be proportional to the changed behavior and respect `docs/development/13-free-tier-and-credit-guardrails.md`.

## Implementation notes

Optional notes discovered during implementation. Do not use this section to silently expand scope or replace an unresolved planning decision.

If a Builder discovers a material unresolved business/domain/architecture decision, stop with `BLOCKED — PLANNING DECISION REQUIRED` and route it to the appropriate planning role.

## Completion summary

Fill before moving to `tasks/completed/`.

### What changed

- ...

### Verification

- `python3 scripts/harness_check.py`: PASS/FAIL
- local implementation checks:
- cloud verification: PASS/FAIL/N/A — environment and evidence:
- ephemeral resource teardown: PASS/N/A

### Acceptance criteria status

- AC01: PASS/FAIL
- AC02: PASS/FAIL

### Architecture/security/cost notes

- ...

### Harness improvement

What reusable harness improvement was made, if any? If none, state `None required`.

### Follow-up tasks

- ...
