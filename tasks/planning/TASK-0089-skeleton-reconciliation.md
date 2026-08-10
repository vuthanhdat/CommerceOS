# TASK-0089 — Phase 0 Skeleton Reconciliation

_Status: completed planning audit, 2026-08-10._

## Scope

This audit compares the existing Phase 0 code skeleton with the currently approved CommerceOS technical architecture. It does not implement any business capability or pre-provision target AWS services.

## Skeleton elements that remain compatible

The following Phase 0 choices remain useful and should be preserved:

- .NET 10 solution/project foundation;
- technical `Platform` Domain/Application/Infrastructure projects as composition/readiness scaffolding only;
- `CommerceOS.Api` with anonymous health endpoint and no embedded business truth;
- React/TypeScript/Vite Storefront and Back Office foundations;
- AWS CDK application and `FoundationStack` with bounded CloudWatch logging/tagging;
- repository harness as the single local verification entry point;
- architecture/CDK/frontend test foundations;
- no speculative Cognito, business Lambda, business DynamoDB table, EventBridge, SQS, Step Functions, S3/CloudFront application resources before named Ready tasks justify them;
- no one-service/one-stack-per-bounded-context assumption.

These elements are consistent with the accepted modular serverless-monolith direction and cost posture.

## Explicit skeleton conflict

### SKEL-001 — Application architecture test is stricter than accepted Contracts rule

Current executable test:

`tests/CommerceOS.ArchitectureTests/DependencyRulesTests.cs`

contains `ApplicationProjectsReferenceOnlyTheirOwnDomainProject()`. It asserts that every project reference from a `*.Application` project must resolve to that module's own `*.Domain` project.

The accepted architecture rule now allows:

- an Application project to reference its own Domain project; and
- an explicitly approved **foreign producer-owned `*.Contracts`** project when a real cross-module/delivery consumer requires it;
- while still forbidding foreign Domain, foreign Application implementation, and foreign Infrastructure references.

The skeleton test therefore encodes a narrower rule than the approved architecture. Once the first legitimate cross-module Contracts reference is introduced, correct architecture would fail the existing executable guardrail.

The current suite also lacks mechanical guardrails for the `*.Contracts` project itself, such as prohibiting Domain entities, repository implementations, AWS SDK/framework/persistence types, and private implementation dependencies.

### Required remediation

Created canonical `TASK-0093 — Reconcile architecture-test contract rules`.

It is the first safe Ready frontier because:

- the architecture rule is already approved;
- the conflict exists in current code, not hypothetical future code;
- remediation is local-only test/harness work;
- it changes no business behavior, domain semantics, AWS resource, persistence model, or public contract;
- leaving it unresolved would make later correct cross-module work fight a stale harness.

TASK-0089 deliberately does **not** edit the test itself.

## Missing Phase 0 capabilities that are not skeleton conflicts

The repository has a local/synthesizable foundation but has not yet proven the roadmap's real-AWS Phase 0 exit conditions. This is missing planned work, not evidence that the code skeleton is architecturally wrong.

V2 represents this as:

- `TASK-0094` — real AWS dev FoundationStack/Budget deploy-destroy-redeploy proof; **Refined**, cloud/input gated;
- `TASK-0095` — GitHub OIDC, conditional preview and main-to-dev delivery; **Outline**, dependent on proven cloud foundation.

The Planner does not make these Ready merely because they are early-phase tasks. Real AWS execution needs explicit environment/notification inputs and cloud authorization under the project governance/cost rules.

## Things that must not be “reconciled” by speculative implementation

The target architecture contains future Cognito, API Gateway, module-owned DynamoDB tables, queues, EventBridge routes, Step Functions, S3/CloudFront and workers. Their absence in the Phase 0 skeleton is intentional.

Do **not** create remediation tasks simply to make a target architecture diagram visually complete. Resources appear only when a named Ready capability/consumer/workflow requires them.

Likewise, do not split the existing skeleton into microservices or one stack/table/Lambda per domain. The accepted target is a modular serverless monolith with operational deployment boundaries chosen from actual runtime pressure.

## Reconciliation verdict

| Check | Result |
|---|---|
| Repository/module foundation | Compatible |
| Frontend foundations | Compatible |
| CDK/FoundationStack posture | Compatible |
| Cost/no-speculative-resource posture | Compatible |
| Current architecture-test Application reference rule | **Conflict — remediation required (`TASK-0093`)** |
| Real AWS Phase 0 deployment proof | Missing capability, represented by `TASK-0094`; not a code conflict |
| OIDC/preview/dev delivery | Missing capability, represented by `TASK-0095`; not a code conflict |

**Skeleton reconciliation: PASS WITH EXPLICIT REMEDIATION.**
