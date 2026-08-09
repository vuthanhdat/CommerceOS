# CommerceOS — CI/CD Pipeline

_Last reviewed: 2026-08-09._

## 1. Goal

CommerceOS CI/CD must provide reliable automation without turning every pull request into a full, expensive AWS deployment.

The pipeline is designed around three principles:

1. **build once, promote the same artifact**;
2. **cloud verification only when cloud semantics are affected**;
3. **all AWS deployments are Infrastructure as Code**.

---

## 2. Branch and release model

Initial model:

```text
feature/task branch
      ↓
Pull Request
      ↓
CI + selected preview verification
      ↓
merge main
      ↓
auto deploy DEV
      ↓
release candidate
      ↓
on-demand STAGING
      ↓
manual approval
      ↓
PROD later
```

Avoid long-lived environment branches such as `dev`, `staging`, and `prod` unless a later ADR demonstrates a need.

`main` represents the current integrated codebase. Environment promotion is a deployment concern, not a branch-copy concern.

---

## 3. Pull Request CI

Every PR runs local/mechanical checks:

```text
checkout
   ↓
harness check
   ↓
dependency restore/install
   ↓
format/lint
   ↓
build
   ↓
unit tests
   ↓
architecture tests
   ↓
contract tests
   ↓
CDK synth
   ↓
security/static checks
```

A docs-only PR should not deploy AWS resources.

A cloud-sensitive PR may trigger an ephemeral preview job.

### Cloud-sensitive changes

Examples:

- `infra/**`;
- IAM definitions;
- Lambda packaging/runtime configuration;
- API Gateway configuration;
- Cognito configuration;
- DynamoDB table/index/capacity definitions;
- SQS/EventBridge/Step Functions definitions;
- S3 bucket policies/events/lifecycle.

The exact path/rule detection is implemented in Phase 0 and should prefer false-positive CI work over silently skipping a required cloud verification.

---

## 4. Preview deployment

Preview environments are opt-in/conditional, not universal.

Flow:

```text
PR requires cloud verification
          ↓
create environment id: pr-<number>
          ↓
CDK deploy selected stacks
          ↓
cloud integration tests
          ↓
collect logs/results
          ↓
CDK destroy
```

Preview requirements:

- use dedicated stack/resource prefixes;
- use tags including `Project=CommerceOS`, `Environment=pr-<n>`, `ManagedBy=CDK`, `Ephemeral=true`;
- deploy only affected stacks where practical;
- configure low capacity/concurrency;
- no persistent crawler schedule unless specifically tested;
- automatic teardown in a `finally`/cleanup step;
- periodic cleanup workflow for leaked ephemeral stacks.

A preview environment must not become a hidden permanent cost center.

---

## 5. Merge to main

After merge:

```text
main
 ↓
CI verification
 ↓
build/version artifacts
 ↓
CDK synth artifact
 ↓
auto deploy DEV
 ↓
DEV smoke/cloud integration tests
```

If DEV verification fails, the pipeline stops. It must not promote to staging automatically.

---

## 6. Build once, promote

Application packages and frontend artifacts should be built once for a commit/release candidate and promoted through environments.

Desired model:

```text
source commit
    ↓
build immutable artifact
    ↓
DEV
    ↓
STAGING
    ↓
PROD
```

Avoid rebuilding different binaries independently for each environment because that weakens provenance.

Environment-specific configuration is injected at deployment/runtime rather than compiled into different code versions wherever possible.

CDK synthesis can consume explicit environment configuration while application artifacts remain immutable.

---

## 7. Staging deployment

During the learning/Free Tier phase, staging should normally be on-demand.

Trigger options:

- manual workflow dispatch;
- version/release candidate tag;
- explicit promotion action after DEV PASS.

Pipeline:

```text
select validated artifact
      ↓
CDK diff staging
      ↓
policy/cost guard checks
      ↓
CDK deploy staging
      ↓
E2E tests
      ↓
failure/recovery tests
      ↓
release report
```

Staging may be destroyed after validation while credits are constrained.

---

## 8. Production deployment

Production is introduced only after the project reaches the relevant hardening milestone.

Required properties:

- manual approval/environment protection;
- separate AWS role and preferably separate AWS account;
- same artifact previously verified in staging;
- CDK diff visible before deploy;
- destructive/replacement changes surfaced explicitly;
- backup/rollback/migration plan for stateful changes;
- post-deployment smoke tests;
- alarm/health verification.

No AI agent may autonomously bypass the production approval gate.

---

## 9. GitHub Actions → AWS authentication

CI/CD must use GitHub Actions OpenID Connect (OIDC) federation with AWS IAM roles.

Do not store long-lived `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` repository secrets for deployment.

Expected trust separation:

```text
GitHub PR verification
      ↓ OIDC
AWS Preview/Dev deployment role

GitHub release workflow
      ↓ OIDC + protected environment
AWS Staging deployment role

GitHub production workflow
      ↓ OIDC + manual approval
AWS Production deployment role
```

Each role uses least privilege appropriate to its pipeline stage.

Reference: https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_oidc.html

---

## 10. Proposed GitHub workflows

Phase 0 should evolve toward:

```text
.github/workflows/
  harness.yml              # current H0 repository checks
  ci.yml                   # build/lint/unit/architecture/contract/CDK synth
  preview.yml              # conditional ephemeral AWS environment
  deploy-dev.yml           # main → dev
  deploy-staging.yml       # manual/release candidate
  deploy-prod.yml          # protected manual promotion, later
  cleanup-preview.yml      # remove leaked ephemeral stacks
```

Do not create empty ceremonial workflows. Add each workflow when its toolchain/deployment target exists.

---

## 11. Deployment verification

A successful `cdk deploy` does not mean the release is healthy.

Post-deploy checks should verify affected capabilities, for example:

- HTTP health/smoke API;
- authentication/authorization;
- Lambda invocation;
- DynamoDB write/read/conditional operation;
- SQS publish/consume/DLQ where changed;
- EventBridge route where changed;
- Step Functions test execution where changed;
- Mock Payment round trip;
- CloudWatch expected logs/metrics.

---

## 12. Cost controls in CI/CD

Because the project starts with a small AWS credit balance, deployment automation must include cost hygiene.

Rules:

- no full-stack preview for docs/frontend-only changes;
- preview/staging destroyed automatically when not needed;
- crawler schedules disabled in previews;
- no load test by default;
- CloudWatch log retention short in non-prod;
- DynamoDB capacity profile is small/bounded;
- Lambda reserved concurrency protects expensive/bursty workers;
- every stack is tagged for cost attribution;
- a change adding a new paid AWS service requires ADR + monthly cost estimate;
- CI should eventually inspect `cdk diff` for newly introduced resource types and require explicit acknowledgement.

---

## 13. Pipeline failure policy

Never solve a failing CI/CD gate by weakening or skipping the gate unless the guard itself is demonstrated to be incorrect.

If an agent encounters a failure:

```text
failure
  ↓
classify
  ├─ implementation defect
  ├─ environment defect
  ├─ flaky/non-deterministic test
  ├─ insufficient IAM
  ├─ incorrect IaC
  └─ harness/guardrail defect
  ↓
fix root cause
  ↓
rerun verification
```

Disabling tests, using administrator credentials, or manually creating missing AWS resources are not acceptable shortcuts.
