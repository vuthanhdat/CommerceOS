# ADR-011 — Refund Approval Propagation and Accounting Correction Integration

Status: Accepted
Date: 2026-08-10
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

Resolved `PD-023` defines an explicit MVP refund approval workflow and separates three kinds of truth:

- Sales owns `RefundRequested`, `RefundApproved`, and `RefundRejected`;
- Inventory owns accepted `StockReturned`;
- Payments owns provider refund execution/evidence and only creates `PaymentRefunded` after verified provider evidence;
- Accounting owns immutable compensating/reversal journals and never edits posted history.

Approved accounting consequences are:

- `RefundApproved` for an already recognized fulfilled sale authorizes a revenue compensation: `Dr Sales Revenue / Cr Customer Deposits`;
- accepted `StockReturned` authorizes reversal of the applicable original issue-cost COGS effect: `Dr Inventory / Cr COGS`;
- verified `PaymentRefunded` clears the customer-deposit refund liability against Cash: `Dr Customer Deposits / Cr Cash`.

`RefundRequested` and `RefundRejected` create none of these effects. Duplicate/replayed delivery must not create duplicate return, refund, or journal effects.

The architecture must preserve module ownership, provider ambiguity, and append-only Accounting without introducing a distributed transaction or a global workflow state whose business meaning has not been approved.

## Decision

### Sales owns the approval state and publishes the authoritative fact

Refund review is a Sales-owned aggregate/process attached to the eligible Order/refund intent.

Conceptual commands:

```text
RequestRefund(...)
ApproveRefund(refundRequestId, expectedRevision, ...)
RejectRefund(refundRequestId, expectedRevision, ...)
```

Trusted Merchant Access supplies the explicit authorization: Owner/Admin/Staff
may request a refund, Owner/Admin alone may approve or reject it, and Viewer has
neither capability. This ADR still requires an authorized merchant mutation
context and does not treat UI visibility or client-supplied roles as authority.

When Sales accepts `RefundApproved`, the same Sales transaction writes:

- the terminal approval decision/revision;
- one versioned `RefundApproved` integration outbox event;
- required durable Audit intent for the privileged approval action where the Audit policy applies.

`RefundRejected` similarly records the terminal rejection plus Audit evidence but emits no stock/payment/accounting work authorization.

### Use reliable event choreography after approval

Do **not** use one Step Functions execution to own the refund lifecycle.

Publish `RefundApproved` through the standard ADR-006 path:

```text
Sales transaction
  RefundApproved + OUTBOX
          ↓ DynamoDB Stream
        relay
          ↓
      EventBridge
       ├── Inventory SQS/DLQ
       ├── Payments SQS/DLQ
       └── Accounting SQS/DLQ
```

Each consumer owns its effect and source-idempotency record.

This is choreography of independent owned facts, not a distributed transaction. Sales approval remains committed even if a downstream consumer is temporarily unavailable.

### Inventory application

Inventory consumes `RefundApproved` and applies one logical approved return using a producer-owned application command such as:

```text
ApplyApprovedReturn(
  tenantId,
  refundApprovalId,
  orderId,
  approvedLines,
  sourceMetadata)
    -> StockReturned | AlreadyApplied | Failure
```

Inventory:

- validates the approved return reference/quantity contract without reading Sales persistence;
- applies `OnHand += approvedQuantity` exactly once;
- records immutable StockMovement/source-idempotency;
- emits `StockReturned` with references needed by downstream Accounting, including the refund approval and original issue provenance/reference;
- never infers provider money movement.

### Payments refund execution and ambiguity

Payments consumes `RefundApproved` and owns the refund obligation/provider operation.

Conceptually:

```text
StartApprovedRefund(
  tenantId,
  refundApprovalId,
  paymentId,
  amount,
  currency,
  sourceMetadata)
    -> Refunded | DeclinedOrNoCommit | OutcomeUnknown | AlreadyApplied
```

Rules:

- stable internal/provider refund operation identity is persisted before unsafe external retry;
- one logical approved refund amount can be applied at most once;
- cumulative verified refunds cannot exceed captured amount;
- timeout/network ambiguity remains `OutcomeUnknown`;
- no second unsafe refund operation is started while the prior logical operation outcome is Unknown;
- reconciliation/query remains Payments-owned;
- only verified provider evidence creates `PaymentRefunded` and its integration outbox fact;
- workflow retry/queue retry/time passage never proves refund success/failure.

Payments may use its existing provider-reconciliation worker/Scheduler pattern when a named Ready task requires it. No new refund-specific managed service is needed.

### Accounting consumes three distinct authoritative facts

Accounting never reads Sales, Inventory, or Payments tables.

#### Revenue compensation from `RefundApproved`

For an approved refund that corresponds to already recognized sale revenue, Accounting atomically writes:

```text
source claim(refundApprovalId, revenue-correction)
+ balanced immutable Journal
    Dr Sales Revenue
    Cr Customer Deposits
```

The journal links to the original recognized sale/refund approval and never edits the original journal.

#### COGS reversal from `StockReturned`

Accounting consumes `StockReturned` and uses the supplied original stock-issue provenance/reference to find its **own** original Accounting posting/valuation provenance. It does not ask Inventory for an accounting cost amount and does not read Inventory persistence.

Atomic effect:

```text
source claim(stockReturnedId)
+ balanced immutable Journal
    Dr Inventory
    Cr COGS
+ Accounting-owned valuation correction as required by the approved valuation model
```

The amount uses the applicable original issue-cost basis/provenance approved by the domain baseline.

#### Cash settlement from `PaymentRefunded`

Accounting consumes verified `PaymentRefunded` and atomically writes:

```text
source claim(paymentRefundedId)
+ balanced immutable Journal
    Dr Customer Deposits
    Cr Cash
```

Provider callback/transport evidence alone is not an Accounting source fact; only Payments-owned verified `PaymentRefunded` is.

### No global refund-completed state is invented

The resolved domain baseline does not define a single cross-domain `RefundCompleted` authority whose truth depends on all downstream effects completing.

Therefore:

- queue/DLQ/consumer recovery states are operational only;
- Sales does not rewrite `RefundApproved` because Inventory/Payments/Accounting is temporarily delayed;
- Reporting/support may compose per-domain progress/evidence through application queries/projections, but the projection is not source truth;
- if future product semantics require one merchant-facing global completion state or compensation policy across these effects, that requires a new domain/architecture decision.

### Event contracts

`RefundApproved` integration data must include the stable consumer-required business facts, not database serialization or pointers requiring foreign table reads. At minimum conceptually:

```text
refundApprovalId
orderId
paymentId
approvedAmount + currency
approved returned line quantities
original issue/source references needed to validate return provenance
occurredAt / correlation / causation
```

`StockReturned` includes stable return/movement identity, refund approval/order references, returned quantities, and original issue provenance/reference needed by Accounting.

`PaymentRefunded` includes stable refund/payment/provider-operation evidence reference, approved refund linkage, amount/currency, and verified occurrence time.

Sensitive provider-private/raw payload data is excluded.

## Alternatives considered

### Cross-domain DynamoDB transaction for approval + stock + payment + journals

Rejected because each bounded context owns separate persistence/business truth and foreign table writes are prohibited.

### One Step Functions Standard refund workflow beginning at RefundRequested

Rejected for MVP. Human approval is durable Sales business state, not a reason to keep a workflow execution waiting. After approval, Inventory, Payments, and Accounting own independent eventual effects; a global workflow would add coupling/cost and encourage an unapproved global completion state.

### Sales directly calls Inventory, Payments, and Accounting synchronously inside ApproveRefund

Rejected because approval truth should not become unavailable/rolled back due to downstream transient failures, and Accounting/Payment provider work is naturally retryable/ambiguous.

### Reliable `RefundApproved` fact fan-out with owner-local idempotent effects

Chosen because it matches the approved fact ownership, ADR-006 reliability model, and append-only Accounting while preserving Payments ambiguity.

## Consequences

Positive consequences:

- `RefundRequested` can never accidentally trigger stock/payment/accounting effects;
- Sales approval stays authoritative and auditable even when downstream processing is delayed;
- Inventory, Payments, and Accounting keep their own truth/invariants;
- each logical return/refund/journal effect is duplicate-safe under at-least-once delivery;
- provider OutcomeUnknown does not corrupt financial/accounting state;
- no distributed transaction or extra orchestration service is required.

Trade-offs:

- downstream effects are eventually consistent and may temporarily be at different completion points;
- support/reporting needs explicit per-domain status/projection rather than one guessed global state;
- Accounting must retain/index its own source provenance sufficiently to reverse original issue-cost effects without foreign reads;
- event contracts must carry stable provenance references, increasing contract design discipline.

## Security and tenant impact

- every refund event carries TenantId as routing/scope data but never as merchant actor authority;
- consumers scope their own persistence by validated event Tenant and source identity;
- cross-Tenant Order/Payment/refund references are rejected/non-disclosing through owner contracts;
- merchant approval requires normal trusted merchant mutation authority plus the domain-approved refund capability;
- provider evidence is verified inside Payments and raw secrets/card-like data never appears in events/logs;
- platform support can observe through explicit read-only producer queries, not direct table access or manual outcome override.

## Reliability and operability impact

- Sales outbox guarantees recoverable publication after approval commit;
- EventBridge/SQS delivery is at-least-once; every consumer persists a source/inbox claim with its owned effect where possible;
- DLQ/age/retry exhaustion never changes Sales refund decision, StockReturned, Payment outcome, or Journal truth;
- Payment OutcomeUnknown has a dedicated reconciliation path and blocks unsafe duplicate refund execution;
- Accounting posting failures are recoverable without rolling back Sales/Inventory/Payments facts;
- reconciliation/repair uses durable owner records and application queries, never foreign persistence access.

## Cost impact

No new AWS managed service is introduced.

The design reuses conditional DynamoDB Streams, EventBridge, consumer-specific SQS/DLQ, and Lambda workers already approved by ADR-006. Cost scales with approved refunds and event deliveries. The MVP/learning profile is expected to be low volume; no Step Functions transition cost is added for refund processing.

## Reversibility / migration

- Event versions can evolve additively or via explicit version bump without changing fact ownership.
- A later global refund-completion business state would require a new product/domain decision and may justify an orchestrator; current source facts remain valid migration inputs.
- A future non-restock refund policy requires a new product/domain decision and cannot be inferred from this choreography.
- A future real payment provider remains behind Payments' provider port and does not change Accounting source-fact ownership.

## Validation

Dependent implementation must verify:

- `RefundRequested` and `RefundRejected` create no Inventory, Payments refund, or Accounting effects;
- one `RefundApproved` replay produces at most one logical StockReturned, one logical Payments refund operation, and one revenue compensation posting;
- duplicate/out-of-order EventBridge/SQS delivery cannot duplicate or regress consumer state;
- `StockReturned` replay cannot double-increase OnHand or double-reverse COGS;
- Accounting derives return cost from its own original issue posting/provenance and never reads Inventory persistence;
- Payment provider timeout remains OutcomeUnknown and creates no `PaymentRefunded` or Cash journal until verified evidence exists;
- duplicate verified refund evidence creates one `PaymentRefunded` and one Cash-clearing journal;
- cumulative verified refund cannot exceed captured amount;
- queue/DLQ/workflow timeout never becomes business success/failure;
- no Step Functions state machine is introduced for refund unless a later approved process requires one;
- tenant isolation and non-disclosure hold across refund IDs, Order IDs, Payment IDs, and event replay.

## References

- `docs/domains/commerce-operations.md`
- `docs/domains/product-decisions.md` (`PD-023`)
- ADR-005 — DynamoDB module ownership/access patterns
- ADR-006 — reliable cross-domain integration
- ADR-010 — order payment/allocation workflow (not reused for refund)
