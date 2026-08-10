# ADR-006 — Reliable Cross-Domain Integration and Workflow Selection

Status: Accepted
Date: 2026-08-09
Last reconciled: 2026-08-10 after resolved PD-023
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS requires explicit cross-domain contracts and assumes at-least-once delivery. Inventory, Payments, Procurement, Accounting, Reporting, Notification, Audit, SubscriptionBilling, and Product Data Ingestion need different combinations of immediate owner decisions, independent retry, backpressure, fan-out, durable waiting, and reconciliation.

The product/domain reconciliation now provides two concrete specializations:

- ADR-010 selects Step Functions Standard for the durable `OrderPlaced -> reservation -> payment/reconciliation -> OrderConfirmed -> OrderAllocated` process;
- ADR-011 selects reliable fact choreography after Sales `RefundApproved`, with independent Inventory, Payments, and Accounting effects.

The general rule remains: no generic event bus/queue/workflow is created because a diagram contains an AWS icon, and no transport failure manufactures a business fact.

## Decision

### Interaction selection

Use a **synchronous producer-owned application contract** when:

- the caller cannot truthfully complete without the owner's immediate accepted/rejected/current result;
- modules share the current runtime;
- independent buffering/fan-out/durable wait is not required.

Use a **direct SQS work item** when one known worker needs retry/backpressure/latency isolation and fan-out is not the problem.

Use a **versioned integration fact via EventBridge** when:

- a producer has already committed an owner fact;
- one or more independent consumers need that fact;
- eventual consumer completion is acceptable.

Give critical/side-effecting consumers their own **SQS queue + DLQ**.

Use **Step Functions Standard** only for a named approved process with demonstrated durable wait/branch/retry/reconciliation/compensation pressure. ADR-010 is the first accepted specialization; ADR-011 explicitly does not use it for the current refund model.

### Reliable business-fact publication

When a named cross-domain fact consumer is Ready:

1. producer transaction writes accepted business state + complete immutable outbox record in producer module table;
2. filtered DynamoDB Stream invokes an idempotent relay Lambda;
3. relay publishes producer-owned versioned integration envelope to a custom EventBridge bus;
4. EventBridge routes explicit producer/type/version to consumer-specific queue/target;
5. side-effecting consumer records inbox/logical source identity with its owned effect atomically where possible;
6. duplicate publication/delivery is safe;
7. pending outbox remains queryable for reconciliation after stream retention expires.

A database commit followed by best-effort `PutEvents` is not reliable publication.

The outbox payload is stable contract data required by consumers, not raw item/domain serialization and not a pointer requiring foreign-table reads.

### Transactional work-outbox for required one-worker recovery

When a direct SQS work item must survive source commit:

1. source module writes complete `WORKOUTBOX` in the same transaction as local source state;
2. filtered Stream relay sends stable work contract to the named queue;
3. worker invokes target application contract with stable logical source identity;
4. duplicate relay/queue delivery is safe;
5. durable work-outbox remains queryable for repair.

EventBridge is unnecessary when exactly one worker is known and no fact fan-out exists.

ADR-009 onboarding Trial-bootstrap recovery is the first accepted use.

### Integration event envelope

Every tenant-owned integration fact includes:

```text
eventId
eventType
eventVersion
tenantId
aggregateId
occurredAt
correlationId
causationId when applicable
producer
data
```

Rules:

- event identity describes one immutable published fact;
- TenantId is required for tenant-owned facts but never merchant actor authority;
- `data` contains only stable consumer-required facts;
- consumers reject unsupported type/version instead of guessing;
- sensitive/private/provider raw data is minimized;
- redrive preserves eventId/source/correlation/causation;
- an internal event is not published automatically—a named cross-boundary consumer must exist.

### Consumer requirements

Every side-effecting consumer:

- assumes at-least-once and possible out-of-order delivery;
- validates envelope/Tenant/source/type/version and owner-specific invariants;
- writes inbox/logical source marker with its owned effect atomically where possible;
- returns prior equivalent effect and rejects incompatible source reuse;
- cannot regress later accepted state from older evidence;
- classifies transient/permanent failures and bounds retry;
- has DLQ/redrive/reconciliation before production use;
- never reads/writes producer persistence;
- never treats message TenantId as merchant actor authority.

For external side effects, persist stable operation identity before unsafe retry and use provider idempotency/query/reconciliation semantics. Timeout remains potentially ambiguous.

### Named business fact routes

Approved routes may be introduced only with Ready producer/consumer work:

- `OrderConfirmed` -> SubscriptionBilling UsageMeter and Reporting;
- `PaymentCaptured` -> Accounting and asynchronous Sales convergence where needed;
- `OrderFulfilled` -> Accounting revenue and Reporting;
- `StockIssued` -> Accounting COGS;
- `RefundApproved` -> Inventory approved return, Payments refund execution, Accounting revenue compensation;
- `StockReturned` -> Accounting original-issue-cost COGS/inventory reversal;
- `PaymentRefunded` -> Accounting Customer Deposits/Cash settlement;
- `GoodsReceiptRecorded` -> Inventory receipt application and Accounting inventory/GRNI posting;
- `SupplierInvoiceRecorded` -> Accounting AP/GRNI posting;
- `SupplierPaymentRecorded` -> Accounting AP/Cash posting;
- `StockAdjusted` -> Accounting adjustment posting;
- approved privileged action/rejection Audit intents -> Audit;
- selected domain facts -> Notification only where a named recipient/event contract exists.

Refund routes are further constrained by ADR-011: `RefundRequested`/`RefundRejected` cause no stock/payment/accounting effect, provider ambiguity remains Payments-owned, and no global `RefundCompleted` state is inferred from delivery progress.

A route is provisioned only when producer + consumer implementation is Ready; approved meaning does not justify an empty bus/queue.

### Audit delivery

Audit records are not domain events or CloudWatch logs.

For a successful covered mutation, the source transaction writes safe durable Audit-delivery intent/outbox atomically with accepted state.

For a covered rejected attempt with no business-state transaction, the rejecting owner persists standalone idempotent Audit-delivery intent before completing the result where practical.

Audit appends owned evidence idempotently through reliable delivery. No source module writes Audit persistence directly.

### Order Step Functions specialization

ADR-010 selects **Step Functions Standard** from accepted `OrderPlaced` through `OrderAllocated`.

The state machine may coordinate:

```text
Inventory reserve
  -> Payments capture/query/reconcile
  -> Sales Confirm
  -> Sales Allocate
```

with durable technical branches for definitive decline/no-commit, `OutcomeUnknown`, and bounded-automation `NeedsAttention`.

It cannot:

- create Order before price reconfirmation;
- infer Payment failure from timeout/retry exhaustion/workflow failure;
- auto-cancel/release stock from technical failure/Unknown;
- call provider internals instead of Payments contracts;
- post Accounting;
- own refund/fulfillment/shipping business behavior.

Workflow/process state is operational coordination, not source-domain truth.

### Refund choreography specialization

ADR-011 selects:

```text
Sales RefundApproved + OUTBOX
        ↓ EventBridge
        ├── Inventory SQS/DLQ -> StockReturned + outbox
        ├── Payments SQS/DLQ -> provider refund/reconcile -> PaymentRefunded + outbox
        └── Accounting SQS/DLQ -> revenue compensation

StockReturned   -> Accounting SQS/DLQ -> COGS/inventory reversal
PaymentRefunded -> Accounting SQS/DLQ -> Deposits/Cash settlement
```

No Step Functions workflow is selected because human review is durable Sales state and post-approval effects are independently owned/eventually consistent. A later global completion/compensation business policy would require new domain/architecture work.

### Workflow selection beyond ADR-010

Do not introduce Step Functions for Tenancy/Catalog CRUD or preselect it for onboarding, Subscription plan changes, Procurement, refund, or fulfillment merely for tooling consistency.

Any later workflow requires:

- approved business sequence and owner of every transition;
- demonstrated need beyond application code + durable records/queues;
- execution identity/duplicate-start semantics;
- timeout/Unknown rules;
- retry/catch/wait/callback/compensation semantics;
- operator recovery/reconciliation;
- bounded transition/cost envelope.

## Alternatives considered

### Commit state then publish/send directly

Rejected for required delivery because it creates a dual-write gap.

### Share/read producer persistence from consumers

Rejected because it breaks ownership/IAM/migration and cannot reliably express occurrence/causation.

### Transactional outbox + Stream relay + EventBridge/SQS

Chosen for reliable business facts because state and delivery intent commit together while consumers remain independently retryable/idempotent.

### Transactional work-outbox + Stream relay + direct SQS

Chosen for required one-worker recovery because it closes the dual-write gap without unnecessary fan-out.

### Put every cross-domain action in SQS/Step Functions

Rejected because it adds ceremony/latency and creates a distributed monolith for immediate owner decisions.

### One Step Functions refund workflow

Rejected for current MVP because no global refund-completion business state is approved, while the downstream effects have independent owners and Payments has its own ambiguous provider reconciliation lifecycle.

## Consequences

Positive consequences:

- immediate owner decisions stay synchronous and explicit;
- required recovery work survives source commit;
- critical facts survive consumer outages without shared persistence;
- order Payment Unknown has a durable coordinator without false business failure;
- refund approval can fan out to independently recoverable owner effects without a distributed transaction;
- EventBridge, SQS, work-outbox, and Step Functions each have a distinct purpose;
- business fact identity stays separate from operational delivery/workflow state.

Trade-offs:

- reliable integration adds outbox/inbox items, Streams, relays, queues/DLQs, alarms, and reconciliation;
- at-least-once means every consumer/worker must be idempotent;
- refund effects can temporarily be at different completion points;
- Step Functions adds state-machine/task/IAM/transition cost for the named order process;
- eventual consumer completion/NeedsAttention is operationally visible.

## Security and tenant impact

- tenant-owned envelopes/work carry TenantId for routing/scope validation but never merchant actor authority;
- producer authorization occurs before accepting source fact/operation;
- workers use service IAM + contract validation;
- payloads exclude tokens/credentials/raw card/provider/source data/database rows;
- relay/queue/event IAM is explicit and narrow;
- refund consumers validate source Tenant/refund/order/payment references without foreign-table access;
- customer-managed keys are not introduced by default without separate security/cost justification.

## Reliability and operability impact

Observe separately:

- producer transaction cancellation;
- outbox/work pending age;
- Stream relay error/iterator lag;
- EventBridge target failure;
- queue backlog/oldest age/DLQ;
- consumer error/throttle/source conflict;
- Step Functions execution/NeedsAttention;
- provider capture/refund OutcomeUnknown and reconciliation;
- committed source with missing expected consumer effect.

Retry/redrive preserves logical identities. Operational exhaustion never changes domain state unless an owner independently accepts a transition.

## Cost impact

This ADR update deploys nothing and changes runtime cost by zero.

When introduced, the serverless cost model covers low-volume DynamoDB/Lambda/EventBridge/SQS and selective Step Functions. Each Ready task calculates request/write/transition/log amplification.

Refund choreography reuses the approved outbox/EventBridge/SQS pattern and adds no Step Functions transition cost.

No standing-cost infrastructure is introduced.

## Reversibility / migration

- relay transport can change while preserving producer event/work contracts and logical identities;
- consumer target topology can change with replay/cutover without changing producer fact meaning;
- replacing DynamoDB Streams requires equivalent durable outbox dispatch;
- module extraction preserves integration/application contracts while deployment/IAM/network boundaries change;
- replacing Step Functions later must preserve Sales process identity and Payment Unknown semantics;
- a future orchestrated refund process must preserve existing RefundApproved/StockReturned/PaymentRefunded facts as migration/source truth.

## Validation

Dependent implementation must verify:

- producer state + outbox/work intent commit together or neither;
- duplicate relay preserves event/work identity;
- explicit EventBridge source/type/version routing + target failure handling;
- SQS visibility/retry/duplicate/out-of-order/DLQ/redrive;
- consumer inbox/source marker + owned effect idempotency;
- onboarding recovery creates one Trial;
- order workflow duplicate start/task retry does not duplicate business effects;
- workflow/provider timeout/retry exhaustion cannot cause false Payment failure/cancellation/stock release;
- RefundRequested/RefundRejected do not fan out refund effects;
- RefundApproved replay cannot duplicate StockReturned/provider refund/revenue compensation;
- Payment refund Unknown cannot create PaymentRefunded until verified provider evidence;
- Accounting consumers never read producer tables;
- Audit rejection evidence is not best-effort logging;
- CDK creates no integration resource before named Ready use case and enforces least privilege/retention/tags;
- measured cloud cost stays within task guardrails.

## References

- [Integration/AWS matrix](../architecture/integration-and-aws.md)
- [Persistence access patterns](../architecture/persistence-access-patterns.md)
- [Technical baseline](../architecture/technical-baseline.md)
- [ADR-009](ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md)
- [ADR-010](ADR-010-order-payment-allocation-durable-orchestration.md)
- [ADR-011](ADR-011-refund-approval-propagation-and-accounting-correction-integration.md)
