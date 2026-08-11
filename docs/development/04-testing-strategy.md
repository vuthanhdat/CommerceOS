# CommerceOS — Testing Strategy

## 1. Principle

Testing is part of the development harness, not a cleanup step after implementation.

Use the cheapest test that can reliably detect the failure class while exercising critical boundaries end-to-end when lower layers are insufficient.

ADR-012 makes LocalStack the only infrastructure target. LocalStack evidence proves behavior against the declared emulator setup, not exact AWS production equivalence.

## 2. Test layers

### Unit tests

Use for domain invariants, value objects, calculations, journal balancing, inventory/order state transitions, and pure mapping/normalization logic. Unit tests require no infrastructure or network access.

### Integration tests

Use for persistence/access patterns, tenant scoping, API authorization boundaries, event/message serialization, duplicate/redelivery behavior, Mock Provider behavior, callback deduplication, and reconciliation flows.

Prefer deterministic direct adapters when infrastructure semantics are irrelevant. Use LocalStack when an AWS-style capability is the behavior under test and the selected setup supports it sufficiently.

### Architecture tests

Use for dependency direction, domain isolation, forbidden AWS SDK/LocalStack/framework dependencies in Domain/Application, cross-module reference constraints, and valuable contract conventions.

### Contract tests

Use for Mock Provider APIs/callbacks, integration-event schemas, public HTTP contracts, and crawler normalized output.

### Infrastructure tests

Use for:

- CDK synth/assertions;
- expected resources and source-of-truth checks;
- LocalStack bootstrap/deploy/reset/redeploy;
- DynamoDB conditional/transaction behavior;
- queue DLQ/redrive behavior;
- EventBridge routing;
- Step Functions execution semantics where supported;
- API Gateway/Lambda/Cognito/S3 integration where supported;
- configuration isolation across task instances.

IAM/control-plane fidelity, quotas, managed-service timing, and other unsupported/different emulator behaviors are recorded as limitations instead of being silently treated as equivalent to AWS.

### End-to-end tests

Use sparingly for high-value business journeys and failure/recovery paths.

## 3. Mandatory scenario families

### Multi-tenant

- Tenant A can access its own object;
- Tenant B cannot access Tenant A data even with a known ID;
- client-supplied Tenant selectors cannot override trusted Tenant context;
- privileged platform contexts remain explicit and separate.

### Event/queue

- duplicate delivery;
- temporary failure then retry;
- poison/DLQ behavior;
- out-of-order delivery where relevant;
- unsupported/old contract version behavior.

### Payment/provider

- success/decline;
- transient failure then retry;
- timeout before/after provider commit;
- delayed result;
- duplicate callback;
- refund;
- idempotency replay/reconciliation.

### Accounting

- balanced journal accepted;
- unbalanced rejected;
- posted mutation rejected;
- reversal/correction remains auditable;
- duplicate source fact does not duplicate a logical posting.

### Inventory

- sufficient/insufficient reservation;
- concurrent reservation of final units;
- release/issue/return semantics as approved;
- movement auditability.

## 4. Regression rule

For meaningful defects, add a regression test at the lowest reliable layer and then ask whether the same class deserves a reusable harness guardrail.

## 5. Infrastructure limitation rule

When required LocalStack behavior is unsupported, partial, behaviorally different, or edition-dependent:

1. record the exact gap;
2. keep the architecture capability/project contract unchanged;
3. test at the nearest reliable layer;
4. do not add Domain/Application workarounds for the emulator;
5. do not claim exact AWS equivalence;
6. do not fall back to real AWS unless ADR-012 is explicitly superseded.

## 6. Verification entry point

`python3 scripts/harness_check.py` remains the repository entry point. As implementation grows, it should orchestrate restore/install, format/lint, build/typecheck, unit/architecture/contract/integration tests, CDK checks, security/static checks, and selected LocalStack infrastructure verification where appropriate.

Agents and CI should call the same stable entry point whenever practical.

## 7. Test-quality rules

- test observable behavior/invariants, not implementation trivia;
- never weaken assertions merely to make a refactor/emulator pass;
- avoid wall-clock sleeps when deterministic clocks/events can be injected;
- make failure-injection scenarios deterministic;
- preserve useful failure diagnostics;
- keep distributed failure scenarios first-class as the architecture grows.
