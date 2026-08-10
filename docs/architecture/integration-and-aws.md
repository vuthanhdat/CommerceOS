# CommerceOS — Integration and AWS Service Matrix

_Cross-domain, delivery, reliability, orchestration, and cost baseline refreshed on 2026-08-10 after resolved `PD-004`, `PD-023`, and `PD-044`._

## 1. Interaction selection rules

Choose interactions from process need, not from AWS service availability.

### Synchronous producer-owned application contract

Use when the caller cannot truthfully complete without the owner's immediate accepted/rejected/current result and modules share the runtime.

The caller references producer-owned application/contracts only and never reads producer persistence.

### Durable one-worker work item

Use SQS when one known worker needs retry/backpressure/latency isolation and fan-out is not the problem.

If required work must survive a source transaction, write a module-owned transactional work-outbox and relay through DynamoDB Streams. `commit -> best-effort SendMessage` is not sufficient.

### Reliable business fact

Use producer state + outbox -> DynamoDB Stream relay -> EventBridge when a committed owner fact has independent consumers and eventual completion is acceptable.

Critical/side-effecting consumers receive their own SQS + DLQ.

### Durable workflow

Use Step Functions Standard only for a named approved process that needs durable wait/branch/retry/reconciliation and whose business transitions are already defined.

ADR-010 selects Step Functions for `OrderPlaced -> reservation -> payment/reconciliation -> OrderConfirmed -> OrderAllocated` only.

ADR-011 explicitly selects event choreography, not Step Functions, for refund propagation after `RefundApproved`.

## 2. Status legend

- **Accepted** — mechanism selected for the named use case.
- **Conditional** — mechanism selected but resources appear only with a named Ready producer/consumer/process.
- **Domain-gated** — missing business meaning still prevents implementation.
- **Deferred** — no current demonstrated need.

## 3. Current interaction matrix

| Caller / producer | Owner / consumer | Mechanism/status | Required truth/reliability | Remaining gate |
|---|---|---|---|---|
| API Gateway/Cognito | Tenancy Merchant Access | synchronous; **Accepted** | authentication identity only; current Tenant/Membership authority | none for approved merchant auth |
| merchant query | Merchant Access | `ResolveTenantReadAuthority`; **Accepted** | Active Membership; Active or Suspended Tenant; normal role read visibility | none |
| merchant command | Merchant Access | `ResolveTenantMutationAuthority`; **Accepted** | Active Membership + Active Tenant; no Suspended mutation | none |
| platform admin | Tenancy | synchronous `SuspendTenant/ReactivateTenant`; **Accepted** | separate platform trust, reason, revision, Audit intent | platform-auth delivery task only |
| platform support | owning modules | synchronous read-only queries; **Accepted** | separate privileged context; no Tenant Membership/direct storage | platform-auth delivery task only |
| public Storefront | Tenant status + Catalog | synchronous read/composition; **Domain-gated** for Tenant addressing | Suspended must make storefront/checkout unavailable; cache not authority | Storefront Tenant-address semantics |
| protected governed command | SubscriptionBilling | `EvaluateEntitlement`; **Accepted** | current EntitlementSet authority | none for approved keys/values |
| plan selection/catalog UI | SubscriptionBilling | synchronous catalog query; **Accepted** | current sellable PlanVersion; history immutable | implementation task only |
| onboarding | Tenancy + SubscriptionBilling | sync fast path + work-outbox/SQS recovery; **Accepted** | Active Tenant + Owner + exact Trial EntitlementSet | ADR-009 |
| Membership create/reactivate | SubscriptionBilling + Tenancy | sync entitlement then local conditional write; **Accepted** | `MaxActiveMemberships`, all Active roles count, owner-local count guard | none |
| Warehouse create/reactivate | SubscriptionBilling + Inventory | sync entitlement then local conditional write; **Accepted** | `MaxWarehouses`, owner-local count guard | none |
| PDI schedule enable/create | SubscriptionBilling + PDI | sync capability/policy check; **Accepted** | ScheduledProductIngestion + PDI source policy | none |
| scheduled PDI dispatch | SubscriptionBilling + PDI | recheck sync entitlement before run; **Accepted** | stale schedule cannot bypass downgrade/Ended | none |
| checkout | Catalog/Pricing | synchronous owner query; **Accepted** | current sellability/price; price change => reconfirm/no Order | future promotion policy only if introduced |
| Sales OrderPlaced | order process | Sales outbox -> Step Functions Standard; **Accepted/Conditional** | deterministic process identity; ADR-010 | Ready task |
| order workflow | Inventory | synchronous app command; **Accepted** | all-line idempotent reservation | cardinality refinement if DynamoDB transaction insufficient |
| order workflow | Payments | synchronous capture/query/reconcile; **Accepted** | provider ambiguity preserved | none |
| PaymentCaptured | Sales convergence | EventBridge -> Sales queue or workflow-compatible convergence; **Conditional** | duplicate immediate/event path cannot double-confirm | Ready order task |
| PaymentCaptured | Accounting | EventBridge -> Accounting SQS/DLQ; **Conditional** | Dr Cash / Cr Customer Deposits once | Accounting task |
| OrderConfirmed | SubscriptionBilling UsageMeter | EventBridge -> SQS/DLQ; **Conditional** | threshold from current EntitlementSet; warning-only | meter task |
| OrderConfirmed | Reporting | EventBridge projection; **Conditional** | projection only | projection task |
| OrderFulfilled | Accounting | EventBridge -> Accounting SQS/DLQ; **Conditional** | Dr Deposits / Cr Revenue once | fulfillment task |
| StockIssued | Accounting | EventBridge -> Accounting SQS/DLQ; **Conditional** | COGS/Inventory using Accounting valuation truth | moving-average cost-pool scope |
| Sales RefundApproved | Inventory | EventBridge -> Inventory SQS/DLQ; **Accepted/Conditional** | exactly one approved restockable StockReturned | refund task |
| Sales RefundApproved | Payments | EventBridge -> Payments SQS/DLQ; **Accepted/Conditional** | one logical provider refund; Unknown/reconciliation preserved | refund task |
| Sales RefundApproved | Accounting | EventBridge -> Accounting SQS/DLQ; **Accepted/Conditional** | revenue compensation once | refund/accounting task |
| Inventory StockReturned | Accounting | EventBridge -> Accounting SQS/DLQ; **Accepted/Conditional** | original issue-cost COGS reversal using Accounting provenance | valuation cost-pool scope for valuation state only |
| Payments PaymentRefunded | Accounting | EventBridge -> Accounting SQS/DLQ; **Accepted/Conditional** | verified provider truth; Dr Deposits / Cr Cash once | refund/accounting task |
| Procurement GoodsReceiptRecorded | Inventory | EventBridge -> Inventory SQS/DLQ; **Conditional** | receipt evidence committed independently | procurement/inventory task |
| GoodsReceiptRecorded | Accounting | EventBridge -> Accounting SQS/DLQ; **Conditional** | Inventory/GRNI posting | cost-pool scope for valuation state |
| SupplierInvoiceRecorded | Accounting | EventBridge -> Accounting SQS/DLQ; **Conditional** | GRNI/AP + approved variance | task |
| SupplierPaymentRecorded | Accounting | EventBridge -> Accounting SQS/DLQ; **Conditional** | AP/Cash | task |
| StockAdjusted | Accounting | EventBridge -> Accounting SQS/DLQ; **Conditional** | approved gain/loss posting | valuation basis contract |
| PDI dispatcher | crawler worker | SQS/DLQ; **Conditional** | bounded concurrency/retry/kill switch | source-specific task |
| PDI | Catalog | synchronous apply command; **Accepted** | candidate != Applied until Catalog accepts | none |
| covered privileged action/rejection | Audit | durable source-owned Audit intent -> reliable append; **Accepted/Conditional** | audit cannot be best-effort log | Audit task |
| owned facts | Notification | EventBridge -> SQS/DLQ; **Conditional** | per-recipient delivery/read truth | named event/recipient rule |
| SubscriptionBilling | Mock SaaS Billing Provider | provider port + separate app; **Accepted/Conditional** | dedicated provider; Unknown/idempotency/query | provider task |
| Mock SaaS Billing Provider | SubscriptionBilling | authenticated callback/query; **Accepted/Conditional** | evidence until verified/accepted | provider task |
| fulfillment | Inventory/Sales | **Deferred** beyond approved fact sequence | whole-order semantics known; initiation/shipping mechanism not refined | Ready fulfillment refinement |

## 4. Reliable publication pattern

Critical business facts use ADR-006:

```text
owning module TransactWriteItems
  business state + OUTBOX
             │ atomic
             ▼
module DynamoDB Stream
             ▼
idempotent relay Lambda
             ▼
EventBridge custom bus
             ▼
consumer-specific SQS + DLQ
             ▼
idempotent consumer transaction
  INBOX/source marker + owned effect
```

Rules:

1. best-effort `PutEvents` after state commit is not sufficient;
2. event payload contains stable consumer-required facts, not a pointer requiring producer-table access;
3. relay/event/queue delivery is at-least-once and may be out of order;
4. consumers persist source identity and cannot duplicate/regress owned state;
5. stream retention is not the recovery store—pending outbox stays durable/queryable;
6. queue/DLQ/retry age are operational states only;
7. redrive preserves event/source/correlation/causation identity;
8. reconciliation never reads foreign persistence.

No stream/bus/rule/queue is created before a named Ready consumer exists.

## 5. Transactional work-outbox

For one required retryable worker with no fan-out, use direct SQS after a module-owned work-outbox.

Current approved example: onboarding Trial recovery.

```text
Tenancy transaction
  registration state + WORKOUTBOX
             ↓ Stream relay
           SQS
             ↓
worker -> idempotent SubscriptionBilling.StartTrialSubscription
```

The message is work/command intent, not business fact. Queue failure never means Trial business failure.

## 6. Integration event contract

Required envelope:

```json
{
  "eventId": "evt_...",
  "eventType": "RefundApproved",
  "eventVersion": 1,
  "tenantId": "tenant_...",
  "aggregateId": "refund_or_order_...",
  "occurredAt": "2026-08-10T00:00:00Z",
  "correlationId": "corr_...",
  "causationId": "cmd_or_evt_...",
  "producer": "sales",
  "data": {}
}
```

Rules:

- TenantId required for tenant-owned facts but never merchant actor authority;
- `data` is explicit stable integration shape, not Domain/database serialization;
- unsupported event type/version is rejected safely;
- breaking semantic/required-field changes use a new event version;
- sensitive/private/provider raw data is minimized/excluded;
- EventBridge rules match explicit producer/type/version.

## 7. Refund integration under ADR-011

### Sales approval publication

Sales transaction writes:

```text
RefundApproved state/revision
+ RefundApproved OUTBOX
+ required Audit intent
```

`RefundRequested` alone has no downstream effect. `RefundRejected` publishes no stock/payment/accounting authorization.

### Fan-out

```text
RefundApproved
   ├── Inventory queue -> ApplyApprovedReturn -> StockReturned
   ├── Payments queue  -> StartApprovedRefund -> provider reconcile -> PaymentRefunded
   └── Accounting queue -> revenue compensation

StockReturned  -> Accounting queue -> COGS/inventory reversal
PaymentRefunded -> Accounting queue -> Deposits/Cash settlement
```

### Safety

- consumers are source-idempotent;
- Payments persists stable refund operation identity before unsafe provider retry;
- timeout/network ambiguity remains OutcomeUnknown;
- no new refund attempt while prior logical operation is Unknown;
- only verified Payments evidence creates PaymentRefunded;
- Accounting never reads producer tables;
- StockReturned carries original issue reference/provenance so Accounting can locate its own original posting;
- no global `RefundCompleted` business state is invented;
- downstream lag/DLQ does not undo RefundApproved.

### Why no Step Functions

The human review is durable Sales business state. After approval, the three owner effects are independent and naturally event-driven. A global state machine would add coupling and imply an unapproved global completion semantic.

If a future product decision defines global refund completion/compensation, revisit with a focused ADR.

## 8. Order Step Functions Standard workflow

ADR-010 remains scoped to:

```text
OrderPlaced
 -> Inventory reserve
 -> Payments capture
    -> Captured: Sales Confirm + Allocate
    -> definitive NoCommit/Decline: AwaitingPaymentRetry
    -> OutcomeUnknown: Payments reconciliation/wait
                       -> NeedsAttention if bounded automation stops unresolved
```

Workflow cannot:

- create Order before price reconfirmation;
- convert technical timeout/retry exhaustion into Payment failure;
- auto-cancel/release stock on decline/Unknown;
- start another capture attempt while prior is Unknown;
- call provider/table internals;
- post Accounting;
- own refund processing;
- invent fulfillment/shipping.

Sales writes workflow-start outbox atomically with Order/process state; execution identity is deterministic.

## 9. Suspended Tenant integration effects

Resolved `PD-004` does not require async fan-out to rewrite every domain's business state.

- Tenancy stores current `TenantStatus` and resolves read/mutation authority per request.
- Merchant mutations fail before owning-domain command execution when Suspended.
- public Storefront resolution checks current Tenant status and returns unavailable.
- scheduled PDI dispatch must pass current merchant/Tenant/subscription eligibility before work starts.
- platform suspend/reactivate writes durable Audit intent.
- no Membership/Subscription/order/accounting mass-update event is produced merely to mirror suspension.

This avoids duplicated lifecycle authority across modules.

## 10. Plan/entitlement integration

Resolved `PD-044` uses synchronous SubscriptionBilling authority for operations requiring current limits/capabilities.

Do not publish plan-change CRUD events merely so Tenancy/Inventory/PDI can cache authority.

- Membership create/reactivate asks `MaxActiveMemberships`, then Tenancy protects local count.
- Warehouse create/reactivate asks `MaxWarehouses`, then Inventory protects local count.
- PDI schedule enable/dispatch asks `ScheduledProductIngestion`, then applies PDI source policy.
- `OrderConfirmed` is an async fact to SubscriptionBilling only because UsageMeter is an accumulated projection/owned meter.

Restrictive downgrade uses explicit owner assessment/fencing contracts from ADR-008 rather than eventual copied counts.

## 11. Consumer requirements

Every side-effecting consumer:

- assumes duplicate/out-of-order delivery;
- validates envelope/Tenant/source/type/version/business invariants;
- atomically writes inbox/source identity with owned effect where possible;
- returns/replays prior equivalent effect and rejects incompatible source reuse;
- never regresses later accepted state from older evidence;
- classifies transient/permanent errors and bounds retries;
- exposes queue age/errors/throttles/DLQ metrics/alarms;
- documents redrive/reconciliation;
- never reads/writes producer persistence;
- never treats event TenantId as merchant actor authority.

For external side effects, persist stable operation identity before unsafe retry. Timeout remains ambiguous unless provider evidence proves otherwise.

## 12. Audit delivery

Audit is not a log stream and not source business state.

Accepted covered mutation:

```text
source business transaction + durable Audit intent
   -> reliable delivery
   -> Audit appends immutable evidence
```

Covered rejected attempts without a state transaction persist an idempotent source-owned Audit delivery intent before returning where practical.

Current notable privileged facts include:

- Tenant suspend/reactivate reason/outcome;
- refund approve/reject action;
- privileged SubscriptionBilling actions/evidence as domain policy requires.

Audit consumer never reads source tables.

## 13. SQS/DLQ policy

Use Standard queue by default. FIFO only if ordering/dedup cannot safely be handled by source identity and an ADR/task justifies the trade-off.

Every queue defines:

- producer/consumer/message contract;
- visibility timeout vs bounded handler duration;
- batch/concurrency caps;
- redrive/max receives;
- DLQ retention/alarm;
- transient/permanent classification;
- replay preserving identity;
- poison-message/manual recovery;
- safe logs/metrics.

Queue age/DLQ is never business state.

## 14. EventBridge policy

Use custom EventBridge only for versioned fact routing/fan-out with named consumers.

Do not use it for:

- in-process commands/queries;
- generic CRUD/database changes;
- one known work command where SQS is sufficient;
- empty diagram-driven infrastructure.

Each rule documents producer/type/version, target, retry/failure policy, IAM, payload minimization, cost, and compatibility/deletion plan.

## 15. Step Functions policy beyond ADR-010

Any new workflow requires:

- approved business sequence and owner for every transition;
- demonstrated durable wait/branch/callback/retry/compensation need;
- execution identity/duplicate-start rule;
- explicit Unknown/timeout semantics;
- operator recovery;
- transition-cost envelope;
- ADR when material.

ADR-010 does not approve Step Functions for onboarding, Subscription changes, Procurement, refund, or fulfillment by default. ADR-011 explicitly chooses choreography for current MVP refund semantics.

## 16. AWS capability matrix

CommerceOS remains serverless/pay-per-use and Free Tier/credit constrained.

| Capability | Problem solved | Status | Guardrail |
|---|---|---|---|
| API Gateway HTTP API | HTTPS/JWT/throttling | **Accepted** with API tasks | HTTP API by default; separate provider ingress only if isolation warrants |
| Lambda | scale-to-zero API/workers/tasks | **Accepted** | no provisioned concurrency; bounded risky-worker concurrency |
| Cognito | merchant authentication | **Accepted** | identity only; avoid paid SMS/advanced features by default |
| DynamoDB | module-owned transactional persistence | **Accepted** per module | one table/module; no Scan/speculative GSI |
| DynamoDB Streams | outbox/work relay wake-up | **Conditional** | only when a named outbox exists |
| EventBridge custom bus | fact routing/fan-out | **Conditional** | named facts/consumers only |
| SQS + DLQ | recovery/backpressure/consumer isolation | **Conditional** | bounded retry/redrive/concurrency |
| Step Functions Standard | ADR-010 durable order process | **Conditional/Accepted** for that process only | transition budget; no high-frequency polling |
| EventBridge Scheduler | due reconciliation/renewal/PDI schedules | **Conditional** | only named due-work need; timer not business proof |
| S3 + CloudFront | FilesMedia binaries/delivery | **Conditional** | private upload; controlled delivery; no arbitrary hotlink authority |
| CloudWatch | logs/metrics/alarms | **Accepted** | bounded retention, low-cardinality dimensions |
| CDK/CloudFormation | IaC | **Accepted** | source of truth |

Not introduced by current architecture: NAT Gateway, ALB, EC2, RDS/Aurora, Redis/ElastiCache, OpenSearch, Kafka/MSK, EKS, always-on services, provisioned Lambda concurrency, or AppConfig/SSM plan authority.

## 17. CDK composition target

Conceptually:

```text
FoundationStack
IdentityStack
ApiStack

module persistence resources/stacks
  only when Ready

Integration resources
  Stream relay / EventBridge / SQS-DLQ
  only with named producer-consumer

OrderWorkflow capability
  Step Functions Standard for ADR-010

MockPaymentStack
  merchant-order external-like provider

MockSaaSBillingStack
  CommerceOS SaaS external-like provider

CrawlerStack / FilesMedia capability
  only with Ready tasks
```

Refund uses the existing integration capability pattern; there is no `RefundWorkflowStack` under current MVP architecture.

## 18. Remaining gates

Integration work remains domain-gated only where it would encode:

- Storefront Tenant-address semantics;
- moving-average cost-pool scope where Accounting valuation state depends on it;
- non-restock refund behavior;
- refund approval role/capability if not supplied by task refinement;
- unapproved Category/Brand historical-name behavior.

## 19. Stop condition

**INTEGRATION AND AWS BASELINE RECONCILED.**
