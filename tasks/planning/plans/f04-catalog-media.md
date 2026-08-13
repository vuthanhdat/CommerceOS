# F04 — Catalog & Files/Media

## Feature goal
Give each Tenant authoritative canonical Products with approved lifecycle, uniqueness, public projection and merchant-managed media.

## Source requirements
REQ-CAT-001..006, REQ-MED-001.

## Scope
Catalog module/table; Product lifecycle/SKU/Money/slug; Category/Brand/specifications; FilesMedia asset upload/metadata; media association; external source mapping and ImportCandidate apply.

## Out of scope
Variants, hierarchical categories, external image hotlinking/copying, catalog-manager role, promotion engine.

## Architecture
Catalog and FilesMedia remain separate ownership boundaries. Catalog never uses stock/accounting/source snapshot persistence as authority. DynamoDB claims protect SKU/slug/name uniqueness.

## Task sequence
TASK-0130 -> TASK-0131 -> {TASK-0132, TASK-0134}; TASK-0130 -> TASK-0133.

## Definition of Done
Tenant isolation and lifecycle rules are mechanically tested; public projection excludes private/source/internal fields; imports are explicit merchant-authorized Catalog commands.
