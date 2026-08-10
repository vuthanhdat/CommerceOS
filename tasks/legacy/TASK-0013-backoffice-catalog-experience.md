# TASK-0013 — Deliver the back-office catalog experience

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 2
Milestone: Milestone A
Depends on: TASK-0011, TASK-0012

## Goal

Merchant catalog staff can manage the canonical catalog through a responsive back-office experience that respects roles, validation, and concurrency conflicts.

## Business context

The catalog is not usable if only raw APIs exist; the UI must make product lifecycle and source ownership clear before imports are added.

## In scope

- build product/category/brand list, create, edit, publish, unpublish, archive, and media-reference screens;
- surface validation, authorization, stale-update, and empty/loading/error states;
- add frontend contract tests and an end-to-end merchant catalog journey;

## Out of scope

- external import-review UI;
- inventory editing, variants, advanced pricing, or storefront presentation;

## Acceptance criteria

### AC01 — Catalog journey

Given an authorized catalog manager signs in
when the user creates, edits, and publishes a product
then the UI reflects persisted state and public eligibility without requiring manual API calls.

### AC02 — Role and error states

Given a viewer or stale editor attempts a restricted/conflicting change
when the API rejects it
then the UI explains the safe next action without exposing tenant data.

### AC03 — Responsive usability

Given catalog screens run at supported viewport sizes
when list and edit flows are exercised
then core actions remain usable with accessible labels and keyboard focus.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and cloud verification is N/A unless AWS semantics changed.

## Architecture impact

- Owning domain: Catalog / Back Office
- Domains touched: Back Office, Catalog API, Authorization
- Persistence impact: No new source of truth; uses Catalog APIs only.
- Events/contracts impact: No new event; UI consumes lifecycle results.
- AWS/IaC impact: Back-office build artifact only; no new managed service.
- ADR required? No — the task follows accepted architecture; create one only if implementation changes a significant decision.

## Security and tenant impact

- Authentication: Use the established merchant or public identity boundary as applicable.
- Authorization: UI hides unauthorized actions but server authorization remains authoritative.
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
- Operational states/errors: Frontend error reporting preserves correlation ids for support and distinguishes validation/conflict/unavailable states.

## Cost impact

- Request/compute impact: Normal back-office API traffic.
- Storage impact: No new source of truth; uses Catalog APIs only.
- Network impact: Normal API traffic only unless external fetching is explicitly in scope.
- New AWS resources/services: Back-office build artifact only; no new managed service.
- Free Tier allowance relevant to this task: Use the documented Lambda/DynamoDB/S3/SQS/CloudWatch allowances and non-production caps where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measurements are material.
- Estimated one-off cloud-test/load-test cost, if any: None expected.

## Test plan

- Unit: React components, validation mapping, role-aware actions, and state reducers.
- Integration: Frontend API adapters against catalog contract fixtures.
- Architecture: Enforce domain dependency direction, ownership, and trusted tenant context.
- Contract: Consumer tests for Catalog HTTP schemas.
- IaC: N/A unless infrastructure changes.
- E2E/manual: Authenticated merchant completes the catalog lifecycle in the browser.
- **Cloud verification required?** No — frontend behavior is local; existing deployed API may receive an optional smoke test without new AWS semantics.
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

