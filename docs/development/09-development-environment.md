# CommerceOS — Development Environment Strategy

_Last reviewed: 2026-08-09._

## 1. Goal

CommerceOS uses many AWS managed/serverless services, but development must keep a fast feedback loop and remain compatible with an AWS Free Tier / approximately USD 100 credit learning budget.

The project therefore uses a **hybrid development model**:

```text
LOCAL
fast, deterministic, cheap
    │
    ▼
AWS DEV / SANDBOX
real AWS service semantics
    │
    ▼
STAGING
production-like verification
    │
    ▼
PROD
only when the project is ready
```

We deliberately do **not** attempt to reproduce all of AWS locally.

Local development validates application/domain behavior. AWS dev validates cloud semantics such as IAM, Lambda packaging/runtime, API Gateway authorization, SQS delivery/retry, EventBridge routing, Step Functions execution, Cognito behavior, and CDK deployments.

---

## 2. Environment model

### `local`

Purpose:

- fastest agent/developer inner loop;
- unit and architecture tests;
- deterministic integration tests;
- frontend development;
- parser fixture tests;
- mock-payment failure simulation.

Expected components:

```text
Frontend                 localhost
.NET application logic   local process / test host
Mock Payment             local process
DynamoDB                 DynamoDB Local where persistence integration is needed
Lambda/API               SAM/local host only when useful
Events/queues             in-memory/test adapters for most tests
Crawler                   fixtures by default; no live crawling in CI
```

Local adapters must implement the same application ports/contracts used by AWS adapters. Domain code must not depend directly on AWS SDKs.

### `dev`

Purpose:

- persistent personal learning environment;
- verify real AWS semantics;
- integration testing;
- manual exploration;
- CloudWatch/operability learning.

Characteristics:

- one shared/personal dev environment initially;
- low-volume synthetic data;
- small provisioned/usage limits;
- destructive removal policy is acceptable for non-critical resources;
- short log retention;
- crawler disabled or manual by default;
- failure injection enabled for Mock Payment;
- no production secrets/data.

### `preview` / PR sandbox

Purpose:

- cloud verification for changes that actually require AWS;
- isolation from persistent `dev`.

Preview environments are **not created for every PR by default** because the project is credit-constrained.

They are created only when one of these conditions applies:

- IaC changed materially;
- IAM/authorization changed;
- SQS/EventBridge/Step Functions behavior changed;
- Lambda runtime/package integration changed;
- a task explicitly requires cloud acceptance criteria.

Preview resources must:

- use `pr-<number>` naming/tags;
- deploy only required stacks/resources;
- have bounded concurrency/capacity;
- be destroyed automatically after tests or by TTL cleanup;
- never create NAT Gateway, ALB, EC2, RDS, or another always-on resource without an accepted ADR.

### `staging`

Purpose:

- production-like end-to-end validation;
- release candidate verification;
- failure/recovery tests;
- deployment rehearsal.

During the Free Tier learning period, staging is **ephemeral or on-demand**, not permanently duplicated infrastructure.

Suggested behavior:

```text
release candidate
      ↓
cdk deploy staging
      ↓
E2E + failure verification
      ↓
collect diagnostics
      ↓
cdk destroy staging
```

As the project matures, staging may become persistent if cost/operational value justifies it.

### `prod`

Production is intentionally deferred. A separate production AWS account is preferred once CommerceOS handles real users/data.

---

## 3. Inner loop vs outer loop

### Inner loop — local

Target: seconds, not minutes.

```text
edit
 ↓
build
 ↓
unit tests
 ↓
architecture tests
 ↓
local contract/integration tests
 ↓
repeat
```

No network dependency should be required for normal domain development.

### Outer loop — AWS

Target: run only when cloud semantics matter.

```text
local checks PASS
      ↓
CDK synth
      ↓
CDK deploy selected stack(s)
      ↓
cloud integration tests
      ↓
inspect logs/metrics
      ↓
destroy preview if ephemeral
```

A task may not claim cloud-related acceptance criteria are complete based only on local emulation.

---

## 4. AWS service test strategy

| AWS capability | Local strategy | Source of truth before release |
|---|---|---|
| Lambda business logic | direct invocation/unit tests | real Lambda in dev/staging |
| API Gateway | local HTTP/SAM where useful | real HTTP API |
| DynamoDB | DynamoDB Local + repository tests | real DynamoDB for IAM/capacity/conditional behavior |
| Cognito | fake/auth test context | real Cognito |
| SQS | in-memory adapter/contract tests | real SQS including retry/DLQ |
| EventBridge | in-memory event bus/contract tests | real EventBridge routing |
| Step Functions | workflow definition tests; individual logic tests | real Step Functions execution |
| S3 | filesystem/in-memory abstraction for unit tests | real S3 policy/event/lifecycle tests |
| CloudWatch | logging interfaces | real logs/metrics/alarms |
| IAM | cannot be faithfully emulated | real AWS only |

Do not make LocalStack or another full-cloud emulator a mandatory dependency. It may be evaluated later, but AWS remains the integration source of truth.

---

## 5. Environment configuration contract

Infrastructure receives an explicit environment profile rather than scattering environment checks through application code.

Example conceptual configuration:

```text
EnvironmentConfig
- name
- awsAccount
- awsRegion
- isProduction
- removalPolicy
- logRetentionDays
- enableCrawler
- enableFailureInjection
- enablePitr
- lambdaReservedConcurrency
- dynamodbCapacityProfile
- costProfile
```

Example intent:

```text
local
  crawler: fixture-only
  payment failure injection: enabled

dev
  crawler: manual
  payment failure injection: enabled
  low capacity/bounded concurrency

staging
  crawler: controlled
  payment failure injection: enabled for explicit tests

prod
  crawler: scheduled
  failure injection: disabled by default
  stronger retention/protection
```

---

## 6. Data rules per environment

### Local

Use deterministic fixtures and generated tenants/orders.

### Dev

Use synthetic tenants and small imported/crawled sample data.

### Staging

Use generated production-shaped data, never a casual copy of real production data.

### Production

Real business data only after security/operational hardening.

No environment may depend on real card data because CommerceOS uses the Mock Payment Provider.

---

## 7. Free Tier implications

Development must prefer monthly-free/pay-per-use services and bounded workloads.

Persistent dev is acceptable for resources with no meaningful idle cost (for example Lambda functions, SQS queues, EventBridge rules, DynamoDB within intended free/provisioned allowance), but resources that accumulate cost through logs, storage, custom metrics, excessive workflow transitions, or data transfer must have explicit limits.

See `13-free-tier-and-credit-guardrails.md` for the service budget policy.

---

## 8. References

- AWS Lambda testing guidance: https://docs.aws.amazon.com/lambda/latest/dg/testing-guide.html
- DynamoDB Local: https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocal.html
- Step Functions local testing limitations: https://docs.aws.amazon.com/step-functions/latest/dg/sfn-local.html
- AWS CDK environments: https://docs.aws.amazon.com/cdk/v2/guide/environments.html
