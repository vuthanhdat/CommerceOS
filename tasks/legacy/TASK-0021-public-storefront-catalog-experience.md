# TASK-0021 — Deliver the public storefront catalog experience

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 4
Milestone: Milestone A
Depends on: TASK-0020

## Goal

Anonymous shoppers can browse a responsive tenant storefront, filter categories, and view published product details without seeing private catalog state.

## Business context

The first public product surface validates that tenant onboarding and publication produce customer-visible value.

## In scope

- build tenant-slug routing, storefront header/configuration, published product list and detail pages;
- add category/basic filter behavior, pagination, empty/loading/error/not-found states, and responsive accessibility;
- add frontend contract tests and the create-publish-display end-to-end journey;

## Out of scope

- cart, checkout, live inventory availability, search service, custom domains, or advanced promotions;
- CDN/S3 deployment and production caching policy;

## Acceptance criteria

### AC01 — Browse storefront

Given a tenant has an active storefront and published products
when a shopper opens the tenant route
then the correct tenant branding and paginated public products render.

### AC02 — Product detail safety

Given a shopper requests published, unpublished, archived, or another-tenant product paths
when the routes load
then only the matching published public product is shown and all others use non-leaking not-found behavior.

### AC03 — Responsive access

Given list/detail pages are used by keyboard and supported viewports
when core navigation/filter actions run
then content remains readable and controls have accessible labels/focus.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then all repository checks pass and cloud verification is explicitly N/A unless AWS semantics changed.

## Architecture impact

- Owning domain: Storefront
- Domains touched: Storefront web app and public Catalog/Tenant APIs
- Persistence impact: No new source of truth; browser state consumes public APIs.
- Events/contracts impact: No domain event.
- AWS/IaC impact: No new AWS service; produces a static web artifact for TASK-0022.
- ADR required? No — follows accepted architecture; create one if a significant new decision emerges.

## Security and tenant impact

- Authentication: Use the established merchant identity or explicit anonymous storefront boundary.
- Authorization: Anonymous UI never calls protected catalog endpoints or renders private fields.
- Tenant scoping: Tenant selection comes only from resolved route slug/public API, never a private tenant id in browser state.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Debounce/bound filters and pagination; no unbounded client polling.

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
- Operational states/errors: UI distinguishes tenant unavailable, product missing, API unavailable, and empty catalog with correlation support.

## Cost impact

- Request/compute impact: Static UI plus bounded public API calls.
- Storage impact: No new source of truth; browser state consumes public APIs.
- Network impact: Normal web/API payloads; avoid unbounded responses.
- New AWS resources/services: No new AWS service; produces a static web artifact for TASK-0022.
- Free Tier allowance relevant to this task: Use existing pay-per-use services and documented learning-profile limits.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume.
- Estimated one-off cloud-test/load-test cost, if any: None expected.

## Test plan

- Unit: Routing, view states, filters, accessibility, and public DTO rendering.
- Integration: Frontend adapter contract fixtures and tenant-route isolation.
- Architecture: Verify domain ownership, inward dependencies, and no cross-domain persistence shortcuts.
- Contract: Consumer tests against PublicStorefront/PublicProduct v1.
- IaC: N/A unless infrastructure changes.
- E2E/manual: Tenant login creates/publishes a product; anonymous browser displays list/detail.
- **Cloud verification required?** No — the web behavior is locally testable; hosted delivery is TASK-0022.
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

