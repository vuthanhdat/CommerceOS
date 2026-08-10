# TASK-0022 — Deploy the storefront with CDN caching and image delivery

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 4
Milestone: Milestone A
Depends on: TASK-0005, TASK-0021

## Goal

The Storefront static artifact is reproducibly deployed through S3 and CloudFront with safe tenant-aware caching, private origin access, and bounded logging.

## Business context

Public delivery must be fast and cache-friendly without exposing S3 directly or allowing one tenant's response to be served for another tenant.

## In scope

- add WebStack S3 origin and CloudFront distribution with private origin access, HTTPS, SPA routing, tags, and bounded logs;
- deploy immutable Storefront artifacts through the build-once pipeline and define invalidation/version strategy;
- implement public catalog cache-key/TTL policy and a documented merchant-image delivery strategy;

## Out of scope

- custom merchant domains, paid WAF configuration, advanced image processing, or global active-active deployment;
- caching protected back-office responses;

## Acceptance criteria

### AC01 — Private static delivery

Given the WebStack is deployed
when a shopper requests the Storefront
then assets are delivered by CloudFront over HTTPS while direct public S3 access is denied.

### AC02 — Tenant-safe caching

Given two tenant slugs/products are requested
when CloudFront and API cache behavior is inspected
then cache keys include the correct public tenant/resource identity and no response crosses tenants.

### AC03 — Reproducible release

Given one immutable Storefront artifact is selected
when dev/preview deployment runs
then the artifact version is deployed, smoke-tested, and preview resources are removable.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then all repository checks pass and real-AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Platform Web Delivery / Storefront
- Domains touched: CDK, CI/CD, Storefront, public Catalog API
- Persistence impact: S3 stores versioned static artifacts and permitted merchant media only; no transactional data.
- Events/contracts impact: No domain event; deployment artifact/version contract.
- AWS/IaC impact: WebStack with S3, CloudFront, IAM policies, certificates managed by CloudFront defaults, and bounded logs.
- ADR required? No — S3/CloudFront are accepted; a flat-rate plan/custom domain/WAF change needs current cost review.

## Security and tenant impact

- Authentication: Public content is anonymous; origin write/deploy authority is restricted to delivery roles.
- Authorization: Block S3 public access, least-privilege origin/deploy permissions, TLS, safe headers, and no protected API caching.
- Tenant scoping: Cache policy is proven tenant-safe and only public projections are cacheable.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Bound object sizes, cache-busting/invalidation, and public request behavior; review CDN protection without paid WAF by default.

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
- Operational states/errors: Deployment version, CloudFront/S3 errors, cache hit behavior, and invalidation failures are observable.

## Cost impact

- Request/compute impact: Static delivery reduces backend reads; invalidations are bounded.
- Storage impact: Small versioned SPA/media objects with cleanup/retention policy.
- Network impact: CloudFront serves public assets; response sizes and image strategy are bounded.
- New AWS resources/services: WebStack with S3, CloudFront, IAM policies, certificates managed by CloudFront defaults, and bounded logs.
- Free Tier allowance relevant to this task: Use existing pay-per-use services and documented learning-profile limits.
- Expected monthly cost change or `negligible` with rationale: negligible under normal CloudFront/S3 learning allowances; verify current account pricing eligibility.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded preview/dev checks.

## Test plan

- Unit: CDK cache/origin/policy assertions and frontend asset manifest tests.
- Integration: Real S3 private-origin denial, CloudFront delivery/cache keys, and deploy artifact verification.
- Architecture: Verify domain ownership, inward dependencies, and no cross-domain persistence shortcuts.
- Contract: Artifact manifest and cache-control contract.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Deploy preview Storefront, load two tenant routes, verify cache separation, then destroy it.
- **Cloud verification required?** Yes — CloudFront, S3 policy, cache, TLS, and deployment semantics require AWS verification.
- AWS environment/stack(s) required: ephemeral or dev WebStack plus public Catalog API
- Preview/staging teardown plan: Destroy preview distribution/bucket after emptying test objects; retain only documented dev WebStack.

