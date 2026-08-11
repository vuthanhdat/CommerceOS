# TASK-0094 — Establish the LocalStack foundation lifecycle

Status: Backlog
Specification maturity: Ready
Execution permission: YES
Owner: Builder — Platform Engineering
Recommended model: Default implementation model
Created: 2026-08-10
Reconciled: 2026-08-11
Ready: 2026-08-11
Roadmap phase: Phase 0
Depends on: completed TASK-0003, completed TASK-0093
Infrastructure verification: Required — LocalStack only

## Goal

Establish the first reproducible CommerceOS infrastructure environment using LocalStack. Prove that the existing FoundationStack and repository tooling can start, become ready, bootstrap/deploy, inspect, reset/destroy as defined, and redeploy without hidden manual resources or any real-AWS account dependency.

## Planning readiness

- Owning area: Platform Engineering / local infrastructure harness.
- Domain/business semantics: N/A; this task changes no business behavior.
- Module/layer ownership: resolved by the technical baseline and ADR-012.
- Runtime target: LocalStack only.
- IaC authority: AWS CDK remains repository infrastructure source of truth under ADR-001 as amended by ADR-012.
- Configuration boundary: endpoint, synthetic credentials, region/account placeholders, ports, instance/resource prefixes and LocalStack feature switches remain infrastructure configuration.
- Dependency gate: TASK-0093 is Completed.
- Human/cloud/account/cost gate: none.
- Remaining planning blocker: none.

## In scope

- establish and document the required LocalStack runtime prerequisites and supported edition/version assumptions;
- define configuration for endpoint, synthetic credentials, region, account placeholder, task-instance/resource prefix, ports, and reset policy;
- make the repository provide one deterministic LocalStack start/readiness/bootstrap/deploy path;
- deploy the existing `FoundationStack` to LocalStack through repository-owned CDK-compatible tooling;
- inspect stack resources/tags/log configuration where supported;
- prove `start -> readiness -> synth/bootstrap/deploy -> smoke -> reset/destroy -> redeploy`;
- ensure no required application resource exists only through manual LocalStack setup;
- isolate mutable LocalStack state/resource names sufficiently for the repository task-instance/worktree model;
- document unsupported, partially supported, edition-dependent, or behaviorally different LocalStack features encountered by this foundation;
- add/update harness checks required to keep the lifecycle reproducible.

## Out of scope

- real AWS account provisioning, authentication, deployment, validation, or teardown;
- AWS IAM deployment roles or GitHub OIDC federation;
- AWS Budgets, Free Tier/credit controls, Cost Explorer, or cloud cost evidence;
- real-cloud preview/staging/production environments;
- Cognito, business API/Lambda, module DynamoDB tables, queues/events/workflows, crawler or storefront resources not already required by the FoundationStack;
- changing business/domain semantics;
- claiming full AWS compatibility from LocalStack behavior.

## Acceptance criteria

### AC01 — Reproducible declared foundation

Given a clean checkout with documented local prerequisites,
when the documented infrastructure lifecycle is executed,
then LocalStack becomes ready and the selected FoundationStack can be synthesized and deployed without hidden manual application resources.

### AC02 — Configuration boundary is explicit

Endpoint, synthetic credentials, region/account placeholders, ports, instance/resource prefixes, reset policy, and LocalStack feature switches are runtime/infrastructure configuration. Domain/Application projects contain no LocalStack-specific dependency or branching.

### AC03 — Reversible lifecycle

The foundation can be returned to the documented known state and deployed again successfully. Reset/destroy behavior and any intentionally persistent local bootstrap state are explicit.

### AC04 — Worktree/task isolation is defined

Concurrent task instances do not silently share mutable application resource names or ports. The launcher/configuration either derives isolated values from the task instance or documents a safe serialization constraint.

### AC05 — Verification evidence is complete

Repository harness/IaC tests, `cdk synth`, LocalStack readiness/deploy/smoke evidence, reset/redeploy evidence, and relevant configuration checks are recorded in the completion summary.

### AC06 — Compatibility claims are bounded

Any unsupported, partially supported, edition-dependent, or behaviorally different LocalStack capability encountered is recorded as a limitation. No real AWS fallback is used to make the task pass.

## Architecture/security/resource constraints

- ADR-012 is authoritative.
- AWS CDK remains the IaC source of truth under ADR-001 as amended.
- Use only synthetic LocalStack credentials/configuration.
- No business Tenant data is introduced.
- No LocalStack-specific types/configuration may leak into Domain/Application code.
- Infrastructure state is operational/testing state, never business authority.
- Do not add speculative infrastructure services beyond the FoundationStack outcome required by this task.
- Local machine CPU/memory/disk growth should remain bounded and documented when material; AWS monetary cost is not a planning gate.

## Test plan

- repository harness and CDK assertion tests;
- `cdk synth` without real AWS credentials;
- LocalStack start/readiness check;
- bootstrap/deploy and smoke inspection;
- reset/destroy and redeploy proof;
- task-instance port/resource-isolation check or explicit serialization proof;
- architecture check that Domain/Application do not acquire LocalStack dependencies;
- limitation evidence for any unsupported/different feature.

## Completion requirements

Before moving to `tasks/completed/`, record:

- commands used for start/readiness/deploy/reset/redeploy;
- LocalStack version/edition assumptions actually verified;
- resource/configuration isolation result;
- known emulator limitations discovered;
- `python3 scripts/harness_check.py` result;
- no-real-AWS verification statement.

**Current gate: READY — Builder may execute TASK-0094 now.**
