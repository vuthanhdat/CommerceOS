# CommerceOS — Integration and AWS Service Matrix

_Cross-domain, delivery, reliability, orchestration, and cost baseline originally reconciled by TASK-0088 and refreshed on 2026-08-10 after the product/domain decision pass._

## 1. Interaction selection rules

Choose the interaction from the business/process need, not from the availability of an AWS service.

### Synchronous producer-owned application contract

Use when:

- the caller cannot truthfully complete without the owner's immediate accepted/rejected/current result;
- modules share the current runtime and latency/failure coupling is acceptable;
- no independent buffering, durable waiting, or fan-out is required.

The consumer references only the producer-owned contract/application boundary. It never reads producer persistence.

### Durable one-worker work item

Use SQS when one known worker needs retry/backpressure/latency isolation and fact fan-out is not the problem.

If the work **must** survive a source transaction, write a module-owned work-outbox in the same transaction and relay it idempotently through DynamoDB Streams to the named queue. `commit -> best-effort SendMessage` is not reliable enough for required recovery work.

### Reliable business fact

Use producer outbox + DynamoDB Stream relay + EventBridge when a committed owner fact has independent consumer(s) and eventual completion is acceptable.

Critical/side-effecting consumers receive their own SQS queue + DLQ.

### Durable workflow

Use Step Functions Standard only for a named approved process that needs durable wait/branch/retry/reconciliation/compensation and whose business transitions are already owned/defined.

ADR-010 now selects Standard Workflows for the approved `OrderPlaced -> reservation -> payment/reconciliation -> OrderConfirmed -> OrderAllocated` process. Other processes remain unselected until they demonstrate the same need.

## 2. Status legend

- **Accepted** — architecture mechanism is selected for the named use case when implementation becomes Ready.
- **Conditional** — mechanism is selected but resources/contracts are created only with a named Ready producer/consumer/work item.
- **Domain/Product-gated** — implementation would encode missing business meaning.
- **Deferred** — no current need justifies a mechanism yet.

## 3. Current interaction matrix

| Caller / producer | Owner / consumer | Mechanism/status | Required truth/reliability | Remaining gate |
|---|---|---|---|---|
| API Gateway/Cognito | Tenancy / Merchant Access | synchronous; **Accepted** | JWT validates identity only; Merchant Access resolves current Tenant + Membership | none for approved `PD-001/003`; `PD-004` only for exact Suspended support/read routes |
| protected API | Merchant Access | synchronous; **Accepted** | current authority on every request; strong subject discovery + selected Tenant validation; no JWT/cache authority | none for ordinary Active-Tenant path |
| protected subscription-governed use case | SubscriptionBilling | synchronous `EvaluateEntitlement`; **Accepted** | current entitlement authority from SubscriptionBilling; owner-local usage/state remains separate | exact package keys/values under `PD-044` where needed |
| onboarding delivery | Tenancy + SubscriptionBilling | sync fast path + durable work-outbox/SQS recovery; **Accepted** | completed result requires Active Tenant + Owner + 30-day Trial; no cross-module transaction | none for approved MVP onboarding; ADR-009 |
| back office/API | Catalog | synchronous commands/queries; **Accepted** | Catalog owns lifecycle/uniqueness/public Product truth | Storefront Tenant address only for public Tenant route |
| public Storefront | Catalog public Product projection | synchronous read; **Domain-gated** at Tenant-address binding | Published public projection; cache cannot authorize checkout | Storefront Tenant-address ownership/lifecycle/uniqueness |
| checkout / Sales PlaceOrder | Catalog/Pricing | synchronous owner query; **Accepted** | current sellability/price; any price change requires reconfirmation and no Order | future Pricing promotion semantics only if introduced |
| Sales order process | Inventory | synchronous application command from workflow task; **Accepted** | all-line source-idempotent reservation; Inventory owns quantity truth | cardinality process refinement only if one transaction cannot cover real order size |
| Sales order process | Payments | synchronous capture/query/reconcile application contracts; **Accepted** | one Payment obligation/Order, multiple attempts, Unknown preserved | none for merchant-order MVP semantics |
| Sales OrderPlaced | order process | Sales outbox -> Step Functions Standard start; **Accepted/Conditional** | deterministic process/execution identity; duplicate-safe start; ADR-010 | implementation Ready task only |
| Payments | merchant-order Mock Provider | provider port/adapter + HTTPS/callback/query; **Conditional** | external-like idempotency, verified evidence, Unknown/reconciliation | concrete mock-provider implementation task |
| PaymentCaptured | Sales convergence | producer outbox -> EventBridge -> Sales SQS or workflow-compatible convergence path; **Conditional** | duplicate immediate result + event cannot double-confirm | named consumer introduced with order workflow |
| PaymentCaptured | Accounting | outbox -> EventBridge -> Accounting SQS/DLQ; **Conditional** | one source posting Dr Cash / Cr Customer Deposits | Accounting implementation Ready task |
| OrderConfirmed | SubscriptionBilling UsageMeter | outbox -> EventBridge -> SubscriptionBilling SQS/DLQ; **Conditional** | idempotent current-billing-period warning meter; never blocks checkout | exact threshold/package value under `PD-044` if not otherwise approved |
| OrderConfirmed | Reporting | EventBridge projection; **Conditional** | approved operational KPI source fact; projection never authority | projection task |
| OrderFulfilled | Accounting | outbox -> EventBridge -> Accounting SQS/DLQ; **Conditional** | one revenue posting Dr Deposits / Cr Sales Revenue | fulfillment implementation contract |
| StockIssued | Accounting | outbox -> EventBridge -> Accounting SQS/DLQ; **Conditional** | one COGS posting using Accounting valuation truth | moving-average cost-pool domain scope before valuation implementation |
| Procurement GoodsReceiptRecorded | Inventory | outbox -> EventBridge -> Inventory SQS/DLQ; **Conditional** | Procurement evidence remains committed if Inventory application fails; Inventory applies source idempotently | Procurement/Inventory Ready tasks |
| GoodsReceiptRecorded | Accounting | outbox -> EventBridge -> Accounting SQS/DLQ; **Conditional** | accepted Procurement cost evidence drives Inventory/GRNI posting; no foreign table read | moving-average cost-pool scope for valuation state |
| SupplierInvoiceRecorded | Accounting | outbox -> EventBridge -> Accounting SQS/DLQ; **Conditional** | GRNI/AP + approved variance posting | Ready task |
| SupplierPaymentRecorded | Accounting | outbox -> EventBridge -> Accounting SQS/DLQ; **Conditional** | AP/Cash posting | Ready task |
| StockAdjusted | Accounting | outbox -> EventBridge -> Accounting SQS/DLQ; **Conditional** | approved adjustment gain/loss posting | valuation basis supplied by source/domain contract |
| PaymentRefunded / StockReturned | Accounting | **Domain/Product-gated** | no Accounting effect inferred | `PD-023` |
| Product Data Ingestion dispatcher | crawler worker | direct SQS/DLQ; **Conditional** | bounded source concurrency/retry/kill switch; immutable snapshot | source-specific Ready policy/task |
| Product Data Ingestion | Catalog | explicit synchronous Catalog apply command; **Accepted** | Approved candidate != Applied until Catalog accepts; no cross-table access | none for `PD-040` semantics |
| scheduled ingestion start | SubscriptionBilling + PDI policy | synchronous entitlement + source-policy checks; **Accepted** | entitlement cannot override globally disabled/unapproved source | exact plan capability value under `PD-044` if needed |
| owned critical fact | Notification | EventBridge -> Notification SQS/DLQ; **Conditional** | per-recipient state; delivery failure never reverses source | named recipient/event contract |
| approved privileged action/rejection | Audit | source-owned durable audit intent -> reliable append; **Accepted/Conditional** | source success cannot silently omit recoverable required Audit evidence; rejection not reduced to logs | Audit implementation task |
| SubscriptionBilling | Mock SaaS Billing Provider | provider-neutral port -> separate external-like app; **Accepted/Conditional** | dedicated provider separate from merchant-order Payments; Unknown/idempotency/query/callback | exact package amount only when charge needs it |
| Mock SaaS Billing Provider | SubscriptionBilling | authenticated provider callback/query evidence; **Accepted/Conditional** | evidence only until SubscriptionBilling verifies/accepts | provider task |
| platform-admin support | SubscriptionBilling | synchronous read-only application query; **Accepted** | separate admin context; no direct table/mutation/override | platform-admin auth/delivery task |
| owned facts | Reporting | EventBridge projection; **Conditional** | rebuildable/display only | named formula/projection task |
| fulfillment | Inventory/Sales | **Deferred** beyond approved fact sequence | whole-order semantics known, but initiation/shipping/cardinality mechanism not yet refined | Ready fulfillment contract/architecture refinement |

## 4. Reliable publication pattern

Critical business facts use ADR-006:

```text
owning module TransactWriteItems
  business state + OUTBOX record
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
  INBOX/source marker + consumer-owned effect
```

Rules:

1. state commit followed by best-effort `PutEvents` is not reliable publication;
2. outbox payload is the complete stable integration contract, not a pointer requiring producer-table access;
3. relay may publish the same eventId more than once; consumers must be duplicate-safe;
4. Stream/Lambda/EventBridge/SQS are at-least-once paths and may deliver out of order;
5. stream retention is not the recovery store — pending outbox remains queryable;
6. EventBridge target failure and consumer queue failure are distinct operational failure points;
7. redrive preserves original eventId/version/correlation/causation/source identity;
8. queue/DLQ/retry/age are operational states only and never mutate business truth;
9. reconciliation compares durable source/consumer-owned records via application contracts/own state, never foreign table reads.

No Stream/bus/rule/queue is created until a named producer/consumer is Ready.

## 5. Transactional work-outbox

For a required one-worker command, use a targeted work-outbox instead of EventBridge fan-out.

Current approved example: onboarding Trial-bootstrap recovery under ADR-009.

```text
Tenancy transaction
  local onboarding outcome + WORKOUTBOX
             │
             ▼
DynamoDB Stream relay
             ▼
SQS onboarding recovery queue
             ▼
worker invokes idempotent SubscriptionBilling.StartTrialSubscription
```

Rules:

- work message is a command/request, not a business fact;
- duplicate delivery is expected and command identity is stable;
- queue failure never means Trial business failure;
- successful worker result updates operation status only through the owning application contract;
- EventBridge is unnecessary when there is one known target and no fan-out.

## 6. Integration event contract

An internal domain fact becomes an integration event only when a named cross-boundary consumer exists.

Required envelope:

```json
{
  "eventId": "evt_...",
  "eventType": "OrderConfirmed",
  "eventVersion": 1,
  "tenantId": "tenant_...",
  "aggregateId": "order_...",
  "occurredAt": "2026-08-10T00:00:00Z",
  "correlationId": "corr_...",
  "causationId": "cmd_or_evt_...",
  "producer": "sales",
  "data": {}
}
```

Rules:

- `tenantId` required for tenant-owned facts;
- `data` carries stable consumer-required facts, never database/domain serialization;
- consumer rejects unsupported type/version safely;
- additive evolution may remain compatible only under the contract policy; breaking semantic/required-field change creates a new version;
- sensitive/private fields minimized; no token/credential/raw provider/source payload/card-like data;
- EventBridge rule matches explicit producer/type/version.

## 7. Consumer requirements

Every side-effecting consumer:

- assumes duplicate/out-of-order delivery;
- validates envelope/Tenant/source/type/version/business invariants;
- atomically writes inbox/source identity with owned effect where possible;
- returns prior effect for equivalent replay and rejects incompatible source reuse;
- never regresses a later state from older evidence;
- classifies transient/permanent errors and bounds automatic retry;
- exposes queue age/errors/throttles/DLQ through built-in metrics/alarms;
- documents redrive/reconciliation before production use;
- never writes/reads producer persistence;
- never treats message TenantId as merchant actor authority.

For external side effects, persist stable operation identity before unsafe retry and use provider idempotency/query semantics. Timeout remains potentially ambiguous.

## 8. Order Step Functions Standard workflow

ADR-010 is the first approved Step Functions specialization.

### Scope

Start only after `OrderPlaced` is accepted. It coordinates:

```text
Inventory reserve
  -> Payments capture
     -> Captured: Sales Confirm + Allocate
     -> definitive NoCommit/Decline: AwaitingPaymentRetry process state
     -> Unknown: Payments reconciliation/wait
                  -> Captured / definitive NoCommit
                  -> NeedsAttention if bounded automation stops unresolved
```

### Semantic safety

The workflow cannot:

- create an Order before price reconfirmation;
- convert technical timeout/retry exhaustion/workflow failure into Payment failure;
- auto-cancel Order or release stock on decline/Unknown/technical failure;
- start another capture attempt while prior attempt is Unknown;
- call provider persistence/API directly instead of Payments contracts;
- post Accounting;
- infer refund Accounting;
- invent automatic fulfillment/shipping.

Workflow/process states are technical only unless an owning domain accepts a business transition.

### Start reliability

Sales writes a workflow-start outbox atomically with Order/process state. Relay starts a Standard execution with deterministic Order/process identity. Duplicate starts are idempotent.

### Cost guardrail

Each Ready implementation task calculates:

- normal-success transition count;
- decline path transition count;
- Unknown/reconciliation transition envelope;
- expected monthly workflow executions;
- retry/wait strategy that avoids high-frequency polling.

## 9. EventBridge policy

Use custom EventBridge for versioned fact routing/fan-out with named consumers.

Do not use it for:

- ordinary in-process commands/queries;
- generic database changes;
- one known work queue command where SQS is sufficient;
- an empty platform bus created from a target diagram.

Each rule documents producer/type/version, target, retry/failure policy, IAM, payload minimization, cost, compatibility/deletion plan.

## 10. SQS/DLQ policy

Use Standard queue by default. FIFO only when ordering/dedup requirements cannot be safely handled by source/aggregate idempotency and the trade-off is justified.

Every queue defines:

- producer/consumer and message contract;
- visibility timeout > bounded handler duration;
- batch/concurrency cap;
- max receives/redrive policy;
- DLQ retention/alarm;
- transient/permanent failure classification;
- replay preserving identity;
- poison-message/manual recovery;
- safe logs/metrics.

Queue age/DLQ is never business state.

## 11. Step Functions policy beyond ADR-010

Do not wrap CRUD or every cross-module call in a state machine.

Any new workflow requires:

- approved business sequence/owner for every transition;
- demonstrated durable wait/branch/callback/retry/compensation need;
- execution identity/duplicate-start rule;
- explicit Unknown/timeout semantics;
- operator recovery/reconciliation;
- state-transition cost envelope;
- an ADR when the choice is material.

ADR-010 does not approve Step Functions for onboarding, Subscription plan changes, Procurement, refund, or fulfillment by default.

## 12. Audit delivery

Audit is not a log stream and not source business state.

### Successful covered mutation

Source module writes a durable Audit intent/outbox atomically with accepted state. Audit appends idempotently later.

### Covered rejected attempt

When rejection produces no source-state transaction, the rejecting owning module persists a standalone idempotent Audit-delivery intent before returning where practical. This is still source-owned delivery evidence, not an Audit table write.

Audit consumer:

- appends immutable evidence;
- validates Tenant/actor/action/outcome/source identity;
- does not read source tables;
- preserves tenant-visible non-disclosure rules.

## 13. AWS capability matrix

The project remains serverless/pay-per-use and Free-Tier/credit constrained.

| Capability | Problem solved | Status / introduction point | Cost/security guardrail |
|---|---|---|---|
| API Gateway HTTP API | HTTPS routing/JWT/throttling | **Accepted** with first protected/public API | HTTP API by default; bounded payload/load; provider ingress may be separate route/API if isolation warrants |
| Lambda | scale-to-zero API/workers/task handlers | **Accepted** | no provisioned concurrency; bounded risky-worker concurrency; no sleeping/long polling |
| Cognito | merchant authentication/token lifecycle | **Accepted** in IdentityStack | identity only; avoid paid SMS/advanced features by default |
| DynamoDB | module-owned transactional persistence | **Accepted** per module | one table/module; no Scan/speculative GSI/PITR; explicit capacity/cost |
| DynamoDB Streams | outbox/work wake-up | **Conditional** with first durable outbox relay | filter relevant records; retain durable outbox for repair |
| EventBridge custom bus | versioned fact routing/fan-out | **Conditional** with first named fact consumer | no CRUD/generic events/empty bus |
| SQS + DLQ | one-worker recovery/backpressure/critical consumer isolation | **Conditional** with named worker/consumer | bounded retries/batches; DLQ alarm/redrive |
| Step Functions Standard | durable order payment/allocation orchestration | **Accepted** for ADR-010 process when Ready | count transitions; no high-frequency Unknown polling; no unrelated CRUD workflows |
| EventBridge Scheduler | crawler/provider reconciliation/cleanup schedule | **Conditional** when a scheduled use case is approved | dev schedules disabled/manual by default; no demo loops |
| S3 | private static/media/raw/export objects | **Conditional** with Web/FilesMedia/Ingestion task | lifecycle raw/temp data; no arbitrary external media copy |
| CloudFront | public static/media delivery/cache | **Conditional** with Web/FilesMedia | cache never transaction/authorization authority |
| CloudWatch | logs/metrics/alarms | **Accepted** with runtime | bounded retention, built-ins first, low-cardinality custom metrics |
| CDK/CloudFormation | reproducible reviewed infrastructure | **Accepted** | no Console source of truth |

No NAT Gateway, ALB/NLB, EC2 application server, RDS/Aurora, Redis/ElastiCache, OpenSearch, MSK/Kafka, EKS, always-on ECS/Fargate, paid WAF, or provisioned Lambda concurrency is approved.

## 14. Regional and stack mapping

Initial application workloads remain single-region `ap-southeast-1`; CloudFront is global.

```text
FoundationStack
  bounded shared technical observability/config only

IdentityStack
  Cognito user pool + SPA client when protected API starts

CommerceStack
  API Gateway HTTP API
  commerce-api Lambda
  module DynamoDB constructs as Ready tasks introduce them

WebStack
  private S3 origins
  CloudFront

Integration resources
  per-module Streams/outbox relays
  EventBridge bus/rules
  per-consumer SQS/DLQ/workers

OrderWorkflow resources
  Step Functions Standard
  task-handler Lambda composition
  only with ADR-010 Ready implementation

CrawlerStack
  only with crawler Ready work

MockPaymentStack
  merchant-order external-like provider

MockSaaSBillingStack
  separate simulated SaaS provider
```

Stacks are deployment/update groups, not bounded contexts.

## 15. Files/Media AWS mapping

Merchant-uploaded Product media uses:

```text
client
  -> FilesMedia upload authorization (API/Lambda)
  -> private S3 object under server-assigned Tenant/Asset key
  -> FilesMedia metadata becomes accepted
  -> Catalog attaches MediaAssetId through application contract
  -> CloudFront/public projection serves only approved public association
```

Rules:

- bucket private by default;
- client cannot choose another Tenant object prefix;
- no arbitrary external image hotlink/copy;
- S3 key is not public business identity;
- lifecycle/deletion must respect future FilesMedia/reference policy;
- CloudFront cache does not authorize Catalog/public lifecycle.

## 16. Provider boundaries

### Merchant-order Mock Provider

Payments adapter treats provider as external:

- provider idempotency identity;
- success/decline/Unknown;
- callback/query verification;
- duplicate/out-of-order evidence;
- reconciliation before unsafe retry.

### Mock SaaS Billing Provider

Separate application/provider seam from merchant-order Payments:

- PlatformCharge idempotency;
- success/definitive NoCommit/Unknown;
- callback/query/reconciliation;
- no real card/bank data;
- no provider state leaking into merchant-order Payments.

Provider webhook ingress may use a separate API/Lambda boundary if secret/IAM/rate-limit/failure isolation benefits justify it.

## 17. Security controls

- API Gateway/Cognito authenticates; Merchant Access authorizes Tenant/Membership; owning module authorizes operation.
- SubscriptionBilling separately authorizes subscription-governed capability/limit.
- tenant-owned data keys derive Tenant scope from trusted context only.
- worker/workflow messages contain minimum stable IDs/data and are not merchant authority.
- provider ingress is authenticated using provider-specific evidence, not merchant tokens.
- IAM is least privilege per runtime/queue/table/stream/state machine.
- no token, invitation secret, signing secret, card-like data, raw provider payload, or cross-Tenant identifier leakage in logs/events/problems.

## 18. Reliability/observability controls

Observe separately:

- authority/entitlement dependency errors;
- DynamoDB condition/transaction/throttle errors;
- outbox relay lag/failure;
- EventBridge target failure;
- queue age/depth/DLQ;
- workflow execution age/failure/NeedsAttention count;
- provider Unknown/reconciliation age/count;
- Accounting source/posting gaps;
- onboarding operation Pending/NeedsAttention age;
- crawler source-specific throttling/failure.

These are operational facts only. Logs/metrics never claim a business transition that the owning aggregate did not commit.

## 19. Cost posture

This architecture reconciliation deploys nothing and changes runtime cost by **$0**.

When implemented:

- no integration resource exists without a named contract;
- no state machine exists without ADR-010 Ready task;
- every GSI/queue/worker/schedule/state-machine/provider task includes a cost note;
- normal dev remains near `$0-$5/month` target from repository guardrails;
- Step Functions Unknown paths use waits/backoff, not frequent polling;
- preview/staging resources are ephemeral where possible.

## 20. Remaining gates

Do not finalize integration/AWS behavior that would encode:

- `PD-004` exact Suspended/closure/retention/privacy semantics;
- `PD-023` refund/return Accounting;
- exact plan price/entitlement packages under `PD-044`;
- Storefront Tenant-address business semantics;
- Accounting moving-average cost-pool dimension;
- any future fulfillment/shipping process not yet refined.

## 21. Verification

Dependent cloud/integration tasks must verify applicable cases:

- Cognito valid/invalid/expired token and no business authority in claims;
- Tenant A/B selector/message/workflow override isolation;
- onboarding crash/retry/queue redrive without duplicate Trial;
- outbox+source atomicity and duplicate relay/event delivery;
- EventBridge type/version routing and target failure behavior;
- SQS visibility/retry/DLQ/redrive/poison message;
- order workflow duplicate start, technical task failure, decline, Unknown, reconciliation and no false cancellation/release;
- provider callback signature/dedup/out-of-order behavior;
- Accounting consumer replay and reconciliation;
- CDK least privilege, bounded logging/tags/retention and absence of speculative resources;
- measured transition/request/log cost stays within Ready task budget.

## 22. References

- [Technical baseline](technical-baseline.md)
- [Product-decision technical reconciliation](product-decision-technical-reconciliation.md)
- [ADR-006](../adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md)
- [ADR-008](../adr/ADR-008-subscription-billing-module-entitlement-and-provider-boundary.md)
- [ADR-009](../adr/ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md)
- [ADR-010](../adr/ADR-010-order-payment-allocation-durable-orchestration.md)
- [Free Tier and credit guardrails](../development/13-free-tier-and-credit-guardrails.md)