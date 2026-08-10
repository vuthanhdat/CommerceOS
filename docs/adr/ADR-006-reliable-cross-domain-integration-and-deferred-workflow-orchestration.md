# ADR-006 — Reliable Cross-Domain Integration and Deferred Workflow Orchestration

Status: Accepted
Date: 2026-08-09
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS requires explicit cross-domain contracts and assumes at-least-once delivery. Future Inventory, Payments, Procurement, Accounting, Reporting, Notification, Audit, and Product Data Ingestion flows need independent retry, backpressure, fan-out, or honest partial/recovery states.

The existing architecture diagrams name EventBridge, SQS, DLQ, direct Lambda, and Step Functions but do not decide when each is justified or how a committed DynamoDB fact is published reliably. Writing domain state and then calling EventBridge is a dual-write gap: a crash can commit the fact and lose its integration effect. Publishing first can expose a fact that never committed.

TASK-0087 also leaves high-risk business sequences intentionally unresolved. A generic checkout workflow that maps timeout/“failed” to stock release or Order failure would invent `PD-014`, `PD-017`, `PD-018`, and `PD-042`. Accounting routes cannot be created until one approved source fact/policy is selected for each effect.

The architecture needs one reliable delivery pattern without pre-creating resources or choosing missing business semantics.

## Decision

### Interaction selection

- Use a synchronous producer-owned application contract when the caller needs the owner's immediate accepted/rejected result, both modules share the current runtime, and independent retry/backpressure/waiting is not required.
- Use direct SQS work messages when one known worker needs buffering/backpressure/retry and fact fan-out is not the problem.
- Use versioned integration facts through EventBridge when a committed owner fact has one or more independent consumers and eventual consistency is acceptable.
- Give each critical/bursty side-effecting consumer its own SQS queue and DLQ.
- Permit direct EventBridge-to-Lambda only for bounded, idempotent, demonstrably rebuildable low-risk projections with an explicit recovery source/failure destination.
- Do not use EventBridge merely to avoid an in-process call and do not publish generic database changes.

### Reliable fact publication

When the first named cross-domain fact consumer is Ready:

1. The producer's module transaction writes its accepted business state and a complete immutable outbox record in the same module table transaction.
2. A filtered DynamoDB Stream event source invokes an idempotent relay Lambda for outbox records.
3. The relay publishes the producer-owned, versioned integration envelope to a custom EventBridge bus and marks publication/retry evidence without changing the business fact.
4. EventBridge routes by producer, type, and version to consumer-specific SQS queues when durable side effects/backpressure are required.
5. The consumer atomically records its inbox/logical source identity with its owned effect where possible.
6. Duplicate publication/delivery is safe. Equivalent replay returns the prior effect; incompatible source reuse conflicts.
7. Pending outbox records remain queryable for reconciliation after stream records expire. The stream is a wake-up/delivery mechanism, not the sole recovery record.

The outbox payload contains the stable contract fields needed by consumers; it is not a pointer that forces cross-domain persistence reads and is not a raw database row.

### Envelope and identity

Every tenant-owned integration event includes:

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

An internal domain fact is not published automatically. A named producer-owned contract and consumer must exist. Redrive/retry preserves eventId and logical source; it does not create a new fact.

### Retry, DLQ, and recovery

- Classify transient versus permanent validation/business failures.
- Bound Lambda/queue/EventBridge retries and configure consumer redrive/DLQ behavior and alarms.
- Use partial batch response/batch isolation for stream/queue handlers so successfully processed records are not needlessly repeated.
- Alarm on relay errors/lag, EventBridge target failures, queue oldest-message age/backlog, Lambda errors/throttles, and DLQ depth.
- Redrive has a documented operator procedure, preserves identity, and remains idempotent.
- Critical integrations define reconciliation for a committed source with no expected consumer effect.
- Retry exhaustion, DLQ placement, queue age, and redrive are operational states only; they do not change Order, Payment, stock, journal, or another business state.

### Audit

Audit records are not domain events. After `PD-033` designates an action as requiring Audit evidence, the source transaction writes a safe, durable audit-delivery intent/outbox record atomically with the accepted state. Audit appends its owned immutable evidence idempotently. A completed success therefore cannot silently omit recoverable evidence.

The exact covered successes/rejections, readers, and safe content remain product-gated. A rejected attempt with no source-state transaction requires the separately approved rejection-audit contract; this ADR does not invent its coverage.

### Step Functions

Do not introduce a Step Functions state machine for Tenancy/Catalog or preselect one for checkout, payment, refund, Procurement, or another flow.

A future workflow requires its own task/ADR after the business sequence is approved and must demonstrate a need for durable waiting, branching, callback, retry, timeout handling, or compensation that is clearer/reliabler than application state plus queues. It must define execution idempotency, unknown outcomes, operator recovery, reconciliation, and bounded state-transition cost.

### Provisioning

No event bus, stream relay, queue, DLQ, or workflow resource is created until its producer contract and named consumer/work item are Ready. `FoundationStack` does not contain an empty event bus by default.

## Alternatives considered

### Option A — Commit state, then call EventBridge directly

- Benefits: least infrastructure/code and low latency.
- Costs/risks: unrecoverable dual-write gap on crash/outage unless every producer has another durable reconciliation mechanism; unsafe for Accounting, Inventory, Payment, Procurement, and mandatory Audit effects.

### Option B — Share/read producer persistence from consumers

- Benefits: no event-publication dual write; consumers can poll current state.
- Costs/risks: destroys ownership, couples consumers to private schemas, broadens IAM, cannot distinguish fact occurrence/causation safely, and makes migrations difficult.

### Option C — Transactional outbox + stream relay + EventBridge/SQS

- Benefits: business state and delivery intent commit atomically; fan-out/backpressure/retry are explicit; producer/consumer ownership and duplicate handling remain clear; pay-per-use services fit the project.
- Costs/risks: additional writes/storage/functions/events/queues; at-least-once duplicates; stream retention requires durable outbox repair; operational tooling/DLQs/reconciliation are mandatory.

### Option D — Put every cross-module action in SQS/Step Functions

- Benefits: uniform distributed model and durable execution history.
- Costs/risks: asynchronous ceremony for immediate local decisions, more cost/latency/contracts, premature workflow semantics, and a distributed monolith.

Chosen: synchronous contracts by default for immediate local ownership; Option C for reliable published facts; direct SQS for one-worker work; Step Functions deferred to demonstrated need.

## Consequences

### Positive

- No Builder must invent a dual-write, bus/queue topology, duplicate strategy, or workflow default.
- Critical future side effects survive producer response loss and independent consumer outages without sharing persistence.
- EventBridge solves fact routing/fan-out; SQS solves work/backpressure; Step Functions remains tied to actual orchestration pressure.
- Business fact identity and operational delivery state remain separate.
- Audit can meet durable-evidence requirements without becoming a shared write model.

### Negative / trade-offs

- Reliable integration adds outbox/inbox items, streams, relay code, queues/DLQs, alarms, and recovery procedures.
- Delivery is at least once; every consumer must implement idempotency and out-of-order safeguards.
- Eventual consistency and honest processing/needs-attention states become visible to applications/operations.
- Stream delivery is time-bounded; durable pending-outbox reconciliation is required.
- Resource count grows per critical consumer, though resources remain pay-per-use and conditional.
- Business product decisions still gate the exact event routes and workflow sequence.

## Security and tenant impact

- Tenant isolation: tenant-owned envelopes carry TenantId and consumers validate/scope their own persistence by it; a message is never merchant actor authority.
- Authentication/authorization: producer application authorization occurs before accepting the fact; workers use service IAM plus contract validation, not interactive capabilities.
- Sensitive data/secrets: envelopes contain minimum stable facts and no tokens, credentials, raw personal/payment/source payloads, or database rows. Broad EventBridge routing does not justify broad data.
- IAM: relay can read its module stream and publish to the named bus; EventBridge can target named queues; consumers receive only their queue and owned table. No wildcard cross-module table access.
- Encryption/key choices must be compatible with EventBridge/SQS target policies and justified in the implementing task; customer-managed keys are not introduced by default.

## Reliability and operability impact

- Failure modes: producer transaction cancellation, stream/relay failure, EventBridge target delivery failure, queue consumer failure, poison message, duplicate/out-of-order event, and missing consumer effect are separately observable.
- Retry/recovery: bounded retry, partial-batch handling, DLQs, identity-preserving redrive, durable outbox repair, and source/effect reconciliation are required.
- Idempotency: relay/event delivery may duplicate; consumer inbox/logical source and effect commit together where possible. External effects also use provider idempotency and reconcile ambiguous outcomes.
- Observability: preserve request/command/event/correlation/causation identities; monitor iterator age, failed target delivery, queue age/depth, DLQ, consumer errors, and reconciliation gaps.
- Operational burden: every critical integration task includes an owner/runbook/redrive/reconciliation test and cloud verification; an event is not “done” when `PutEvents` succeeds.

## Cost impact

- Learning profile: TASK-0088 deploys nothing. When first used, the existing cost model already budgets low-volume DynamoDB, Lambda, EventBridge, and SQS; incremental outbox/inbox writes/storage and relay invocations are expected to be negligible but measured. Current modeled EventBridge cost is about $0.03 and SQS remains under its initial free request allowance.
- Beta profile: current model includes about $0.40 EventBridge and SQS under its initial free allowance, before per-consumer write/compute amplification. Queue/relay/log usage must be measured by contract.
- Larger-scale implication: event fan-out, outbox/inbox writes, Lambda duration, queue requests, and log volume become cost drivers. Batch/filter/item-size choices and direct-vs-queued rebuildable projections should be revisited with metrics.
- Step Functions: remains absent; the current cost model's ~$0.15 learning/~$2.40 beta scenario is not a commitment and must be recalculated for an approved state machine including retries.
- Cost-model update required? No current runtime change. The implementation task updates measured event/queue/transition assumptions when material.

## Reversibility / migration

- Changing the relay transport from EventBridge requires preserving producer event contracts, event IDs, consumer subscriptions, and replay history during cutover.
- A consumer can move from direct Lambda to SQS by adding a versioned route and replay/cutover plan; critical effects should begin queued to avoid losing recovery isolation.
- Replacing DynamoDB Streams requires another durable outbox dispatcher with equivalent crash/reconciliation guarantees.
- Extracting a module/service retains integration contracts but requires independent deployment/IAM/observability and timeout/compatibility management.
- Introducing Step Functions later migrates an approved process using stable command/fact identities; it cannot reinterpret past facts or bypass consumer idempotency.

## Validation

- Producer failure injection proves business state and outbox commit together or neither commits.
- Relay duplicate/retry tests publish the same eventId safely and never manufacture a new fact.
- Real DynamoDB Streams/Lambda tests cover duplicate delivery, partial batch failure, retry exhaustion/destination behavior, iterator lag, and recovery from pending outbox records.
- EventBridge cloud tests verify source/type/version matching, SQS target permissions, retry, and target DLQ behavior.
- SQS tests cover visibility timeout, bounded receive/redrive, duplicates, out-of-order events, poison messages, DLQ alarm, and identity-preserving redrive.
- Consumer tests prove inbox/source marker and owned effect are atomic where possible and incompatible reuse fails.
- Reconciliation finds a committed source with a missing critical consumer effect and repairs/requeues idempotently without foreign-table access.
- Architecture/contract tests enforce the required envelope and forbid published database-row/domain-entity schemas.
- CDK assertions prove no event/queue/workflow resources exist before a named consumer and verify least-privilege grants/retention/tags when introduced.

## References

- relevant task: [TASK-0088](../../tasks/completed/TASK-0088-technical-architecture-baseline-reconciliation.md)
- architecture docs: [Integration and AWS service matrix](../architecture/integration-and-aws.md), [Persistence access patterns](../architecture/persistence-access-patterns.md)
- AWS: [Lambda with DynamoDB Streams](https://docs.aws.amazon.com/lambda/latest/dg/with-ddb.html), [stream event-source parameters](https://docs.aws.amazon.com/lambda/latest/dg/services-ddb-params.html), [SQS at-least-once delivery](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/standard-queues-at-least-once-delivery.html), [EventBridge targets](https://docs.aws.amazon.com/eventbridge/latest/userguide/eb-targets.html), [EventBridge target DLQs](https://docs.aws.amazon.com/eventbridge/latest/userguide/eb-rule-dlq.html)
