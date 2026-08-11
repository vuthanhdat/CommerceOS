# TASK-0094 — Establish the LocalStack foundation lifecycle

Status: Backlog
Specification maturity: Refined
Execution permission: NO
Owner: Builder — Platform Engineering
Recommended model: Default implementation model
Created: 2026-08-10
Reconciled: 2026-08-11
Roadmap phase: Phase 0
Depends on: completed TASK-0003, TASK-0093
Infrastructure verification: Required

## Goal

Replace the obsolete real-AWS dev-foundation task with a LocalStack-only foundation task. Prove that the existing FoundationStack and repository tooling can start, bootstrap/deploy, inspect, reset, destroy when required, and redeploy against LocalStack without hidden manual resources or AWS-account dependencies.

This task remains Refined only because TASK-0093 is not yet Completed and the implementation details of the local lifecycle commands still need Builder execution. There is no human cloud authorization, AWS account, region-selection, Budget, IAM, OIDC, or cloud-cost gate.

## In scope

- establish/document the required LocalStack runtime prerequisites and supported edition/version assumptions;
- define configuration for endpoint, synthetic credentials, region, account placeholder, task-instance/resource prefix, ports, and reset policy;
- deploy the existing `FoundationStack` to LocalStack through repository-owned CDK-compatible tooling;
- inspect stack resources/tags/log configuration where supported;
- prove start -> readiness -> deploy/bootstrap -> smoke -> reset/destroy -> redeploy;
- ensure no required application resource exists only through manual LocalStack setup;
- document unsupported, partially supported, edition-dependent, or behaviorally different LocalStack features encountered by the foundation;
- add/update harness checks needed to make the lifecycle reproducible.

## Out of scope

- real AWS account provisioning or validation;
- AWS IAM deployment roles or GitHub OIDC federation;
- AWS Budgets, Free Tier/credit controls, or cloud cost evidence;
- real-cloud preview/staging environments;
- Cognito/business API/Lambda/module DynamoDB tables/queues/events/workflows/crawler/storefront resources not already part of the FoundationStack;
- changing business/domain semantics.

## Remaining Ready gates

This task may move to Ready when:

1. `TASK-0093` is Completed;
2. current architecture documents/ADR-012 are the accepted source of truth;
3. no additional material architecture decision is discovered during refinement.

No cloud authorization or account-specific input is required.

## Acceptance criteria once Ready

### AC01 — Reproducible declared foundation

A clean checkout can start the required local infrastructure, synthesize and deploy the selected FoundationStack to LocalStack, and every required application resource maps back to repository-owned IaC/bootstrap configuration.

### AC02 — Configuration boundary is explicit

Endpoint, synthetic credentials, region/account placeholders, ports, instance/resource prefixes, and reset policy are configuration concerns. Domain/Application projects contain no LocalStack-specific dependency or branching.

### AC03 — Reversible lifecycle

The foundation can be reset/destroyed as specified and redeployed successfully from a known state. Hidden manually-created resources are not required.

### AC04 — Verification evidence is complete

Repository harness/IaC tests, `cdk synth`, LocalStack deploy/smoke evidence, reset/redeploy evidence, and known emulator limitations are recorded.

### AC05 — Compatibility claims are bounded

Any unsupported or behaviorally different LocalStack capability is explicitly documented. The task does not claim exact AWS compatibility and does not use a real AWS fallback.

## Architecture/security constraints

- ADR-012 is authoritative.
- AWS CDK remains infrastructure source of truth under ADR-001 as amended.
- Use only synthetic LocalStack credentials/configuration.
- No business Tenant data is introduced.
- No LocalStack-specific types/configuration may leak into Domain/Application code.
- Infrastructure state is operational/testing state, never business authority.

## Test plan once Ready

- local harness and CDK assertion tests;
- `cdk synth`;
- LocalStack start/readiness check;
- deploy/bootstrap and smoke inspection;
- reset/destroy and redeploy proof;
- configuration-isolation check for a task instance;
- limitations evidence for any unsupported/different feature.

**Current gate: REFINED — waiting only on dependency/refinement, not cloud authorization.**
