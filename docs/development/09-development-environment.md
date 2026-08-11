# CommerceOS — Development Environment Strategy

_Last reviewed: 2026-08-11. ADR-012 is authoritative._

## 1. Goal

CommerceOS uses a **LocalStack-only infrastructure strategy**. No real AWS account is used for development, staging, validation, or deployment exercises.

```text
local-fast
   ↓
localstack-dev
   ↓
localstack-test
   ↓
localstack-stage (when production-shaped local validation is useful)
```

These are local configuration profiles, not cloud accounts.

The term **preview** is retained in the engineering vocabulary only for an ephemeral, isolated LocalStack validation instance created for a task, branch, or CI run. A preview is never a real AWS environment under ADR-012.

## 2. Profiles

### `local-fast`

Purpose:

- fastest inner loop;
- unit/architecture/contract tests;
- frontend work;
- parser fixtures;
- Mock Payment failure simulation;
- direct application host where infrastructure semantics are irrelevant.

No network dependency is required for normal domain development.

### `localstack-dev`

Purpose:

- developer learning/exploration;
- deploy AWS-style infrastructure locally;
- inspect queues/events/workflows/logs;
- synthetic integration data.

State may persist between runs, but all required resources must still be reproducible from repository-owned IaC/bootstrap commands.

### `localstack-test`

Purpose:

- deterministic infrastructure-sensitive integration tests;
- failure/retry/idempotency tests;
- destructive reset and repeatability checks;
- disposable branch/task **preview** instances when isolated infrastructure verification is useful.

The test profile should be disposable or reset before suites.

### `localstack-stage`

Purpose:

- production-shaped local E2E validation when a distinct stage adds value;
- deployment/redeployment rehearsal;
- failure/recovery scenarios using isolated local resources.

There is no AWS staging account and no production AWS target under the current decision.

## 3. Preview semantics

A LocalStack preview is an operational instance, not a separate business environment or cloud account.

Typical uses:

- a task/worktree needs isolated infrastructure-sensitive verification;
- CI creates a disposable LocalStack runtime for a selected integration job;
- a branch needs a reproducible short-lived environment to demonstrate a capability before merge.

Preview instances must use isolated ports/resource prefixes or an explicit serialization constraint, must be reproducible from repository-owned commands, and must be reset/removed deterministically. They must never require AWS credentials, IAM/OIDC federation, cloud authorization, or cloud teardown.

## 4. Inner and outer loops

### Inner loop

```text
edit -> build -> unit -> architecture -> contract -> repeat
```

### Infrastructure loop

```text
local checks PASS
      ↓
start/wait LocalStack
      ↓
CDK synth
      ↓
deploy selected stacks to LocalStack
      ↓
integration/E2E/failure checks
      ↓
reset or preserve state according to profile
```

A task may claim only the semantics actually demonstrated by the selected LocalStack setup. LocalStack evidence is not automatically evidence of exact AWS behavior.

## 5. Capability test strategy

| Capability | Fast strategy | Infrastructure strategy |
|---|---|---|
| serverless handlers | direct invocation/unit | LocalStack Lambda/API Gateway where supported |
| persistence | repository tests | LocalStack DynamoDB |
| identity edge | test principal adapter | LocalStack Cognito where sufficiently supported |
| work queue | deterministic adapter | LocalStack SQS/DLQ |
| fact routing | contract adapter | LocalStack EventBridge |
| durable workflow | definition/task tests | LocalStack Step Functions where supported |
| object storage | filesystem/in-memory port | LocalStack S3 |
| observability | logging interfaces | CloudWatch-style LocalStack APIs where useful |

Unsupported, partial, edition-dependent, or behaviorally different features must be documented as limitations. Do not silently assume AWS compatibility and do not fall back to real AWS by default.

## 6. Configuration contract

Runtime differences are configuration concerns. Infrastructure/delivery configuration may include:

```text
EnvironmentConfig
- name
- serviceEndpoint
- region
- syntheticAccessKey
- syntheticSecretKey
- accountIdPlaceholder
- resourcePrefix
- instanceId
- ports
- localStackFeatureFlags
- resetPolicy
- enableCrawler
- enableFailureInjection
```

Domain/Application code must not contain LocalStack-specific branching.

## 7. Data rules

All profiles use synthetic/generated data. No real card data is used because CommerceOS uses Mock Payment providers.

Persistent `localstack-dev` data is convenience state, never a prerequisite hidden from bootstrap scripts.

## 8. Parallel task isolation

Task/worktree instances derive distinct ports and resource prefixes. Shared mutable LocalStack resources are treated as exclusive only when configuration cannot isolate them safely.

## 9. References

- `../adr/ADR-012-localstack-only-infrastructure-runtime.md`
- `../architecture/localstack-runtime-and-lifecycle.md`
