# ADR-001 — AWS CDK as the Infrastructure as Code Source of Truth

Status: Accepted, amended by ADR-012
Date: 2026-08-09
Amended: 2026-08-11

## Context

CommerceOS needs reproducible, reviewable, machine-verifiable infrastructure for a serverless learning architecture. Manual infrastructure setup creates hidden state, drift, inconsistent environments, and poor agent/CI observability.

The project originally assumed deployment to real AWS dev/preview/staging environments. ADR-012 supersedes those account/deployment/validation assumptions: LocalStack is now the only infrastructure/runtime target unless a later human decision explicitly changes that.

## Decision

Use **AWS CDK** as the application Infrastructure as Code source of truth for CommerceOS.

All AWS-style application resources must be represented in version-controlled CDK code unless an explicit documented exception exists.

CDK synthesis remains part of verification. Deployment targets LocalStack through compatible tooling/configuration rather than a real AWS account.

The AWS Console, AWS IAM deployment roles, GitHub OIDC federation to AWS, AWS account bootstrapping, AWS Budgets, and real-cloud preview/staging environments are not part of the current architecture.

LocalStack endpoint overrides, synthetic credentials, region, account placeholders, ports, and feature flags are infrastructure configuration concerns and must not leak into Domain/Application code.

## Alternatives considered

### Manual infrastructure creation

Rejected as the primary method because it is not reproducible and creates hidden dependencies for developers, agents, and tests.

### Raw CloudFormation

Viable as the synthesized substrate, but CDK remains preferable for reusable constructs, tests, programming abstractions, and agent-maintainable infrastructure code.

### Terraform / OpenTofu

Strong alternatives. Not selected initially because CommerceOS intentionally learns AWS-style serverless architecture and already uses CDK. A migration would require a new ADR.

### AWS SAM only

May be useful for local handler workflows but is not the overall infrastructure source of truth because CommerceOS includes identity, persistence, events, queues, workflows, object storage, and other infrastructure capabilities.

## Consequences

Positive:

- Git contains the desired infrastructure architecture;
- LocalStack environments can be bootstrapped/reset reproducibly;
- IaC changes remain reviewable through synth/assertion tests;
- infrastructure and application changes share task/PR context;
- agents can reason about service dependencies from repository code;
- no AWS account access is required.

Trade-offs:

- CDK/CloudFormation-compatible tooling remain project dependencies;
- LocalStack may not implement every synthesized resource or AWS behavior identically;
- infrastructure tests must explicitly record emulator limitations;
- stateful-resource migration/protection concepts can be learned, but not validated against real AWS under the current decision.

## Security and tenant impact

- no real AWS deployment credentials are required;
- synthetic LocalStack credentials are configuration only;
- tenant isolation and authorization remain application concerns and are not relaxed by the emulator;
- infrastructure code remains reviewable for delivery/persistence boundaries.

## Cost impact

There is no AWS Free Tier, AWS Budget, or cloud-spend gate under ADR-012.

Local machine resource usage and LocalStack licensing/edition requirements are tooling concerns and must be documented when material.

## Reversibility / migration

The CDK decision remains reversible. Moving to Terraform/OpenTofu/raw CloudFormation would require inventory, state/recreation strategy, validation, and cutover planning.

Changing the runtime target from LocalStack to real AWS would require an explicit human architecture decision superseding ADR-012; it is not an implementation-task default.

## Validation

The decision is satisfied when:

- a clean checkout can `cdk synth` the infrastructure;
- CDK assertion tests cover required guardrails/contracts;
- selected stacks can be deployed/bootstraped to LocalStack without hidden manual resources;
- infrastructure-sensitive tests run against LocalStack where supported;
- reset/redeploy is reproducible when relevant;
- unsupported/different LocalStack behavior is explicitly documented.
