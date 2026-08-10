# ADR-008 — Subscription & Billing Module, Entitlement Decision, and Provider Boundary

Status: Accepted
Date: 2026-08-10
Last reconciled: 2026-08-10 after product-decision propagation
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS has a distinct Subscription & Billing bounded context that owns the merchant Tenant's commercial relationship with CommerceOS:

- stable Plan identity and immutable accepted PlanVersion terms;
- Trial/paid Subscription lifecycle;
- immutable effective EntitlementSets;
- approved UsageMeters;
- PlatformCharge obligations/evidence;
- interpretation of the dedicated simulated SaaS billing provider.

It remains separate from merchant-order Payments, Tenancy, Sales, Inventory, Accounting, and Reporting.

The original ADR was accepted before `PD-043`–`PD-053` were resolved. The product/domain reconciliation now closes those broad gates except the exact sellable `Starter`/`Growth`/`Business` prices and entitlement/limit packages under `PD-044`.

Approved business policy now includes:

- automatic 30-day no-card Trial after merchant registration;
- monthly paid periods with explicit anchor/month-end behavior;
- VND whole-đồng SaaS Money, no conversion/tax/statutory-invoice/proration machinery in MVP;
- upgrade effectivity only after verified successful PlatformCharge and a fresh monthly period;
- downgrade at renewal, blocked by authoritative excess hard-limit usage with no destructive remediation;
- definitive renewal failure -> PastDue with seven-day grace; `OutcomeUnknown` does not;
- Ended disables ordinary merchant mutations, scheduled automation, and public commerce while preserving approved authenticated read/history/export/recovery access and data;
- distinct hard capability, hard counted-resource, and warning-only order-volume enforcement categories;
- dedicated simulated SaaS billing provider separate from merchant-order provider;
- platform-admin Subscription/Billing support is read-only in MVP.

The architecture must encode these semantics without moving entitlement authority into JWTs/Tenancy/UI caches or letting SubscriptionBilling mutate foreign aggregates.

## Decision

### 1. One `SubscriptionBilling` implementation module initially

```text
CommerceOS.SubscriptionBilling.Domain
CommerceOS.SubscriptionBilling.Application
CommerceOS.SubscriptionBilling.Contracts   # only with a real consumer/delivery need
CommerceOS.SubscriptionBilling.Infrastructure
```

Hosted concepts:

```text
SubscriptionBilling
  ├── Plan / immutable PlanVersion terms
  ├── Subscription
  ├── EntitlementSet
  ├── UsageMeter
  └── PlatformCharge
```

Rules:

- not a generic licensing/shared-policy helper;
- separate from merchant-order Payments and Tenancy;
- Domain has no AWS/HTTP/provider/persistence dependency;
- foreign modules consume producer-owned application/Contracts boundaries only;
- no foreign table access/cross-domain DynamoDB transaction;
- no empty project/resource before a Ready task needs it.

### 2. Initial deployment remains shared `commerce-api`

Synchronous SubscriptionBilling application contracts initially run inside the shared commerce Lambda.

Separate worker/provider-ingress Lambdas are allowed for named async/provider use cases. The bounded-context boundary alone does not justify a separate network service.

### 3. SubscriptionBilling is the sole entitlement authority

For a subscription-governed protected command:

```text
AuthenticatedPrincipal
      ↓
Tenancy.ResolveTenantAuthority
      ↓
TrustedTenantContext
      ↓
domain role policy
      ↓
SubscriptionBilling.EvaluateEntitlement
      ↓
owner-authoritative usage/state
      ↓
owner invariant + commit
```

Conceptual contract:

```text
EvaluateEntitlement(
  TrustedTenantScope,
  EntitlementKey,
  DecisionMetadata)
    -> EffectiveEntitlementDecision | Failure
```

Decision may expose current meaning/provenance such as:

```text
tenantId
entitlementKey
value
entitlementSetId
subscriptionId
sourceTermsId
effectiveFrom
effectiveUntil?
decisionRevision
```

Never entitlement authority:

- client-supplied TenantId/plan/entitlement/limit;
- JWT custom claims;
- browser/session cache;
- Reporting/platform-admin projections;
- provider-private state;
- copied plan labels in another module.

No cross-request entitlement cache is authoritative initially.

### 4. Hard-limit enforcement combines two owners' truths

For a hard counted-resource limit:

1. SubscriptionBilling supplies current trusted entitlement/limit;
2. owning domain supplies current authoritative count/state;
3. owning domain conditionally protects its own write.

SubscriptionBilling never reads Tenancy/Inventory persistence or disables/deletes foreign resources to force compliance. Stale Reporting/UI counts cannot authorize writes.

### 5. Restrictive downgrade uses owner assessment/fencing

Approved downgrade is for next renewal and blocks when authoritative current usage exceeds a target hard limit.

Safe protocol:

- SubscriptionBilling persists one durable downgrade transition identity/target terms;
- each affected hard-limit owner exposes a producer-owned current-usage assessment contract;
- affected owner commands use an owner-local constraint/fence revision while lower limits are being finalized so a concurrent old-limit write cannot slip through;
- owner alone writes owner state;
- duplicate assessment/finalization is idempotent;
- excess usage -> `BlockedByUsage/RemediationRequired`; current plan continues under approved policy;
- remediation occurs only through normal owner commands and preserves invariants;
- lower EntitlementSet becomes effective only after required owner assessments/fences prove safety;
- interruption remains durable/reconcilable and elapsed time never means completion.

No distributed ACID or destructive automatic remediation.

### 6. Automatic Trial uses ADR-009

`StartTrialSubscription` is an idempotent SubscriptionBilling command invoked by the onboarding coordinator with stable Tenant/onboarding source identity.

SubscriptionBilling atomically creates one:

- 30-day Trial Subscription;
- Trial EntitlementSet;
- immutable source/provenance history.

Completed onboarding is not reported until Trial acceptance is proven. Tenancy never stores copied Subscription authority.

### 7. Paid periods are explicit

Paid Subscriptions are monthly in MVP. Accepted period boundaries/anchor are persisted explicitly, including approved month-end behavior.

Trial remains a separate fixed 30-day period. Billing time semantics are not derived from server timezone/provider timestamps.

### 8. PlatformCharge is a separate aggregate

PlatformCharge owns:

- stable charge identity;
- Tenant/Subscription/period/terms reference;
- VND whole-đồng Money amount;
- simulated provider operation/evidence references;
- known success/definitive no-commit-or-decline/Unknown outcome;
- traceability to supported Subscription transition/renewal.

It is neither merchant-order Payment nor merchant Accounting Journal.

### 9. Upgrade effectivity follows verified charge success

Approved upgrade flow:

1. persist stable plan-change/PlatformCharge operation identity;
2. execute required PlatformCharge through provider-neutral billing port;
3. Declined/NoCommit/Unknown does not make higher EntitlementSet effective;
4. after verified successful charge, atomically accept new Subscription period/terms + immutable EntitlementSet inside SubscriptionBilling;
5. new paid period starts at approved boundary with no proration/credit machinery.

Callback receipt alone is evidence, not entitlement effectivity.

### 10. Renewal/PastDue/Ended remain separate from Tenant/Membership

- definitive renewal-charge failure may create PastDue + approved seven-day grace;
- `OutcomeUnknown` does not create PastDue;
- after grace expiry without successful renewal, Subscription becomes Ended and operational entitlements end;
- Tenant/Membership remain intact;
- approved authenticated read/history/export/recovery paths remain available;
- reactivation creates new accepted period/history rather than rewriting old history.

### 11. Order-volume UsageMeter is warning-only

Approved source is `OrderConfirmed` from Sales.

- source application idempotent by event/source identity;
- meter window is current Subscription billing period;
- threshold crossing creates warning/visibility only;
- never rejects shopper checkout;
- never cancels Order;
- never automatically charges overage.

This is a named ADR-006 consumer and may use Sales outbox -> EventBridge -> SubscriptionBilling SQS/DLQ when implemented.

### 12. Dedicated simulated SaaS provider

Application seam:

```text
SubscriptionBilling.Application
      │ IPlatformBillingPort
      ▼
SubscriptionBilling.Infrastructure
      │ provider adapter
      ▼
Mock SaaS Billing Provider (external-like app)
```

This provider is separate from merchant-order Mock Payment Provider and may be deployed separately when its Ready task is introduced.

It must support deterministic:

- success;
- definitive decline/no-commit;
- timeout/Unknown;
- duplicate delivery;
- idempotency/retry;
- query/reconciliation;
- out-of-order evidence.

Rules:

- stable logical PlatformCharge/provider idempotency identity;
- timeout/network/missing callback never proves no commit;
- callback/query evidence authenticated/verified before definitive interpretation;
- duplicate/older evidence cannot duplicate/regress accepted state;
- unsafe retry waits for reconciliation while Unknown;
- provider IDs stay SubscriptionBilling evidence only;
- no real card/CVV/bank secrets or prohibited credentials in Domain/logs/fixtures.

A real billing provider later requires new product/security/compliance/architecture decisions behind this port.

### 13. Platform-admin is read-only support

Separate admin context/application queries may expose safe support visibility/history but cannot:

- comp/assign plan;
- override plan/entitlement;
- override charge outcome;
- cancel/reactivate;
- bypass limits;
- directly access table persistence.

Future mutation requires explicit product/Audit decision.

### 14. Persistence ownership

SubscriptionBilling owns one DynamoDB table when Ready.

Approved logical access needs:

| Access | Protection |
|---|---|
| Plan/PlanVersion | platform-scoped inside module; accepted versions immutable; exact package values `PD-044` gated |
| current Subscription | strong/current Tenant read; expected revision |
| EntitlementSet history/current reference | coherent with Subscription transitions |
| Trial source claim | idempotent by onboarding/Tenant source identity |
| plan-change transition | expected revision + idempotency + durable status |
| UsageMeter | Tenant + meter/window; source-idempotent increment |
| PlatformCharge | separate aggregate/revision + stable logical charge identity |
| provider evidence/inbox | verified dedup + non-regression |
| due reconciliation | sparse bounded/sharded operational index only when provider path needs it; never Tenant authority |
| integration outbox | atomic with source fact for named consumer |
| platform-admin read | application query only; read-only separate context |

No application Scan, foreign table access, or GSI as entitlement authority.

### 15. Async integration

Use ADR-006 only for named consumers:

- `OrderConfirmed` -> UsageMeter;
- SubscriptionBilling facts -> Reporting when a defined projection exists;
- approved privileged SubscriptionBilling action/evidence -> Audit;
- provider callback/reconciliation work -> dedicated handler/queue as required.

Do not publish generic Subscription/Plan CRUD events.

### 16. AWS/deployment mapping

When corresponding tasks become Ready:

| Need | AWS mapping |
|---|---|
| synchronous SubscriptionBilling | existing `commerce-api` Lambda |
| module state | SubscriptionBilling DynamoDB table |
| Trial recovery | ADR-009 Tenancy work-outbox/Stream -> SQS worker calling SubscriptionBilling |
| order meter | EventBridge -> SubscriptionBilling SQS/DLQ + worker |
| Mock SaaS provider | separate API Gateway/Lambda/DynamoDB app/stack |
| provider callback ingress | API Gateway/Lambda handler |
| provider reconciliation | bounded worker; EventBridge Scheduler only if periodic due-work inquiry is required |
| reliable Subscription facts | module outbox/Stream -> EventBridge -> consumer queue |
| observability | CloudWatch built-ins/alarms/logs with bounded retention |

No Step Functions is selected merely for ordinary Subscription plan/entitlement CRUD. A later complex billing workflow requires a focused ADR.

No NAT Gateway, ALB, EC2, RDS/Aurora, Redis/ElastiCache, OpenSearch, MSK/Kafka, EKS, always-on service, or provisioned Lambda concurrency.

## Alternatives considered

### Put subscription/plan claims in Tenancy or `TrustedTenantContext`

Rejected because it creates stale duplicate commercial authority.

### Let every consuming module copy current plan/limit state and decide locally

Rejected as authority because revocation/restrictive transitions can race/stale. Display projections remain allowed but non-authoritative.

### Reuse merchant-order Payments/Mock Provider

Rejected because shopper-to-merchant and merchant-to-CommerceOS money flows have different ownership/provider semantics.

### Separate SubscriptionBilling microservice now

Rejected because no measured IAM/runtime/scale pressure justifies network separation. The simulated provider remains external-like by design.

### Cross-domain transaction for hard-limit downgrade

Rejected because resource owners retain state/invariants; use explicit owner assessment/fencing.

## Consequences

### Positive

- one authoritative Subscription/entitlement boundary;
- approved Trial/upgrade/downgrade/renewal/Ended/meter behavior can flow into implementation tasks;
- hard-limit writes remain owner-local/concurrency-safe;
- SaaS provider uncertainty isolated from merchant-order Payments;
- provider simulation teaches external-system behavior without real money/card data;
- no always-on infrastructure required.

### Negative / trade-offs

- governed writes add synchronous entitlement dependency/fail-closed behavior;
- restrictive downgrade finalization requires owner participation/fencing;
- provider simulation adds distinct app/stack/reconciliation surface;
- admin support uses explicit queries rather than direct storage;
- exact package values remain blocked under `PD-044`.

## Security and tenant impact

- merchant operations derive Tenant scope only from trusted execution context;
- known Subscription/PlatformCharge/provider IDs cannot cross Tenant boundaries;
- provider callback authentication is separate from merchant authentication and resolves to known module-owned charge/Tenant evidence;
- secrets/raw provider payloads minimized/redacted and never broadly emitted;
- platform-admin uses separate explicit read-only context.

## Reliability and operability impact

- Trial, plan change, UsageMeter source, PlatformCharge, and provider evidence use stable logical identities;
- equivalent replay returns prior logical result; incompatible reuse conflicts;
- Unknown remains Unknown until verified evidence;
- out-of-order evidence cannot regress later known state;
- downgrade transition keeps durable per-owner progress and never finalizes from elapsed time;
- async consumers follow at-least-once/idempotent ADR-006 rules;
- observe entitlement failures, Trial recovery, transition age, PlatformCharge Unknown age, callback verification/dedup, reconciliation, queue/DLQ, and outbox lag without treating telemetry as business truth.

## Cost impact

This ADR update deploys nothing and changes runtime cost by zero.

Future conditional cost is limited to:

- one low-volume module DynamoDB table;
- separate small Mock SaaS provider API/Lambda/DynamoDB when introduced;
- queues/workers/EventBridge for named meter/Audit/Reporting integrations;
- optional bounded Scheduler reconciliation if needed.

Every Ready task includes table/index/worker/provider/schedule request/storage/log assumptions. No paid real billing provider is selected.

## Reversibility / migration

- splitting SubscriptionBilling into a separate service later preserves producer-owned contracts/table ownership and requires network/IAM/latency migration planning;
- changing DynamoDB persistence requires data migration while retaining application/business contracts;
- replacing the simulated provider or adding a real provider changes Infrastructure adapter/provider-reference/reconciliation mapping, not Domain ownership;
- entitlement caching later requires explicit staleness/revocation/restrictive-transition design;
- Plan/PlanVersion schema evolution must preserve immutable accepted history and explicit migration/versioning;
- pending downgrade/charge operations require identity-preserving resume/cutover if orchestration implementation changes.

## Validation

Dependent implementation must prove:

- no foreign Domain/Infrastructure/table dependency;
- Tenant A cannot read/evaluate/mutate Tenant B subscription/charge by known IDs/provider refs/cursors;
- client/JWT plan/entitlement cannot authorize a command;
- automatic Trial duplicate-safe and onboarding cannot report complete before Trial acceptance;
- hard-limit owner write + downgrade fence cannot falsely finalize lower limit during concurrent old-limit write;
- upgrade cannot grant higher entitlement before verified successful charge;
- definitive renewal failure and Unknown remain distinct;
- Ended access does not require rewriting Tenant/Membership;
- `OrderConfirmed` replay increments meter once and never blocks checkout;
- provider timeout/duplicate/out-of-order evidence preserves Unknown/idempotency/non-regression;
- merchant-order Payments cannot receive/reuse SaaS provider state;
- platform-admin has no mutation/direct-table path;
- CDK creates no speculative queue/event/schedule/provider resource.

## Remaining product gate

Architecture does not define exact `Starter`/`Growth`/`Business` prices or entitlement/limit package values. That exact commercial catalog remains `PD-044` gated.

Structural Plan/PlanVersion history and enforcement mechanisms are approved; Builders must not invent package values merely because the technical seams exist.

## References

- `docs/domains/subscription-billing.md`
- `docs/domains/product-decisions.md`
- `docs/architecture/subscription-billing-technical-extension.md`
- `docs/architecture/product-decision-technical-reconciliation.md`
- ADR-004 — trusted Tenant authority
- ADR-005 — DynamoDB module ownership/access patterns
- ADR-006 — reliable integration
- ADR-007 — HTTP/idempotency conventions
- ADR-009 — onboarding completion/recovery