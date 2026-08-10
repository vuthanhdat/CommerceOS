# TASK-0018 — Deliver merchant import review and catalog mapping

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 3
Milestone: Milestone A
Depends on: TASK-0013, TASK-0017

## Goal

A merchant can review a source candidate, select safe fields, and create or update a canonical tenant product without losing merchant ownership or overrides.

## Business context

External snapshots are evidence, not the merchant catalog; human review is the boundary that turns selected source facts into merchant-owned data.

## In scope

- build import-candidate detail/diff APIs and a back-office review screen;
- allow explicit field selection, attribution, product creation or mapping, and source-reference-only choices;
- preserve merchant overrides on later candidate review and make the import command idempotent;

## Out of scope

- automatic price synchronization, unattended catalog mutation, or copying unlicensed descriptions/images;
- cross-source product matching and scheduled refresh;

## Acceptance criteria

### AC01 — Reviewed creation

Given a tenant has a validated import candidate
when an authorized merchant selects permitted fields and confirms creation
then one canonical product is created with merchant-owned values and a traceable external mapping.

### AC02 — Override preservation

Given a mapped source later proposes different data
when the merchant reviews or ignores the candidate
then existing merchant overrides remain unchanged unless the field is explicitly selected.

### AC03 — Tenant and replay safety

Given another tenant or a duplicate confirm request targets the candidate
when the command runs
then cross-tenant access is denied and replay cannot create a second product/mapping.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected real-AWS evidence plus teardown is recorded.

## Architecture impact

- Owning domain: Product Data Ingestion / Catalog
- Domains touched: Ingestion, Catalog, Back Office, Audit
- Persistence impact: Add explicit ExternalProductMapping/ImportDecision contracts; Catalog alone persists canonical products.
- Events/contracts impact: ProductImported and ExternalProductMapped carry ids and selected-field metadata, never source database rows.
- AWS/IaC impact: Existing Catalog/Ingestion APIs and DynamoDB; no new managed service.
- ADR required? No — uses the documented explicit cross-domain application command rather than direct table access.

## Security and tenant impact

- Authentication: Use the established merchant or public identity boundary as applicable.
- Authorization: Import review and confirmation require catalog-manage permission; source fields are sanitized before display/storage.
- Tenant scoping: All merchant-owned reads/writes derive tenant scope from trusted context; client tenant ids cannot override it and cross-tenant denial is tested.
- Sensitive data/secrets: Do not copy restricted content/images; retain source URL, timestamp, attribution, and safe field provenance.
- Abuse/rate-limit considerations: Bound candidate payload size, mapping count, and repeated confirmation.

## Reliability and idempotency impact

- Retry behavior: ConfirmImport is idempotent and handles stale candidate/product versions explicitly.
- Timeout semantics: N/A unless an external boundary is called.
- Duplicate-delivery behavior: Repeated candidate/event delivery cannot repeat Catalog creation/update.
- Idempotency key/strategy: Tenant + importCandidateId + target product/action version.
- DLQ/recovery/reconciliation: N/A — no asynchronous work is introduced.

## Observability impact

- Logs: Structured logs include safe tenant/entity/job identifiers, operation, and correlation context.
- Metrics: Use built-in service metrics and only bounded business metrics justified by the operational risk.
- Traces/correlation: Preserve request/correlation identifiers across changed boundaries.
- Operational states/errors: Field validation, stale candidate, mapping conflict, and policy restriction are actionable in UI/API.

## Cost impact

- Request/compute impact: Scales with user requests or explicitly bounded background jobs.
- Storage impact: Add explicit ExternalProductMapping/ImportDecision contracts; Catalog alone persists canonical products.
- Network impact: Normal API traffic only unless external fetching is explicitly in scope.
- New AWS resources/services: Existing Catalog/Ingestion APIs and DynamoDB; no new managed service.
- Free Tier allowance relevant to this task: Use the documented Lambda/DynamoDB/S3/SQS/CloudWatch allowances and non-production caps where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measurements are material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview verification.

## Test plan

- Unit: Field-selection policy, override merge rules, mapping invariants, and request hashing.
- Integration: Explicit Ingestion-to-Catalog command, tenant isolation, concurrency, and event contracts.
- Architecture: Enforce domain dependency direction, ownership, and trusted tenant context.
- Contract: ImportDecision, CatalogImport command, ProductImported/ExternalProductMapped v1.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Review a candidate, create a product, edit an override, and prove later source data does not overwrite it.
- **Cloud verification required?** Yes — cross-module persistence/API integration and DynamoDB concurrency require selected AWS verification.
- AWS environment/stack(s) required: Catalog and Ingestion resources in CommerceStack/CrawlerStack
- Preview/staging teardown plan: Destroy ephemeral resources and record intentionally retained dev resources.

