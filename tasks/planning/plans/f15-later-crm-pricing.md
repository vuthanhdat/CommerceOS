# F15 — Later CRM & Pricing/Promotion

## Feature goal
Preserve explicitly documented post-MVP product capabilities without smuggling their semantics into current Sales/Catalog code.

## Source requirements
REQ-CRM-001, REQ-PRI-001.

## Scope
Later explicit Customer profile/contact preferences and later Pricing/Promotion authority after design refinement.

## Out of scope
Automatic guest-to-customer matching, rewriting historical Order snapshots, manual authoritative guest discounts in current MVP, speculative promotion engine.

## Task sequence
TASK-0240 may begin only after core reporting/data boundaries are stable. TASK-0241 requires design/product refinement after architecture audit.

## Definition of Done
Later capabilities have explicit owning-context semantics and integrate through contracts rather than hidden flags in Sales/Catalog.
