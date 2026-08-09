# CommerceOS — Definition of Done

A task is complete only when its behavior, constraints, and verification are complete enough to merge safely.

## Mandatory checklist

### Product / scope

- [ ] Task goal is satisfied.
- [ ] All acceptance criteria are satisfied.
- [ ] Out-of-scope work was not silently added.
- [ ] Follow-up work is recorded separately when necessary.

### Code quality

- [ ] Code builds successfully.
- [ ] Naming and structure match repository conventions.
- [ ] No obvious dead code, disabled checks, or commented-out implementation remains.
- [ ] Complexity introduced is justified by the task.

### Testing

- [ ] Unit tests cover business rules changed by the task.
- [ ] Integration tests cover persistence/external-boundary behavior where relevant.
- [ ] Architecture tests cover new structural rules where relevant.
- [ ] Regression tests are added for fixed defects when practical.
- [ ] Failure paths are tested for distributed/external operations.
- [ ] Task explicitly states whether real-AWS cloud verification is required.
- [ ] If AWS semantics are affected, selected real-AWS integration verification has passed; local emulation alone is not treated as sufficient evidence.
- [ ] Ephemeral preview/staging resources created for verification were destroyed or intentionally retained with a documented reason.

### Multi-tenancy and security

- [ ] Tenant-owned operations are scoped from trusted authentication context.
- [ ] Cross-tenant access behavior is tested when tenant data is affected.
- [ ] Authorization is explicit for protected operations.
- [ ] No secret, credential, real payment data, or sensitive fixture is committed.
- [ ] Input validation and abuse/throttling implications are considered.
- [ ] CI/CD does not rely on long-lived AWS access keys when OIDC/temporary credentials are the defined deployment mechanism.

### Distributed-system correctness

When the task uses events, queues, external calls, retries, workflows, or payment behavior:

- [ ] idempotency is considered and implemented where side effects can repeat;
- [ ] duplicate delivery is safe;
- [ ] timeout semantics are defined;
- [ ] retry/backoff behavior is defined;
- [ ] poison/failure handling and DLQ/recovery are considered;
- [ ] correlation/causation identifiers are preserved where applicable;
- [ ] reconciliation is considered for ambiguous or eventually consistent state.

### Domain integrity

- [ ] Domain boundaries remain intact.
- [ ] No direct cross-domain persistence shortcut was introduced.
- [ ] Relevant business invariants remain true.
- [ ] Accounting changes preserve balanced entries and posted-journal immutability.
- [ ] Inventory changes preserve concurrency-safe stock invariants.

### Observability

- [ ] Important failure paths are observable.
- [ ] Structured logs include correlation context where applicable.
- [ ] New operational states can be diagnosed without attaching a debugger in production.
- [ ] Metrics/alarms are added when the task introduces a meaningful operational risk.
- [ ] Non-production log retention/metric choices respect the Free Tier/cost guardrails.

### Architecture and cost

- [ ] Architecture impact is documented.
- [ ] Significant architecture decisions have an ADR.
- [ ] New AWS services/resources have a stated reason.
- [ ] AWS application infrastructure is represented in AWS CDK rather than hidden manual Console configuration.
- [ ] `cdk synth`/IaC validation passes when infrastructure is affected.
- [ ] `cdk diff` is reviewed for material resource/IAM/replacement changes before deployment.
- [ ] Material monthly-cost impact is estimated or noted in the cost model.
- [ ] Relevant Free Tier allowance or credit-funded behavior is identified.
- [ ] Resource retention/cleanup behavior is defined where applicable.
- [ ] New recurring/base-cost services comply with the ADR + cost-estimate rule.

### Documentation

- [ ] Product/domain/architecture docs are updated if behavior or contracts changed.
- [ ] Public/domain event contracts are documented when introduced or changed.
- [ ] Environment/CI-CD/IaC/cost documentation is updated if deployment behavior changes.
- [ ] Task completion summary is recorded before moving the task to `completed`.

### Verification

- [ ] `python3 scripts/harness_check.py` passes.
- [ ] All implementation-specific verification commands pass.
- [ ] Required cloud verification passes or is explicitly N/A with rationale.
- [ ] No guardrail was weakened merely to make the change pass.

## Review principle

The implementation author/agent should not treat its own successful implementation as proof of correctness. Review must re-check the task from the perspective of acceptance criteria, invariants, failure modes, architecture boundaries, security, environment behavior, AWS semantics, and cost.
