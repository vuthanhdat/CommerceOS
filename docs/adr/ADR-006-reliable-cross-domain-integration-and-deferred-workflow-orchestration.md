# ADR-006 — Reliable Cross-Domain Integration and Workflow Selection

Status: Accepted
Date: 2026-08-09
Last reconciled: 2026-08-10
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS requires explicit cross-domain contracts and assumes at-least-once delivery. Inventory, Payments, Procurement, Accounting, Reporting, Notification, Audit, SubscriptionBilling, and Product Data Ingestion need different combinations of immediate owner decisions, independent retry, backpressure, fan-out, durable waiting, and reconciliation.

The original ADR deliberately deferred Step Functions for checkout/payment because business sequence and timeout semantics were unresolved. The 2026-08-10 product/domain reconciliation now defines the purchase-confirmation sequence and Payment `OutcomeUnknown` behavior sufficiently for one named workflow: ADR-010 selects Step Functions Standard from accepted `OrderPlaced` through `OrderAllocated`.

The general rule remains: no generic event bus/queue/workflow is created because a diagram contains an AWS icon, and no transport failure is allowed to manufacture a business fact.

## Decision

### 1. Interaction selection

Use a **synchronous producer-owned application contract** when:

- the caller cannot truthfully complete without the owner's immediate accepted/rejected/current result;
- modules share the current runtime;
- independent buffering/fan-out/durable wait is not required.

Use a **direct SQS work item** when:

- one known worker needs retry/backpressure/latency isolation;
- fan-out is not the problem.

Use a **versioned integration fact via EventBridge** when:

- a producer has already committed an owner fact;
- one or more independent consumers need that fact;
- eventual consumer completion is acceptable.

Give critical/side-effecting consumers their own **SQS queue + DLQ**.

Use **Step Functions Standard** only for a named approved process with real durable wait/branch/retry/reconciliation/compensation pressure. ADR-010 is the first accepted specialization.

### 2. Reliable business-fact publication

When a named cross-domain fact consumer is Ready:

1. producer transaction writes accepted business state + complete immutable outbox record in the producer's module table;
2. filtered DynamoDB Stream invokes an idempotent relay Lambda;
3. relay publishes producer-owned versioned integration envelope to a custom EventBridge bus;
4. EventBridge routes explicit producer/type/version to consumer-specific queue/target;
5. side-effecting consumer records inbox/logical source identity with its owned effect atomically where possible;
6. duplicate publication/delivery is safe;
7. pending outbox remains queryable for reconciliation after stream retention expires.

A database commit followed by best-effort `PutEvents` is not reliable publication.

The outbox payload is the stable contract data required by consumers, not a raw item/domain serialization and not a pointer that forces foreign-table reads.

### 3. Transactional work-outbox for required one-worker recovery

A direct SQS work item also needs crash-safe source delivery when the work is required after a source commit.

In that case:

1. source module writes a complete `WORKOUTBOX` record in the same transaction as the local source operation/state;
2. filtered Stream relay sends the stable work contract directly to the named SQS queue;
3. worker invokes the target application contract using stable logical work/source identity;
4. duplicate relay/queue delivery is safe;
5. durable work-outbox remains queryable for repair.

EventBridge is unnecessary when there is exactly one known worker and no fact fan-out.

ADR-009 onboarding Trial-bootstrap recovery is the first accepted use of this pattern.

### 4. Integration event envelope

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
- `tenantId` required for tenant-owned facts;
- `data` contains only stable consumer-required facts;
- consumers reject unsupported type/version rather than guessing;
- sensitive/private data minimized;
- redrive preserves eventId/source/correlation/causation rather than manufacturing a new fact;
- an internal domain event is not published automatically — a named contract/consumer must exist.

### 5. Consumer requirements

Every side-effecting consumer:

- assumes at-least-once and possible out-of-order delivery;
- validates envelope/Tenant/source/type/version and owner-specific invariants;
- writes inbox/logical source marker atomically with its owned effect where possible;
- returns prior effect for equivalent replay and rejects incompatible source reuse;
- cannot regress later accepted state from older evidence;
- classifies transient/permanent errors and bounds automatic retry;
- has DLQ/redrive/reconciliation before production use;
- never reads/writes producer persistence;
- never treats message TenantId as merchant actor authority.

For external side effects, persist stable operation identity before unsafe retry and use provider idempotency/query/reconciliation semantics. Timeout remains potentially ambiguous.

### 6. Named business fact routes after reconciliation

The following routes are now approved in principle and may be introduced by their Ready producer/consumer tasks:

- `OrderConfirmed` -> SubscriptionBilling UsageMeter and Reporting;
- `PaymentCaptured` -> Accounting and asynchronous Sales convergence where needed;
- `OrderFulfilled` -> Accounting revenue and Reporting;
- `StockIssued` -> Accounting COGS;
- `GoodsReceiptRecorded` -> Inventory receipt application and Accounting inventory/GRNI posting;
- `SupplierInvoiceRecorded` -> Accounting AP/GRNI posting;
- `SupplierPaymentRecorded` -> Accounting AP/Cash posting;
- `StockAdjusted` -> Accounting adjustment posting;
- approved privileged action/rejection Audit intents -> Audit;
- selected domain facts -> Notification only where a named recipient/event contract exists.

`PaymentRefunded`/`StockReturned` do not receive an Accounting posting route until `PD-023` is resolved.

A route is still provisioned only when the producer + consumer implementation task is Ready; approval of business meaning does not justify an empty bus/queue.

### 7. Audit delivery

Audit records are not domain events or CloudWatch logs.

For a **successful covered mutation**, the source transaction writes a safe durable Audit-delivery intent/outbox atomically with accepted state.

For a **covered rejected attempt** that has no business-state transaction, the rejecting owning module persists a standalone idempotent Audit-delivery intent before completing the application result where practical.

Audit appends its owned evidence idempotently through the reliable delivery path. No source module writes the Audit table directly.

### 8. Order Step Functions specialization

ADR-010 selects **Step Functions Standard** for the approved process from already accepted `OrderPlaced` through `OrderAllocated`.

The state machine may coordinate:

```text
Inventory reserve
  -> Payments capture/query/reconcile
  -> Sales Confirm
  -> Sales Allocate
```

with durable technical branches for:

- definitive decline/no-commit -> payment retry needed;
- `OutcomeUnknown` -> reconciliation/wait;
- bounded automation unable to resolve -> technical `NeedsAttention`.

The state machine cannot:

- create Order before price reconfirmation;
- infer Payment failure from timeout/retry exhaustion/workflow failure;
- auto-cancel Order or release stock from technical failure/Unknown;
- call provider internals instead of Payments contracts;
- post Accounting;
- invent refund/fulfillment/shipping business behavior.

Workflow/process state is operational coordination, not source-domain truth.

### 9. Workflow selection beyond ADR-010

Do not introduce Step Functions for Tenancy/Catalog CRUD or preselect it for onboarding, Subscription plan changes, Procurement, refunds, or fulfillment merely for consistency of tooling.

Any later workflow requires a focused task/ADR showing:

- approved business sequence and owner of every transition;
- why application code + durable records/queues are insufficient;
- execution identity/duplicate-start semantics;
- timeout/Unknown rules;
- retry/catch/wait/callback/compensation semantics;
- operator recovery/reconciliation;
- bounded transition/cost envelope.

## EventBridge policy

Use EventBridge for versioned business-fact routing/fan-out with named consumers.

Do not use it for:

- ordinary in-process commands/queries;
- generic database changes;
- one-worker commands where SQS is sufficient;
- an empty FoundationStack event bus.

Each rule documents owner/source/type/version, target, retry/failure policy, IAM, payload minimization, compatibility/deletion, and cost.

## SQS/DLQ policy

Standard queue by default. FIFO only if an explicit ordering/dedup requirement cannot be safely handled by source/aggregate idempotency and its cost/throughput trade-off is justified.

Every queue defines:

- producer/consumer + message contract;
- visibility timeout greater than bounded handler duration;
- batch/concurrency cap;
- maximum receives/redrive policy;
- DLQ retention/alarm;
- transient/permanent classification;
- identity-preserving replay;
- poison-message/manual recovery;
- tenant-safe logs/metrics.

Queue age/retry/DLQ is never business state.

## Alternatives considered

### Commit state then publish/send directly

Rejected for required delivery because it creates a dual-write gap.

### Share/read producer persistence from consumers

Rejected because it breaks ownership/IAM/migration and cannot reliably express occurrence/causation.

### Transactional outbox + Stream relay + EventBridge/SQS

Chosen for reliable business facts because state and delivery intent commit together while consumers remain independently retryable/idempotent.

### Transactional work-outbox + Stream relay + direct SQS

Chosen for required one-worker recovery because it closes the dual-write gap without unnecessary fan-out/EventBridge.

### Put every cross-domain action in SQS/Step Functions

Rejected because it adds asynchronous ceremony/latency and creates a distributed monolith for immediate owner decisions.

### Never use Step Functions

Rejected after the order/payment domain reconciliation because `OutcomeUnknown` creates a legitimate durable cross-module wait/reconciliation process. ADR-010 uses Step Functions selectively for that named process.

## Consequences

### Positive

- no Builder invents a dual-write, event topology, duplicate strategy, or workflow default;
- immediate owner decisions stay simple/synchronous;
- required recovery work survives source commit;
- critical facts survive consumer outages without shared persistence;
- order Payment Unknown has a durable coordinator without false business failure;
- EventBridge, SQS, and Step Functions each have a distinct purpose;
- business fact identity and operational delivery/workflow state remain separate.

### Negative / trade-offs

- reliable integration adds outbox/inbox items, Streams, relays, queues/DLQs, alarms, and reconciliation;
- work-outbox adds another durable delivery shape to govern/test;
- at-least-once means every consumer/worker must be idempotent;
- Step Functions introduces state-machine/task/IAM/transition cost for the named order process;
- eventual consumer completion/NeedsAttention becomes visible operationally.

## Security and tenant impact

- tenant-owned envelopes/work items carry TenantId for routing/scope validation but are never merchant actor authority;
- producer authorization occurs before accepting source fact/operation;
- workers use service IAM + contract validation;
- minimum stable payload only, no tokens/credentials/raw card/provider/source data/database rows;
- relay/queue/event IAM is explicit and narrow;
- customer-managed keys are not introduced by default without security/cost justification.

## Reliability and operability impact

Observe separately:

- producer transaction cancellation;
- work/event outbox pending age;
- Stream/relay error/iterator lag;
- EventBridge target failure;
- queue backlog/oldest age/DLQ;
- consumer error/throttle/source conflict;
- Step Functions execution/NeedsAttention state;
- provider Unknown/reconciliation;
- committed source with missing expected consumer effect.

Retry/redrive preserves logical identities. Operational exhaustion never changes domain state unless an owner command independently accepts a transition.

## Cost impact

This ADR update deploys nothing and changes runtime cost by zero.

When introduced, current serverless cost model already covers low-volume DynamoDB/Lambda/EventBridge/SQS and selective Step Functions. Each Ready task must calculate its own request/write/transition/log amplification.

Step Functions Unknown paths use waits/backoff and bounded automated campaigns, not high-frequency polling.

No standing-cost infrastructure is introduced.

## Reversibility / migration

- changing relay transport preserves producer event/work contracts and logical identities during cutover;
- consumer direct-Lambda/SQS topology can change with replay/cutover plan without changing producer fact meaning;
- replacing DynamoDB Streams requires an equivalent durable outbox dispatcher;
- extracting a module retains integration/application contracts while changing deployment/IAM/network boundaries;
- replacing Step Functions later must preserve Sales process/source identities and Payment Unknown/recovery semantics.

## Validation

Dependent implementation must verify:

- producer state + outbox/work intent commit together or neither;
- duplicate Stream relay preserves same event/work identity;
- EventBridge source/type/version routing + target failure handling;
- SQS visibility/retry/duplicate/out-of-order/DLQ/redrive;
- consumer inbox/source marker + owned effect idempotency;
- onboarding work-outbox/SQS recovery produces one Trial;
- order workflow duplicate start/task retry does not duplicate business effects;
- workflow/provider timeout/retry exhaustion cannot cause false Payment failure/cancellation/stock release;
- Accounting/source consumers never read producer tables;
- Audit rejection evidence is not reduced to best-effort logging;
- CDK creates no Stream/bus/queue/state-machine before named Ready use case and enforces least privilege/retention/tags;
- measured cloud cost stays within Ready-task guardrails.

## References

- [Integration/AWS matrix](../architecture/integration-and-aws.md)
- [Persistence access patterns](../architecture/persistence-access-patterns.md)
- [Technical baseline](../architecture/technical-baseline.md)
- [ADR-009](ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md)
- [ADR-010](ADR-010-order-payment-allocation-durable-orchestration.md)