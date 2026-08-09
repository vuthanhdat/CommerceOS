# TASK-0081 — Add tenant-safe product variants

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Later product capability
Milestone: Unscheduled
Depends on: TASK-0013, TASK-0031

## Goal

A tenant can sell products with variants whose SKU, attributes, publication, price, inventory, cart, order, and reporting identity remain unambiguous and tenant-safe.

## Business context

Variants were intentionally deferred until the base Product/Inventory/Sales boundaries were proven; adding them now must not collapse those domains into one shared model.

## In scope

- add Catalog ProductVariant lifecycle, attribute combination, tenant-unique SKU, optional price/media override, and public projection;
- extend Inventory stock identity, Storefront selection/cart, Sales price/line snapshots, fulfillment, imports/mapping, promotions, and reports through explicit contracts;
- migrate existing simple products to a compatible default/no-variant representation and provide back-office management;

## Out of scope

- arbitrary product configurator/BOM, bundles/kits, variant-level accounting policy change, or cross-tenant shared SKU;
- direct Catalog ownership of variant stock;

## Acceptance criteria

### AC01 — Variant catalog

Given merchant creates valid unique attribute combinations/SKUs and publishes variants
when storefront queries product
then only published selectable variants with correct offer/media identity appear.

### AC02 — Variant stock/order

Given shopper selects a variant and checks out/fulfills
when contracts execute
then Inventory reserves/issues that exact variant and Sales preserves immutable variant/SKU/attribute/price snapshot.

### AC03 — Migration compatibility

Given existing non-variant products are migrated/loaded
when old journeys run
then they remain sellable with no duplicate SKU/stock/order identity and rollback is documented.

### AC04 — Tenant/concurrency safety

Given duplicate SKU/attribute combination or final stock is raced
when commands execute
then uniqueness and Inventory final-unit invariants still hold per tenant/variant.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected cloud evidence, cost, and cleanup are recorded.

## Architecture impact

- Owning domain: Catalog with explicit Inventory/Sales/Storefront contracts
- Domains touched: Catalog, Inventory, Storefront, Sales, Pricing, Ingestion mapping, Reporting
- Persistence impact: Extend Catalog variant records/indexes; Inventory/Sales/Reporting store their own variant identities/snapshots/projections; migration metadata.
- Events/contracts impact: Version existing Product/Stock/Order facts for variant identity compatibility.
- AWS/IaC impact: Existing DynamoDB/API/Lambda/web resources; table/index migrations via CDK.
- ADR required? Yes if event/public API/storage compatibility or migration strategy is materially breaking.

## Security and tenant impact

- Authentication: Use the established merchant/shopper/internal identity boundary.
- Authorization: Variant management follows catalog permissions; all variant lookups and stock remain tenant-scoped.
- Tenant scoping: Trusted tenant context scopes all data/actions; public/shopper identities can access only explicitly authorized tenant resources.
- Sensitive data/secrets: Minimize/redact PII, secrets, and provider data; no real card data.
- Abuse/rate-limit considerations: Cap variant combinations/attributes/media and reject combinatorial explosions.

## Reliability and idempotency impact

- Retry behavior: Variant create/migration uses conditional SKU/combination identity and command keys.
- Timeout semantics: Unknown migration/write outcome is queried by variant/SKU/command key.
- Duplicate-delivery behavior: Duplicate variant events/commands cannot duplicate SKU, stock item, order line, or projection.
- Idempotency key/strategy: Tenant + product + variant id/SKU/version; preserve order/stock source keys.
- DLQ/recovery/reconciliation: Migration has checkpoint/rollback; inconsistent variant mappings become operational exceptions.

## Observability impact

- Logs: Structured/redacted logs include safe tenant/entity/operation/event and correlation data.
- Metrics: Measure success/failure, duplicates, latency, backlog/stuck state, and relevant usage/cost.
- Traces/correlation: Preserve correlation/causation through all changed boundaries.
- Operational states/errors: Invalid combination, SKU conflict, missing stock, stale version/migration state are visible.

## Cost impact

- Request/compute impact: Additional bounded variant queries/lines; avoid product×variant scans.
- Storage impact: Extend Catalog variant records/indexes; Inventory/Sales/Reporting store their own variant identities/snapshots/projections; migration metadata.
- Network impact: Bounded API/CDN/provider traffic only.
- New AWS resources/services: Existing DynamoDB/API/Lambda/web resources; table/index migrations via CDK.
- Free Tier allowance relevant to this task: Validate current pricing/allowances at scheduling time and keep non-production usage bounded.
- Expected monthly cost change or `negligible` with rationale: likely negligible; measure new index/read/projection volume.
- Estimated one-off cloud-test/load-test cost, if any: Estimate before execution and record actual bounded test usage.

## Test plan

- Unit: Combination/SKU/lifecycle/offer projection and compatibility mapping.
- Integration: Cross-domain variant contracts, DynamoDB uniqueness/migration/concurrency, tenant isolation.
- Architecture: Enforce domain ownership, tenant isolation, inward dependencies, and event/idempotency rules.
- Contract: ProductVariant/PublicVariant and updated stock/cart/order/event schemas with versioning.
- IaC: CDK assertions/synth/diff and affected real-AWS policy/resource tests.
- E2E/manual: Create/publish/select/buy/fulfill a variant and run non-variant compatibility journey.
- **Cloud verification required?** Yes — DynamoDB index/migration/concurrency and deployed cross-domain/API compatibility need AWS.
- AWS environment/stack(s) required: Catalog/Inventory/Sales/Reporting resources plus web apps
- Preview/staging teardown plan: Destroy preview and synthetic migration data; preserve only approved dev migration.

