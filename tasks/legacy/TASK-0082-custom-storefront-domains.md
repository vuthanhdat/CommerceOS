# TASK-0082 — Support custom storefront domains

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Later product capability
Milestone: Unscheduled
Depends on: TASK-0022, TASK-0070
Execution gate: Requires DNS/certificate ownership validation and current cost review.

## Goal

A tenant can attach a verified custom domain to its storefront and receive HTTPS/CDN delivery without taking over another tenant's domain or bypassing slug-based tenant isolation.

## Business context

Custom domains add DNS/certificate ownership, asynchronous validation, CloudFront configuration, and cost/operational complexity beyond the initial path-based storefront.

## In scope

- design/implement domain request, ownership-token/DNS validation, certificate issuance/renewal, status, detach, conflict, and audit workflow;
- map verified host to tenant public context and configure CloudFront/S3/API behavior through CDK/controlled automation;
- provide merchant setup/status UI, safe fallback to tenant slug, failure/retry/cleanup, and current DNS/certificate cost review;

## Out of scope

- domain registration/resale, arbitrary DNS hosting migration, wildcard ownership without validation, or paid edge service without approval;
- trusting Host alone before verified mapping;

## Acceptance criteria

### AC01 — Ownership validation

Given tenant requests an unclaimed domain
when required DNS proof succeeds
then domain becomes active for that tenant with HTTPS and audited evidence.

### AC02 — Takeover prevention

Given another tenant requests same/subdomain or validation is stale/removed
when activation/requests run
then ownership conflict is denied and invalid domain cannot route to tenant data.

### AC03 — Delivery isolation

Given two custom/slug routes are requested
when CloudFront/public tenant resolution runs
then each maps only to its verified tenant and protected/private data/cache never crosses hosts.

### AC04 — Lifecycle recovery

Given certificate/validation/deploy fails or domain detaches
when workflow handles it
then status/action are visible, slug fallback remains available, and stale mappings/resources are cleaned safely.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected cloud evidence, cost, and cleanup are recorded.

## Architecture impact

- Owning domain: Storefront / Tenant / Platform Web Delivery
- Domains touched: Storefront, Tenant settings, CloudFront/S3/API, DNS/certificates, Audit
- Persistence impact: Add tenant DomainMapping/validation/status/operation records with global uniqueness.
- Events/contracts impact: StorefrontDomainRequested/Verified/Activated/Failed/Detached if consumers need them.
- AWS/IaC impact: CloudFront aliases/distributions, ACM certificates, DNS validation and possibly Route 53 only if accepted; existing WebStack/API.
- ADR required? Yes — domain/certificate automation, DNS ownership, deployment topology, and cost are material decisions.

## Security and tenant impact

- Authentication: Only owner/admin requests/detaches domains; public host resolution uses verified active mapping.
- Authorization: DNS ownership validation, global uniqueness, certificate lifecycle, host allowlist, origin/cache isolation and anti-takeover rules.
- Tenant scoping: Trusted tenant context scopes all data/actions; public/shopper identities can access only explicitly authorized tenant resources.
- Sensitive data/secrets: Minimize/redact PII, secrets, and provider data; no real card data.
- Abuse/rate-limit considerations: Rate-limit domain requests/validation/deploys, cap domains per tenant, reject unsafe hostnames.

## Reliability and idempotency impact

- Retry behavior: Validation/deployment retries boundedly; permanent DNS/conflict errors wait for merchant action.
- Timeout semantics: Certificate/DNS/deploy timeout remains Pending/Failed and never activates unverified routing.
- Duplicate-delivery behavior: Repeated domain request/validation callbacks/deploys converge on one mapping.
- Idempotency key/strategy: Normalized domain + tenant + operation/version; global conditional uniqueness.
- DLQ/recovery/reconciliation: Detach/rollback/cleanup and expired validation are explicit/audited; slug fallback remains.

## Observability impact

- Logs: Structured/redacted logs include safe tenant/entity/operation/event and correlation data.
- Metrics: Measure success/failure, duplicates, latency, backlog/stuck state, and relevant usage/cost.
- Traces/correlation: Preserve correlation/causation through all changed boundaries.
- Operational states/errors: Validation/certificate/deploy/renewal status, expiry, routing errors, correlation and next action visible.

## Cost impact

- Request/compute impact: Low-frequency control-plane operations; static traffic remains CDN-served.
- Storage impact: Add tenant DomainMapping/validation/status/operation records with global uniqueness.
- Network impact: DNS/ACM/CloudFront traffic; current regional/global constraints documented.
- New AWS resources/services: CloudFront aliases/distributions, ACM certificates, DNS validation and possibly Route 53 only if accepted; existing WebStack/API.
- Free Tier allowance relevant to this task: Validate current pricing/allowances at scheduling time and keep non-production usage bounded.
- Expected monthly cost change or `negligible` with rationale: estimate certificates/DNS/CloudFront changes at implementation time; no paid add-on without approval.
- Estimated one-off cloud-test/load-test cost, if any: Estimate before execution and record actual bounded test usage.

## Test plan

- Unit: Domain normalization/ownership/status/uniqueness/host routing policy.
- Integration: Real DNS validation test domain if human-provided, ACM/CloudFront routing, cache/tenant isolation, cleanup.
- Architecture: Enforce domain ownership, tenant isolation, inward dependencies, and event/idempotency rules.
- Contract: Domain management/status and host-to-public-tenant resolution.
- IaC: CDK assertions/synth/diff and affected real-AWS policy/resource tests.
- E2E/manual: Verify a controlled test domain, serve storefront HTTPS, attempt takeover, detach/fallback.
- **Cloud verification required?** Yes — DNS, ACM, CloudFront aliases/caching and ownership validation require AWS/external DNS.
- AWS environment/stack(s) required: controlled dev/test domain plus WebStack
- Preview/staging teardown plan: Remove DNS validation/aliases/certificates/test mappings and destroy preview resources; never touch unrelated DNS.

