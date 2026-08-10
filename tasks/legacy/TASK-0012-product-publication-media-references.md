# TASK-0012 — Deliver product publication and media references

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 2
Milestone: Milestone A
Depends on: TASK-0011

## Goal

Authorized merchants can publish, unpublish, and archive valid products and attach policy-safe media references without exposing invalid catalog data publicly.

## Business context

Publication is a business transition separating incomplete back-office records from sellable public catalog content.

## In scope

- implement publish/unpublish/archive state transitions and required-data validation;
- add merchant-owned or permitted external media-reference metadata with ordering and attribution;
- define the public product projection contract produced from published canonical data;

## Out of scope

- binary image processing/upload pipeline and copyrighted source-image copying;
- storefront UI, CDN caching, variants, inventory availability, or promotions;

## Acceptance criteria

### AC01 — Valid publication

Given a draft product contains required sellable fields
when an authorized user publishes it
then its public projection becomes readable and ProductPublished is represented as a meaningful domain fact.

### AC02 — Invalid lifecycle

Given a product is incomplete or archived
when publish or edit attempts violate lifecycle rules
then the operation is rejected and archived products cannot be newly published.

### AC03 — Safe media references

Given media metadata is added
when the reference is saved or projected
then ownership/license/attribution fields are preserved and no external image is copied without permission.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected real-AWS evidence plus teardown is recorded.

## Architecture impact

- Owning domain: Catalog / Files & Media
- Domains touched: Catalog, Storefront contract, Audit
- Persistence impact: Extend Product lifecycle/version and media-reference records; public projection remains Catalog-owned.
- Events/contracts impact: Define versioned ProductPublished/ProductUnpublished facts for later publication; no direct cross-domain table write.
- AWS/IaC impact: Catalog DynamoDB/API changes; S3 upload infrastructure is out of scope unless merchant-owned media handling is explicitly added via follow-up.
- ADR required? No — the task follows accepted architecture; create one only if implementation changes a significant decision.

## Security and tenant impact

- Authentication: Use the established merchant or public identity boundary as applicable.
- Authorization: Catalog lifecycle and media management require explicit permissions; public projection exposes only approved fields.
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
- Operational states/errors: Invalid transitions, missing publication fields, broken media metadata, and projection failures are visible.

## Cost impact

- Request/compute impact: Scales with user requests or explicitly bounded background jobs.
- Storage impact: Extend Product lifecycle/version and media-reference records; public projection remains Catalog-owned.
- Network impact: Normal API traffic only unless external fetching is explicitly in scope.
- New AWS resources/services: Catalog DynamoDB/API changes; S3 upload infrastructure is out of scope unless merchant-owned media handling is explicitly added via follow-up.
- Free Tier allowance relevant to this task: Use the documented Lambda/DynamoDB/S3/SQS/CloudWatch allowances and non-production caps where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measurements are material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview verification.

## Test plan

- Unit: Lifecycle state machine, publication validation, and media-reference rules.
- Integration: Atomic state transitions, public projection filtering, and cross-tenant media/product access.
- Architecture: Enforce domain dependency direction, ownership, and trusted tenant context.
- Contract: PublicProduct v1 and publication command schemas.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Publish, view publicly, unpublish, and archive a product.
- **Cloud verification required?** Yes — DynamoDB transition behavior and public/protected API wiring require selected AWS verification.
- AWS environment/stack(s) required: Catalog API resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and record intentionally retained dev resources.

