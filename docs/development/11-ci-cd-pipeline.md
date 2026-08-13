# CommerceOS — CI/CD Pipeline

_Last reviewed: 2026-08-11. ADR-012 is authoritative._

## 1. Goal

CommerceOS CI validates repository quality and AWS-style infrastructure behavior without deploying to a real AWS account.

Principles:

1. build once per commit where artifacts exist;
2. run infrastructure verification only when the affected capability needs it;
3. all infrastructure remains repository-owned IaC;
4. LocalStack is the infrastructure target;
5. no AWS IAM/OIDC/account/cost gate exists.

## 2. Branch model

```text
feature/task branch
      ↓
Pull Request
      ↓
CI
      ├── mechanical/local checks
      └── selected LocalStack integration verification
      ↓
merge main
      ↓
main verification
```

`main` is the integrated codebase. There is no automatic AWS DEV/STAGING/PROD promotion path under the current architecture.

## 3. Pull-request CI

Every relevant PR runs:

```text
checkout
  ↓
harness check
  ↓
restore/install
  ↓
format/lint/build
  ↓
unit tests
  ↓
architecture tests
  ↓
contract tests
  ↓
CDK synth/assertions when applicable
  ↓
security/static checks
```

Infrastructure-sensitive changes may additionally run an isolated LocalStack job.

Examples:

- CDK resource definitions;
- Lambda/API Gateway delivery wiring;
- Cognito integration where supported;
- DynamoDB access-model infrastructure;
- SQS/EventBridge/Step Functions definitions;
- S3 integration.

## 4. LocalStack verification job

```text
start isolated LocalStack
      ↓
wait for readiness
      ↓
deploy/bootstrap selected stacks
      ↓
run smoke/integration/failure checks
      ↓
collect diagnostics
      ↓
reset/stop/remove state
```

Requirements:

- unique task/job resource prefix;
- deterministic bootstrap;
- no manually-created resources;
- bounded test volume;
- explicit limitation record for unsupported/partial/different emulator behavior;
- cleanup executes even after test failure where practical.

## 5. Build once

Application artifacts should be immutable for a commit. Runtime-specific configuration such as LocalStack endpoints, synthetic credentials, region, ports, and resource prefixes is injected at runtime/deployment, not compiled into Domain/Application code.

## 6. GitHub workflows

Evolve toward workflows such as:

```text
.github/workflows/
  harness.yml
  ci.yml
  localstack-integration.yml
```

Do not create ceremonial deployment workflows for AWS preview/dev/staging/prod because those environments are not current targets.

### Current foundation workflows

- `ci.yml` runs the full repository harness on pull requests and `main` pushes.
- `harness.yml` provides the same full harness gate for the protected `main`
  branch after installing the required .NET, Node.js, and Python toolchains.
- `localstack-integration.yml` runs only for foundation launcher, CDK, and
  workflow changes (or manually). It pulls the pinned `localstack:4.8.1` image,
  uses task instance `0077`, executes the repository-owned lifecycle and
  inspection commands, collects container diagnostics on failure, and always
  removes the task-owned container.

All three use synthetic credentials only. The LocalStack job verifies the
current CloudFormation/CDK and CloudWatch Logs foundation against LocalStack
Community; it does not claim real-AWS equivalence.

## 7. Verification after infrastructure deployment

A successful LocalStack deploy is insufficient by itself. Verify affected capabilities, for example:

- HTTP health/smoke;
- identity-edge translation where supported;
- Lambda invocation;
- DynamoDB read/write/conditional behavior;
- SQS publish/consume/DLQ;
- EventBridge routing;
- Step Functions execution where supported;
- Mock Provider round trips;
- deterministic reset/redeploy.

## 8. Limitation policy

Do not weaken tests merely because LocalStack differs from AWS. Classify the gap, preserve the capability contract, test at the nearest reliable layer, and record that the exact AWS behavior is not proven.

A real AWS account is not the fallback unless ADR-012 is explicitly superseded.

## 9. Failure policy

Never solve a failing pipeline by bypassing a valid guardrail.

Classify failures as implementation defect, local environment defect, emulator limitation, flaky/non-deterministic test, incorrect IaC, or harness defect; fix the root cause or update the documented limitation/guard when the guard itself is wrong.
