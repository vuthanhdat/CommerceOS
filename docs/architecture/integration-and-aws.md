# CommerceOS — Integration and AWS-style Service Matrix

_Reconciled to ADR-012 LocalStack-only runtime on 2026-08-11._

## 1. Selection rule

Choose interactions from business/process need first, not from service availability.

Architecture contracts describe capabilities. AWS-style service names are LocalStack implementation mappings where sufficiently supported.

### Synchronous producer-owned application contract

Use when the caller cannot truthfully complete without the owner's immediate accepted/rejected/current result and modules share the runtime.

### Durable one-worker work item

Use a durable queue capability when one known worker needs retry/backpressure/latency isolation and fan-out is not the problem. Preferred mapping: SQS/DLQ in LocalStack.

If work must survive a source transaction, use a module-owned work-outbox plus a reliable relay. `commit -> best-effort send` is insufficient.

### Reliable business fact

Use producer state + outbox -> owned change-feed relay -> fact-routing capability -> consumer-specific queue/DLQ when a committed owner fact has independent consumers.

Preferred mapping: DynamoDB Streams -> EventBridge -> SQS/DLQ in LocalStack where supported.

### Durable workflow

Use a durable workflow capability only for a named approved process that needs wait/branch/retry/reconciliation.

ADR-010 keeps the order payment/allocation workflow. ADR-011 keeps refund propagation as choreography rather than a new workflow.

## 2. Runtime-target rule

Under ADR-012:

- LocalStack is the only infrastructure target;
- real AWS accounts are not used for validation/deployment;
- AWS account/IAM/OIDC/Budget/cost gates are obsolete;
- LocalStack endpoints, synthetic credentials, region/account placeholders, ports, resource prefixes, and edition/feature flags are configuration concerns;
- Domain/Application code depends on project-owned contracts, never LocalStack details;
- unsupported/partial/different/edition-dependent behavior is documented explicitly and does not justify weakening business contracts.

## 3. Core interaction matrix

| Caller / producer | Owner / consumer | Mechanism | Required truth/reliability |
|---|---|---|---|
| identity edge | Tenancy Merchant Access | synchronous | identity evidence only; current Tenant/Membership authority remains server-owned |
| merchant query | Merchant Access | `ResolveTenantReadAuthority` | Active Membership; Active/Suspended Tenant per policy |
| merchant command | Merchant Access | `ResolveTenantMutationAuthority` | Active Membership + Active Tenant + domain authorization |
| platform admin | Tenancy | synchronous suspend/reactivate | explicit platform trust + reason/revision/Audit |
| platform support | owning modules | read-only producer queries | no Tenant Membership/direct storage bypass |
| governed command | SubscriptionBilling | `EvaluateEntitlement` | current EntitlementSet authority |
| onboarding | Tenancy + SubscriptionBilling | sync fast path + durable recovery work | Active Tenant + Owner + exact Trial outcome |
| membership/warehouse limit | SubscriptionBilling + owning module | sync entitlement + owner-local conditional write | current hard limit + authoritative local count |
| checkout | Catalog/Pricing | synchronous owner query | current sellability/price |
| Sales `OrderPlaced` | order process | durable workflow | deterministic process identity; ADR-010 |
| order workflow | Inventory | synchronous app task | idempotent all-line reservation |
| order workflow | Payments | synchronous capture/query/reconcile | provider ambiguity preserved |
| `PaymentCaptured` | Accounting/Sales | reliable business fact | duplicate-safe convergence/posting |
| `OrderConfirmed` | UsageMeter/Reporting | reliable business fact | idempotent meter/projection |
| `OrderFulfilled` | Accounting/Reporting | reliable business fact | revenue/projection |
| `StockIssued` | Accounting | reliable business fact | COGS/Inventory posting |
| `RefundApproved` | Inventory/Payments/Accounting | reliable fan-out | ADR-011 independent effects |
| `StockReturned` | Accounting | reliable business fact | COGS/inventory reversal |
| `PaymentRefunded` | Accounting | reliable business fact | verified provider truth |
| Procurement facts | Inventory/Accounting | reliable business facts | owner evidence only |
| PDI dispatcher | crawler worker | durable work queue | bounded retry/concurrency/kill switch |
| covered privileged action | Audit | durable Audit intent + reliable append | not best-effort logging |
| owned facts | Notification | fact routing + queue | per-recipient delivery/read truth |

## 4. Reliable publication pattern

```text
owning module transaction
  business state + OUTBOX
          ↓
owned change feed
          ↓
idempotent relay
          ↓
fact routing
          ↓
consumer-specific queue/DLQ
          ↓
idempotent consumer
  INBOX/source marker + owned effect
```

Rules:

1. best-effort publish after commit is insufficient for critical facts;
2. event payload contains stable consumer-required facts, not producer-table pointers;
3. delivery may duplicate or arrive out of order;
4. consumers persist source identity and never duplicate/regress owned state;
5. queue/DLQ/retry state is operational only;
6. redrive preserves event/source/correlation/causation identity;
7. reconciliation never reads foreign persistence.

## 5. Integration event contract

Required envelope:

```json
{
  "eventId": "evt_...",
  "eventType": "RefundApproved",
  "eventVersion": 1,
  "tenantId": "tenant_...",
  "aggregateId": "...",
  "occurredAt": "...",
  "correlationId": "corr_...",
  "causationId": "...",
  "producer": "sales",
  "data": {}
}
```

TenantId scopes the business fact but never becomes merchant actor authority.

## 6. Refund choreography

```text
RefundApproved
   ├── Inventory -> StockReturned
   ├── Payments -> provider refund/reconcile -> PaymentRefunded
   └── Accounting -> revenue compensation

StockReturned   -> Accounting COGS/inventory reversal
PaymentRefunded -> Accounting Deposits/Cash settlement
```

No global `RefundCompleted` authority is created.

## 7. Order workflow

ADR-010 remains:

```text
OrderPlaced
 -> Inventory reserve
 -> Payments capture
    -> Captured: Sales Confirm + Allocate
    -> definitive decline/no-commit: AwaitingPaymentRetry
    -> OutcomeUnknown: Payments reconciliation/wait
                       -> NeedsAttention if bounded automation ends unresolved
```

Technical timeout/retry exhaustion never becomes business failure, cancellation, or stock release.

Preferred LocalStack mapping is Step Functions where supported. If the emulator cannot reproduce a needed behavior, record the gap and test the application/workflow contract at the nearest reliable layer.

## 8. Queue policy

Every durable work/consumer queue defines producer/consumer/message contract, visibility vs bounded handler duration, retry/redrive, DLQ handling, replay identity, poison-message recovery, and safe diagnostics.

FIFO is used only when ordering/dedup requirements cannot be met safely by source identity and the task/ADR justifies it.

## 9. Fact-routing policy

Use EventBridge-style routing only for named versioned business facts and named consumers.

Do not use it for in-process commands/queries, generic CRUD/database changes, one known worker where a queue is enough, or empty diagram-driven infrastructure.

## 10. Durable-workflow policy

Any new Step Functions-style workflow requires:

- approved business sequence and transition owners;
- demonstrated durable wait/branch/retry/compensation need;
- execution identity/duplicate-start rule;
- explicit timeout/Unknown semantics;
- operator recovery;
- ADR when material.

## 11. Capability mapping

| Capability | Preferred LocalStack mapping | Status rule |
|---|---|---|
| HTTP/serverless delivery | API Gateway + Lambda | add only with named delivery need |
| identity edge | Cognito where supported | test adapter allowed behind same project contract |
| module persistence | DynamoDB | one owner/module; ADR-005 |
| work queue/DLQ | SQS | named worker/consumer only |
| fact routing | EventBridge | named producer/consumer only |
| change feed | DynamoDB Streams | named reliable relay only |
| durable workflow | Step Functions | ADR-approved workflow only |
| object storage | S3 | FilesMedia use case only when Ready |
| logs/metrics | CloudWatch-style APIs | operational evidence only |

Service names describe the learning mapping, not a requirement for AWS-hosted execution.

## 12. LocalStack limitation rule

Each infrastructure-sensitive task must state whether its required LocalStack behavior is supported sufficiently.

If not:

1. document the exact limitation;
2. preserve the capability-first project contract;
3. test at the nearest reliable layer;
4. do not introduce a domain workaround for the emulator;
5. do not silently claim AWS equivalence;
6. do not fall back to real AWS unless ADR-012 is explicitly superseded.
