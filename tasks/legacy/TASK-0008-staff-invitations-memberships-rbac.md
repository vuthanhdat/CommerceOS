# TASK-0008 — Manage staff invitations, memberships, and tenant roles

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 1
Milestone: Milestone A
Depends on: TASK-0007

## Goal

Tenant owners and admins can invite staff, activate or disable memberships, and assign the documented initial roles without escaping tenant boundaries.

## Business context

A business becomes usable only when staff can join with distinct responsibilities and disabled memberships stop authorizing immediately.

## In scope

- implement Invitation and UserMembership lifecycle including accept, expire, disable, and reactivate rules;
- implement Owner/Admin/Sales/Warehouse/Accountant/Viewer role assignment rules;
- deliver staff-management APIs and back-office screens with audit hooks;

## Out of scope

- granular permission claims beyond the initial role matrix;
- platform-admin impersonation or cross-tenant support access;

## Acceptance criteria

### AC01 — Invite and join

Given an authorized tenant owner/admin creates a valid invitation
when the intended user accepts it before expiry
then one active membership is created in the inviting tenant.

### AC02 — Tenant-safe role management

Given a tenant administrator changes a staff role or status
when the target membership belongs to the same tenant
then the permitted change succeeds and an unauthorized/cross-tenant change is denied.

### AC03 — Disabled membership

Given an active staff membership is disabled
when the user reuses a previously valid access token
then protected tenant operations are denied after membership resolution.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and the selected real-AWS verification evidence and teardown result are recorded.

## Architecture impact

- Owning domain: Tenant & Identity / Authorization
- Domains touched: Tenant, Back Office, API, Audit hooks
- Persistence impact: Add invitation and membership lifecycle records keyed by tenant and identity with uniqueness/expiry rules.
- Events/contracts impact: Versioned StaffInvited, StaffJoined, RoleAssigned, and MembershipDisabled business events or durable audit hooks.
- AWS/IaC impact: DynamoDB membership/invitation persistence and existing Cognito/API integration; email delivery is out of scope.
- ADR required? No — initial RBAC is already defined; granular permissions are deferred to TASK-0067.

## Security and tenant impact

- Authentication: Invitation acceptance binds a verified Cognito identity; membership status is checked on protected requests.
- Authorization: Only authorized same-tenant administrators manage staff; the last-owner and self-disable rules are explicit.
- Tenant scoping: Every tenant-owned operation derives tenant scope from trusted context; request data cannot override it and cross-tenant denial is tested.
- Sensitive data/secrets: Invitation tokens are hashed/redacted and never logged; only required staff contact data is stored.
- Abuse/rate-limit considerations: Invitation creation and acceptance are rate-limited, expire, and resist enumeration/replay.

## Reliability and idempotency impact

- Retry behavior: Invitation creation/acceptance is idempotent for the same invitation and identity.
- Timeout semantics: N/A unless an external/cloud boundary is exercised.
- Duplicate-delivery behavior: N/A — no at-least-once consumer is introduced.
- Idempotency key/strategy: An invitation can create at most one membership.
- DLQ/recovery/reconciliation: N/A — no asynchronous work is introduced.

## Observability impact

- Logs: Structured logs include operation, safe tenant/entity identifiers, and correlation context.
- Metrics: Use built-in metrics; add a bounded custom metric only for a meaningful operational risk.
- Traces/correlation: Preserve request/correlation identifiers across every boundary changed by this task.
- Operational states/errors: Invitation expiry, duplicate identity, forbidden role change, and disabled membership are diagnosable.

## Cost impact

- Request/compute impact: Low-volume membership/invitation reads and writes; no email service yet.
- Storage impact: Add invitation and membership lifecycle records keyed by tenant and identity with uniqueness/expiry rules.
- Network impact: No material transfer beyond normal API/cloud verification traffic.
- New AWS resources/services: DynamoDB membership/invitation persistence and existing Cognito/API integration; email delivery is out of scope.
- Free Tier allowance relevant to this task: Use the documented free/pay-per-use profile and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible at the learning profile; update the cost model if measured impact is material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for a bounded dev/preview verification; record actual resources and destroy ephemeral stacks.

## Test plan

- Unit: Invitation state transitions, role rules, last-owner protection, and membership activation.
- Integration: Tenant-scoped membership persistence, Cognito identity binding, and cross-tenant authorization.
- Architecture: Enforce dependency direction, domain ownership, and trusted tenant-context rules where relevant.
- Contract: Staff-management HTTP schemas and membership lifecycle events.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Owner invites staff, staff joins, role changes, then disabled access is denied.
- **Cloud verification required?** Yes — Cognito identity binding and protected API behavior require selected real-AWS verification.
- AWS environment/stack(s) required: IdentityStack and Tenant endpoints in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources after evidence is collected; document any intentionally retained dev resource.

