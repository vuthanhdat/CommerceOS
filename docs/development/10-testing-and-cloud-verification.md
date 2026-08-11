# CommerceOS — Testing & Infrastructure Verification

_Last reviewed: 2026-08-11. ADR-012 is authoritative._

## 1. Purpose

CommerceOS separates business correctness from infrastructure-sensitive correctness without using a real AWS account.

```text
business/domain correctness
      ↓
fast local tests
      ↓
infrastructure-sensitive correctness
      ↓
LocalStack integration tests
      ↓
local release confidence
```

LocalStack results prove behavior against the declared emulator setup only. They do not prove exact AWS production behavior.

## 2. Test layers

### Unit tests

Fast, deterministic, no infrastructure dependency. Cover domain invariants, calculations, state transitions, accounting rules, inventory concurrency logic, payment ambiguity/reconciliation decisions, and event creation.

### Architecture tests

Mechanically enforce:

- Domain must not depend on AWS SDKs, LocalStack packages, HTTP frameworks, or persistence implementations;
- Application depends on project-owned ports/contracts rather than emulator details;
- one module cannot depend on another module's Infrastructure/private persistence;
- tenant scoping rules and contract direction remain intact.

### Contract tests

Cover HTTP DTOs, Mock Provider APIs, event envelopes/versions, queue messages, and crawler normalized schemas without requiring live external sites.

### Local integration tests

Use direct/local adapters where infrastructure semantics are not the subject of the test.

### LocalStack integration tests

Use when correctness depends on an AWS-style infrastructure capability supported by the selected LocalStack setup, including:

- DynamoDB conditional writes/transactions/access patterns;
- SQS retry/redrive/DLQ and duplicate delivery behavior;
- EventBridge routing/version matching;
- Step Functions retry/catch/wait/branch behavior where supported;
- API Gateway/Lambda integration where supported;
- Cognito delivery-edge behavior where sufficiently supported;
- S3 integration;
- CDK/CloudFormation-compatible deployment/redeployment to LocalStack.

IAM/control-plane fidelity, quotas, managed-service timing, and other unsupported/different behaviors must be recorded as limitations rather than assumed equivalent to AWS.

## 3. Change matrix

| Change | Required verification |
|---|---|
| Pure domain rule | unit + architecture as applicable |
| API DTO/validation | local contract/integration |
| DynamoDB key/access pattern | repository tests + LocalStack DynamoDB |
| Lambda/API delivery wiring | build/unit + LocalStack where supported |
| SQS/EventBridge behavior | contract/unit + LocalStack |
| Step Functions definition | definition tests + LocalStack execution when supported |
| CDK resource change | assertion + synth + LocalStack deployment when material |
| Frontend-only styling | lint/unit/build |
| Accounting invariant | unit + owned persistence integration |

No row requires real-AWS validation.

## 4. Failure-oriented tests

Cover duplicate/out-of-order delivery, timeout before/after external commit, retry then success, provider callback duplication, concurrent reservation, cross-tenant access, stale revisions, DLQ routing, idempotency replay, accounting duplicate source facts, unbalanced journals, and parser changes.

## 5. LocalStack lifecycle in tests

Infrastructure suites must be reproducible:

```text
start/wait LocalStack
  ↓
bootstrap/deploy declared infrastructure
  ↓
seed deterministic synthetic data
  ↓
run tests
  ↓
collect diagnostics
  ↓
logical reset or clean reset
```

Tests must not depend on manually-created emulator resources.

## 6. Limitations rule

When the needed LocalStack feature is unsupported, partial, behaviorally different, or edition-dependent:

1. record the exact gap;
2. test the project-owned capability contract at the nearest reliable layer;
3. preserve production-portable Domain/Application boundaries;
4. do not claim AWS-equivalence;
5. do not require a real AWS fallback unless ADR-012 is explicitly superseded.

## 7. Verification commands

The repository should evolve toward stable commands such as:

```text
python tools/commerceos.py check --fast
python tools/commerceos.py check
python tools/commerceos.py check --localstack --instance <id>
```

The LocalStack mode adds infrastructure bootstrap plus selected integration/E2E checks and performs the reset policy declared by the task/profile.

Until implemented, `python3 scripts/harness_check.py` remains the repository verification entry point.

## 8. Release gates

Select gates by task impact:

1. repository harness PASS;
2. build/lint PASS;
3. unit/architecture PASS;
4. contract tests PASS;
5. CDK synth/assertion PASS when infrastructure changes;
6. LocalStack integration PASS for affected supported capabilities;
7. E2E/failure tests PASS for critical flow changes;
8. known emulator limitations documented;
9. reviewer confirms acceptance criteria and failure paths.

There is no AWS cloud execution, AWS cost, AWS account, or cloud teardown gate.
