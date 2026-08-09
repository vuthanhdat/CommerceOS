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

## Architecture / domain review

- [ ] Owning domain is clear.
- [ ] No cross-domain persistence shortcut was introduced.
- [ ] New architecture decisions have an ADR when required.
- [ ] New AWS services/resources are justified and cost impact is documented.

## Security / multi-tenancy

- [ ] Tenant-owned operations derive tenant scope from trusted identity context.
- [ ] Cross-tenant behavior is tested where relevant.
- [ ] Authorization/input/secrets concerns are addressed.

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

## Harness impact

Did this change reveal a reusable harness gap?

- [ ] No
- [ ] Yes — describe the new/updated instruction, test, guardrail, tool, fixture, or documentation:

## Out-of-scope / follow-up

- ...
