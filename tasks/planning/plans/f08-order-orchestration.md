# F08 — Durable Order Orchestration

## Feature goal
Make the approved `OrderPlaced -> reserve -> payment/reconciliation -> Confirmed -> Allocated` process survive interruptions without inventing business facts.

## Source requirements
REQ-ORC-001, REQ-SAL-002, REQ-PAY-004, REQ-HARD-001/003.

## Scope
ADR-010 workflow definition/start identity; application-task composition; retry/catch/wait/reconciliation; AwaitingPaymentRetry/NeedsAttention operational handling; LocalStack Step Functions verification where supported.

## Out of scope
Refund workflow; Accounting posting inside workflow; technical timeout interpreted as business failure/cancellation/release.

## Task sequence
TASK-0170 -> TASK-0171 -> TASK-0172.

## Definition of Done
Duplicate starts and task retries are safe; OutcomeUnknown remains Payments-owned; technical failures are recoverable/observable; emulator gaps are explicitly recorded.
