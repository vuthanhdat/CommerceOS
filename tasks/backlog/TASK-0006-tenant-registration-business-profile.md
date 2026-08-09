# TASK-0006 — Deliver tenant registration and business profiles

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 1
Milestone: Milestone A
Depends on: TASK-0004

## Goal

A merchant can register a tenant with a server-generated identity and maintain its tenant-owned business profile.

## Business context

Every later business capability needs a stable tenant aggregate and trusted ownership boundary before tenant data is created.

## In scope

- introduce Tenant and BusinessProfile domain/application/infrastructure projects with explicit invariants;
- persist tenant and profile data using documented DynamoDB access patterns;
- expose onboarding and profile query/update APIs plus a minimal back-office onboarding surface;

## Out of scope

- Cognito authentication and staff membership;
- storefront slug/domain configuration and subscription billing;

## Acceptance criteria

### AC01 — Tenant registration

Given a valid new merchant registration
when the onboarding command succeeds
then one tenant with a server-generated immutable tenant id and initial business profile is created.

### AC02 — Profile ownership

Given an authenticated tenant context is available in tests
when a business profile is read or updated
then only the matching tenant profile is returned or changed.

### AC03 — Validation

Given registration or profile input is invalid
when the command is submitted
then it is rejected without a partial tenant/profile write.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and the selected real-AWS verification evidence and teardown result are recorded.

## Architecture impact

- Owning domain: Tenant & Identity
- Domains touched: Tenant, API, Back Office, Platform persistence
- Persistence impact: Add tenant-owned Tenant and BusinessProfile items/table with documented keys and atomic creation behavior.
- Events/contracts impact: Define versioned TenantCreated only if a real consumer exists; otherwise retain an application result without premature publication.
- AWS/IaC impact: DynamoDB and API/Lambda resources for the tenant slice when deployed through CDK.
- ADR required? No — uses accepted modular serverless and DynamoDB direction; create an ADR only if the access model changes architecture materially.

## Security and tenant impact

- Authentication: Registration bootstrap is an explicit onboarding boundary; profile operations require the trusted identity introduced by TASK-0007 before public release.
- Authorization: Tenant identifiers are server-generated; protected profile changes require owner/admin authorization once identity is connected.
- Tenant scoping: Every tenant-owned operation derives tenant scope from trusted context; request data cannot override it and cross-tenant denial is tested.
- Sensitive data/secrets: No secrets, tokens, real card data, or unnecessary personal data are stored or logged.
- Abuse/rate-limit considerations: Validate inputs and bound externally reachable or potentially expensive operations.

## Reliability and idempotency impact

- Retry behavior: Registration uses a stable onboarding key or uniqueness constraint so retry cannot create duplicate tenants.
- Timeout semantics: N/A unless an external/cloud boundary is exercised.
- Duplicate-delivery behavior: N/A — no at-least-once consumer is introduced.
- Idempotency key/strategy: Duplicate accepted onboarding submissions return the same logical result or a deterministic conflict.
- DLQ/recovery/reconciliation: N/A — no asynchronous work is introduced.

## Observability impact

- Logs: Structured logs include operation, safe tenant/entity identifiers, and correlation context.
- Metrics: Use built-in metrics; add a bounded custom metric only for a meaningful operational risk.
- Traces/correlation: Preserve request/correlation identifiers across every boundary changed by this task.
- Operational states/errors: Registration conflicts, validation failures, and profile update failures have stable error codes.

## Cost impact

- Request/compute impact: Low-volume API/Lambda and DynamoDB operations.
- Storage impact: Add tenant-owned Tenant and BusinessProfile items/table with documented keys and atomic creation behavior.
- Network impact: No material transfer beyond normal API/cloud verification traffic.
- New AWS resources/services: DynamoDB and API/Lambda resources for the tenant slice when deployed through CDK.
- Free Tier allowance relevant to this task: Use the documented free/pay-per-use profile and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible at the learning profile; update the cost model if measured impact is material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for a bounded dev/preview verification; record actual resources and destroy ephemeral stacks.

## Test plan

- Unit: Tenant/profile invariants, normalization, and registration uniqueness.
- Integration: DynamoDB atomic creation, tenant-scoped profile access, and API validation.
- Architecture: Enforce dependency direction, domain ownership, and trusted tenant-context rules where relevant.
- Contract: Tenant onboarding and business-profile HTTP schemas.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Register a synthetic merchant and read/update its profile.
- **Cloud verification required?** Yes — DynamoDB access behavior, Lambda packaging, API Gateway wiring, and IAM must be verified on AWS.
- AWS environment/stack(s) required: selected CommerceStack resources in dev or preview
- Preview/staging teardown plan: Destroy ephemeral resources after evidence is collected; document any intentionally retained dev resource.

