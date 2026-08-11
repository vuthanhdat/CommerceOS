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

## Business context

CommerceOS is a learning-first commerce platform. This task introduces no business capability and changes no product/domain semantics. Its purpose is to provide the deterministic local infrastructure foundation required before business modules begin exercising AWS-style persistence, messaging, identity, workflow, and delivery capabilities through LocalStack.

The outcome must therefore improve development/verification infrastructure without becoming business authority or forcing Domain/Application code to know that LocalStack is the runtime target.

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

## Architecture impact

- ADR-012 remains authoritative for the LocalStack-only runtime policy.
- ADR-001 remains authoritative for AWS CDK as IaC source of truth, as amended by ADR-012.
- The task may change infrastructure/runtime tooling, configuration, CDK bootstrap/deploy support, and harness checks.
- It must not alter module boundaries, persistence ownership, tenant-context contracts, sync/async integration semantics, or domain/application dependency direction.
- LocalStack-specific types, endpoints, credentials, and feature switches remain outside Domain/Application code.
- Do not add speculative services beyond the FoundationStack outcome required here.

## Security and tenant impact

- No real AWS credentials, IAM roles, OIDC federation, or production secrets are introduced.
- Only synthetic LocalStack credentials/configuration may be used.
- No business Tenant data is introduced by this task.
- Existing tenant isolation and authorization rules remain unchanged and cannot be weakened because infrastructure runs locally.
- Task/worktree resource prefixes and ports must avoid accidental cross-task state sharing.

## Reliability and idempotency impact

- Start/readiness/deploy/reset/redeploy must be deterministic and safe to repeat.
- Re-running bootstrap/deploy against a documented known state must not depend on hidden manual resources.
- Reset/destroy behavior must make stale infrastructure state visible rather than allowing accidental reuse.
- Infrastructure lifecycle idempotency is an operational property only; it does not create or change business idempotency semantics.
- Unsupported or behaviorally different LocalStack features must fail visibly or be recorded as limitations rather than silently bypassed.

## Observability impact

- The lifecycle must expose enough diagnostics to distinguish LocalStack readiness, CDK synthesis/deployment, resource creation, and smoke-check failures.
- Where the emulator supports relevant logs/metadata, verification should capture them as operational evidence.
- Completion evidence must record commands, LocalStack version/edition assumptions, isolation result, limitations, and harness outcome.
- LocalStack observability is development/verification evidence, never business truth.

## Cost impact

- No AWS monetary cost, Budget, Free Tier, credit, or Cost Explorer gate applies under ADR-012.
- Local machine CPU, memory, disk, container/runtime overhead, CI minutes, and any LocalStack edition/licensing requirement are the relevant resource concerns.
- Resource growth should remain bounded and material prerequisites/limits documented.

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

## Completion summary

- Implemented `tools/commerceos.py` LocalStack lifecycle commands: config,
  start/readiness, synth/bootstrap/deploy, inspect/smoke, reset/destroy, and
  redeploy/lifecycle orchestration.
- Added task-instance-derived LocalStack ports, container names, CDK stack names,
  resource prefixes, synthetic credentials, region, account placeholder, and
  clean-container reset policy.
- Updated CDK foundation naming and tests to prove stable task isolation.
- Verification evidence: on instance `0001`, `start`, `readiness`, `synth`,
  `bootstrap`, `deploy`, `inspect`, `smoke`, `reset`, `redeploy`, and `destroy`
  all completed successfully against LocalStack Community `4.8.1`; the health
  response reported `cloudformation`, `iam`, `logs`, `s3`, `ssm`, and `sts` as
  available/running. `python3 scripts/harness_check.py` passed (43 orchestrator
  tests, .NET build/tests, architecture checks, frontend checks, and CDK synth).
  No real AWS account or credentials are used.
- LocalStack limitation: this task verifies the emulator health endpoint and the
  foundation CloudFormation/Logs mapping only; it does not claim exact AWS
  control-plane/IAM behavior. Pin `COMMERCEOS_LOCALSTACK_IMAGE` for repeatable
  version-specific evidence. The available `latest` image required a Pro auth
  token, so it is not the default and no token is committed.
