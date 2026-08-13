# F07 — Sales, Inventory & Merchant Payments

## Feature goal
Establish authoritative Order, stock and provider-payment boundaries that stay correct under concurrency, retries and ambiguous outcomes.

## Source requirements
REQ-SAL-001..003, REQ-INV-001..003, REQ-PAY-001..004.

## Scope
SalesOrder persistence/lifecycle; Inventory Warehouse/StockItem/reservation/movements; Payments obligation/attempts; external-like merchant Mock Provider; signed callbacks; evidence dedupe; reconciliation; pre-fulfillment cancellation with independent downstream effects.

## Out of scope
Partial fulfillment/backorder/split tender/authorize-only capture; real money/card data; global distributed ACID.

## Architecture
Separate module-owned tables and contracts. Payment timeout is OutcomeUnknown. Inventory conditional/bounded transactions preserve non-negative available stock. Provider is a separate application.

## Task sequence
TASK-0160; TASK-0161 -> TASK-0162; TASK-0160 -> TASK-0163; foundation -> TASK-0164; TASK-0163+0164 -> TASK-0165; order/stock/payment -> TASK-0166.

## Definition of Done
Concurrent last-unit reservation is safe; duplicate provider requests/callbacks are harmless; Unknown blocks unsafe retry; Sales cancellation never falsely reports foreign-domain completion.
