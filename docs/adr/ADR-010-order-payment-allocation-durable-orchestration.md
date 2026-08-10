# ADR-010 — Durable Order Payment/Allocation Orchestration with Step Functions Standard

Status: Accepted
Date: 2026-08-10
Decision owners: CommerceOS Technical Architecture
Supersedes: the order/payment Step Functions deferral in TASK-0088/ADR-006 for this named process only
Superseded by: N/A

## Context

The earlier architecture deliberately did not choose an order workflow mechanism because order state, stock reservation, payment retry, ambiguous outcomes, cancellation, and allocation semantics were unresolved.

The 2026-08-10 product/domain reconciliation now approves the relevant MVP sequence:

```text
authoritative checkout validation
  -> if price changed: shopper reconfirmation required; no Order placed
  -> OrderPlaced
  -> all-line Inventory reservation
  -> full immediate merchant-order Payment capture attempt
  -> verified PaymentCaptured
  -> OrderConfirmed
  -> OrderAllocated
```

Additional approved semantics:

- allocation is all-or-nothing; no backorder/partial allocation;
- one Payment obligation per Order with multiple immutable attempts;
- definitive decline/no-commit terminates only the current attempt;
- no new capture attempt while the prior attempt is `OutcomeUnknown`;
- `OutcomeUnknown` requires provider inquiry/reconciliation and reserved stock remains held;
- elapsed time, transport timeout, retry exhaustion, DLQ, or operator impatience never proves Payment failure;
- cancellation is a separate Sales fact and stock release/refund are separate owned effects;
- fulfillment follows later and is whole-order, but shipping/warehouse UX/automatic trigger is not defined.

This creates a real durable orchestration problem across modules and time. The process must survive Lambda/process interruption without letting technical recovery manufacture Sales, Inventory, or Payment facts.

## Decision

### 1. Use Step Functions Standard for the named process

When the first Ready order/payment implementation frontier exists, use AWS Step Functions **Standard Workflows** to coordinate from an already accepted `OrderPlaced` through `OrderAllocated`.

The state machine is an application/process coordinator. It owns no Product, Order, stock, Payment, or Accounting business state.

### 2. Keep price reconfirmation outside the state machine

`Sales.PlaceOrder` synchronously obtains authoritative Catalog/Pricing validation before an Order exists.

If authoritative price changed:

- return reconfirm-required;
- create no Order;
- start no workflow.

Only accepted `OrderPlaced` starts orchestration.

### 3. Start workflow durably and idempotently

The Sales transaction that accepts `OrderPlaced` records:

- Order + immutable commercial snapshots;
- checkout idempotency/source identity;
- Sales-owned technical order-process record/reference;
- workflow-start outbox/work record.

A filtered Stream relay starts Standard Workflow using a deterministic execution identity derived from the Sales-owned Order/process identity. Duplicate start delivery resolves to the same logical process.

The API may attempt a fast-path start after commit, but the outbox is the durable recovery source; `commit -> best-effort StartExecution` is not sufficient.

### 4. State machine invokes application contracts only

Task-handler Lambda/composition calls producer-owned application contracts; it does not read/write module tables directly and does not call the Mock Payment Provider directly.

Conceptual flow:

```text
OrderPlaced
   |
   v
Inventory.ReserveOrderStock(order/source)
   |
   +-- rejected --------------------> ReservationNeedsAttention
   |
   v
Payments.CaptureOrderPayment(order obligation/source)
   |
   +-- Captured --------------------> Sales.ConfirmOrder
   |                                      |
   |                                      v
   |                                 Sales.MarkOrderAllocated
   |                                      |
   |                                      v
   |                                  OrderAllocated
   |
   +-- Declined / definitive NoCommit -> AwaitingPaymentRetry
   |
   +-- OutcomeUnknown ---------------> Payments.ReconcilePayment
                                             |
                                             +-- Captured -> confirm/allocate
                                             +-- definitive NoCommit -> AwaitingPaymentRetry
                                             +-- still Unknown -> bounded technical wait/retry
                                                                   -> NeedsAttention
```

`ReservationNeedsAttention`, `AwaitingPaymentRetry`, and workflow `NeedsAttention` are technical/process states. Unless Sales explicitly accepts a business transition, SalesOrder remains in its accepted business state.

### 5. No automatic cancellation/release from technical failure

The workflow must not map any of these to `OrderCancelled`, `PaymentDeclined`, or stock release:

- Lambda exception;
- Step Functions timeout/failure;
- task retry exhaustion;
- DLQ placement;
- provider transport timeout;
- missing callback;
- elapsed time;
- `OutcomeUnknown`;
- operator redrive/restart.

A definitive provider decline/no-commit establishes only the PaymentAttempt result accepted by Payments. Order remains eligible for another payment attempt unless Sales separately accepts cancellation.

Reserved stock remains held while Payment is Unknown. This ADR invents no expiry timer.

### 6. Payment retries continue the same Order process

A later approved retry command for the same Order continues the Sales-owned order process with a new immutable PaymentAttempt while preserving the same Payment obligation.

The exact shopper/back-office UX belongs to the introducing task, but it must preserve:

- no next attempt while prior outcome is Unknown;
- no new SalesOrder merely to retry payment;
- stable Order/process identity;
- duplicate retry request idempotency;
- immutable old attempt history.

The initial execution may end in `AwaitingPaymentRetry`; a later deterministic continuation execution may perform the next attempt. Architecture does not require keeping one callback token open while waiting for user action.

### 7. Unknown outcome uses reconciliation, not inference

Payments owns provider uncertainty and exposes producer-owned command/query contracts for current known outcome/reconciliation. Workflow asks Payments to reconcile and never interprets provider-private evidence.

Automatic reconciliation uses bounded technical waits/retries. If that campaign cannot establish a result, process becomes `NeedsAttention` while Payment remains Unknown and stock remains held.

Support/operator action may re-run reconciliation but cannot manually declare financial success/failure without provider evidence under approved MVP policy.

### 8. Sales convergence remains idempotent

After verified `PaymentCaptured`:

- `Sales.ConfirmOrder` is source-idempotent against capture identity;
- `Sales.MarkOrderAllocated` verifies accepted full reservation evidence and is idempotent;
- duplicate synchronous Captured result + later `PaymentCaptured` integration event cannot double-confirm;
- stale/out-of-order evidence cannot regress later Sales state.

Payments may publish `PaymentCaptured` reliably for Sales convergence and Accounting even when the immediate workflow call observed Captured.

### 9. Fulfillment is not preselected into this workflow

The first state machine ends at `OrderAllocated`.

The domain approves whole-order fulfillment and `StockIssued`/`OrderFulfilled` facts but does not define merchant/shipping initiation, carrier interaction, or large-cardinality Inventory issue mechanism.

A Ready fulfillment task may extend orchestration or introduce a dedicated workflow. This ADR does not invent automatic fulfillment.

### 10. Accounting is event-driven

Workflow does not post Journals.

Approved facts are delivered under ADR-006:

- `PaymentCaptured` -> Accounting deposit posting;
- later `OrderFulfilled` -> Accounting revenue posting;
- later `StockIssued` -> Accounting COGS posting.

Accounting failure never rolls back operational facts and workflow success never means posting completed.

### 11. Why Standard, not Express

Standard Workflow is selected because the process may require durable waits/reconciliation beyond a short synchronous execution and needs long-lived execution history/identity suited to operational coordination.

Business side effects remain idempotent; workflow execution guarantees do not replace owner idempotency.

## Alternatives considered

### Option A — Entirely synchronous in-process code

Rejected because API/Lambda timeout cannot safely contain Payment Unknown/reconciliation and interruption would require a custom durable process manager anyway.

### Option B — Chain SQS commands between modules

Rejected for this named process because orchestration/branch/wait state becomes distributed/implicit across consumers and is harder to understand/recover.

### Option C — Custom application-owned durable process manager

Reasonable but not chosen. It would require implementing timers/retries/waits/execution recovery that Standard Workflows already provide, while the project explicitly wants serverless orchestration learning.

### Option D — Step Functions Standard

Chosen because it provides explicit durable branch/wait/retry/recovery coordination with pay-per-transition/no idle compute and fits approved Payment Unknown semantics.

### Option E — Step Functions Express

Rejected because it is not a natural fit for potentially long-lived payment ambiguity/operator recovery.

## Consequences

### Positive

- first Step Functions use is tied to an approved process/real durable-wait need;
- Order/Inventory/Payments remain separate owners with explicit contracts;
- Payment Unknown has a durable recovery path without false failure/release;
- duplicate workflow start/task retry does not imply duplicate business effect;
- operational process state is visible without turning Step Functions history into business truth;
- Accounting remains independent/recoverable.

### Negative / trade-offs

- order flow gains state-machine definitions, task handlers, IAM, alarms, execution history, and transition cost;
- shared module code may be composed into separate workflow task handlers;
- some Orders may remain operationally NeedsAttention while holding inventory because financial correctness forbids timer-based release;
- fulfillment requires later refinement instead of being folded in prematurely.

## Security and tenant impact

- workflow input contains trusted Tenant/Order/process references produced after authorized Sales acceptance; browser TenantId is never workflow authority;
- task handlers validate service execution context and scope every owner contract to trusted Tenant/source identity;
- state-machine role invokes only named task handlers and has no direct business-table access;
- provider secrets/card-like data are excluded from workflow input/history/logs;
- error/result payloads minimize shopper personal/provider evidence because Step Functions retains execution history.

## Reliability and operability impact

- Sales workflow start has durable outbox + deterministic process/execution identity;
- Inventory reservation, Payments capture/reconcile, and Sales convergence commands are source-idempotent;
- retry policies distinguish transient technical failure from owner-returned business outcome;
- `OutcomeUnknown` is a durable Payments observation, not an exception/failure branch;
- state-machine failure/restart never manufactures compensation;
- redrive/continuation preserves Order/process/Payment identities;
- alarms cover execution failure/age, task errors/throttles, reconciliation age, and technical NeedsAttention without mapping them to business failure.

## Cost impact

This ADR deploys nothing and changes runtime cost by zero.

When implemented, minimum conditional resources are:

- one Step Functions Standard state machine for the named process;
- Lambda task-handler/composition functions as justified by packaging/IAM needs;
- bounded workflow log/alarm configuration;
- Sales workflow-start outbox/Stream relay capability if not already present.

Ready task must calculate:

- normal-success transition count;
- decline path transition count;
- Unknown/reconciliation transition envelope;
- expected monthly executions;
- wait/backoff strategy avoiding high-frequency polling.

No unrelated always-on infrastructure is introduced.

## Reversibility / migration

- workflow state names are implementation details; external contracts depend on Sales/Inventory/Payments facts/results, not ASL state names;
- a later custom orchestrator may replace Step Functions if stable process/source identities and Unknown/recovery semantics are preserved;
- module extraction can replace local task-handler composition with authenticated internal contracts without changing business ownership;
- fulfillment can be a separate workflow/extension after its contract is refined;
- pending executions during migration require explicit drain/resume/cutover using durable Sales process state/source identities.

## Validation

Dependent implementation must prove:

- price change creates no Order/workflow;
- duplicate OrderPlaced workflow-start delivery creates one logical process;
- duplicate/concurrent reservation creates one whole accepted reservation effect;
- definitive Payment decline does not auto-cancel Order/release stock;
- Payment timeout/Unknown does not start another attempt/release stock;
- repeated reconciliation is idempotent and can later converge;
- workflow timeout/failure/retry exhaustion leaves business facts unchanged unless an owner command succeeded;
- duplicate immediate Captured result + `PaymentCaptured` event cannot confirm Sales twice;
- Tenant A workflow/task input cannot operate on Tenant B Order/Payment/stock;
- workflow history/logs contain no prohibited provider secret/card data;
- CDK asserts Standard workflow, least-privilege invocation, bounded logging/tags and no unrelated standing-cost resource;
- measured transition count/cost stays inside Ready-task budget.

## References

- `docs/domains/commerce-operations.md`
- `docs/domains/product-decisions.md`
- `docs/architecture/product-decision-technical-reconciliation.md`
- ADR-005 — DynamoDB module ownership/access patterns
- ADR-006 — reliable integration/workflow-selection rule
- ADR-007 — external contract/idempotency conventions