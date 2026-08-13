# F13 — Notification & Audit

## Feature goal
Provide durable operational communication and privileged/security evidence without allowing either supporting context to become source business truth.

## Source requirements
REQ-NOT-001, REQ-AUD-001, REQ-HARD-001.

## Scope
Audit append/query model; privileged action/rejection integration; non-disclosing tenant-visible evidence; Notification per-recipient unread/read/acknowledged state; critical event consumers.

## Out of scope
Notification acknowledgement resolving business failures; audit log as event store/domain state; email delivery unless later explicitly needed.

## Architecture
Audit and Notification own separate persistence. Critical delivery uses durable owner intent/reliable facts, not best-effort logging.

## Task sequence
TASK-0220 -> TASK-0221; Reporting/refund/critical facts -> TASK-0222.

## Definition of Done
Audit can explain covered privileged actions without cross-Tenant disclosure; notification state is per recipient and source exceptions remain independently unresolved.
