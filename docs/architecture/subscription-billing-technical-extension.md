# CommerceOS — Subscription & Billing Technical Architecture Extension

_Originally reconciled by TASK-0092 on 2026-08-10 and refreshed after the 2026-08-10 product/domain decision pass._

## 1. Authority and scope

This document extends the canonical [technical baseline](technical-baseline.md) with focused Subscription & Billing implementation architecture.

Business meaning comes from:

- `docs/domains/subscription-billing.md`;
- `docs/domains/product-decisions.md`;
- `docs/02-business-domains.md`.

Technical authority is primarily:

- [ADR-008](../adr/ADR-008-subscription-billing-module-entitlement-and-provider-boundary.md);
- [ADR-009](../adr/ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md);
- [ADR-005](../adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md);
- [ADR-006](../adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md).

The broad `PD-043`–`PD-053` gates that existed when TASK-0092 was written are now resolved by the product/domain reconciliation. Only the exact sellable `Starter`/`Growth`/`Business` prices and entitlement/limit packages remain deferred under the exact commercial portion of `PD-044`.

This document introduces no application code or AWS resources.

## 2. Module boundary

```text
CommerceOS.SubscriptionBilling.Domain
CommerceOS.SubscriptionBilling.Application
CommerceOS.SubscriptionBilling.Contracts    # only with a real consumer/delivery need
CommerceOS.SubscriptionBilling.Infrastructure
```

Hosted business concepts:

```text
SubscriptionBilling
  ├── Plan / immutable accepted PlanVersion terms
  ├── Subscription
  ├── EntitlementSet
  ├── UsageMeter
  └── PlatformCharge
```

Rules:

- separate from Tenancy, merchant-order Payments, Sales, Inventory, Accounting, Reporting;
- not a generic license/shared policy library;
- Domain has no AWS/framework/provider/persistence dependency;
- consumers use producer-owned contracts only;
- no foreign table access or cross-domain DynamoDB transaction;
- initially runs synchronously in shared `commerce-api`; deployment split only for measured/named pressure.

## 3. Current trust chain

For a governed merchant mutation:

```text
Cognito identity evidence
      ↓
Tenancy.ResolveTenantAuthority
      ↓
TrustedTenantContext
      ↓
owning domain role policy
      ↓
SubscriptionBilling.EvaluateEntitlement
      ↓
owner-authoritative usage/state
      ↓
owner invariant + commit
```

SubscriptionBilling never trusts:

- client/JWT/browser plan/entitlement/limit;
- Reporting/UI projections;
- copied plan names in another module;
- provider-private state.

`TrustedTenantContext` is not expanded with cached commercial state.

## 4. Entitlement decision contract

Conceptual contract:

```text
EvaluateEntitlement(
  TrustedTenantScope,
  EntitlementKey,
  DecisionMetadata)
    -> EffectiveEntitlementDecision | Failure
```

Decision fields may include:

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

Current decision categories:

1. **Hard capability** — operation not available when not entitled.
2. **Hard counted-resource growth/activation limit** — current owner-authoritative count plus proposed change cannot exceed current trusted limit.
3. **Soft/warning order-volume meter** — observation/warning only; never blocks shopper checkout or automatically charges overage.

Exact keys/values per sellable package remain `PD-044` gated.

## 5. Hard-limit owner coordination

SubscriptionBilling supplies limit meaning; the resource owner supplies current count/state.

Examples:

- Merchant Access owns Active Membership count + last-owner invariant;
- Inventory owns Warehouse/Location count + stock invariants;
- Product Data Ingestion owns source/run policy in addition to subscription capability.

A hard-limit write is rejected by the owner when the current trusted entitlement plus current owner state disallow growth.

No copied Reporting count authorizes a write.

## 6. Restrictive downgrade consistency

Approved MVP downgrade behavior:

- scheduled for next renewal boundary;
- revalidate current hard-limit usage against target package;
- if usage exceeds target, downgrade is `BlockedByUsage/RemediationRequired` and current plan continues;
- no automatic destructive remediation.

Technical protocol:

1. SubscriptionBilling persists one durable downgrade transition identity/target terms.
2. Each affected hard-limit owner exposes a producer-owned assessment contract.
3. Owner writes use an owner-local constraint/fence revision while a restrictive transition is being finalized so a concurrent write cannot slip through under the old higher limit.
4. Owner alone reads/writes owner resource state.
5. Duplicate assessment/finalization is idempotent.
6. Too-high usage blocks effectivity; remediation uses normal owner commands and still preserves invariants.
7. SubscriptionBilling finalizes lower EntitlementSet only after required owner assessments/fences prove the transition safe.
8. Interrupted transition is durable/reconcilable; elapsed time never means completion.

No distributed ACID and no foreign resource mutation.

## 7. Onboarding Trial

Successful merchant onboarding requires a 30-day no-card Trial.

Producer-owned command:

```text
StartTrialSubscription(
  tenantId,
  onboardingOperationId,
  sourceIdentity,
  correlationMetadata)
    -> TrialAccepted | AlreadyApplied | Failure
```

SubscriptionBilling atomically creates/replays:

- Trial Subscription;
- Trial EntitlementSet;
- immutable provenance/history/source claim.

ADR-009 owns cross-domain completion:

- Tenancy commits Tenant/Owner + durable onboarding operation/work intent;
- synchronous Trial call is the fast path;
- completed onboarding is returned only after Trial acceptance;
- interruption uses SQS recovery with same idempotent source identity;
- no cross-module transaction/rollback.

## 8. Paid period and time semantics

MVP paid Subscription periods are monthly.

Persistence records accepted period boundaries/anchor explicitly, including approved month-end behavior. Do not derive billing periods from server timezone or provider timestamps.

Trial is a separate fixed 30-day period.

Tenant business timezone remains relevant to merchant reporting/accounting semantics; Subscription billing period semantics follow the approved Subscription domain and are not silently substituted with Tenant reporting-day logic.

## 9. PlatformCharge

`PlatformCharge` is a separate aggregate from Subscription.

It owns:

- charge identity;
- Tenant/Subscription/period/terms reference;
- VND whole-đồng Money amount;
- provider operation/evidence references;
- known success/definitive decline-or-no-commit/Unknown outcome;
- attempt/reconciliation traceability.

It is not merchant-order Payments and not merchant Accounting.

Equivalent logical charge retry has one effect; incompatible identity reuse conflicts.

## 10. Upgrade

Approved technical flow:

```text
upgrade request
  -> persist stable plan-change/PlatformCharge operation
  -> execute dedicated SaaS provider charge
  -> Declined/NoCommit: no higher entitlement effect
  -> Unknown: reconcile; no higher entitlement effect
  -> verified success
  -> accept new Subscription period + immutable EntitlementSet
  -> fresh monthly period starts; no proration/credit machinery
```

Provider callback receipt alone does not grant higher entitlements.

## 11. Renewal, PastDue, Ended, reactivation

- definitive renewal-charge failure may move Subscription to PastDue and begins the approved seven-day grace period;
- `OutcomeUnknown` does **not** create PastDue;
- successful reconciliation/charge during grace follows the approved renewal path;
- after grace expiry without successful renewal, Subscription becomes Ended and ordinary operational entitlements cease;
- Ended does not delete/disable Tenant or Membership;
- approved authenticated read/history/export/recovery paths remain available;
- reactivation creates new accepted terms/period/history and never rewrites old ended history.

Technical scheduling/time checks must use explicit persisted period/grace boundaries and idempotent processing. A timer firing is a request to evaluate current state, not proof that the business transition is valid.

## 12. Order-volume meter

Approved meter source:

```text
Sales OrderConfirmed
  -> Sales outbox
  -> EventBridge
  -> SubscriptionBilling SQS/DLQ
  -> idempotent UsageMeter increment for current billing period
```

Rules:

- one logical OrderConfirmed counts once;
- correction/cancellation/refund does not silently rewrite the original confirmation count unless future product policy explicitly changes meter semantics;
- threshold/warning state is operational/commercial visibility only;
- never reject shopper checkout or create automatic overage charge.

## 13. Dedicated simulated SaaS billing provider

Architecture seam:

```text
SubscriptionBilling.Application
      │ IPlatformBillingPort
      ▼
SubscriptionBilling.Infrastructure
      │ provider adapter/protocol mapping
      ▼
Mock SaaS Billing Provider
```

The mock SaaS provider is a separate external-like application from the merchant-order Mock Payment Provider.

Required simulation capabilities:

- deterministic success;
- deterministic definitive decline/no-commit;
- timeout/Unknown;
- duplicate callback/delivery;
- provider-supported idempotency;
- query/reconciliation;
- out-of-order evidence.

Rules:

- stable PlatformCharge operation identity before unsafe external retry;
- timeout/missing callback/network failure remains Unknown when commit cannot be proven;
- callback/query evidence authenticated/verified before definitive interpretation;
- duplicates replay prior interpretation; older evidence cannot regress later known state;
- unsafe retry waits for reconciliation when Unknown;
- provider IDs/records remain SubscriptionBilling evidence only;
- no real card/CVV/bank/provider secrets in domain state/logs/fixtures.

A real provider later requires a new product/security/compliance/architecture decision behind this port.

## 14. Platform-admin support

MVP platform-admin Subscription/Billing surface is **read-only**.

Separate admin context/application queries may show safe support state/history. They may not:

- mutate plan/terms/entitlements;
- comp a subscription;
- override charge outcome;
- cancel/reactivate on behalf of merchant;
- bypass hard limits;
- directly read/write DynamoDB.

Future mutation authority requires explicit product/Audit decisions.

## 15. Persistence ownership

One SubscriptionBilling DynamoDB table when persistence is Ready.

Approved access patterns:

| ID | Use case | Protection |
|---|---|---|
| `SUB-AP-01R` | current Subscription | strong/current tenant read; expected revision for mutation |
| `SUB-AP-02R` | current Entitlement | coherent Subscription + effective EntitlementSet provenance; no GSI authority |
| `SUB-AP-03` | history | bounded immutable tenant history; eventual permitted for display |
| `SUB-AP-04R` | Trial/upgrade/downgrade/renew/reactivate | expected revision + idempotency + immutable accepted history; intent != effectivity |
| `SUB-AP-05R` | order-volume UsageMeter | tenant + billing-period meter; OrderConfirmed source idempotency |
| `SUB-AP-06R` | PlatformCharge | separate aggregate/revision; VND whole-đồng amount; logical charge idempotency |
| `SUB-AP-07R` | provider evidence | verify/dedupe/non-regression |
| `SUB-AP-08R` | due reconciliation | sparse bounded/sharded operational index only when needed; no Scan/tenant authority |
| `SUB-AP-09R` | platform-admin support query | explicit app query, separate admin context, read-only |
| `SUB-AP-10` | Trial source claim | one Trial per stable onboarding/Tenant source |
| `SUB-AP-11` | restrictive downgrade operation | durable transition + per-owner assessment/fence acknowledgement |

Plan/PlanVersion can use a platform-scoped module-owned partition because Plan catalog is platform-global commercial truth. Accepted versions are immutable. Exact package records wait for `PD-044` values.

## 16. Integration matrix

| Caller/producer | Consumer | Mechanism | Truth |
|---|---|---|---|
| onboarding coordinator | SubscriptionBilling | synchronous command + ADR-009 SQS recovery | exactly one Trial per onboarding source |
| protected merchant owner | SubscriptionBilling | synchronous EvaluateEntitlement | current commercial authority |
| SubscriptionBilling downgrade | Merchant Access/Inventory/etc. | synchronous owner assessment/fence contracts | owner current usage/state; no foreign persistence |
| Sales OrderConfirmed | UsageMeter | EventBridge -> SQS/DLQ | idempotent warning count |
| SubscriptionBilling facts | Reporting | EventBridge projection when named | display only, never entitlement authority |
| privileged SubscriptionBilling action | Audit | durable audit intent -> reliable append | source/action evidence |
| SubscriptionBilling | Mock SaaS Provider | provider adapter call/query | external-like evidence, Unknown safe |
| Mock SaaS Provider | SubscriptionBilling | authenticated callback/query evidence | evidence only until accepted |
| platform admin | SubscriptionBilling | synchronous read-only query | no override/direct storage |

No generic Subscription CRUD event is published.

## 17. AWS/CDK mapping

| Need | AWS mapping | Status |
|---|---|---|
| synchronous application surface | existing `commerce-api` Lambda | conditional with first Ready use case |
| SubscriptionBilling state | module-owned DynamoDB table | conditional with persistence task |
| onboarding Trial recovery | Tenancy work-outbox/Stream -> SQS + worker | accepted by ADR-009 when onboarding task becomes Ready |
| order meter | EventBridge -> SubscriptionBilling SQS/DLQ + worker | conditional with meter task |
| Mock SaaS provider | separate API Gateway/Lambda/DynamoDB stack/app | conditional with provider task |
| provider callback ingress | API Gateway/Lambda handler | conditional with provider task |
| reconciliation schedule | EventBridge Scheduler | only if periodic due-work inquiry is actually required |
| reliable Subscription facts | outbox/Stream -> EventBridge -> consumer queue | only with named consumer |
| logs/metrics/alarms | CloudWatch | with runtime; bounded retention/low-cardinality |

No Step Functions is selected merely for ordinary Subscription plan/entitlement operations. A later workflow requires a focused ADR if durable branching/waiting grows beyond the current application/process design.

No NAT Gateway, ALB, EC2, RDS/Aurora, Redis, OpenSearch, Kafka/MSK, EKS, always-on service, or provisioned Lambda concurrency is introduced.

## 18. Security and tenant isolation

- merchant SubscriptionBilling routes require trusted Tenant context;
- request TenantId/plan/entitlement/provider ref never scopes authority;
- exact Plan catalog administration is platform-scoped and separate from tenant commands;
- platform admin is explicit read-only support context;
- provider callback auth is separate from merchant auth and resolves to known module-owned PlatformCharge/Tenant evidence;
- secrets/raw provider payloads minimized/redacted and not broadcast in integration events;
- Tenant-visible failures remain non-disclosing.

## 19. Reliability and observability

Observe, when runtime exists:

- entitlement authority unavailable/error counts;
- Trial bootstrap pending/failed/recovered;
- downgrade transition age/BlockedByUsage/fence failures;
- PlatformCharge Unknown age/count;
- provider callback verification/dedup failures;
- reconciliation due/failed/succeeded;
- order-meter queue age/DLQ/dedup conflicts;
- outbox relay lag/error.

Telemetry never becomes Subscription business truth.

Recovery rules:

- preserve logical identities on retries/redrive;
- never retry stale business revision blindly;
- Unknown remains Unknown until evidence;
- no foreign table repair;
- platform support actions use explicit approved application contracts only.

## 20. Cost impact

This documentation update has zero runtime cost.

Later cost remains serverless/conditional:

- one low-volume module DynamoDB table;
- no Stream/EventBridge/SQS without named consumer;
- separate small Mock SaaS provider resources when introduced;
- no Scheduler unless reconciliation really needs a schedule;
- no real paid provider selected.

Each Ready task includes table/index/worker/provider/schedule request/storage/log assumptions.

## 21. Remaining gate

The only current Subscription/Billing product gate is exact sellable package definition under `PD-044`:

- package prices;
- exact capability list;
- exact hard counted limits;
- exact warning thresholds/marketing package values.

Architecture must not invent these values. Structural PlanVersion history and enforcement mechanisms are already approved.

## 22. Verification checklist

Dependent implementation must verify:

- module dependency/persistence isolation;
- Tenant A/B subscription/charge/provider-ref isolation;
- no JWT/client/cache entitlement authority;
- duplicate Trial source creates one Trial and onboarding false completion is impossible;
- upgrade cannot grant higher entitlement before verified charge success;
- Unknown renewal does not create PastDue;
- definitive renewal failure/grace/Ended processing is idempotent at time boundaries;
- downgrade/current usage race cannot falsely finalize a lower limit;
- owner remediation never auto-deletes/disables resources;
- OrderConfirmed meter replay increments once and never blocks checkout;
- provider timeout/duplicate/out-of-order evidence preserves Unknown/idempotency/non-regression;
- merchant-order Payments cannot reference/reuse SaaS provider state;
- platform-admin has no mutation/direct-table path;
- CDK contains no speculative queue/event/schedule/provider resource;
- cost note covers all introduced integration/provider resources.

## 23. Conclusion

`SUBSCRIPTION & BILLING TECHNICAL BASELINE RECONCILED`

The current domain policy can now flow into task refinement without asking Builders to invent Trial consistency, entitlement trust, hard-limit ownership, upgrade/downgrade concurrency, renewal uncertainty, order metering, provider separation, platform-admin authority, persistence ownership, or AWS topology.

Exact commercial package values remain deliberately product-owned under `PD-044`.