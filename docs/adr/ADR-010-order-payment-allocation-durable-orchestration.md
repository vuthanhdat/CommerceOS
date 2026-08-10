# ADR-010 — Durable Order Payment/Allocation Orchestration with Step Functions Standard

Status: Accepted
Date: 2026-08-10
Decision owners: CommerceOS Technical Architecture
Supersedes: the order/payment Step Functions deferral in TASK-0088/ADR-006 for this named process only
Superseded by: N/A

## Context

The earlier architecture deliberately did not choose an order workflow mechanism because product decisions for order state, stock reservation, payment cardinality/retry, ambiguous outcomes, cancellation, and allocation were unresolved. ADR-006 therefore prohibited a generic Step Functions checkout state machine that could accidentally interpret timeout or technical failure as business failure.

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

Additional approved semantics materially change the architecture decision:

- allocation is all-or-nothing; no backorder or partial allocation;
- one Payment obligation exists per Order with multiple immutable attempts;
- definitive decline/no-commit terminates only the current attempt;
- a new capture attempt cannot start while the prior attempt is `OutcomeUnknown`;
- `OutcomeUnknown` requires provider inquiry/reconciliation and reserved stock remains held;
- elapsed time, network timeout, retry exhaustion, queue age, or operator impatience never proves Payment failure;
- cancellation is a separate Sales fact and any stock release/refund is a separate owned effect;
- fulfillment follows later and is whole-order, but the domain baseline does not define shipping/warehouse UX or an automatic fulfillment trigger.

This creates a real durable orchestration problem: cross-module steps must survive process/Lambda interruption, payment can remain ambiguous across time, duplicate starts/retries must be safe, and technical recovery must not manufacture Sales/Inventory/Payment facts.

## Decision

### 1. Use Step Functions Standard for the named purchase-confirmation process

When the first Ready order/payment implementation frontier exists, use AWS Step Functions **Standard Workflows** to coordinate the process from an already accepted `OrderPlaced` through `OrderAllocated`.

The state machine is an application/process coordinator. It owns no Product, Order, stock, Payment, or Accounting business state.

### 2. Keep price reconfirmation outside the state machine

`Sales.PlaceOrder` synchronously obtains authoritative Catalog/Pricing validation before an Order exists.

If any authoritative current price differs from the shopper-confirmed price:

- return the approved reconfirm-required result;
- create no Order;
- start no workflow execution.

Only an accepted `OrderPlaced` is eligible to start orchestration.

### 3. Start the workflow durably and idempotently

The Sales transaction that accepts `OrderPlaced` records:

- the Order and immutable commercial snapshots;
- checkout idempotency/source identity;
- a Sales-owned technical order-process record/reference;
- a workflow-start outbox/work record.

A filtered stream relay starts the Standard Workflow with a deterministic execution identity derived from the Sales-owned Order/process identity. Duplicate start delivery resolves to the existing logical execution rather than creating another business process.

The API may attempt a fast-path start after commit, but the outbox is the durable recovery source. `commit -> best-effort StartExecution` is not sufficient by itself.

### 4. State machine calls only application contracts

The state machine invokes Lambda task handlers/composition that call producer-owned application contracts. It never reads/writes module tables directly and never calls the Mock Payment Provider directly.

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
                                                                   -> NeedsAttention if automation stops
```

`ReservationNeedsAttention`, `AwaitingPaymentRetry`, and workflow `NeedsAttention` are technical/process states. Unless the Sales domain explicitly accepts a corresponding business transition, the SalesOrder remains in its accepted business state.

### 5. No automatic cancellation or stock release from technical failure

The workflow must not map any of these to `OrderCancelled`, `PaymentDeclined`, or stock release:

- Lambda exception;
- Step Functions timeout;
- task retry exhaustion;
- workflow failure;
- DLQ placement;
- provider transport timeout;
- missing callback;
- elapsed time;
- `OutcomeUnknown`;
- operator redrive/restart.

A definitive provider decline/no-commit establishes only the PaymentAttempt result accepted by Payments. The Order remains eligible for another payment attempt unless Sales separately accepts cancellation.

Reserved stock remains held while Payment is Unknown. This ADR does not invent an expiry timer.

### 6. Payment retries continue the same Order process

A later approved payment-retry command for the same Order resumes/continues the Sales-owned order process with a new immutable PaymentAttempt while preserving the same Payment obligation.

The exact shopper/back-office UX that requests another attempt belongs to the introducing application task, but it must preserve these architecture constraints:

- no second attempt while prior outcome is Unknown;
- no new SalesOrder for the same accepted checkout merely to retry payment;
- stable Order/process identity;
- duplicate retry request is idempotent;
- old attempt evidence/history remains immutable.

The initial workflow may end in `AwaitingPaymentRetry` and a subsequent deterministic execution/continuation may perform the next attempt; Architecture does not require a long-lived callback token merely to wait for user input.

### 7. Unknown outcome uses durable reconciliation, not failure inference

Payments owns provider uncertainty and exposes producer-owned command/query contracts for current known outcome and reconciliation. The workflow asks Payments to reconcile; it does not interpret provider-private evidence.

Automatic reconciliation uses bounded operational waits/retries. If the configured technical campaign cannot establish a result, the process enters `NeedsAttention` while the Payment remains Unknown and stock remains held.

A support/operator action may re-run reconciliation but cannot manually declare financial success/failure without provider evidence under the approved MVP policy.

### 8. Sales convergence remains idempotent

After verified `PaymentCaptured`:

- `Sales.ConfirmOrder` is source-idempotent against the Payment capture identity;
- `Sales.MarkOrderAllocated` verifies the accepted full reservation evidence and is idempotent;
- duplicate synchronous result plus later `PaymentCaptured` integration-event delivery cannot double-confirm the Order;
- stale/out-of-order evidence cannot regress a later Sales state.

Payments may publish `PaymentCaptured` reliably for Sales convergence and Accounting even when the immediate workflow call already observed Captured. Consumers deduplicate by stable source/event identity.

### 9. Fulfillment is not preselected into this state machine

The first state machine ends at `OrderAllocated`.

The domain baseline approves whole-order fulfillment and `StockIssued`/`OrderFulfilled` fact meaning, but it does not define:

- merchant/shipping workflow initiation;
- carrier/warehouse interaction;
- whether issue is one module transaction or a durable multi-item process at large order cardinality.

A Ready fulfillment task may extend orchestration or introduce a dedicated fulfillment workflow without changing the accepted purchase-confirmation process. Architecture must not invent an automatic fulfillment trigger.

### 10. Accounting is event-driven, not a state-machine step

The state machine does not post journals.

Approved owner facts are delivered independently under ADR-006:

- `PaymentCaptured` -> Accounting deposit posting;
- later `OrderFulfilled` -> Accounting revenue posting;
- later `StockIssued` -> Accounting COGS posting.

Accounting failure never rolls back the operational Order/Payment/Inventory fact and workflow success never means the journal was posted.

## Why Standard Workflows

Step Functions Standard is selected instead of Express because the process may need durable waits/reconciliation beyond short synchronous duration and requires exactly-once-style workflow execution identity/history semantics suitable for long-running operational coordination. Business side effects remain idempotent because workflow execution guarantees do not replace domain idempotency.

## Alternatives considered

### Option A — Keep the entire process as synchronous in-process application code

Benefits:
- lowest AWS/resource complexity and latency.

Costs/risks:
- Lambda/API timeout cannot safely contain payment Unknown/reconciliation;
- process interruption loses coordination progress unless a separate process manager is built anyway;
- retry can duplicate cross-module side effects without durable execution state.

Rejected.

### Option B — Chain SQS commands between Sales, Inventory, and Payments

Benefits:
- durable, cheap, backpressured steps.

Costs/risks:
- orchestration state/branching/wait logic becomes distributed across consumers;
- harder to understand and recover a single order process;
- Unknown/retry/continuation logic becomes an implicit state machine in messages/database records.

Rejected for the named purchase-confirmation process. SQS remains appropriate behind individual integrations/workers where needed.

### Option C — Application-owned durable process manager without Step Functions

Benefits:
- cloud-neutral orchestration and full state control.

Costs/risks:
- CommerceOS would implement timers/retries/waits/execution recovery that Standard Workflows already provide;
- more custom scheduler/locking/recovery code for a learning project intended to study serverless orchestration.

Reasonable alternative but not chosen for this approved process.

### Option D — Step Functions Standard

Benefits:
- explicit durable branch/wait/retry/recovery coordination;
- suitable for Payment Unknown/reconciliation;
- execution history supports learning/operations;
- pay-per-transition with no idle compute;
- existing AWS architecture/cost guardrails already anticipate selective use.

Costs/risks:
- additional state-machine/task/IAM/observability complexity;
- transition cost grows with polling/retries;
- careless workflow mapping could still invent business failure, so owner contracts and semantic tests remain mandatory.

Chosen.

### Option E — Step Functions Express

Benefits:
- high-throughput/low-cost short workflows.

Costs/risks:
- not a natural fit for potentially long-lived payment ambiguity/operator recovery;
- encourages treating the process as short-lived even though the business explicitly preserves Unknown.

Rejected for this process.

## Consequences

### Positive

- The first Step Functions use is tied to a now-approved process and real durable-wait need rather than architecture fashion.
- Order, Inventory, and Payments remain separate owners with explicit contracts.
- Payment Unknown has a durable recovery path without false failure or stock release.
- Duplicate workflow starts and task retries do not imply duplicate business effects.
- Operational process state is visible without turning Step Functions history into business truth.
- Accounting remains decoupled and independently recoverable.

### Negative / trade-offs

- Order flow now has state-machine definitions, task handlers, IAM, alarms, execution history, and transition cost.
- `commerce-api` plus workflow task handlers may initially share module code but have different delivery compositions.
- Some Orders can remain operationally `NeedsAttention` while still holding inventory because the approved business policy prioritizes financial correctness over automatic expiry.
- Fulfillment remains a later architecture refinement rather than being folded into this state machine prematurely.

## Security and tenant impact

- Workflow input contains immutable trusted Tenant/Order/process references produced after authorized Sales acceptance; browser `tenantId` is never workflow authority.
- Task handlers re-establish/validate service execution context and scope every module operation to the trusted Tenant/source identity.
- The state machine role can invoke only named task handlers and required AWS resources; it has no direct DynamoDB business-table access when handlers own persistence access.
- Provider secrets/card-like data are not workflow input/history.
- Error/result payloads minimize shopper personal data and provider evidence to avoid retention in Step Functions execution history.

## Reliability and idempotency impact

- Sales workflow start has a durable outbox and deterministic execution identity.
- Inventory reservation, Payments capture/reconcile, and Sales convergence commands are source-idempotent.
- Retry policies distinguish technical transient failures from owner-returned business outcomes.
- `OutcomeUnknown` is a normal durable business observation from Payments, not an exception path.
- State-machine failure/restart never manufactures compensation.
- Redrive/continuation preserves Order/process/Payment logical identity.
- Published integration facts retain eventId/correlationId/causationId and are independently idempotent under ADR-006.

## AWS and cost impact

When a Ready task introduces the process, the minimum new resources are:

- one Step Functions Standard state machine for the named order payment/allocation process;
- Lambda task-handler/composition functions as justified by packaging/IAM needs;
- workflow log/alarm configuration with bounded retention;
- Sales workflow-start outbox/stream relay capability if not already present.

Existing conditional resources such as provider callback endpoints, EventBridge/SQS Accounting consumers, and DLQs are introduced only by their named tasks.

Cost guardrails:

- calculate a normal-success transition count before deployment;
- calculate separate decline and Unknown/reconciliation transition envelopes;
- use waits/backoff rather than high-frequency polling;
- cap automated reconciliation attempts operationally without converting exhaustion into business failure;
- keep dev/CI real-AWS workflow executions low-volume;
- no Express/Standard duplicate implementation merely for experimentation.

This ADR itself deploys nothing and changes runtime cost by zero.

## Reversibility / migration

- Workflow state names are implementation details; external API/domain contracts depend on Sales/Inventory/Payments results, not Amazon States Language state names.
- A later application-owned orchestrator may replace Step Functions if it preserves stable process/source identities and recovery semantics.
- Module extraction can replace in-process/task-handler calls with authenticated internal contracts without changing business ownership.
- Fulfillment can be added as a separate state machine or extension only when its implementation contract is refined.

## Validation

Dependent implementation must include scenarios proving:

- price change does not create Order/workflow;
- duplicate OrderPlaced workflow-start delivery creates one logical execution/process;
- concurrent/duplicate reservation requests produce one whole accepted reservation effect;
- definitive Payment decline does not auto-cancel Order or release stock;
- Payment timeout/Unknown does not start another attempt or release stock;
- repeated reconciliation remains idempotent and can later converge to Captured or definitive NoCommit;
- state-machine timeout/failure/retry exhaustion leaves business facts unchanged unless an owner command explicitly succeeded;
- duplicate immediate `Captured` result plus `PaymentCaptured` event cannot confirm Sales twice;
- Tenant A workflow/task input cannot operate on Tenant B Order/Payment/stock references;
- workflow history/logs contain no prohibited provider secret/card data;
- CDK assertions cover Standard workflow type, least-privilege task invocation, bounded logging, tags, and no unrelated always-on infrastructure;
- measured transition count/cost stays within the Ready task's budget.

## References

- domain baseline: `docs/domains/commerce-operations.md`
- product decisions: `docs/domains/product-decisions.md`
- technical reconciliation: `docs/architecture/product-decision-technical-reconciliation.md`
- ADR-005: DynamoDB module ownership/access patterns
- ADR-006: reliable cross-domain integration and workflow-selection rule
- ADR-007: external contract/idempotency conventions