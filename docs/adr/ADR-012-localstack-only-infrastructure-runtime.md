# ADR-012 — LocalStack-only infrastructure runtime

Status: Accepted
Date: 2026-08-11
Decision owner: CommerceOS human maintainer
Supersedes: real-AWS deployment/validation assumptions in ADR-001 and environment/deployment guidance; does not supersede capability, domain-boundary, persistence-ownership, or integration semantics that remain valid under LocalStack.

## Context

CommerceOS is a learning-first serverless architecture project. The project will no longer use a real AWS account for development, staging, validation, or deployment because cloud-account cost and operational risk are not justified for the learning goal.

The existing architecture intentionally uses AWS-style capabilities such as Lambda, API Gateway, DynamoDB, SQS, EventBridge, Step Functions, S3, CloudWatch, and Cognito. Many architectural decisions concern capability boundaries, consistency, failure handling, idempotency, tenant isolation, and module ownership rather than the physical AWS account in which those capabilities run.

The project therefore needs to preserve those architectural lessons while removing every requirement to provision, authenticate to, validate against, or control cost in a real AWS account.

## Decision

LocalStack is the default and only infrastructure/runtime target for CommerceOS development, integration testing, staging-like validation, and deployment exercises.

AWS-style services may continue to be used when LocalStack supports the required learning scenario. Service names in architecture documents identify capability mappings, not a requirement for AWS-hosted execution.

Application and domain code must not depend directly on LocalStack-specific APIs, endpoints, credentials, environment variables, or SDK wrappers. Runtime differences are configuration and infrastructure-adapter concerns.

Infrastructure configuration must own at least:

- service endpoint/base URL overrides;
- synthetic access key/secret values required by SDKs or tooling;
- region;
- account-id placeholders where tooling requires them;
- resource naming prefixes;
- LocalStack feature flags/edition-dependent capability switches;
- bootstrap/reset data and lifecycle settings.

No acceptance criterion may require access to a real AWS account. No task may require AWS account provisioning, AWS IAM/OIDC federation, AWS Budget configuration, real-cloud preview environments, cloud cost approval, or real-cloud teardown evidence.

## Capability-first architecture rule

Technical contracts describe the required capability first, for example:

- durable key-value persistence with conditional writes and bounded transactions;
- at-least-once work queue with retry and DLQ behavior;
- durable fact routing/fan-out;
- durable workflow orchestration with wait/retry/branch semantics;
- object storage;
- identity evidence at the delivery edge;
- logs/metrics/traces.

A LocalStack service mapping is then selected only when it supports the capability sufficiently for the named task.

Domain and Application layers depend on project-owned ports/contracts. Infrastructure adapters may use AWS SDKs configured for LocalStack. LocalStack-specific branching is prohibited in Domain/Application code.

## Service mapping

Current preferred mappings, subject to named-task validation:

| Capability | LocalStack/AWS-style mapping | Rule |
|---|---|---|
| HTTP/serverless delivery | API Gateway + Lambda where supported | direct local host remains valid for fast tests; transport is not domain authority |
| identity edge | Cognito where supported, otherwise a test identity adapter behind the same project contract | never weaken trusted tenant-context rules merely to fit emulator behavior |
| module persistence | DynamoDB | preserve ADR-005 ownership, conditional-write and transaction semantics |
| work queue / DLQ | SQS | consumers remain at-least-once/idempotent |
| fact routing | EventBridge | use only for named producers/consumers under ADR-006 |
| durable workflow | Step Functions Standard semantics | ADR-010 scope remains unchanged; document emulator gaps explicitly |
| object storage | S3 | FilesMedia business rules remain unchanged |
| logs/metrics | CloudWatch-style APIs where useful | emulator observability is operational evidence, not business truth |
| IaC | AWS CDK/CloudFormation-compatible flow through LocalStack tooling | repository remains infrastructure source of truth |

A missing or behaviorally different LocalStack feature is a documented platform limitation. It must not be silently treated as AWS-equivalent evidence.

## Environment model

CommerceOS uses local runtime profiles rather than cloud accounts:

```text
local-fast
  -> unit/architecture/contract tests, direct process adapters where useful

localstack-dev
  -> persistent developer learning environment

localstack-test
  -> isolated integration-test environment, disposable/resettable

localstack-stage
  -> production-shaped local validation profile when a distinct stage is useful
```

These are configuration profiles over local infrastructure, not AWS accounts.

## Infrastructure lifecycle

The repository must provide or evolve toward deterministic commands for:

1. start LocalStack and required companion processes;
2. wait for health/readiness;
3. deploy/bootstrap version-controlled infrastructure;
4. seed only required technical/bootstrap data;
5. run integration/E2E/failure tests;
6. inspect diagnostic evidence;
7. reset state for deterministic reruns;
8. stop/remove local infrastructure and volumes when a clean slate is required.

Tests must not depend on manually created LocalStack resources.

Parallel worktrees/tasks must isolate resource names and ports through configuration when they run concurrently.

## Integration-testing strategy

A task that depends on infrastructure semantics validates against LocalStack when the required feature is supported sufficiently.

Required test categories remain:

- tenant isolation;
- conditional/concurrent persistence behavior;
- idempotent duplicate message/event handling;
- retry/DLQ behavior;
- event routing/versioning;
- workflow branch/retry/wait/Unknown semantics where supported;
- bootstrap/reset reproducibility;
- configuration isolation between task instances.

When LocalStack differs from AWS or lacks the feature, the task must:

1. record the exact limitation;
2. test the project-owned contract and failure behavior at the nearest reliable layer;
3. avoid claiming AWS-equivalence;
4. keep application/domain code portable by preserving the capability boundary.

No real-AWS verification fallback is required or permitted by default.

## ADR impact

- ADR-001 remains valid for AWS CDK as the repository IaC source of truth, but its AWS-account deployment, IAM-role/OIDC, account bootstrap, preview/staging, and cost-control assumptions are superseded by this ADR.
- ADR-002 remains valid for toolchain, repository shape, modular boundaries, and CDK language. Lambda runtime references are capability/toolchain choices, not evidence that AWS-hosted deployment is required.
- ADR-003 through ADR-011 remain valid unless a specific clause requires real AWS hosting. Their module, tenant, persistence, reliable-integration, contract, onboarding, entitlement, orchestration, and refund semantics are unchanged.
- Any later ADR that requires a real AWS account must explicitly supersede this ADR and requires a new human architecture decision.

## Alternatives considered

### Continue using a real AWS account for dev/staging/validation

Rejected for the current project because account provisioning, IAM/OIDC, billing exposure, and cloud cleanup add operational/cost risk without being necessary for the learning objective.

### Use only in-memory/local test doubles

Rejected as the sole infrastructure strategy because the project intentionally learns serverless service integration, IaC, queues, events, workflows, object storage, and infrastructure lifecycle behavior that simple test doubles cannot exercise sufficiently.

### Use LocalStack as the default runtime while retaining optional AWS fallback

Rejected. An implicit AWS fallback would keep cloud credentials/cost/authorization as hidden readiness dependencies. Any future real-AWS target must be an explicit new architecture decision that supersedes this ADR.

### Replace AWS-style services with unrelated local technologies

Not selected as the default because doing so would reduce fidelity to the intended AWS-style serverless learning model. Such substitutions may be used only behind project-owned capability contracts when LocalStack cannot support a required scenario and the limitation is documented.

## Security and tenant impact

- Removing real AWS accounts eliminates the need for real deployment credentials, IAM role provisioning, and cloud-account authorization in the normal workflow.
- Synthetic LocalStack credentials are configuration only and must never be treated as real secrets or business authority.
- Existing tenant isolation, TrustedTenant contexts, privileged support/admin rules, and non-disclosing cross-tenant behavior remain unchanged.
- LocalStack endpoints, credentials, account placeholders, and feature switches cannot leak into Domain/Application contracts.
- Emulator convenience must never weaken authentication/authorization semantics; where Cognito behavior is insufficient, a test identity adapter must preserve the same project-owned trust contract.

## Reliability and operability impact

- The repository must provide deterministic start/readiness/bootstrap/deploy/smoke/reset/redeploy behavior for infrastructure-sensitive work.
- LocalStack state is disposable operational/testing state and must not become hidden business authority.
- Parallel worktrees/tasks must isolate mutable resource names, ports, and state or declare explicit serialization constraints.
- Unsupported, partial, edition-dependent, or behaviorally different emulator behavior must be visible in task evidence and limitation documentation.
- LocalStack verification proves the project-owned capability contract only to the extent actually exercised; it does not prove AWS control-plane quotas, performance, IAM edge cases, or managed-service operations.

## Cost impact

There is no AWS account cost model, AWS Budget requirement, Free Tier gate, or cloud-spend approval for CommerceOS under this decision.

Local machine resource usage and any LocalStack licensing/edition constraint are operational/tooling concerns and must be documented when they affect reproducibility.

## Consequences

Positive:

- no AWS billing exposure or account-provisioning dependency;
- deterministic local lifecycle and easier destructive reset;
- AWS-style serverless concepts remain available for learning;
- task execution no longer stalls on cloud authorization/account/region/budget inputs;
- infrastructure tests can be automated locally and in suitable CI environments.

Trade-offs:

- LocalStack is not guaranteed to match AWS semantics exactly;
- some services/features may be unavailable or edition-dependent;
- performance, quotas, IAM edge cases, managed-service operational behavior, and true AWS control-plane behavior are not validated;
- documentation must clearly distinguish architecture capability from emulator evidence.

## Reversibility / migration

This decision is reversible only through a new explicit human architecture decision and ADR that supersedes ADR-012.

Moving to real AWS later would require a deliberate migration plan covering account/environment topology, authentication and deployment credentials, IAM/OIDC, configuration/endpoint removal, service compatibility gaps, cost controls, cloud verification, state/data migration where applicable, and updated CI/CD semantics.

Moving from LocalStack to another emulator/local runtime would likewise require validating each capability mapping, updating infrastructure adapters/configuration, preserving Domain/Application independence, and documenting compatibility differences.

Because application/domain code depends on project-owned capabilities rather than LocalStack-specific APIs, the current boundary is intended to keep such migration technically possible.

## Validation

The architecture is reconciled when:

- no current Ready/Refined task requires a real AWS account;
- LocalStack lifecycle/bootstrap/reset is the default infrastructure path;
- infrastructure endpoints/credentials/region are configuration concerns;
- Domain/Application code remains LocalStack-independent;
- unsupported/different emulator behavior is explicitly surfaced;
- obsolete AWS account, IAM/OIDC, Budget, preview/staging and cloud-validation gates are removed or superseded.
