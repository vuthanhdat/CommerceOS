# F09 — Procurement

## Feature goal
Let merchants replenish stock through immutable supplier/PO/receipt/invoice/payment evidence without making Procurement owner of Inventory or Accounting truth.

## Source requirements
REQ-PROC-001..003, REQ-INV-003, REQ-HARD-001.

## Scope
Supplier; PurchaseOrder Draft/Submitted rules; GoodsReceipt and compensating corrections; reliable Inventory application; one SupplierInvoice/full SupplierPayment; approved variance evidence.

## Out of scope
Partial invoice/payment, tax/freight/PO discounts, bank execution, destructive correction of submitted/received evidence.

## Architecture
Procurement owns its table and publishes reliable facts to Inventory/Accounting. Downstream application state is independently recoverable.

## Task sequence
TASK-0180 -> TASK-0181 -> TASK-0182; TASK-0181 -> TASK-0183.

## Definition of Done
Submitted/confirmed evidence is immutable, replay-safe and Tenant scoped; Inventory/accounting consequences never rely on foreign-table reads.
