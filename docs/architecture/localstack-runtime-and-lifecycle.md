# CommerceOS — LocalStack Runtime and Infrastructure Lifecycle

_Last reviewed: 2026-08-11. ADR-012 is authoritative._

## 1. Purpose

This document defines how CommerceOS runs AWS-style infrastructure without a real AWS account.

LocalStack is the only infrastructure/runtime target for development, integration testing, staging-like validation, and deployment exercises. Application and domain architecture remains capability-first and does not depend on LocalStack-specific implementation details.

## 2. Runtime profiles

| Profile | Purpose | State policy |
|---|---|---|
| `local-fast` | unit, architecture, contract, direct-host tests | no infrastructure required unless a test explicitly needs it |
| `localstack-dev` | persistent developer learning/exploration | may preserve synthetic state between runs |
| `localstack-test` | deterministic integration/E2E/failure tests | disposable or reset before suites |
| `localstack-stage` | production-shaped local validation | isolated namespace/state; reset explicitly |

These profiles are configuration profiles, not AWS accounts.

## 3. Configuration boundary

Infrastructure/delivery configuration owns:

- LocalStack endpoint/base URL;
- SDK service URL overrides;
- synthetic access key/secret required by tooling;
- region;
- account-id placeholder where required;
- stack/resource naming prefix;
- task/worktree instance suffix;
- ports;
- LocalStack feature/edition switches;
- persistence volume/reset policy;
- log verbosity and diagnostic paths.

Domain code may not read any of these values. Application code may depend only on project-owned capability ports/contracts.

## 4. Capability mapping

| Architecture capability | Default LocalStack mapping | Notes |
|---|---|---|
| HTTP/serverless delivery | API Gateway + Lambda | direct ASP.NET/local host remains valid for fast feedback |
| identity evidence | Cognito when sufficiently supported; otherwise test identity adapter | tenant authority stays in Merchant Access, never in emulator claims |
| persistence | DynamoDB | ADR-005 ownership/access-pattern rules remain authoritative |
| work queue | SQS + DLQ | at-least-once/idempotency rules remain authoritative |
| fact routing | EventBridge | only named producers/consumers under ADR-006 |
| durable workflow | Step Functions | only ADR-approved workflows; emulator differences must be recorded |
| object storage | S3 | FilesMedia rules unchanged |
| logging/metrics | CloudWatch-style APIs when useful | observability evidence is operational, not business state |
| IaC | AWS CDK through LocalStack-compatible deployment tooling | repository remains infrastructure source of truth |

## 5. Bootstrap flow

Target developer/harness flow:

```text
start LocalStack
    ↓
wait for readiness
    ↓
deploy version-controlled CDK stacks to LocalStack
    ↓
apply idempotent technical/bootstrap seed data
    ↓
run smoke checks
    ↓
ready
```

No resource required by a test or task may exist only because a developer created it manually in the LocalStack UI/CLI.

## 6. Reset flow

Two reset levels are supported conceptually:

### Logical reset

Use idempotent cleanup/seed commands to clear task-owned synthetic resources/data while retaining the LocalStack process.

Use when it is faster and deterministic.

### Clean reset

```text
stop LocalStack
    ↓
remove task-owned volumes/state
    ↓
start LocalStack
    ↓
bootstrap infrastructure
    ↓
seed required data
```

Use when tests require a known-empty control plane or logical cleanup is insufficient.

## 7. Worktree/task isolation

Concurrent writable tasks must not share mutable infrastructure identities accidentally.

Use the task instance to derive:

- stack names;
- resource-name prefixes;
- queue/bus/table/bucket names where necessary;
- ports;
- test tenant/data namespaces.

A task must declare an exclusive resource only when LocalStack cannot safely isolate the capability.

## 8. Integration testing

Infrastructure-sensitive tests use `localstack-test` when the feature is supported sufficiently.

Required scenarios include, as applicable:

- conditional writes and optimistic concurrency;
- bounded transactions;
- tenant-scoped persistence queries;
- duplicate queue/event delivery;
- retry and DLQ behavior;
- out-of-order event handling;
- event routing/version matching;
- workflow retry/catch/wait/Unknown handling;
- object-storage policies used by application flows;
- deterministic bootstrap/reset/redeploy.

The test result proves CommerceOS behavior against the declared LocalStack profile. It does not prove exact AWS production behavior.

## 9. LocalStack limitations register rule

Every task using an AWS-style feature must verify that the required LocalStack behavior is available in the selected project setup.

If unsupported, partially supported, edition-dependent, or behaviorally different:

1. record the capability and exact gap in the task/architecture evidence;
2. keep the project contract capability-first;
3. test business/application semantics at the nearest reliable layer;
4. do not introduce a LocalStack-specific business rule or domain workaround;
5. do not fall back to a real AWS account unless ADR-012 is explicitly superseded.

Examples of gaps that must be treated this way include IAM/control-plane fidelity, quotas, managed-service operational timing, service-specific edge cases, or edition-gated APIs.

## 10. Delivery and identity boundary

Authentication transport is not tenant authorization.

Even if Cognito is emulated, the resulting authenticated subject is only identity evidence. Trusted tenant read/mutation contexts are still resolved from Tenancy/Merchant Access authority according to ADR-004.

Tests may use a test identity adapter when Cognito emulation is insufficient, but the adapter must produce the same project-owned `AuthenticatedPrincipal` contract and must not bypass Merchant Access.

## 11. IaC rule

CDK remains the desired-infrastructure source of truth.

The default deployment path targets LocalStack. Real AWS account IDs, IAM roles, OIDC federation, AWS Budgets, account bootstrapping, and cloud preview/staging resources are not prerequisites.

A normal infrastructure change should be reviewable through:

```text
CDK assertion tests
  ↓
cdk synth
  ↓
LocalStack-compatible deploy
  ↓
smoke/integration tests
  ↓
reset/redeploy verification when relevant
```

## 12. Operational boundaries

Queue age, DLQ contents, workflow execution state, emulator logs, and local container state are operational evidence. They are never business authority.

Provider timeout/Unknown semantics, idempotency, accounting immutability, tenant isolation, and cross-domain ownership rules remain unchanged regardless of emulator behavior.

## 13. Foundation lifecycle commands

The repository-owned launcher is the deterministic entry point for the foundation
loop. It uses a task instance to allocate an isolated LocalStack edge port and
resource/container prefix:

```text
python tools/commerceos.py config --instance 0001
python tools/commerceos.py lifecycle --instance 0001
python tools/commerceos.py inspect --instance 0001
python tools/commerceos.py reset --instance 0001
python tools/commerceos.py redeploy --instance 0001
python tools/commerceos.py destroy --instance 0001
```

`lifecycle` performs start, readiness, CDK synth, CDK bootstrap, CDK deploy, and
health smoke verification. `inspect` emits the LocalStack health response plus
the FoundationStack description (including tags), stack resources, and
task-prefixed log groups. `reset` removes the exact task-owned container and
starts a clean instance. `destroy` removes that container without touching any
other task instance. The default image assumption is the pinned
`localstack/localstack:4.8.1` community-era image; override it with
`COMMERCEOS_LOCALSTACK_IMAGE` when a different locally verified image is needed.

The foundation's current supported mapping is CloudFormation-compatible CDK
deployment plus CloudWatch Logs. The repository uses the LocalStack-aware
`cdklocal` wrapper for CDK commands; the wrapper is a host prerequisite and
prevents the lifecycle from silently targeting a real AWS endpoint. The AWS CLI
is also required for `smoke`, which verifies the deployed CloudFormation stack
and foundation log group through the same LocalStack endpoint. LocalStack
control-plane/IAM fidelity and exact
AWS compatibility are not claimed; any later capability must be verified and
recorded separately before being used by a Ready task. Newer `latest` images may
require a LocalStack Pro auth token; auth tokens are not a CommerceOS prerequisite
and must not be committed.
