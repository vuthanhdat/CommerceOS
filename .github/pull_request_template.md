## Task

- Task: `TASK-XXXX`
- Goal:

## What changed

- ...

## Acceptance criteria

- [ ] AC01
- [ ] AC02

## Verification

- [ ] `python3 scripts/harness_check.py`
- [ ] build/typecheck (when application code exists)
- [ ] unit tests
- [ ] integration tests where relevant
- [ ] architecture/contract/IaC checks where relevant

### Cloud verification

- Cloud verification required? Yes / No — why:
- AWS environment / stack(s):
- [ ] selected real-AWS checks passed, if required
- [ ] ephemeral preview/staging resources destroyed or intentionally retained with reason

Do not treat local emulation as sufficient evidence when correctness depends on IAM, Cognito, API Gateway integration, Lambda runtime/packaging, SQS/EventBridge/Step Functions semantics, S3 policy/events/lifecycle, or material CDK behavior.

## Architecture / domain review

- [ ] Owning domain is clear.
- [ ] No cross-domain persistence shortcut was introduced.
- [ ] New architecture decisions have an ADR when required.
- [ ] AWS application infrastructure is represented in CDK rather than undocumented Console configuration.
- [ ] New AWS services/resources are justified and cost impact is documented.

## Free Tier / cost review

- [ ] Relevant Free Tier allowance or credit-funded usage is identified.
- [ ] No prohibited recurring/base-cost service was introduced without ADR + estimate.
- [ ] Non-prod retention/capacity/concurrency/schedules are bounded.
- [ ] `cdk diff` was reviewed when infrastructure changed.
- Estimated monthly cost delta:
- Estimated one-off cloud verification cost:

## Security / multi-tenancy

- [ ] Tenant-owned operations derive tenant scope from trusted identity context.
- [ ] Cross-tenant behavior is tested where relevant.
- [ ] Authorization/input/secrets concerns are addressed.
- [ ] AWS CI/CD authentication follows the OIDC/temporary-credential policy when deployment is involved.

## Distributed-system review

If events, queues, external calls, retries, workflows, or payments are involved:

- [ ] idempotency considered;
- [ ] duplicate delivery safe;
- [ ] timeout semantics defined;
- [ ] retry/backoff defined;
- [ ] DLQ/recovery/reconciliation considered;
- [ ] correlation/causation preserved where applicable.

## Observability

- [ ] Important failures are diagnosable from logs/metrics/status.
- [ ] Logging/metrics choices respect non-prod Free Tier/cost guardrails.

## Harness impact

Did this change reveal a reusable harness gap?

- [ ] No
- [ ] Yes — describe the new/updated instruction, test, guardrail, tool, fixture, or documentation:

## Out-of-scope / follow-up

- ...
