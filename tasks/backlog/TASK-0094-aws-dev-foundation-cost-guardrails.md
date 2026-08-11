# TASK-0094 — Deploy the AWS dev foundation and cost guardrails

Status: Backlog
Specification maturity: Refined
Execution permission: NO
Owner: Builder — Platform Engineering
Recommended model: Default implementation model
Created: 2026-08-10
Roadmap phase: Phase 0
Depends on: completed TASK-0003, TASK-0093
Cloud verification: Required
Exclusive write surface: `aws-dev-foundation` plus task-scoped CDK/test/documentation files only

## Goal

Prove that the existing cost-safe dev `FoundationStack` can be synthesized, deployed, inspected, destroyed, and redeployed from repository-owned CDK/configuration, while establishing and verifying real account-level cost guardrails. Do not add business infrastructure or standing-cost services.

This is a cloud-verification task, not a foundation redesign. The current implementation is scaffolding and must not be treated as cloud-proven until real AWS evidence exists.

## Current-state assessment (2026-08-11)

- TASK-0093 exists as a `Ready` task with canonical lifecycle `Backlog`; no TASK-0093 completion artifact is present on current `main`. Therefore the dependency is not Completed and TASK-0094 cannot be Ready.
- TASK-0094 is correctly `Refined` in `tasks/BACKLOG.v2.yaml`, `tasks/backlog-v2/00-foundation-tenancy.yaml`, and this specification. Its cloud and human gates remain unsatisfied.
- `infra/CommerceOS.Cdk/FoundationStack.cs` creates exactly one `AWS::Logs::LogGroup`, named `/commerceos/{environment}/foundation`, and applies `Project`, `Environment`, `ManagedBy`, `Owner`, `CostProfile`, and `Ephemeral` tags at stack scope.
- `infra/CommerceOS.Cdk/EnvironmentProfile.cs` defines dev as non-production, non-ephemeral, seven-day log retention, `RemovalPolicy.DESTROY`, and `free-tier`. It also defines preview/staging/prod profiles; TASK-0094 executes only the dev profile.
- `infra/CommerceOS.Cdk/Program.cs` reads CDK context `environment`, defaults to `dev`, creates `commerceos-{environment}-foundation`, and synthesizes. It does not bind an account or region and does not create a Budget.
- `tests/CommerceOS.Cdk.Tests/FoundationStackTests.cs` locally asserts one log group, the dev name, seven-day retention, and representative tags. It does not prove an AWS deployment, account-level Budget, bootstrap resources, or absence of resources outside the synthesized template.
- `npm run cdk:synth` and `python3 scripts/harness_check.py` are local verification entry points. Local success is not cloud success.
- Current cost controls are bounded log retention, destroy-on-removal for dev, standard tags, a free-tier cost profile, and an intentionally empty foundation. No real account-level Budget/credit notification is repository-defined today.

## In scope

- reconcile and record the selected dev AWS account, region, temporary/SSO authentication method, and CDK bootstrap state;
- run the local CDK/build/test/harness checks and inspect synthesized output;
- review the exact `cdk diff` before any mutation;
- deploy the existing dev `FoundationStack` only after all gates pass;
- inspect deployed resources, tags, log retention, CloudFormation ownership, and prohibited-service absence;
- configure and verify the approved account-level AWS Budget/credit-spend notifications using the human-provided thresholds and destination;
- destroy the dev application stack, verify cleanup, redeploy it, and repeat the smoke checks;
- record redacted local, synthesis, diff, deploy, guardrail, destroy, cleanup, and redeploy evidence in the task completion artifact.

## Explicitly out of scope

- changing accepted architecture or adding an ADR unless an actual material architecture change is proposed;
- changing `FoundationStack` merely to make deployment easier;
- GitHub OIDC, CI/CD, preview delivery, or TASK-0095 work;
- Cognito, Lambda application/runtime code, API Gateway, DynamoDB business tables, SQS, SNS, EventBridge, Step Functions, S3/Web, crawler, payment, or accounting infrastructure;
- NAT Gateway, ALB, EC2, EKS, OpenSearch, MSK, RDS/Aurora, Redis/ElastiCache, always-on compute, or any other standing-cost service;
- production or staging deployment;
- inventing an account, region, budget amount, threshold, notification recipient, role, profile, or authentication method;
- committing credentials, tokens, account secrets, or unredacted billing information.

## Resolved implementation boundaries

The Builder must preserve the existing CDK source of truth. The dev application stack is the `commerceos-dev-foundation` stack produced by `Program.cs` with `EnvironmentProfile.Create("dev")`; its only expected application resource is the seven-day CloudWatch log group. AWS CDK bootstrap resources are prerequisite CDK infrastructure and are not application-owned resources; they must be inventoried separately and must not be destroyed by this task unless a human explicitly authorizes a separate cleanup task.

Account-level Budget/credit notifications are billing-account guardrails, not a `FoundationStack` resource. They may be configured through the approved account mechanism, but the Builder must record their effective configuration and verification without moving them into the application stack or inventing a CDK representation. If the account requires a different supported mechanism, stop for human direction rather than substituting a threshold or recipient.

Expected repository changes are limited to:

- this task specification if execution findings require clarification;
- existing CDK/test files only if a local assertion is demonstrably missing for the already-accepted foundation behavior;
- a redacted task completion/evidence artifact after successful execution.

No CDK source change is expected before execution. Do not touch business modules, application code, unrelated IaC, orchestrator code, or canonical task maturity unless the Planner later records a separate decision.

## Builder execution sequence

1. **Preflight and local verification.** Confirm clean task worktree, completed TASK-0093 artifact on current `main`, required .NET/Node/npm/CDK versions, and no credentials in repository files. Run the repository harness, CDK assertion tests, and `npm run cdk:synth`. Expected result: all local checks pass and only the expected log group appears in the synthesized dev template. Record commands, commit SHA, tool versions, and outputs. Stop on any failure.
2. **Configuration and authorization gate.** Obtain, from the human, the concrete dev account ID, region, approved temporary/SSO authentication method, Budget notification destination, and exact approved thresholds. Confirm the AWS caller identity and selected region with read-only commands. Expected result: identity and region match the recorded authorization. Stop if any value is missing, mismatched, expired, or ambiguous.
3. **Bootstrap prerequisite inspection.** Read-only inspect whether the selected account/region is CDK-bootstrapped and inventory bootstrap stack/resources, ownership, and retention. If bootstrap is missing, stop with the exact prerequisite and request explicit human authorization for bootstrap; do not bootstrap automatically in this task. If bootstrap state is incomplete or unknown, stop.
4. **Synthesis and reviewed diff.** Run `cdk synth` for the dev context and `cdk diff` against the selected environment. Review logical IDs, replacements, removal policies, tags, retention, IAM, and resource types. Expected diff: only the declared foundation log group, or an explicitly explained first-create diff. Stop on any unexpected resource, policy, replacement, region/account mismatch, or standing-cost service.
5. **Cost/safety inspection.** Confirm the synthesized template contains no prohibited service and that the expected log group has seven-day retention, dev naming, destroy removal policy, and all standard tags. Record an estimated cost of negligible/near-zero for this empty foundation and identify CloudWatch ingestion/storage as the only potentially non-negligible cost if logs are emitted or retained.
6. **First deploy.** After human confirmation of the reviewed diff, deploy only the dev FoundationStack. Do not deploy all stacks or use a broad account-wide operation. Expected result: CloudFormation reaches `CREATE_COMPLETE`/equivalent and the stack account/region matches the gate. If a command times out, state is `UNKNOWN` until CloudFormation is inspected; do not retry blindly.
7. **Post-deploy smoke and drift boundary.** Read-only inspect CloudFormation resources, tags, log retention, removal policy, stack events, and CloudWatch log-group existence. Enumerate application-owned resources and compare them to synthesized output. Separately enumerate CDK bootstrap resources. Stop on missing tags, unbounded retention, unexpected resources, drift, or any standing-cost resource.
8. **Budget/notification verification.** Configure the approved account-level Budget/credit notifications only with the supplied values, then read back the effective configuration and verify notification destination/status without exposing sensitive recipient data in the repository. Also inspect available Free Tier/credit monitoring and current balance/usage as permitted. Stop on unsupported billing features, delivery/configuration failure, or any mismatch; do not silently lower thresholds or change recipients.
9. **Destroy and cleanup.** Destroy only the dev application stack after recording the pre-destroy inventory. Inspect CloudFormation completion and then verify the application log group is absent and no unexpected application resource remains. Expected retained items are only pre-existing/account-level guardrails and CDK bootstrap resources, which must be listed and attributed. Stop on failed/partial destroy or unknown CloudFormation state.
10. **Redeploy and final smoke.** Redeploy the same commit/profile/account/region, repeat resource/tag/retention/stack-ownership checks, and record successful `CREATE_COMPLETE` plus final state. Destroy again if the approved dev policy requires no persistent stack after verification; otherwise record the explicitly approved retained state and its cost rationale.
11. **Evidence and completion.** Produce the task completion artifact with AC01–AC04 evidence, redactions, timestamps, commit SHA, account/region identifiers as approved for recording, stack names, CloudFormation event summaries, resource inventories, Budget verification, destroy/redeploy results, and any follow-up. Do not claim cloud acceptance from synth or tests alone.

## Failure handling (fail closed)

- **Missing bootstrap:** stop; report account/region and required bootstrap prerequisite. Do not bootstrap without explicit human authorization.
- **Expired/incorrect authentication:** stop and re-establish only through the approved temporary/SSO method; never add long-lived keys or switch accounts by guesswork.
- **Synth or local test failure:** stop and fix only task-scoped defects; do not deploy.
- **Unexpected diff/resource/service:** stop before mutation, attach the diff, and route the architecture/cost decision to the Technical Architect/human.
- **CloudFormation deploy failure:** inspect stack events and final state first. If rollback/cleanup is incomplete, record `UNKNOWN` or failed state and reconcile before any further operation.
- **Operation still in progress/timeout:** inspect CloudFormation; do not issue a blind deploy/destroy retry.
- **Destroy failure or retained application resource:** stop, inventory dependencies/protection/drift, and obtain direction before manual deletion. Do not delete bootstrap or account-level guardrails.
- **Budget configuration or notification failure:** stop; preserve the stack in the safest approved state and request the missing account/billing decision. Do not invent an equivalent threshold/destination.
- **Unexpected standing-cost resource:** stop all further cloud mutation, identify owner and origin, and escalate for cleanup/architecture review.
- **Redeploy failure:** inspect CloudFormation state and resource drift before retrying; do not treat a failed first destroy/redeploy as reversible proof.

## Cost guardrail verification

The Builder must verify all of the following against both repository output and the real account:

- standard tags: `Project=CommerceOS`, `Environment=dev`, `ManagedBy=CDK`, `Owner=personal-learning`, `CostProfile=free-tier`, `Ephemeral=false`;
- bounded log retention: `RetentionInDays=7`; no continuous workload or unbounded log producer is introduced;
- account-level Budget/credit alerts: exact human-approved thresholds and destination, effective status, and read-back evidence;
- prohibited standing-cost absence: no NAT Gateway, ALB, EC2, EKS, RDS/Aurora, Redis/ElastiCache, OpenSearch, MSK, always-on compute, or business resource exists because of this task;
- post-destroy state: application-owned log group removed; only explicitly identified bootstrap/account-level resources remain;
- bootstrap/application boundary: bootstrap resources are inventoried and excluded from application-stack acceptance;
- cost exposure: empty foundation is expected near-zero/negligible, while CloudWatch log ingestion/storage and any existing bootstrap/account charges must be checked rather than assumed free.

## Acceptance criteria and exact evidence

### AC01 — Reproducible declared foundation

Pass requires local harness/CDK assertion PASS, synthesized template, reviewed diff, and a real deploy from a clean task commit showing that every application resource is the declared dev FoundationStack resource. Synth alone is local/synthesis evidence, not deployment evidence.

### AC02 — Cost guardrails are real

Pass requires real account read-back evidence for all standard tags, seven-day log retention, approved Budget/credit notifications, and prohibited-service absence, plus a cost note covering bootstrap and CloudWatch exposure. Local assertions alone are insufficient.

### AC03 — Reversible deployment

Pass requires CloudFormation success evidence for deploy → destroy → cleanup → redeploy, with no blind retry after unknown state, and a separate list of intentionally retained bootstrap/account-level resources.

### AC04 — Verification evidence is complete

Pass requires one redacted completion artifact containing local verification, synthesis, diff review, real AWS deploy/smoke, Budget verification, destroy/cleanup, redeploy/final smoke, timestamps, commit/account/region/stack identity, failures or explicit none, and reviewer/human authorization references. No cloud result may be inferred from synth.

## Test plan

- local: `dotnet test tests/CommerceOS.Cdk.Tests/CommerceOS.Cdk.Tests.csproj`, relevant repository checks, and `python3 scripts/harness_check.py`;
- synthesis: `npm run cdk:synth` plus inspection of the generated dev template;
- cloud: read-only identity/bootstrap/resource checks, reviewed `cdk diff`, deploy/smoke, account-level Budget read-back, destroy/cleanup, and redeploy/final smoke;
- no business, tenant, or real payment data; no load test or recurring schedule.

## Ready-gate assessment

TASK-0094 remains `Refined` and non-dispatchable. The technical execution plan is now Builder-executable once the gates are satisfied, but the following human/cloud inputs are still missing from repository evidence:

1. a completed TASK-0093 on authoritative `main`;
2. explicit human authorization for real AWS dev-account mutations, including the selected account;
3. concrete dev AWS account ID and region;
4. approved account-level Budget/credit notification thresholds and destination, plus the approved temporary/SSO authentication method if not already recorded in the task authorization.

Do not ask the Builder to discover or guess any of these values. Do not mark this task Ready merely because its plan is complete.

**Current gate: REFINED — not executable.**
