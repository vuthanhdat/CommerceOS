# CommerceOS — Testing & Cloud Verification

_Last reviewed: 2026-08-09._

## 1. Purpose

CommerceOS needs high confidence without making every code change depend on a full AWS deployment.

The test strategy separates **business correctness** from **cloud-semantic correctness**.

```text
business/domain correctness
      ↓
local tests
      ↓
cloud-sensitive correctness
      ↓
AWS integration tests
      ↓
release confidence
```

---

## 2. Test layers

### Unit tests

Fast, deterministic, no AWS network calls.

Primary targets:

- domain invariants;
- calculations;
- state transitions;
- validators;
- accounting posting rules;
- inventory reservation rules;
- payment retry/reconciliation decisions;
- event creation.

### Architecture tests

Mechanically enforce rules such as:

- Domain must not depend on AWS SDKs;
- one domain cannot depend on another domain's Infrastructure implementation;
- API/Infrastructure dependencies point inward rather than into domain internals;
- tenant-owned types follow required scoping conventions;
- event consumers use the project idempotency mechanism where applicable.

Architecture rules should progressively move from prose to executable tests.

### Contract tests

Contracts include:

- HTTP request/response schemas;
- Mock Payment API;
- webhook schema/signature contract;
- domain event envelope/version;
- queue message schemas;
- crawler normalized product schema.

Contract tests should not require live external product sites.

### Local integration tests

Use local/test implementations where they provide useful confidence:

- DynamoDB Local for repository/access-pattern behavior;
- local API host/SAM for handler/request wiring where useful;
- Mock Payment local process;
- deterministic event/queue adapters;
- filesystem/local object-storage adapter.

### AWS cloud integration tests

Required when correctness depends on real AWS behavior, including:

- IAM permissions and denials;
- Cognito/JWT authorization;
- API Gateway integration;
- Lambda runtime/package deployment;
- DynamoDB provisioned configuration and conditional operations;
- SQS visibility/retry/DLQ behavior;
- EventBridge rule matching/routing;
- Step Functions retry/catch/wait/timeout execution;
- S3 policies/events/lifecycle;
- CloudWatch log/metric/alarm wiring;
- CDK deployment behavior.

### End-to-end tests

Keep E2E tests small and business-oriented.

Examples:

```text
Merchant creates product
      ↓
publishes product
      ↓
storefront displays product
```

```text
Customer places order
      ↓
stock reservation
      ↓
Mock Payment success
      ↓
order confirmed
```

```text
Mock Payment timeout-after-commit
      ↓
order enters ambiguous state
      ↓
reconciliation queries provider
      ↓
order resolves once
```

---

## 3. Test matrix by change type

| Change | Local check | AWS cloud verification |
|---|---|---|
| Pure domain rule | required | normally no |
| API DTO/validation | required | selected smoke test |
| DynamoDB key/access pattern | required | required before merge/release |
| IAM policy | synth/static check | required |
| Lambda packaging/runtime | build/unit | required |
| SQS/EventBridge behavior | contract/unit | required |
| Step Functions definition | definition/static tests | required |
| CDK resource change | synth/diff | required for material changes |
| Frontend-only styling | lint/unit | usually no |
| Crawler parser | fixture tests | live/manual sampling only |
| Accounting invariant | unit + integration | cloud only if infrastructure changed |

---

## 4. Failure-oriented tests

Happy-path testing is not sufficient for CommerceOS.

The suite should deliberately cover:

- duplicate event/message delivery;
- timeout before external commit;
- timeout after external commit;
- 5xx then success;
- webhook duplication;
- out-of-order callback where relevant;
- inventory concurrent reservation;
- cross-tenant access attempt;
- stale version/optimistic concurrency failure;
- DLQ routing after bounded retry;
- idempotency key replay;
- accounting duplicate source event;
- accounting unbalanced journal rejection;
- missing expected posting/reconciliation;
- crawler parser changes/missing fields.

Every production-relevant defect should prompt the question: **what automated test or guardrail should make this class of defect harder to repeat?**

---

## 5. Crawler testing

CI must not depend on Amazon, The Gioi Di Dong, Dien May Xanh, CellphoneS, or another live site being reachable.

Store sanitized parser fixtures in the repository where legally/technically appropriate, for example:

```text
tests/fixtures/product-sources/
  source-a/
    normal.html
    no-price.html
    changed-layout.html
```

Test flow:

```text
fixture
  ↓
source parser
  ↓
normalized external product
  ↓
schema/assertions
```

Live crawling is a controlled dev/staging operation with rate limits and source kill switches.

---

## 6. Test data

Use builders/factories so tests can create explicit tenant-scoped objects.

Minimum synthetic identities:

- Tenant A owner;
- Tenant A salesperson;
- Tenant A accountant;
- Tenant B owner;
- anonymous storefront customer.

Cross-tenant tests must be first-class acceptance tests rather than incidental security checks.

---

## 7. Verification commands

The project should evolve toward one stable entry point.

Target interface:

```text
python tools/commerceos.py check --fast
```

Runs:

- harness/document checks;
- format/lint;
- build;
- unit tests;
- architecture tests;
- fast contract tests.

```text
python tools/commerceos.py check
```

Adds:

- local integration tests;
- CDK synth;
- security/static checks.

```text
python tools/commerceos.py check --cloud --env pr-123
```

Adds selected real-AWS deployment/integration checks and tears down ephemeral resources when appropriate.

Until Phase 0 creates this command, `python3 scripts/harness_check.py` remains the repository harness entry point.

---

## 8. Release quality gates

A release candidate must not proceed merely because unit tests are green.

Required gates are selected by task impact and include:

1. repository harness PASS;
2. build/lint PASS;
3. unit/architecture tests PASS;
4. contract tests PASS;
5. CDK synth PASS;
6. cloud integration PASS for affected AWS semantics;
7. E2E PASS for critical flow changes;
8. no unexpected resource/cost increase in CDK diff;
9. reviewer checks task acceptance criteria and failure paths.

---

## 9. Free Tier testing policy

Cloud tests must be cost-aware:

- deploy only affected stacks;
- avoid high-volume load tests during normal PR validation;
- bound Step Functions executions/state transitions;
- use tiny DynamoDB provisioned capacity profiles where practical;
- keep SQS/event test volumes small;
- destroy preview/staging resources promptly;
- keep logs short-lived;
- do not run crawler schedules continuously merely to exercise Scheduler.

Load testing and fault-injection campaigns must define an estimated AWS cost before execution.
