# TASK-0067 — Move authorization from coarse roles to granular permissions

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 16
Milestone: Milestone E
Depends on: TASK-0008, TASK-0066

## Goal

Every protected CommerceOS operation is authorized by an explicit granular permission matrix instead of scattered hard-coded role checks, while existing tenant roles map predictably to permissions.

## Business context

As capabilities grow, coarse roles cannot safely express refund, inventory adjustment, journal posting, source control, recovery, and platform-admin authority.

## In scope

- inventory protected operations and define versioned permissions, role-to-permission defaults, owner safeguards, and migration/compatibility policy;
- centralize authorization policies/context checks across API, application commands, async/manual operations, and Back Office action discovery;
- add matrix-driven unit/integration tests and audit coverage for sensitive allow/deny decisions;

## Out of scope

- tenant-defined custom roles/ABAC, external enterprise federation, or platform-admin impersonation;
- relying on hidden UI controls instead of server authorization;

## Acceptance criteria

### AC01 — Permission matrix

Given all protected endpoints/commands/actions are inventoried
when the matrix is validated
then each has an owning permission, allowed default roles, tenant/platform scope, and audit requirement.

### AC02 — Server enforcement

Given users with allowed/denied permissions invoke representative operations
when authorization runs
then allowed actions work and denied/cross-tenant actions fail regardless of UI.

### AC03 — Migration compatibility

Given existing role memberships are migrated/resolved
when users sign in
then permissions match documented defaults without accidentally expanding privilege.

### AC04 — Guardrail

Given a new protected route/command is added without declared permission policy
when architecture/security checks run
then verification fails with actionable guidance.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and cloud evidence, cost, and teardown/retention decisions are recorded.

## Architecture impact

- Owning domain: Tenant & Identity / Authorization / Engineering Harness
- Domains touched: All protected domains, Back Office, Audit, Platform Operations
- Persistence impact: Add permission/role mapping version/config only if needed; domain records remain unchanged.
- Events/contracts impact: Role/PermissionAssigned changes are versioned/audited; async worker authority is IAM/application policy, not user-role spoofing.
- AWS/IaC impact: API/Lambda authorization and IAM policy refinements; no new managed service.
- ADR required? Yes if authorization architecture materially changes beyond the accepted initial RBAC evolution.

## Security and tenant impact

- Authentication: Use established merchant/platform/deployment identities.
- Authorization: This task defines/enforces least privilege, owner/admin safeguards, and explicit platform versus tenant authority.
- Tenant scoping: Permission cannot grant another tenant's data scope; platform cross-tenant permission is separate and audited.
- Sensitive data/secrets: Secrets/PII/payment/audit data are minimized, protected, and redacted from logs/tests.
- Abuse/rate-limit considerations: Rate-limit sensitive operations and prevent permission enumeration/self-escalation.

## Reliability and idempotency impact

- Retry behavior: Retry behavior is tested and unsafe duplicates are protected.
- Timeout semantics: Timeout/error behavior is included in the hardening evidence.
- Duplicate-delivery behavior: Duplicate behavior is tested where operations can repeat.
- Idempotency key/strategy: Role/permission changes use membership/config version and command id.
- DLQ/recovery/reconciliation: Applicable recovery paths are verified or explicitly N/A.

## Observability impact

- Logs: Structured, redacted logs retain safe tenant/entity/event/operation/correlation context.
- Metrics: Use built-in metrics first; measure security, saturation, errors, latency, failures, recovery, and cost at bounded cardinality.
- Traces/correlation: Verify end-to-end correlation/causation through affected journeys.
- Operational states/errors: Authorization deny reason category, policy version, actor and correlation are auditable without leaking targets.

## Cost impact

- Request/compute impact: Measured/bounded according to the hardening scenario.
- Storage impact: Add permission/role mapping version/config only if needed; domain records remain unchanged.
- Network impact: Measured and bounded; no unapproved fixed-cost network component.
- New AWS resources/services: API/Lambda authorization and IAM policy refinements; no new managed service.
- Free Tier allowance relevant to this task: Respect the approximately USD 100 credit envelope and normal $0–$5/month target.
- Expected monthly cost change or `negligible` with rationale: Record measured change; update docs/04-cost-model.md when material.
- Estimated one-off cloud-test/load-test cost, if any: Estimate and approve before execution; record actual spend/request volume afterward.

## Test plan

- Unit: Complete role-permission matrix, owner safeguards, policy composition, and action discovery.
- Integration: Representative allowed/denied/cross-tenant tests for every permission family and API/async admin path.
- Architecture: Re-run and extend tenant/domain/event/IaC/security guardrails for discovered recurring risks.
- Contract: Permission claims/policy and Back Office capabilities contract.
- IaC: CDK assertions/synth/diff plus real AWS policy/resource verification.
- E2E/manual: Run Owner/Admin/Sales/Warehouse/Accountant/Viewer journeys and prove sensitive denials.
- **Cloud verification required?** Yes — Cognito/API authorization and least-privilege IAM behavior need real AWS verification.
- AWS environment/stack(s) required: IdentityStack and representative Commerce/Async endpoints
- Preview/staging teardown plan: Destroy ephemeral resources and document intentionally retained protected data.

