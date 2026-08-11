# CommerceOS — Infrastructure as Code

_Last reviewed: 2026-08-11. ADR-001 and ADR-012 are authoritative._

## 1. Decision

All CommerceOS AWS-style application infrastructure is defined as code using AWS CDK and deployed to LocalStack-compatible infrastructure tooling.

The repository is the source of truth. No real AWS account, AWS Console setup, AWS IAM/OIDC deployment role, AWS Budget, or AWS account bootstrap is required by the current architecture.

```text
Git repository
     │
     ├── application code
     ├── tests
     └── infrastructure code
              │
              ▼
           CDK synth
              │
              ▼
  CloudFormation-compatible template
              │
              ▼
          LocalStack
```

## 2. Why this remains mandatory

Infrastructure as Code provides reproducibility, reviewable architecture changes, deterministic local bootstrap/reset, agent-readable infrastructure context, automated policy checks, and a direct relationship between a Git commit and local deployed resources.

## 3. Repository structure

```text
infra/
  CommerceOS.Cdk/
    app
    config
    stacks
    constructs
    tests
src/
  ...
tools/
  commerceos.py
```

CDK remains the infrastructure definition system. LocalStack is the runtime target.

## 4. Stack strategy

Use a small number of operational stacks rather than one stack per domain. Existing conceptual stack names such as IdentityStack, ApiStack, integration/async stacks, crawler/provider stacks, and FilesMedia resources remain operational boundaries only.

No stack is created merely to mirror a domain diagram. Infrastructure appears only for named Ready work.

## 5. Environment configuration

Profiles are local runtime configurations:

```text
config/
  localstack-dev
  localstack-test
  localstack-stage
```

Configuration may define:

- LocalStack endpoint;
- synthetic SDK credentials;
- region;
- account-id placeholder where tooling requires one;
- resource naming prefix/task instance;
- ports;
- reset/persistence policy;
- log retention/verbosity;
- crawler/failure-injection flags;
- LocalStack feature/edition switches.

Application business logic must not branch on LocalStack details.

## 6. Naming and isolation

Resources should carry stable project/environment/task-instance naming. Parallel worktrees derive unique prefixes and ports where needed.

## 7. IaC workflow

```text
change IaC
   ↓
CDK unit/assertion tests
   ↓
cdk synth
   ↓
review resource/contract changes
   ↓
deploy to LocalStack
   ↓
smoke/integration verification
   ↓
reset/redeploy when relevant
```

There is no AWS OIDC or account deployment step.

## 8. Manual drift

Permanent manually-created LocalStack resources are prohibited when application/tests depend on them. Any useful experiment must be reproduced in CDK/bootstrap automation or removed.

## 9. Stateful resources

LocalStack state is synthetic learning/test state. Profiles may preserve or destroy it according to documented reset policy, but no business correctness may depend on hidden persistent emulator state.

## 10. Capability-first rule

CDK constructs should express the required capability while keeping Domain/Application code independent of the infrastructure implementation.

Examples:

- DynamoDB for conditional/transactional module persistence;
- SQS/DLQ for retryable one-worker work;
- EventBridge for named business-fact fan-out;
- Step Functions for ADR-approved durable workflows;
- S3 for managed object storage;
- API Gateway/Lambda/Cognito only as delivery/runtime mappings where supported.

## 11. LocalStack limitation rule

Infrastructure verification must record unsupported, partially supported, behaviorally different, or edition-dependent features. Do not hide a limitation by weakening application contracts or by claiming exact AWS equivalence.

A real AWS account is not the fallback under the current architecture.

## 12. Security

No real AWS credentials are required. Synthetic LocalStack credentials are configuration, not secrets of business value.

Application authorization, tenant isolation, provider secret hygiene, and trusted execution-context rules remain unchanged.

## 13. Reproducibility criteria

An infrastructure profile is reproducible when:

- a clean checkout can synthesize it;
- LocalStack prerequisites are documented;
- bootstrap/deploy creates every required resource;
- tests do not rely on manually-created resources;
- reset/redeploy is deterministic for affected state;
- resource mappings trace back to version-controlled CDK definitions;
- known LocalStack limitations are documented.

## 14. References

- `../adr/ADR-001-aws-cdk-infrastructure-as-code.md`
- `../adr/ADR-012-localstack-only-infrastructure-runtime.md`
- `../architecture/localstack-runtime-and-lifecycle.md`
