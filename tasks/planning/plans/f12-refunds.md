# F12 — Returns & Refunds

## Feature goal
Implement the approved restockable refund review and independent cross-domain consequences without inventing a global RefundCompleted authority.

## Source requirements
REQ-REF-001..004, REQ-HARD-001, REQ-AUD-001.

## Blocking design gap
OQ-003: exact merchant role/capability allowed to approve/reject refund requests must be explicitly resolved before the approval command/UI becomes Ready.

## Scope
Sales RefundRequested -> Approved/Rejected review; atomic RefundApproved outbox; Inventory StockReturned; Payments provider refund/reconciliation; Accounting revenue/COGS/Cash corrections; support/reporting progress projection.

## Out of scope
Non-restock refunds, refund without approval, global refund workflow/state, editing original journals.

## Architecture
ADR-011 reliable event choreography. Sales owns approval truth; Inventory/Payments/Accounting own independent effects and idempotency.

## Task sequence
TASK-0210 -> TASK-0211 -> TASK-0212 -> TASK-0213 -> TASK-0214.

## Definition of Done
Request alone has no downstream effects; duplicate approval/facts are safe; Unknown provider refund remains unresolved; accounting corrections are append-only and source-traceable.
