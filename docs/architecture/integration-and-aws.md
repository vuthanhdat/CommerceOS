# CommerceOS — Integration and AWS Service Matrix

_Cross-domain, delivery, reliability, and cost baseline reconciled by TASK-0088._

## 1. Selection rules

Choose the interaction from the business need, not from the availability of an AWS icon.

### Synchronous application contract

Use when:

- the caller cannot truthfully complete without the owner's immediate accepted/rejected result;
- both modules share the current runtime and latency/failure coupling is acceptable;
- no durable waiting, burst buffering, or independent retry is required.

The consumer references only the producer-owned Contracts project. It never reads the producer's table.

### Asynchronous command/work item

Use SQS directly when one known worker must perform bursty, slow, externally constrained, or retryable work and fan-out is not the problem. A queued request is not proof that the requested business effect succeeded.

### Asynchronous business fact

Use the producer outbox and EventBridge when a committed owner fact has one or more independent consumers and eventual consistency is acceptable. Critical/bursty consumers receive their own SQS queue and DLQ.

### Durable workflow

Use Step Functions only when an approved business process needs durable waiting, branching, callback, timeout handling, bounded retry, or compensation across several steps. It is not a wrapper for CRUD and cannot decide an unresolved business sequence.

## 2. Decision legend

- **Accepted now** — use this mechanism for the stated boundary.
- **Conditional** — architecture is selected, but resources/contracts appear only with the named Ready consumer.
- **Product-gated** — mechanism or sequence would encode unresolved business behavior.
- **Deferred** — no current problem justifies a choice; the safe interim constraint is stated.

## 3. Cross-domain interaction matrix

| Producer/caller | Owner/consumer | Mechanism/status | Required truth and reliability | Gates/deferrals |
|---|---|---|---|---|
| API Gateway/Cognito | Tenancy / Merchant Access | synchronous; **Accepted now** | Cognito validates identity; `ResolveTenantAuthority` resolves current Tenant + Membership before tenant data access | tenant-selection and capabilities: `PD-001`, `PD-003` |
| onboarding delivery | Tenant Management + Merchant Access inside Tenancy | synchronous coordinator + one module transaction; **Accepted now** | completed result is Active Tenant + Active initial Owner, never a partial bootstrap | request/admission/profile: `PD-002`, `PD-034`; Audit coverage: `PD-033` |
| protected API/module | Merchant Access | synchronous authority result; **Accepted now** | current authority on every request; no JWT/cache authority | `PD-001`, `PD-003`, `PD-004` details |
| back office/API | Catalog | synchronous application commands/queries; **Accepted now** | Catalog owns validation/state/persistence; no EventBridge for CRUD | Catalog product gates apply |
| Storefront | Catalog public projection | synchronous read contract; **Product-gated** | Published Catalog projection only; cache never authorizes checkout | missing Tenant-address decision; `PD-006`–`PD-010`, `PD-037` |
| Sales checkout | Catalog/Pricing | synchronous owner query; **Conditional after product gates** | current sellability/commercial facts, never browser price or table read | `PD-011`–`PD-014` |
| Sales | Inventory reserve/release/issue | **Product-gated** | explicit Inventory command/result and Inventory facts; idempotent logical source; no implied Sales fact | `PD-014`, `PD-015`, `PD-018`, `PD-041`, `PD-042`; sync vs durable command remains deferred |
| Sales | Payments | **Product-gated** | Sales supplies accepted obligation; Payments owns provider interaction/known outcome | `PD-014`, `PD-016`–`PD-018` |
| Payments | Mock Payment Provider | synchronous HTTPS commands/queries + asynchronous signed webhook/inquiry; **Conditional** | provider idempotency, verified evidence, deduplication, unknown-outcome reconciliation | provider boundary accepted; exact flow waits for `PD-016`–`PD-018` and a payment ADR |
| Payments/Inventory facts | Sales convergence | outbox → EventBridge → Sales SQS/DLQ; **Conditional** | idempotent source application; no stale/out-of-order regression; sync result and later fact share a logical source | final facts/transitions wait for `PD-014`–`PD-018`, `PD-042` |
| Product Data Ingestion acquisition | crawler worker | direct SQS/DLQ work queue; **Conditional** | bounded source concurrency/retry/policy, immutable snapshot; worker states are telemetry | `PD-026`; source-specific policy task |
| Product Data Ingestion | Catalog import application | explicit Catalog command; **Product-gated** | candidate becomes Applied only after Catalog accepts; no cross-table access | final sync/async handshake and mapping wait for `PD-040` |
| Procurement `GoodsReceiptRecorded` | Inventory | outbox → EventBridge → Inventory SQS/DLQ; **Conditional later** | Procurement remains committed if Inventory application fails; Inventory emits `StockReceived` only after its effect; recovery state remains honest | `PD-025`, `PD-027`–`PD-029`, especially `PD-028` |
| selected operational fact | Accounting | outbox → EventBridge → accounting SQS/DLQ; **Conditional later** | posting + source dedup atomic; reconciliation finds missing expected posting | exact single trigger/policy waits for `PD-020`–`PD-024`, `PD-038`, `PD-039` |
| owned domain facts | Reporting | EventBridge projection; SQS for durable backlog, or direct Lambda only if explicitly rebuildable/low-risk; **Conditional later** | lag/failure never blocks or authorizes source; projection replay idempotent | formula/time gates `PD-030`, `PD-031` |
| owned domain facts | Notification | EventBridge → SQS/DLQ; **Conditional later** | non-critical delivery failure never reverses transaction | audience/state `PD-032` |
| designated privileged action | Audit | same source transaction writes durable audit intent/outbox → idempotent Audit append; **Product-gated then Conditional** | success cannot silently omit required evidence; Audit record is not a domain event | coverage/readers/rejections: `PD-033` |
| any later multi-step process | Step Functions | **Deferred** | select only for approved durable orchestration pressure | order/payment/refund/procurement decisions must resolve first |

The matrix records direction and reliability. It does not select any pending business fact, state, timing, or role.

## 4. Reliable publication

Critical cross-domain delivery uses [ADR-006](../adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md):

```text
owning module TransactWriteItems
  business state + OUTBOX record
             │ atomic
             ▼
module DynamoDB Stream (OUTBOX-filtered)
             ▼
idempotent relay Lambda
             ▼
EventBridge custom event bus
             ▼
consumer-specific SQS + DLQ
             ▼
idempotent consumer transaction
  INBOX/source key + consumer-owned effect
```

Rules:

1. A database commit followed by a best-effort `PutEvents` call is not reliable publication.
2. The outbox record stores the complete stable integration contract, not a pointer that forces the relay/consumer to query producer persistence.
3. The relay is idempotent. Republishing the same eventId is permitted; consumers remain idempotent.
4. DynamoDB Streams/Lambda delivery is at least once and may duplicate. Partial batch response and bounded failure handling are required.
5. Stream retention is not the recovery store. Pending outbox records remain queryable/reconcilable after stream records expire.
6. Critical EventBridge rules use target delivery failure handling; critical consumers use queue redrive/DLQ handling. The two failure points are observable separately.
7. Redrive preserves the original eventId, eventVersion, correlationId, causationId, occurredAt, and logical source identity.
8. Queue age, retry count, DLQ placement, redrive time, and relay state are operational facts only.
9. A reconciliation query compares durable producer/consumer-owned source records; it does not repair by reading another module's table directly.

No stream, relay, event bus, queue, or DLQ is created until a named integration contract and consumer are Ready.

## 5. Integration event contract

An internal domain fact becomes an integration event only when a real cross-boundary consumer exists. The producer owns the schema and publishes a stable, past-tense business meaning from the TASK-0087 fact catalog.

Required envelope:

```json
{
  "eventId": "evt_...",
  "eventType": "ProductPublished",
  "eventVersion": 1,
  "tenantId": "tenant_...",
  "aggregateId": "product_...",
  "occurredAt": "2026-08-09T10:00:00Z",
  "correlationId": "corr_...",
  "causationId": "cmd_...",
  "producer": "catalog",
  "data": {}
}
```

Rules:

- `tenantId` is required for tenant-owned facts; a platform-global event documents why it is absent.
- `causationId` is the direct causing command/event and may be absent only for a documented root fact.
- `data` contains the stable facts the consumer contract needs, not a database row or domain entity serialization.
- consumers reject unknown event types and unsupported major versions safely; they do not guess a schema.
- additive compatible evolution may remain in the same version only under the contract compatibility policy; breaking meaning/required fields create a new version.
- sensitive/private fields are minimized and never routed broadly for convenience.
- an EventBridge rule matches producer/type/version explicitly rather than accepting every event.

The older examples `GoodsReceived`, `PaymentFailed`, `ProductSourceChanged`, and a generic `TenantCreated` are not automatically approved contracts. Use the authoritative fact meanings in TASK-0087 and the applicable product gate.

## 6. Consumer requirements

Every side-effecting consumer:

- assumes at-least-once and possible out-of-order delivery;
- validates envelope, TenantId, source identity, event type/version, and semantic invariants;
- writes its inbox/logical source marker atomically with its owned effect where possible;
- returns the prior result for equivalent replay and rejects incompatible source reuse;
- cannot regress a later state from older evidence;
- uses bounded retry with classified transient/permanent failures;
- emits safe structured logs and built-in queue/Lambda metrics;
- alarms on oldest message age, consumer errors/throttles, and DLQ depth;
- has a documented redrive and reconciliation procedure before production use;
- never writes the producer's table or treats a message as merchant authority.

For an external side effect, such as payment-provider operation or notification delivery, the consumer persists an operation identity/state before or with the call where possible, passes an external idempotency key, treats timeouts as potentially ambiguous, and reconciles rather than assuming failure.

## 7. EventBridge, SQS, and Step Functions policy

### EventBridge

Use for versioned fact routing/fan-out when at least one consumer is named. Do not use for:

- ordinary in-process Tenant/Catalog calls;
- generic database-change events;
- commands that need one known worker and no fan-out;
- an event bus created in `FoundationStack` merely because the target diagram includes one.

Each rule has an owner, source/type/version pattern, target, retry/failure policy, IAM policy, cost estimate, and deletion/compatibility plan.

### SQS and DLQ

Use a standard queue by default for at-least-once work. FIFO is selected only when a documented ordering/deduplication requirement cannot be met through aggregate/source idempotency and its throughput/cost trade-off is justified.

Every queue specifies:

- producer/consumer and message contract;
- visibility timeout greater than the bounded consumer duration;
- batch size/concurrency cap;
- maximum receives/redrive policy;
- DLQ retention and alarm;
- transient/permanent classification;
- replay procedure preserving identity;
- poison-message/manual recovery behavior;
- tenant-safe logs and metrics.

### Step Functions

Step Functions Standard is not selected for first-frontier work. A future workflow ADR/task must show:

- approved business sequence and owner of every transition;
- why application code + durable records/queues are insufficient;
- execution identity/idempotency and duplicate-start behavior;
- timeout/unknown-outcome semantics;
- retry/catch/wait/callback/compensation policy;
- operator recovery and reconciliation;
- bounded state-transition estimate for learning and beta profiles.

The previous illustrative checkout diagram is not an implementation decision. No workflow may map generic “failed” or elapsed time directly to stock release, Order failure, payment failure, refund, or accounting effect without the relevant product decision and owner fact.

## 8. AWS service rationale and decision status

The estimates reference the repository [cost model](../04-cost-model.md) and [Free Tier guardrails](../development/13-free-tier-and-credit-guardrails.md). They are planning values, not quotes.

| AWS capability | Problem solved | Status / introduction point | Learning/Beta cost posture and guardrails |
|---|---|---|---|
| API Gateway HTTP API | public HTTPS routing, JWT validation, throttling boundary | **Accepted** with first protected API; one commerce API, separate provider endpoint later | model ~$0.25 / ~$2.50; HTTP API only unless a REST-only need is documented; bounded payload/load tests |
| Lambda | scale-to-zero .NET API and worker compute | **Accepted**; one `commerce-api`, then workers split by runtime/failure profile | model ~$0 / ~$0.50; no provisioned concurrency; caps for risky workers; no sleep/long polling |
| Cognito | merchant staff authentication/token lifecycle | **Accepted** in `IdentityStack`; never Membership/Tenant authority | model $0 under initial MAU assumptions; no SMS/advanced paid feature by default; guest shoppers excluded |
| DynamoDB | module-owned serverless persistence, conditions, small transactions | **Accepted** per module as introduced | model ~$0.29 / ~$2.88 on-demand; small provisioned learning profile may approach $0; no speculative GSIs/PITR |
| DynamoDB Streams | durable outbox change capture after atomic commit | **Conditional** with first reliable integration event | no standing compute; adds stream/relay processing and operational complexity; filter OUTBOX records, measure relay invocations, retain outbox for repair |
| EventBridge custom bus | versioned fact routing/fan-out | **Conditional** with first named asynchronous fact consumer | model ~$0.03 / ~$0.40; custom events are credit-funded; no CRUD events or empty bus |
| EventBridge Scheduler | bounded crawl/reconciliation/cleanup schedule | **Conditional** with approved scheduled job | expected within documented allowance; dev schedules disabled/manual by default; no high-frequency demo loop |
| SQS + DLQ | backpressure, independent retry, poisoned-work containment | **Conditional** with first crawler/critical consumer/provider callback | model $0 below initial request allowance; one message causes multiple requests; bounded batches/retries and DLQ alarm |
| Step Functions Standard | durable multi-step waiting/branching/retry/compensation | **Deferred** until an approved workflow ADR | model ~$0.15 / ~$2.40 at existing scenarios; retries multiply transitions; no first-frontier state machine |
| S3 | private static origins; later policy-safe objects/raw ingestion/export | **Conditional** with Web/Ingestion/Files task | model ~$0.11 / ~$0.46; lifecycle raw/temp data, private buckets, no unapproved external-media copy |
| CloudFront | global static delivery/caching | **Conditional** with deployed web applications | initial Free Tier/pay-as-you-go behavior; do not rely on flat-rate plans while the account is ineligible; cache never authorizes a transaction |
| CloudWatch | bounded logs, built-in metrics, alarms | **Accepted**; only foundation log group currently implemented | model $0 at learning/beta log assumptions; short retention, low cardinality, logs are not Audit |
| AWS CDK/CloudFormation | reproducible reviewed infrastructure and teardown | **Accepted** by ADR-001 | no direct service charge; deployed resources determine cost; no Console source of truth |

No new service/resource is deployed and monthly/one-off AWS cost remains zero for TASK-0088.

## 9. Regional and stack mapping

Initial application workloads are single-region in `ap-southeast-1`; CloudFront is global. Global tables/cross-region business writes require a later recovery/data-residency/cost ADR.

```text
FoundationStack (implemented skeleton)
  bounded shared technical observability/config only

IdentityStack (first protected API)
  Cognito user pool + public SPA client, cost-safe settings

CommerceStack (first business API)
  API Gateway HTTP API
  commerce-api Lambda
  Tenancy table/construct
  Catalog table/construct when Catalog starts

WebStack (when static deployment is in scope)
  private S3 origins
  CloudFront

Integration resources (first named async consumer)
  module table streams/outbox relay
  custom EventBridge bus/rules
  per-consumer queues/DLQs/workers

CrawlerStack / MockPaymentStack
  only when their Ready tasks introduce distinct runtime/failure boundaries
```

Stacks are deployment/update groupings, not bounded contexts. Stateful resource replacement/retention is reviewed explicitly; preview synthetic resources are destroyed, while production-like resources use protection appropriate to their data.

## 10. Cost and security controls

- No NAT Gateway, ALB, EC2, RDS/Aurora, OpenSearch, ElastiCache, MSK, EKS, always-on ECS/Fargate, or paid WAF is approved.
- API, functions, tables, streams, queues, rules, buckets, and logs are CDK-defined and tagged.
- IAM grants are resource/action-specific; no business workload receives account-wide DynamoDB/EventBridge/SQS access.
- AWS-managed encryption is the default when compatible with the integration; customer-managed keys require a threat/compliance need and cost/permission analysis.
- Preview resources are ephemeral; recurring dev schedules are disabled/manual unless actively tested.
- Logs and object retention are bounded; retries, queue batch, worker concurrency, and crawler rates are capped.
- Built-in service metrics precede custom metrics; no TenantId/high-cardinality custom dimension.
- CloudFront flat-rate plans are not a current deployment assumption while Free Tier account eligibility rules exclude them. Re-evaluate eligibility and pricing before adopting a paid plan.

## 11. Deferred architecture records

| Decision | Required trigger | Current safe constraint |
|---|---|---|
| payment ambiguity/webhook/inquiry/reconciliation ADR | `PD-016`–`PD-018` resolved and payment task refined | HTTPS boundary + verified evidence; timeout never failure; no unsafe retry |
| accounting trigger/posting recovery ADR | relevant `PD-020`–`PD-024`, `PD-038`, `PD-039` resolved | no route/event contract and no duplicate alternative triggers |
| Step Functions topology ADR | approved flow demonstrates durable orchestration need | no state machine |
| FIFO queue selection | real per-group ordering/dedup pressure not solved by source idempotency | standard at-least-once + idempotent consumer |
| direct EventBridge-to-Lambda projection | projection proven rebuildable and loss/retry recovery documented | use consumer queue for critical side effects |
| multi-region | explicit recovery/data-residency requirement | single-region |

## 12. Required cloud verification when introduced

TASK-0088 requires no cloud verification. The tasks that first implement these AWS semantics require bounded real-AWS evidence for:

- Cognito token/API Gateway authorizer behavior and denial;
- Lambda package/runtime/IAM wiring;
- DynamoDB conditional/transaction/consistent read and cross-tenant keys;
- stream relay duplicate/partial-batch/failure/recovery behavior;
- EventBridge rule matching and target failure/DLQ behavior;
- SQS visibility, retry, redrive, duplicate, and DLQ behavior;
- Step Functions retry/catch/wait/callback behavior if later selected;
- S3/CloudFront policies, private origins, lifecycle, and cache behavior;
- CloudWatch log/metric/alarm wiring;
- CDK diff, cost review, resource tags, and ephemeral teardown.

## 13. AWS references

- [DynamoDB transactions](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/transaction-apis.html)
- [DynamoDB read consistency](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.ReadConsistency.html)
- [Lambda with DynamoDB Streams](https://docs.aws.amazon.com/lambda/latest/dg/with-ddb.html)
- [DynamoDB stream event-source parameters](https://docs.aws.amazon.com/lambda/latest/dg/services-ddb-params.html)
- [API Gateway HTTP API JWT authorizers](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-jwt-authorizer.html)
- [SQS at-least-once delivery](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/standard-queues-at-least-once-delivery.html)
- [EventBridge targets](https://docs.aws.amazon.com/eventbridge/latest/userguide/eb-targets.html)
- [EventBridge target DLQs](https://docs.aws.amazon.com/eventbridge/latest/userguide/eb-rule-dlq.html)
