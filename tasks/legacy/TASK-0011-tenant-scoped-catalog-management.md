# TASK-0011 — Deliver tenant-scoped catalog management

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 2
Milestone: Milestone A
Depends on: TASK-0010

## Goal

Merchant staff can create and edit products, categories, brands, and tenant-unique SKUs through a complete tenant-scoped catalog API.

## Business context

A usable canonical catalog needs merchant-facing organization and stable SKU identity without allowing source data or another tenant to own the records.

## In scope

- add Category and Brand ownership plus product create/update/list/search commands and queries;
- enforce normalized SKU uniqueness within a tenant and safe optimistic concurrency;
- expose paginated/filterable catalog APIs with deterministic validation and conflict responses;

## Out of scope

- publish/unpublish/archive transitions and product media;
- advanced search service, variants, promotions, or inventory quantities;

## Acceptance criteria

### AC01 — Catalog management

Given authorized merchant staff manage valid products, categories, and brands
when commands and queries run
then changes are visible only inside the same tenant with stable pagination.

### AC02 — SKU uniqueness

Given two products in one tenant request the same normalized SKU
when the second write is attempted
then it is atomically rejected while another tenant may use that SKU.

### AC03 — Concurrency protection

Given two editors update the same product version
when both writes race
then one succeeds and the stale update receives an explicit conflict without lost data.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected real-AWS evidence plus teardown is recorded.

## Architecture impact

- Owning domain: Catalog
- Domains touched: Catalog, API, Tenant authorization
- Persistence impact: Extend Catalog keys/indexes for SKU uniqueness, category/brand lookup, pagination, and optimistic versioning.
- Events/contracts impact: ProductCreated/ProductUpdated may be emitted only through the later reliable publication foundation; local domain facts may be recorded now.
- AWS/IaC impact: DynamoDB access patterns and Catalog API/Lambda routes.
- ADR required? No — the task follows accepted architecture; create one only if implementation changes a significant decision.

## Security and tenant impact

- Authentication: Use the established merchant or public identity boundary as applicable.
- Authorization: Role permissions distinguish catalog view and manage operations; all filters remain tenant-scoped.
- Tenant scoping: All merchant-owned reads/writes derive tenant scope from trusted context; client tenant ids cannot override it and cross-tenant denial is tested.
- Sensitive data/secrets: No secrets, tokens, real card data, or unnecessary personal data are stored or logged.
- Abuse/rate-limit considerations: Validate input size/shape and bound expensive or externally reachable operations.

## Reliability and idempotency impact

- Retry behavior: Synchronous failures are deterministic; retryable writes use the stated idempotency strategy.
- Timeout semantics: N/A unless an external boundary is called.
- Duplicate-delivery behavior: N/A — no at-least-once consumer is introduced.
- Idempotency key/strategy: Create uses a command id or conditional uniqueness record; update uses aggregate version.
- DLQ/recovery/reconciliation: N/A — no asynchronous work is introduced.

## Observability impact

- Logs: Structured logs include safe tenant/entity/job identifiers, operation, and correlation context.
- Metrics: Use built-in service metrics and only bounded business metrics justified by the operational risk.
- Traces/correlation: Preserve request/correlation identifiers across changed boundaries.
- Operational states/errors: SKU conflicts, stale versions, invalid references, and pagination errors use stable codes.

## Cost impact

- Request/compute impact: Scales with user requests or explicitly bounded background jobs.
- Storage impact: Extend Catalog keys/indexes for SKU uniqueness, category/brand lookup, pagination, and optimistic versioning.
- Network impact: Normal API traffic only unless external fetching is explicitly in scope.
- New AWS resources/services: DynamoDB access patterns and Catalog API/Lambda routes.
- Free Tier allowance relevant to this task: Use the documented Lambda/DynamoDB/S3/SQS/CloudWatch allowances and non-production caps where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measurements are material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview verification.

## Test plan

- Unit: SKU normalization/uniqueness policy, update rules, category/brand validation.
- Integration: DynamoDB uniqueness and optimistic-concurrency races plus tenant-scoped queries.
- Architecture: Enforce domain dependency direction, ownership, and trusted tenant context.
- Contract: Catalog management HTTP schemas and pagination contract.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Manage a small tenant catalog and prove isolation/unique SKU behavior.
- **Cloud verification required?** Yes — DynamoDB conditional/index behavior and API/IAM wiring require AWS evidence.
- AWS environment/stack(s) required: Catalog resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and record intentionally retained dev resources.

