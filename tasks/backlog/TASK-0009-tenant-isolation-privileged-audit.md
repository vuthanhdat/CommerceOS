# TASK-0009 — Enforce tenant isolation and privileged audit guardrails

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 1
Milestone: Milestone A
Depends on: TASK-0006, TASK-0007, TASK-0008

## Goal

Current tenant-owned APIs are mechanically protected against cross-tenant access and privileged Tenant operations produce safe append-oriented audit records.

## Business context

Tenant isolation must be a reusable harness property rather than a convention each future feature can accidentally omit.

## In scope

- build reusable two-tenant integration fixtures and denial scenarios for Tenant, profile, invitation, and membership operations;
- introduce append-oriented Audit records for privileged tenant administration with actor, tenant, action, entity, timestamp, and correlation id;
- add architecture/static checks that require trusted tenant context in tenant-owned repository/application contracts where practical;

## Out of scope

- full platform-wide audit coverage for domains not yet implemented;
- platform administrator cross-tenant operations;

## Acceptance criteria

### AC01 — Cross-tenant denial suite

Given Tenant A and Tenant B identities and known entity ids exist
when each current tenant-owned read and write is exercised across tenants
then all cross-tenant attempts are denied without revealing object existence.

### AC02 — Audit evidence

Given an authorized privileged tenant action succeeds or is rejected for security reasons
when audit recording applies
then a safe immutable record links actor, tenant, action, entity, outcome, and correlation id.

### AC03 — Reusable guardrail

Given a future tenant repository/API follows the module conventions
when architecture and integration fixtures are applied
then missing trusted tenant scope or obvious client-tenant trust causes a test failure.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and the selected real-AWS verification evidence and teardown result are recorded.

## Architecture impact

- Owning domain: Tenant & Identity / Audit / Engineering Harness
- Domains touched: All current and future tenant-owned domains
- Persistence impact: Add append-oriented tenant Audit records owned by Audit; business domains do not write Audit persistence directly.
- Events/contracts impact: Define an explicit audit application contract; audit records are not treated as domain events.
- AWS/IaC impact: DynamoDB audit persistence and selected API integration; built-in logs remain bounded.
- ADR required? No — implements mandatory existing tenant and audit invariants.

## Security and tenant impact

- Authentication: Tests use verified Tenant A/Tenant B identities and a separately modeled privileged path.
- Authorization: Cross-tenant access is denied at authorization and data-access boundaries; audit access is restricted and audited.
- Tenant scoping: Every tenant-owned operation derives tenant scope from trusted context; request data cannot override it and cross-tenant denial is tested.
- Sensitive data/secrets: Audit before/after summaries exclude secrets, tokens, full sensitive payloads, and mock card material.
- Abuse/rate-limit considerations: Audit input size/cardinality is bounded and attacker-controlled values are sanitized.

## Reliability and idempotency impact

- Retry behavior: Audit append uses a stable operation/correlation key where retries could duplicate the same logical record.
- Timeout semantics: N/A unless an external/cloud boundary is exercised.
- Duplicate-delivery behavior: Repeated delivery of the same audit command does not create misleading duplicate logical evidence.
- Idempotency key/strategy: Privileged action id or correlation/causation identity provides deduplication where required.
- DLQ/recovery/reconciliation: N/A — no asynchronous work is introduced.

## Observability impact

- Logs: Structured logs include operation, safe tenant/entity identifiers, and correlation context.
- Metrics: Use built-in metrics; add a bounded custom metric only for a meaningful operational risk.
- Traces/correlation: Preserve request/correlation identifiers across every boundary changed by this task.
- Operational states/errors: Cross-tenant denials and audit-write failures are visible without leaking target details.

## Cost impact

- Request/compute impact: Small additional audit writes and integration-test traffic.
- Storage impact: Add append-oriented tenant Audit records owned by Audit; business domains do not write Audit persistence directly.
- Network impact: No material transfer beyond normal API/cloud verification traffic.
- New AWS resources/services: DynamoDB audit persistence and selected API integration; built-in logs remain bounded.
- Free Tier allowance relevant to this task: Use the documented free/pay-per-use profile and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible at the learning profile; update the cost model if measured impact is material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for a bounded dev/preview verification; record actual resources and destroy ephemeral stacks.

## Test plan

- Unit: Audit record validation/redaction and trusted-context policies.
- Integration: Reusable cross-tenant API/repository suite plus audit persistence and authorization.
- Architecture: Enforce dependency direction, domain ownership, and trusted tenant-context rules where relevant.
- Contract: Audit append/query application contract.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Execute privileged tenant changes and inspect authorized audit history.
- **Cloud verification required?** Yes — IAM, DynamoDB key enforcement, API authorization, and Cognito-backed cross-tenant behavior need real-AWS evidence.
- AWS environment/stack(s) required: IdentityStack and Tenant/Audit resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources after evidence is collected; document any intentionally retained dev resource.

