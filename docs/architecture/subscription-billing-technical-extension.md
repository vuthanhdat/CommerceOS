# CommerceOS — Subscription & Billing Technical Architecture Extension

_Originally reconciled by TASK-0092 and finalized on 2026-08-10 after resolved `PD-044`._

## 1. Authority and scope

This document extends the canonical [technical baseline](technical-baseline.md) with focused Subscription & Billing implementation architecture.

Business meaning comes from:

- `docs/domains/subscription-billing.md`;
- `docs/domains/tenant-identity.md`;
- `docs/domains/product-decisions.md`;
- `docs/02-business-domains.md`.

Technical authority is primarily:

- [ADR-008](../adr/ADR-008-subscription-billing-module-entitlement-and-provider-boundary.md);
- [ADR-009](../adr/ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md);
- [ADR-005](../adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md);
- [ADR-006](../adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md).

`PD-043`–`PD-053`, including the initial commercial catalog in `PD-044`, are resolved for MVP. This document introduces no application code or AWS resources.

## 2. Module boundary

```text
CommerceOS.SubscriptionBilling.Domain
CommerceOS.SubscriptionBilling.Application
CommerceOS.SubscriptionBilling.Contracts
CommerceOS.SubscriptionBilling.Infrastructure
```

Hosted concepts:

```text
SubscriptionBilling
  ├── Plan / immutable PlanVersion
  ├── dedicated Trial terms version
  ├── Subscription
  ├── EntitlementSet
  ├── UsageMeter
  └── PlatformCharge
```

Rules:

- separate from Tenancy, Sales, Inventory, merchant-order Payments, Accounting, Reporting, and PDI;
- not a generic licensing/shared-policy helper;
- Domain has no AWS/framework/provider/persistence dependency;
- consumers use producer-owned contracts only;
- no foreign table access or cross-domain DynamoDB transaction;
- synchronous application surface initially shares `commerce-api`; split deployment only for named runtime/failure needs.

## 3. Approved MVP commercial catalog

| Terms | Price/month | MaxActiveMemberships | MaxWarehouses | ScheduledProductIngestion | OrderVolumeWarningThreshold |
|---|---:|---:|---:|---|---:|
| Trial | n/a | 3 | 1 | true | 500 |
| Starter | 199,000 VND | 3 | 1 | false | 500 |
| Growth | 499,000 VND | 10 | 3 | true | 2,000 |
| Business | 999,000 VND | 30 | 10 | true | 10,000 |

All paid Plans and Trial enable the approved core CommerceOS capabilities. Paid Plans differentiate by resource scale and scheduled automation.

Technical implications:

- Plan name is display/commercial identity, never runtime authorization authority;
- accepted PlanVersion and Trial terms are immutable;
- future price/limit changes create new versions;
- a version can be withdrawn from new sale without changing historical Subscriptions;
- Enterprise/custom pricing remains outside MVP.

## 4. Plan catalog storage and bootstrap

Plan/PlanVersion and Trial-terms versions are platform-global but SubscriptionBilling-owned records in the module DynamoDB table.

Use a **version-controlled seed artifact plus an idempotent SubscriptionBilling bootstrap/migration command**.

Bootstrap rules:

- initial seed contains exactly the approved Trial/Starter/Growth/Business values;
- equivalent repeated seed is `AlreadyApplied`;
- immutable version identity reused with different content is conflict;
- accepted terms are never TTL-deleted;
- runtime plan-selection/catalog query uses keyed/bounded SubscriptionBilling access, not application `Scan`;
- frontend or other modules never become commercial truth by duplicating constants.

Do not add AppConfig, SSM Parameter Store, a separate configuration database, Redis, or another service merely to host this small versioned catalog.

## 5. Entitlement authority

For a governed merchant mutation:

```text
Cognito identity
      ↓
Tenancy.ResolveTenantMutationAuthority
      ↓
TrustedTenantMutationContext
      ↓
owning-domain role policy
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

Approved initial keys:

```text
CoreCommerceCapabilities
MaxActiveMemberships
MaxWarehouses
ScheduledProductIngestion
OrderVolumeWarningThreshold
```

Decision provenance may expose:

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

Never authority:

- Plan name alone;
- JWT/client/browser values;
- frontend pricing constants;
- Reporting projections;
- copied limit fields in another module;
- provider-private state.

Missing entitlement does not mean enabled or Unlimited. No cross-request entitlement cache is authoritative initially.

## 6. Hard limit: MaxActiveMemberships

Merchant Access owns Membership identity/status and authoritative Active count. SubscriptionBilling owns the current limit.

Before Membership create/reactivate:

1. establish trusted merchant mutation authority;
2. evaluate `MaxActiveMemberships`;
3. Tenancy checks its current module-local Active Membership count/guard;
4. a Tenancy transaction updates Membership + authority/discovery + count/owner guards while ensuring resulting count does not exceed the limit.

All Active Owner/Admin/Staff/Viewer Memberships count.

SubscriptionBilling never reads Tenancy persistence and never auto-disables Members to fit a downgrade.

## 7. Hard limit: MaxWarehouses

Inventory owns Warehouse state/count. SubscriptionBilling owns the current limit.

Before Warehouse create/reactivate:

1. establish trusted merchant mutation authority;
2. evaluate `MaxWarehouses`;
3. Inventory conditionally updates Warehouse plus its module-local active-Warehouse count/guard;
4. reject when resulting count exceeds the limit.

No copied dashboard/Reporting count may authorize the write.

## 8. Scheduled product ingestion

Scheduled ingestion requires two independent authorities:

```text
ScheduledProductIngestion entitlement == true
AND
PDI source/platform/Tenant policy allows acquisition
```

PDI must re-evaluate entitlement:

- when a schedule is created/enabled; and
- again immediately before a scheduled dispatch/run is accepted.

This prevents an old schedule from continuing after downgrade/Ended.

Entitlement loss suppresses future scheduled execution; it does not delete schedule/history/source evidence. Source-policy denial still wins even on Growth/Business/Trial.

Current result:

- Trial: enabled;
- Starter: disabled;
- Growth: enabled;
- Business: enabled.

## 9. Order-volume warning meter

Approved source:

```text
Sales OrderConfirmed
  -> Sales outbox
  -> EventBridge
  -> SubscriptionBilling SQS/DLQ
  -> idempotent UsageMeter increment
```

Threshold is taken from the EntitlementSet governing that billing period:

- Trial/Starter: 500;
- Growth: 2,000;
- Business: 10,000.

Rules:

- one logical `OrderConfirmed` counts once;
- warning threshold never rejects shopper checkout;
- it never cancels an Order;
- it never automatically creates overage billing;
- refund/cancellation does not silently rewrite the historical confirmed-order count unless future product policy says so.

## 10. Trial onboarding

`StartTrialSubscription` is idempotent by stable onboarding/Tenant source identity.

It atomically creates/replays:

- Trial Subscription;
- dedicated immutable Trial terms provenance;
- Trial EntitlementSet:
  - all core capabilities enabled;
  - `MaxActiveMemberships=3`;
  - `MaxWarehouses=1`;
  - `ScheduledProductIngestion=true`;
  - `OrderVolumeWarningThreshold=500`;
- immutable source/history claim.

ADR-009 owns cross-domain completion/recovery. Trial expiry does not auto-convert to Starter.

## 11. Upgrade

Approved flow:

```text
upgrade request
  -> persist plan-change + PlatformCharge identity
  -> execute dedicated SaaS provider charge
  -> Declined/NoCommit: current entitlement remains
  -> OutcomeUnknown: reconcile; current entitlement remains
  -> verified success
  -> atomically accept new paid period + immutable EntitlementSet
  -> fresh monthly period starts
```

No proration/credit machinery exists in MVP. Provider callback receipt alone does not grant higher entitlements.

## 12. Restrictive downgrade consistency

Downgrade is for the next renewal boundary.

Technical protocol:

1. SubscriptionBilling persists one durable downgrade transition + target PlanVersion.
2. Every affected hard-limit owner exposes producer-owned current-usage assessment/fence contracts.
3. Owner-local writes use a constraint/fence revision while lower limits are being finalized so a concurrent old-limit write cannot slip through.
4. Excess usage leaves downgrade `BlockedByUsage/RemediationRequired`; current plan continues.
5. Remediation occurs only via normal owner commands and preserves owner invariants.
6. Lower EntitlementSet becomes effective only after all required owner assessments/fences prove safety.
7. Duplicate/recovery is idempotent; elapsed time never means completion.

No cross-domain ACID and no destructive auto-remediation.

## 13. Renewal, PastDue, Ended, reactivation

- definitive renewal-charge failure may create PastDue and the approved seven-day grace;
- `OutcomeUnknown` does not create PastDue;
- after grace expires without successful renewal, Subscription becomes Ended;
- Ended removes ordinary operational entitlements, scheduled automation, and public commerce;
- Tenant/Membership remain intact;
- approved authenticated read/history/export/recovery remains available;
- reactivation creates new accepted period/terms/history rather than rewriting old history.

Timers/Scheduler invocations request evaluation of current persisted state; timer firing is never itself business proof.

## 14. PlatformCharge and provider boundary

`PlatformCharge` remains a separate SubscriptionBilling aggregate containing:

- charge identity;
- Tenant/Subscription/period/terms reference;
- VND whole-đồng amount;
- provider operation/evidence reference;
- verified success/definitive no-commit/decline/OutcomeUnknown;
- reconciliation traceability.

Dedicated seam:

```text
SubscriptionBilling.Application
      │ IPlatformBillingPort
      ▼
SubscriptionBilling.Infrastructure
      ▼
Mock SaaS Billing Provider
```

The mock SaaS provider is a separate external-like application from merchant-order Mock Payment Provider.

It must simulate success, definitive no-commit/decline, Unknown, duplicates, idempotency, reconciliation/query, and out-of-order evidence.

No real card/bank data or provider secrets belong in domain state/logs/fixtures.

## 15. Platform-admin support

MVP platform-admin Subscription/Billing surface is read-only.

A separate platform support context may query safe current/history state through SubscriptionBilling application contracts. It cannot:

- change PlanVersion/EntitlementSet;
- comp/assign plans;
- override provider/charge result;
- bypass hard limits;
- cancel/reactivate subscriptions;
- directly access DynamoDB.

Future mutation authority requires explicit product/Audit architecture work.

## 16. Persistence access patterns

One SubscriptionBilling DynamoDB table when Ready.

| ID | Use case | Protection |
|---|---|---|
| `SUB-AP-00` | bootstrap/query sellable PlanVersion + Trial terms | platform-scoped keys; immutable versions; idempotent seed; no runtime Scan |
| `SUB-AP-01R` | current Subscription | strong/current tenant read; expected revision |
| `SUB-AP-02R` | current Entitlement | coherent Subscription + EntitlementSet provenance; no GSI authority |
| `SUB-AP-03` | history | bounded immutable tenant history; eventual display allowed |
| `SUB-AP-04R` | Trial/upgrade/downgrade/renew/reactivate | expected revision + idempotency + immutable accepted history |
| `SUB-AP-05R` | UsageMeter | tenant + billing period + `OrderConfirmed` source idempotency |
| `SUB-AP-06R` | PlatformCharge | separate aggregate/revision + stable logical charge identity |
| `SUB-AP-07R` | provider evidence | verify/dedupe/non-regression |
| `SUB-AP-08R` | due reconciliation | sparse bounded/sharded operational index only when required |
| `SUB-AP-09R` | platform support query | application query; separate context; read-only |
| `SUB-AP-10` | Trial source | one logical Trial per stable onboarding source |
| `SUB-AP-11` | restrictive downgrade | durable transition + owner assessment/fence acknowledgements |

Tenancy and Inventory own their own resource-count guard records. These counts do not move into SubscriptionBilling.

## 17. Integration matrix

| Caller/producer | Consumer | Mechanism | Truth |
|---|---|---|---|
| onboarding coordinator | SubscriptionBilling | synchronous + ADR-009 SQS recovery | exactly one Trial per source |
| protected owner module | SubscriptionBilling | synchronous `EvaluateEntitlement` | current commercial authority |
| SubscriptionBilling downgrade | Tenancy/Inventory/etc. | synchronous owner assessment/fence | owner current usage, no foreign storage |
| PDI schedule/dispatch | SubscriptionBilling | synchronous entitlement query | current scheduled-ingestion capability |
| Sales `OrderConfirmed` | UsageMeter | EventBridge -> SQS/DLQ | idempotent warning count |
| SubscriptionBilling facts | Reporting | EventBridge when named | display only |
| privileged SubscriptionBilling action | Audit | durable source intent -> append | audit evidence |
| SubscriptionBilling | Mock SaaS provider | provider adapter call/query | external-like evidence |
| Mock SaaS provider | SubscriptionBilling | authenticated callback/query | evidence until accepted |
| platform support | SubscriptionBilling | synchronous read-only query | no override |

No generic Subscription CRUD event is published.

## 18. AWS/CDK mapping

| Need | AWS mapping | Status |
|---|---|---|
| synchronous surface | existing `commerce-api` Lambda | conditional with Ready use case |
| module state/catalog | SubscriptionBilling DynamoDB table | conditional with persistence task |
| Trial recovery | Tenancy work-outbox/Stream -> SQS + worker | ADR-009 |
| order meter | EventBridge -> SubscriptionBilling SQS/DLQ + Lambda | conditional with meter task |
| Mock SaaS provider | separate API Gateway/Lambda/DynamoDB app | conditional with provider task |
| provider callback | API Gateway/Lambda | conditional |
| reconciliation/renewal schedule | EventBridge Scheduler | only if named due-work polling is required |
| reliable facts | outbox/Stream -> EventBridge -> consumer queue | named consumer only |
| observability | CloudWatch built-ins/logs/alarms | bounded retention |

No AppConfig/SSM Plan catalog authority, NAT Gateway, ALB, EC2, RDS/Aurora, Redis, OpenSearch, MSK/Kafka, EKS, always-on service, or provisioned Lambda concurrency is introduced.

## 19. Verification

Dependent implementation must verify:

- catalog seed exactly matches approved Trial/Starter/Growth/Business values and is idempotent;
- accepted PlanVersion/Trial terms cannot be edited;
- withdrawn versions remain historical but cannot be newly accepted;
- Trial does not alias/auto-convert to Starter;
- no consuming module branches on Plan name or reads SubscriptionBilling table;
- every Active role counts toward `MaxActiveMemberships`;
- Membership/Warehouse concurrent creation cannot bypass hard limits;
- Starter scheduled ingestion is denied; Trial/Growth/Business still require PDI source policy;
- dispatch rechecks scheduled-ingestion entitlement;
- `OrderConfirmed` replay counts once and warning does not block/charge;
- downgrade below current usage remains blocked without destructive remediation;
- provider Unknown is preserved/reconciled;
- no extra configuration service is introduced solely for plan values.

## 20. Stop condition

**SUBSCRIPTION & BILLING TECHNICAL BASELINE READY.**
