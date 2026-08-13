# F11 — Reporting

## Feature goal
Build rebuildable operational/financial projections that explain merchant performance without becoming transaction, entitlement or accounting authority.

## Source requirements
REQ-REP-001..002, REQ-SUB-004.

## Scope
projection ingestion; confirmed-order KPIs; AOV/top products/failed-payment rate/operational gross sales; Tenant business-date semantics; financial/back-office views from Accounting-owned reports; exception/support projections.

## Out of scope
Authorization from projection values, direct scans of foreign transactional tables, false statutory reporting claims.

## Architecture
Reporting consumes producer-owned facts/contracts and owns rebuildable projection persistence only.

## Task sequence
TASK-0200 -> TASK-0201; TASK-0200 + Accounting -> TASK-0202.

## Definition of Done
Projection rebuild is deterministic; source formulas/timezone behavior match domain baseline; stale reporting data cannot authorize writes.
