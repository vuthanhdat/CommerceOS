# TASK-0007 — Integrate Cognito authentication and trusted tenant context

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 1
Milestone: Milestone A
Depends on: TASK-0006

## Goal

Merchant staff can authenticate with Cognito and every protected request receives tenant context derived from verified identity and active membership.

## Business context

A client-supplied tenantId is not authorization. CommerceOS needs one reusable trust boundary before any tenant-owned API expands.

## In scope

- define and deploy Cognito user-pool/client configuration with cost-safe settings;
- validate JWTs at the API boundary and resolve active membership into a trusted request context;
- add reusable authorization/test fixtures for authenticated Tenant A, Tenant B, and anonymous requests;

## Out of scope

- staff invitation and role-management workflows;
- shopper accounts or social/enterprise federation;

## Acceptance criteria

### AC01 — Authenticated tenant context

Given a user has a valid Cognito token and active membership
when the user calls a protected endpoint
then the application receives the membership tenant and subject from trusted context.

### AC02 — Client tenant override denied

Given a Tenant A user supplies Tenant B's id in body, route, query, or header
when a protected operation is attempted
then the supplied value cannot replace trusted tenant scope.

### AC03 — Inactive or invalid identity

Given a token is invalid, expired, or maps only to an inactive membership
when a protected endpoint is called
then access is denied without leaking tenant data.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and the selected real-AWS verification evidence and teardown result are recorded.

## Architecture impact

- Owning domain: Tenant & Identity / Authorization
- Domains touched: API delivery, Tenant membership lookup, all protected domains
- Persistence impact: Membership lookup contract is introduced; no cross-domain table access is permitted.
- Events/contracts impact: Authentication does not emit domain events; membership lifecycle events remain TASK-0008.
- AWS/IaC impact: IdentityStack with Cognito plus API Gateway/JWT/Lambda authorization integration.
- ADR required? No — Cognito and trusted tenant context are accepted product invariants.

## Security and tenant impact

- Authentication: Cognito JWT verification and active membership resolution are the only merchant-user trust path.
- Authorization: Protected endpoints deny missing/invalid tokens and inactive membership; authorization failures reveal no cross-tenant existence.
- Tenant scoping: Every tenant-owned operation derives tenant scope from trusted context; request data cannot override it and cross-tenant denial is tested.
- Sensitive data/secrets: No secrets, tokens, real card data, or unnecessary personal data are stored or logged.
- Abuse/rate-limit considerations: Validate inputs and bound externally reachable or potentially expensive operations.

## Reliability and idempotency impact

- Retry behavior: N/A for the core synchronous behavior; callers receive deterministic failures.
- Timeout semantics: Membership/identity dependency failure returns an explicit unavailable/unauthorized outcome according to documented policy, never a caller-selected tenant fallback.
- Duplicate-delivery behavior: N/A — no at-least-once consumer is introduced.
- Idempotency key/strategy: Required only for retryable writes identified by this task.
- DLQ/recovery/reconciliation: N/A — no asynchronous work is introduced.

## Observability impact

- Logs: Structured logs include operation, safe tenant/entity identifiers, and correlation context.
- Metrics: Use built-in metrics; add a bounded custom metric only for a meaningful operational risk.
- Traces/correlation: Preserve request/correlation identifiers across every boundary changed by this task.
- Operational states/errors: Authentication failures distinguish invalid token, missing membership, inactive membership, and internal resolution failure without exposing secrets.

## Cost impact

- Request/compute impact: Cognito MAU plus bounded authorization/membership reads.
- Storage impact: Membership lookup contract is introduced; no cross-domain table access is permitted.
- Network impact: No material transfer beyond normal API/cloud verification traffic.
- New AWS resources/services: IdentityStack with Cognito plus API Gateway/JWT/Lambda authorization integration.
- Free Tier allowance relevant to this task: Cognito merchant MAU remains within the documented learning allowance; SMS is not enabled.
- Expected monthly cost change or `negligible` with rationale: negligible at the learning profile; update the cost model if measured impact is material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for a bounded dev/preview verification; record actual resources and destroy ephemeral stacks.

## Test plan

- Unit: JWT claim mapping, trusted-context construction, and invalid membership cases.
- Integration: Real Cognito sign-in/token/API authorization plus membership-scoped data access.
- Architecture: Enforce dependency direction, domain ownership, and trusted tenant-context rules where relevant.
- Contract: Authenticated request-context contract used by application commands.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Authenticate synthetic Tenant A and Tenant B staff and prove context isolation.
- **Cloud verification required?** Yes — Cognito token lifecycle, API Gateway authorizer behavior, Lambda integration, and IAM are real-AWS semantics.
- AWS environment/stack(s) required: IdentityStack and selected CommerceStack endpoints
- Preview/staging teardown plan: Destroy ephemeral resources after evidence is collected; document any intentionally retained dev resource.

