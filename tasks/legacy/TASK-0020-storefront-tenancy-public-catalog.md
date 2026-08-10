# TASK-0020 — Expose tenant storefront configuration and public catalog contracts

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 4
Milestone: Milestone A
Depends on: TASK-0006, TASK-0012

## Goal

A merchant can configure a unique public storefront slug and anonymous shoppers can query only that tenant's published catalog through a versioned public contract.

## Business context

Storefront tenancy must resolve from a public slug without exposing private back-office data or trusting a caller-supplied tenant id.

## In scope

- manage basic storefront name, slug, status, and public settings with tenant authorization;
- resolve public tenant context from an active unique slug and expose paginated published-product list/detail APIs;
- define cache metadata, not-found behavior, and public-field filtering;

## Out of scope

- storefront React pages and CDN deployment;
- custom domains, cart, checkout, inventory availability, or promotions;

## Acceptance criteria

### AC01 — Public tenant resolution

Given an active tenant owns a unique storefront slug and published products
when an anonymous shopper queries that slug
then only the tenant's public catalog projection is returned.

### AC02 — Private data excluded

Given draft, unpublished, archived, cost, source-debug, or tenant-private fields exist
when public list/detail is requested
then none of those records or fields are exposed.

### AC03 — Slug lifecycle

Given a merchant requests an invalid, reserved, or already-used slug
when the setting is saved
then the change is atomically rejected without affecting the current storefront.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then all repository checks pass and real-AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Storefront / Tenant & Identity / Catalog
- Domains touched: Storefront, Tenant, Catalog public projection, API
- Persistence impact: Add storefront configuration and tenant-global slug uniqueness; public reads consume Catalog-owned projections.
- Events/contracts impact: StorefrontConfigured/SlugChanged only if a consumer exists; Product publication facts remain Catalog-owned.
- AWS/IaC impact: Public API routes, Lambda integration, and DynamoDB queries in CommerceStack.
- ADR required? No — follows accepted architecture; create one if a significant new decision emerges.

## Security and tenant impact

- Authentication: Use the established merchant identity or explicit anonymous storefront boundary.
- Authorization: Storefront configuration requires permission; anonymous endpoints expose allowlisted fields only.
- Tenant scoping: Protected writes use trusted tenant context; public reads resolve tenant internally from a validated active slug.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Reserved-slug rules, pagination, query limits, and cache-safe errors prevent enumeration/expensive scans.

## Reliability and idempotency impact

- Retry behavior: Synchronous failures are deterministic; retryable writes use explicit concurrency/idempotency controls.
- Timeout semantics: No external ambiguity is introduced unless stated.
- Duplicate-delivery behavior: N/A — no at-least-once consumer introduced.
- Idempotency key/strategy: Optimistic concurrency or command id protects unsafe repeated writes.
- DLQ/recovery/reconciliation: N/A — no asynchronous boundary introduced.

## Observability impact

- Logs: Structured logs contain operation, safe tenant/entity identifiers, and correlation id.
- Metrics: Use built-in metrics and bounded business metrics for meaningful risk.
- Traces/correlation: Preserve correlation across every API/application boundary changed here.
- Operational states/errors: Unknown, suspended, unpublished, and misconfigured storefront states have non-leaking responses.

## Cost impact

- Request/compute impact: Scales with bounded user traffic.
- Storage impact: Add storefront configuration and tenant-global slug uniqueness; public reads consume Catalog-owned projections.
- Network impact: Normal web/API payloads; avoid unbounded responses.
- New AWS resources/services: Public API routes, Lambda integration, and DynamoDB queries in CommerceStack.
- Free Tier allowance relevant to this task: Use existing pay-per-use services and documented learning-profile limits.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded preview/dev checks.

## Test plan

- Unit: Slug normalization/reservation, public-field filtering, and storefront state rules.
- Integration: Global slug uniqueness, public projection queries, pagination, and private-data leakage tests.
- Architecture: Verify domain ownership, inward dependencies, and no cross-domain persistence shortcuts.
- Contract: PublicStorefront and PublicProduct v1 HTTP schemas/cache metadata.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Configure a storefront, publish a product, and read it anonymously by slug.
- **Cloud verification required?** Yes — API Gateway public routes, Lambda/DynamoDB/IAM, and cache headers need selected AWS verification.
- AWS environment/stack(s) required: Storefront routes and Tenant/Catalog resources in CommerceStack
- Preview/staging teardown plan: Destroy preview resources; document retained dev state.

