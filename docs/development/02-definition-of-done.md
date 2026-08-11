# CommerceOS — Definition of Done

A task is complete only when its behavior, constraints, and verification are complete enough to merge safely.

## Mandatory checklist

### Product / scope

- [ ] Task goal and all acceptance criteria are satisfied.
- [ ] Out-of-scope work was not silently added.
- [ ] Follow-up work is recorded separately when necessary.

### Code quality

- [ ] Code builds successfully.
- [ ] Naming/structure match repository conventions.
- [ ] No obvious dead code, disabled checks, or commented-out implementation remains.
- [ ] Complexity introduced is justified by the task.

### Testing

- [ ] Unit tests cover changed business rules.
- [ ] Integration tests cover persistence/external/infrastructure boundaries where relevant.
- [ ] Architecture tests cover new structural rules where relevant.
- [ ] Regression tests are added for fixed defects when practical.
- [ ] Failure paths are tested for distributed/external operations.
- [ ] Task states whether LocalStack/infrastructure verification is required.
- [ ] If an AWS-style infrastructure capability is affected and supported sufficiently, selected LocalStack integration verification has passed.
- [ ] Unsupported/partial/behaviorally different/edition-dependent LocalStack features are explicitly documented; no exact-AWS compatibility claim is made without evidence.
- [ ] Task-owned LocalStack state/resources are reset/removed according to the declared lifecycle when required.

### Multi-tenancy and security

- [ ] Tenant-owned operations are scoped from trusted execution/authentication context.
- [ ] Cross-tenant access behavior is tested when tenant data is affected.
- [ ] Authorization is explicit for protected operations.
- [ ] No secret, credential, real payment data, or sensitive fixture is committed.
- [ ] LocalStack synthetic credentials/endpoints remain configuration concerns and do not leak into Domain/Application code.
- [ ] Input validation and abuse/throttling implications are considered where relevant.

### Distributed-system correctness

When the task uses events, queues, external calls, retries, workflows, or payment behavior:

- [ ] idempotency is implemented where side effects can repeat;
- [ ] duplicate/out-of-order delivery is safe where applicable;
- [ ] timeout/Unknown semantics are defined;
- [ ] retry/backoff behavior is defined;
- [ ] poison/failure handling and DLQ/recovery are considered;
- [ ] correlation/causation identifiers are preserved where applicable;
- [ ] reconciliation is considered for ambiguous/eventually consistent state.

### Domain integrity

- [ ] Domain boundaries remain intact.
- [ ] No direct cross-domain persistence shortcut was introduced.
- [ ] Relevant business invariants remain true.
- [ ] Accounting changes preserve balanced entries and posted-journal immutability.
- [ ] Inventory changes preserve concurrency-safe stock invariants.

### Observability

- [ ] Important failure paths are observable.
- [ ] Structured logs include correlation context where applicable.
- [ ] New operational states can be diagnosed from test/runtime evidence.
- [ ] Metrics/alarms/log evidence are added when the task introduces meaningful operational risk and the selected LocalStack setup supports them sufficiently.

### Architecture and infrastructure

- [ ] Architecture impact is documented.
- [ ] Significant architecture decisions have an ADR.
- [ ] New infrastructure capabilities/resources have a stated problem/rationale.
- [ ] AWS-style application infrastructure is represented in CDK/repository bootstrap rather than hidden manual LocalStack setup.
- [ ] `cdk synth`/IaC validation passes when infrastructure is affected.
- [ ] LocalStack endpoint, synthetic credentials, region/account placeholders, ports, instance prefixes, and feature switches are configuration concerns.
- [ ] Bootstrap/reset/redeploy behavior is defined where applicable.
- [ ] No task requires a real AWS account, IAM/OIDC federation, AWS Budget/credit monitoring, or real-cloud preview/staging evidence unless a later ADR explicitly supersedes ADR-012.

### Documentation

- [ ] Product/domain/architecture docs are updated if behavior/contracts changed.
- [ ] Public/domain event contracts are documented when introduced/changed.
- [ ] Environment/CI/IaC/LocalStack limitation documentation is updated if runtime behavior changes.
- [ ] Task completion summary is recorded before moving the task to `completed`.

### Verification

- [ ] `python3 scripts/harness_check.py` passes.
- [ ] All implementation-specific verification commands pass.
- [ ] Required LocalStack/infrastructure verification passes or is explicitly N/A with rationale.
- [ ] No guardrail was weakened merely to make the change pass.

## Review principle

The implementation author/agent should not treat its own successful implementation as proof of correctness. Review re-checks acceptance criteria, invariants, failure modes, architecture boundaries, tenant/security rules, LocalStack/runtime behavior, and known emulator limitations.
