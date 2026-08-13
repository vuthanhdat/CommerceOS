# TASK-XXXX — <Short task title>

Status: Backlog
Specification maturity: Outline
Owner: <human/agent>
Created: YYYY-MM-DD
Depends on: <TASK-XXXX, ADR-XXX, domain/architecture baseline, or N/A>

> This template is optional planning support for direct human–AI work. The human may authorize
> implementation directly without an automated task lifecycle.

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
- Infrastructure capability/mapping resolved? Yes/No/N/A — references:
- LocalStack support/limitations understood? Yes/No/N/A — references:
- Material ADRs accepted? Yes/No/N/A — references:
- Remaining planning blockers:

Resolve material product and architecture decisions with the human before implementation.

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
- Infrastructure capability / LocalStack mapping impact:
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

## Local runtime/resource impact

- LocalStack services/capabilities used:
- LocalStack version/edition/feature assumptions:
- Endpoint/region/account-placeholder/port/resource-prefix configuration impact:
- CPU/memory/disk/CI runtime impact, if material:
- Persistent local state/volume impact:
- Known emulator limitations or AWS-behavior gaps:

## Test plan

- Unit:
- Integration:
- Architecture:
- Contract:
- IaC:
- E2E/manual:
- **LocalStack/infrastructure verification required?** Yes/No — why:
- LocalStack profile/stack(s)/service(s) required:
- Bootstrap/reset/cleanup plan:
- Limitation fallback test layer, if applicable:

A task that changes API Gateway/Lambda delivery wiring, Cognito integration, DynamoDB infrastructure/access behavior, SQS/EventBridge/Step Functions semantics, S3 integration, or material CDK resources should normally require selected LocalStack verification when the required behavior is sufficiently supported.

Do not treat LocalStack verification as proof of exact AWS behavior. Unsupported, partial, behaviorally different, or edition-dependent features must be documented and tested at the nearest reliable project-owned contract layer.

Under ADR-012, a task must not require a real AWS account, AWS IAM/OIDC deployment, AWS Budget/Free Tier controls, cloud authorization, or real-cloud preview/staging verification unless a later accepted ADR explicitly supersedes that decision.

## Implementation notes

Optional notes discovered during implementation. Do not use this section to silently expand scope or replace an unresolved planning decision.

If implementation discovers a material unresolved business/domain/architecture decision, explain
the decision and ask the human instead of guessing.

## Completion summary

Record this section manually when the task file is used. It is not controlled by an automated
Orchestrator lifecycle.

### What changed

- ...

### Verification

- `python3 scripts/harness_check.py`: PASS/FAIL
- local implementation checks:
- LocalStack/infrastructure verification: PASS/FAIL/N/A — profile and evidence:
- reset/cleanup: PASS/N/A
- known emulator limitations:

### Acceptance criteria status

- AC01: PASS/FAIL
- AC02: PASS/FAIL

### Architecture/security/runtime notes

- ...

### Harness improvement

What reusable harness improvement was made, if any? If none, state `None required`.

### Follow-up tasks

- ...
