# TASK-0010 — Establish the canonical product model and access patterns

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 2
Milestone: Milestone A
Depends on: TASK-0009

## Goal

A merchant can create and retrieve a tenant-owned draft product whose model and DynamoDB access patterns are explicit and protected.

## Business context

Catalog is the canonical merchant-owned description of what can be sold; its ownership and queries must be stable before ingestion, storefront, inventory, or sales depend on it.

## In scope

- create Catalog Domain/Application/Infrastructure projects and the Product aggregate/value objects;
- support draft product creation and retrieval with name, description, base price, cost reference, specifications, and immutable tenant ownership;
- document and test DynamoDB keys, known queries, consistency choices, and domain ownership;

## Out of scope

- category/brand administration, publication lifecycle, and media references;
- variants, external imports, storefront projections, inventory, or sales behavior;

## Acceptance criteria

### AC01 — Tenant-owned draft

Given an authorized merchant provides valid product data
when CreateProduct succeeds
then one draft product is stored under trusted tenant scope and can be retrieved by that tenant.

### AC02 — Cross-tenant protection

Given Tenant B knows Tenant A's product id
when Tenant B requests or attempts to overwrite the product
then the operation is denied or returns non-disclosing not-found behavior.

### AC03 — Access-pattern evidence

Given the catalog repository is implemented
when documented create/get/list patterns are exercised
then queries use tenant keys without scans or cross-domain persistence access.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected real-AWS evidence plus teardown is recorded.

## Architecture impact

- Owning domain: Catalog
- Domains touched: Catalog, API, Tenant authorization
- Persistence impact: Add Catalog DynamoDB items/table and an access-pattern document for product create/get/list.
- Events/contracts impact: No event required for draft creation unless a committed consumer is identified.
- AWS/IaC impact: DynamoDB plus selected Catalog API/Lambda wiring in CommerceStack.
- ADR required? No — the task follows accepted architecture; create one only if implementation changes a significant decision.

## Security and tenant impact

- Authentication: Use the established merchant or public identity boundary as applicable.
- Authorization: Catalog create/read requires an authorized active membership; public access is not added.
- Tenant scoping: All merchant-owned reads/writes derive tenant scope from trusted context; client tenant ids cannot override it and cross-tenant denial is tested.
- Sensitive data/secrets: No secrets, tokens, real card data, or unnecessary personal data are stored or logged.
- Abuse/rate-limit considerations: Validate input size/shape and bound expensive or externally reachable operations.

## Reliability and idempotency impact

- Retry behavior: Synchronous failures are deterministic; retryable writes use the stated idempotency strategy.
- Timeout semantics: N/A unless an external boundary is called.
- Duplicate-delivery behavior: N/A — no at-least-once consumer is introduced.
- Idempotency key/strategy: Use optimistic concurrency or a stable command key where duplicate writes are unsafe.
- DLQ/recovery/reconciliation: N/A — no asynchronous work is introduced.

## Observability impact

- Logs: Structured logs include safe tenant/entity/job identifiers, operation, and correlation context.
- Metrics: Use built-in service metrics and only bounded business metrics justified by the operational risk.
- Traces/correlation: Preserve request/correlation identifiers across changed boundaries.
- Operational states/errors: Validation, duplicate command, missing product, and persistence-conflict errors are stable and diagnosable.

## Cost impact

- Request/compute impact: Scales with user requests or explicitly bounded background jobs.
- Storage impact: Add Catalog DynamoDB items/table and an access-pattern document for product create/get/list.
- Network impact: Normal API traffic only unless external fetching is explicitly in scope.
- New AWS resources/services: DynamoDB plus selected Catalog API/Lambda wiring in CommerceStack.
- Free Tier allowance relevant to this task: Use the documented Lambda/DynamoDB/S3/SQS/CloudWatch allowances and non-production caps where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measurements are material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview verification.

## Test plan

- Unit: Product invariants, money/specification value objects, and tenant ownership.
- Integration: DynamoDB create/get/list, conditional creation, and two-tenant API tests.
- Architecture: Enforce domain dependency direction, ownership, and trusted tenant context.
- Contract: Catalog create/get/list HTTP schemas.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Create and retrieve a draft product as Tenant A; deny Tenant B.
- **Cloud verification required?** Yes — DynamoDB keys/conditions, API integration, IAM, and Lambda packaging require selected AWS verification.
- AWS environment/stack(s) required: Catalog resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and record intentionally retained dev resources.

