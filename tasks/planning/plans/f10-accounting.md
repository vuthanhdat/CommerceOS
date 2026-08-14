# F10 — Accounting

## Feature goal
Provide an internal learning double-entry ledger whose automated postings are balanced, immutable, idempotent and traceable to authoritative operational facts.

## Source requirements
REQ-ACC-001..005, REQ-REP-002, REQ-HARD-001.

## Resolved design input
PD-021 keys the authoritative moving-weighted-average valuation pool by trusted
Tenant + Product. Warehouse is an Inventory-only quantity/location dimension.

## Scope
control-account bootstrap; journal posting/reversal; moving-weighted-average valuation; sale/deposit/revenue/COGS consumers; procurement/adjustment consumers; reliable fact routes; GL/trial balance.

## Out of scope
Statutory Vietnamese tax/e-invoice compliance, AR in MVP, user-selected historical/future manual journal effective date, foreign-table reads.

## Architecture
Accounting owns valuation and journal persistence. Each consumed source fact is deduplicated atomically with its owned posting. ADR-006 reliable publication transports producer facts.

## Task sequence
TASK-0190 -> TASK-0191 -> TASK-0192 -> {TASK-0193, TASK-0194} -> TASK-0195.

## Definition of Done
Every posted journal balances and is immutable; duplicate source facts do not duplicate logical postings; original facts/journals remain traceable through reversals/corrections.
