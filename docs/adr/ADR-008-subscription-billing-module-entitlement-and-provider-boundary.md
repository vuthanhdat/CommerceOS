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

- automatic 30-day no-card Trial after successful merchant registration;
- monthly paid periods with explicit anchor/month-end behavior;
- VND whole-đồng SaaS Money, no currency conversion, tax calculation, statutory invoice, or proration in MVP;
- upgrade effective only after verified successful PlatformCharge, starting a fresh monthly period;
- downgrade scheduled for renewal and blocked by authoritative excess hard-limit usage without destructive remediation;
- definitive renewal failure creates PastDue with seven-day grace; `OutcomeUnknown` does not;
- Ended disables ordinary merchant mutations, scheduled automation, and public commerce while preserving approved authenticated read/history/export/recovery access and data;
- hard capability gates, hard counted-resource growth/activation limits, and warning-only order-volume metering are distinct;
- dedicated simulated SaaS billing provider is separate from the merchant-order provider;
- platform-admin Subscription/Billing support is read-only in MVP.

The architecture must encode those approved semantics without moving entitlement authority into JWTs/Tenancy/UI caches or letting SubscriptionBilling mutate foreign aggregates.

## Decision

### 1. One `SubscriptionBilling` implementation module initially

Map the bounded context to one module:

```text
CommerceOS.SubscriptionBilling.Domain
CommerceOS.SubscriptionBilling.Application
CommerceOS.SubscriptionBilling.Contracts   # only with a real consumer/delivery need
CommerceOS.SubscriptionBilling.Infrastructure
```

It hosts the approved business concepts while preserving their consistency boundaries:

```text
SubscriptionBilling
  ├── Plan / immutable PlanVersion terms
  ├── Subscription
  ├── EntitlementSet
  ├── UsageMeter
  └── PlatformCharge
```

Rules:

- it is not a generic licensing/shared-policy helper;
- merchant-order `Payments` remains a separate bounded context and persistence owner;
- Domain has no AWS/HTTP/provider/persistence dependency;
- foreign modules consume only producer-owned Contracts/application boundaries;
- no foreign table access or cross-domain DynamoDB transaction;
- no empty project/resource is created before a Ready task requires it.

### 2. Initial deployment remains the shared commerce runtime

Synchronous SubscriptionBilling application contracts run inside the existing `commerce-api` Lambda by default when introduced.

Separate worker/provider-ingress Lambdas are allowed only for named async/provider use cases. A full separate SubscriptionBilling service/Lambda is not justified solely by the bounded-context boundary.

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
owner-local authoritative usage/state
      ↓
owner-local invariant + commit
```

Conceptual producer-owned contract:

```text
EvaluateEntitlement(
  TrustedTenantScope,
  EntitlementKey,
  DecisionMetadata)
    -> EffectiveEntitlementDecision | Failure
```

An accepted decision may expose:

```text
tenantId
entitlementKey
value                 # capability / bounded limit / explicit Unlimited
entitlementSetId
subscriptionId
sourceTermsId
effectiveFrom
effectiveUntil?
decisionRevision
```

It does not expose Plan persistence records, provider objects, DynamoDB keys, or marketing-plan conditionals.

Never use as entitlement authority:

- client-supplied TenantId/plan/entitlement/limit;
- JWT custom claims;
- browser/session cache;
- Reporting/platform-admin projections;
- provider-private state;
- copied plan labels in another module.

No cross-request entitlement cache is authoritative initially.

### 4. Hard-limit enforcement combines two owners' truths

For a hard counted-resource limit:

1. SubscriptionBilling supplies the current trusted target limit/entitlement;
2. the owning domain supplies current authoritative count/state;
3. the owning domain conditionally protects its own write.

Examples already approved in principle include Active Membership and Warehouse growth limits. SubscriptionBilling never reads Tenancy/Inventory persistence or disables/deletes foreign resources to force compliance.

A stale Reporting/UI count cannot authorize the write.

### 5. Restrictive downgrade uses owner-coordinated assessment/fencing

Downgrade is now approved for the next renewal boundary and must be blocked when authoritative current usage exceeds a target hard limit.

A safe transition cannot be implemented as “change EntitlementSet, then eventually notice foreign overage”. For each affected hard-limit owner:

- SubscriptionBilling creates one durable downgrade transition identity;
- it invokes a producer-owned owner application contract to assess current authoritative usage against the proposed target;
- owner commands participating in the affected hard limit use an owner-local constraint/fence revision so a concurrent higher-limit write cannot race through while the lower limit is being finalized;
- the owner alone writes the fence/resource/count state;
- duplicate assessment/finalization is idempotent;
- if usage is too high, downgrade remains `BlockedByUsage/RemediationRequired` and the current plan/terms continue for the next period under the approved domain policy;
- remediation occurs only through normal owning-domain commands and must preserve invariants such as last Active Owner;
- no cross-domain ACID and no automatic destructive remediation are used.

If a future exact entitlement package creates a hard limit whose owner cannot provide a safe bounded transition contract, that package implementation remains blocked until the architecture is refined.

### 6. Automatic Trial uses ADR-009 cross-domain onboarding recovery

`StartTrialSubscription` is an idempotent SubscriptionBilling command invoked from the onboarding process using stable Tenant/onboarding source identity.

SubscriptionBilling atomically creates exactly one:

- 30-day Trial Subscription;
- Trial EntitlementSet with immutable provenance/history.

Completed onboarding is not reported until Trial acceptance is proven. Cross-domain completion/recovery is owned by ADR-009; no Trial state is stored as Tenancy authority.

### 7. Paid period model is explicit in SubscriptionBilling persistence

Paid subscriptions are monthly only in MVP.

SubscriptionBilling records accepted period boundaries/anchor explicitly so business behavior is not derived from server timezone or provider timestamps. Month-end fallback behavior follows the approved domain policy.

Trial remains a separate fixed 30-day period.

### 8. PlatformCharge remains a separate aggregate

A PlatformCharge is not a merchant-order Payment and not a merchant Accounting Journal.

It owns:

- stable charge identity;
- Tenant/Subscription/period/terms reference;
- VND whole-đồng Money amount;
- simulated provider operation/evidence references;
- verified success/definitive no-commit/decline/Unknown outcome;
- traceability to the Subscription transition/renewal it supports.

PlatformCharge state and Subscription state remain separate consistency boundaries connected by explicit application rules.

### 9. Upgrade effectivity follows verified charge success

For an approved upgrade request:

1. persist stable plan-change/charge operation identity;
2. execute the required PlatformCharge through the provider-neutral billing port;
3. do not make higher EntitlementSet effective while outcome is Declined/NoCommit/Unknown;
4. after verified successful charge, atomically accept the new Subscription period/terms + immutable EntitlementSet inside SubscriptionBilling;
5. the new paid period starts at the approved upgrade boundary with no proration/credit logic.

Provider callback/transport receipt alone is evidence, not effectivity.

### 10. Renewal failure and Ended preserve independent states

A **definitive** renewal-charge failure may move Subscription to PastDue and starts the approved seven-day grace period.

`OutcomeUnknown` does not create PastDue.

If grace expires without successful renewal, Subscription becomes Ended and current operational entitlements end. Tenant/Membership records are not disabled/deleted. Approved authenticated read/history/export/recovery paths do not require an operational entitlement that no longer exists.

Reactivation creates a new accepted period/history; it does not rewrite ended history.

### 11. Order-volume UsageMeter is warning-only

SubscriptionBilling may own the approved current-billing-period order-volume meter.

- authoritative source fact is `OrderConfirmed` from Sales;
- source application is idempotent by event/source identity;
- meter window is the current Subscription billing period;
- threshold crossing produces warning/operational follow-up only;
- it never rejects shopper checkout;
- it never cancels an Order;
- it never creates overage billing automatically.

This is a named ADR-006 consumer and therefore may use Sales outbox → EventBridge → SubscriptionBilling SQS/DLQ when implemented.

### 12. Dedicated simulated SaaS billing provider

The product now requires a **dedicated simulated SaaS billing provider seam**, separate from the merchant-order Mock Payment Provider.

Stable application seam:

```text
SubscriptionBilling.Application
      │ IPlatformBillingPort
      ▼
SubscriptionBilling.Infrastructure
      │ provider adapter
      ▼
Mock SaaS Billing Provider (external-like application)
```

The provider is implemented as a separate external-like application/deployment when its Ready task is introduced so it can model real provider boundaries without contaminating SubscriptionBilling persistence.

It must support deterministic scenarios for:

- success;
- definitive decline/no-commit;
- timeout/Unknown;
- duplicate delivery;
- idempotency/retry;
- query/reconciliation;
- out-of-order evidence.

Rules:

- one logical PlatformCharge operation has stable internal/provider idempotency identity;
- timeout/network cancellation/missing callback never proves no commit;
- callback/query evidence is authenticated/verified before definitive interpretation;
- duplicate/out-of-order evidence cannot duplicate/regress accepted state;
- unsafe retry waits for reconciliation when outcome is ambiguous;
- provider IDs remain SubscriptionBilling evidence only;
- no real card/bank data, CVV, or payment secrets belong in domain state/logs/fixtures.

A real billing provider later is a new product/security/compliance/architecture decision behind this port.

### 13. Platform administration is read-only support visibility

Platform administrators use a separate authenticated/authorized/audited execution context and producer-owned SubscriptionBilling query contracts.

MVP allows support visibility but no:

- manual comp/plan assignment;
- plan override;
- cancellation/reactivation override;
- charge-outcome mutation;
- entitlement/limit bypass;
- direct table access.

Any future mutation requires a new product decision and application command/Audit policy.

### 14. Persistence ownership and access patterns

SubscriptionBilling owns one DynamoDB table when Ready.

Approved logical records/access needs:

| Record/access | Required protection |
|---|---|
| Plan/PlanVersion | platform-scoped inside SubscriptionBilling; accepted versions immutable; exact package definitions remain `PD-044` gated |
| current Subscription | strong/current tenant read; expected revision |
| immutable EntitlementSet history/current reference | coherent with Subscription transitions inside the module |
| Trial source claim | idempotent by onboarding/Tenant source identity |
| plan-change transition | expected revision + idempotency + durable status |
| UsageMeter | tenant + meter/window; source-idempotent increment |
| PlatformCharge | separate aggregate/revision + stable logical charge identity |
| provider evidence/inbox | verified evidence dedup + non-regression |
| due-reconciliation lookup | sparse bounded/sharded operational index only when provider execution needs it; never Tenant query authority |
| integration outbox | atomic with source fact only for named consumers |
| platform-admin read | application query only; no direct table access |

No application `Scan`, no foreign table access, no GSI as entitlement authority.

### 15. Async integration

Use ADR-006 only for named consumers/facts:

- `OrderConfirmed` → SubscriptionBilling order-volume meter;
- SubscriptionBilling facts → Reporting when a defined projection exists;
- approved privileged SubscriptionBilling actions/evidence → Audit;
- provider callback/reconciliation work → dedicated handler/queue as required by provider runtime.

Do not publish generic Subscription/Plan CRUD events.

## AWS and deployment mapping

When corresponding tasks become Ready:

| Need | AWS mapping |
|---|---|
| synchronous SubscriptionBilling surface | existing `commerce-api` Lambda |
| module transactional state | SubscriptionBilling DynamoDB table |
| Trial bootstrap recovery | ADR-009 Tenancy work-outbox/Stream → SQS worker calling SubscriptionBilling |
| OrderConfirmed meter | EventBridge → SubscriptionBilling SQS/DLQ + worker |
| Mock SaaS Billing Provider | separate API Gateway/Lambda/DynamoDB application/stack |
| provider callback ingress | separate provider-authenticated API/Lambda handler as required |
| provider reconciliation | bounded worker; EventBridge Scheduler only if periodic due-work polling is required |
| reliable SubscriptionBilling facts | module outbox/Stream → EventBridge → consumer queue |
| observability | CloudWatch built-ins/alarms/logs with bounded retention |

No Step Functions is selected merely for ordinary plan/entitlement CRUD. If a future billing process demonstrates durable wait/branch/compensation complexity beyond the current application/worker design, it requires a focused workflow ADR.

No NAT Gateway, ALB, EC2, RDS/Aurora, Redis/ElastiCache, OpenSearch, MSK/Kafka, EKS, always-on service, or provisioned Lambda concurrency is introduced.

## Alternatives considered

### Put subscription/plan claims in Tenancy or TrustedTenantContext

Rejected because it creates stale duplicate commercial authority.

### Let every consuming module copy current plan/limit state and decide locally

Rejected as authority because restrictive transitions and revocation can race/stale. Display projections remain allowed but non-authoritative.

### Reuse merchant-order Payments/Mock Provider for SaaS billing

Rejected because the commercial relationships, provider evidence, and business ownership are different.

### Separate SubscriptionBilling microservice now

Rejected because shared runtime is cheaper/simpler and no measured IAM/runtime/scale pressure requires network separation. The separate simulated provider remains external-like by design.

### Cross-domain transaction for hard-limit downgrade

Rejected because resource owners retain their own state/invariants. Use owner-coordinated assessment/fencing instead.

## Consequences

### Positive

- one authoritative Subscription/entitlement boundary;
- current product policy can now be refined into implementation tasks without stale broad gates;
- Trial onboarding, upgrade, downgrade, renewal, Ended access, and warning meter have explicit technical seams;
- restrictive limits remain concurrency-safe without destructive foreign writes;
- SaaS billing uncertainty is isolated from merchant-order Payments;
- provider simulation teaches real external-system behavior without real money/card data;
- no always-on AWS infrastructure is required.

### Negative / trade-offs

- governed writes add a synchronous entitlement dependency and fail closed when authority is unavailable;
- downgrade hard-limit finalization needs owner participation/fencing;
- provider simulation adds a distinct app/stack and reconciliation surface;
- platform-admin support must use explicit queries rather than direct table access;
- exact plan package work remains blocked by `PD-044`.

## Security and tenant impact

- Tenant scope for merchant SubscriptionBilling operations comes only from trusted execution context.
- known Subscription/PlatformCharge/provider IDs cannot cross Tenant boundaries.
- provider callback authentication is separate from merchant authentication and resolves to known module-owned charge/Tenant evidence.
- secrets/raw provider payloads are minimized/redacted and never broadcast in integration facts.
- platform-admin is a separate explicit context with read-only MVP authority.

## Reliability and idempotency impact

- Trial, plan change, UsageMeter source, PlatformCharge, and provider evidence each use stable logical identities.
- equivalent replay returns prior logical result; incompatible reuse conflicts.
- Unknown remains Unknown until verified evidence resolves it.
- out-of-order evidence cannot regress later known state.
- downgrade transition keeps durable per-owner progress and never finalizes from elapsed time alone.
- async consumers follow at-least-once/idempotent ADR-006 rules.

## Cost impact

This ADR update deploys nothing and changes runtime cost by zero.

Future conditional cost is limited to:

- one low-volume module DynamoDB table;
- provider mock Lambda/API/DynamoDB when implemented;
- queue/worker/EventBridge usage for named meter/Audit/Reporting integrations;
- optional bounded Scheduler reconciliation if needed.

Every Ready task must include table/index/worker/provider/schedule assumptions in its cost note. No paid real billing provider is selected.

## Remaining product gate

Architecture does not define exact `Starter`/`Growth`/`Business` prices or entitlement/limit packages. That exact commercial catalog remains `PD-044` gated.

Structural Plan/PlanVersion history is approved; Builders must not invent package values merely because the persistence/provider seams exist.

## Validation

Dependent implementation must prove:

- no foreign Domain/Infrastructure/table dependency;
- Tenant A cannot evaluate/read/mutate Tenant B subscription/charge by known IDs/provider refs/cursors;
- client/JWT plan/entitlement cannot authorize a command;
- automatic Trial is duplicate-safe and onboarding cannot report complete before Trial acceptance;
- hard-limit owner write + downgrade fencing cannot falsely finalize a lower limit during a concurrent old-limit write;
- upgrade cannot make higher entitlements effective before verified successful charge;
- definitive renewal failure and Unknown remain distinct;
- Ended access does not require recreating Tenant/Membership state;
- `OrderConfirmed` replay increments the order meter once and never blocks shopper checkout;
- simulated provider timeout/duplicate/out-of-order behavior preserves Unknown/idempotency/reconciliation;
- merchant-order Payments cannot receive/reuse SaaS provider state;
- platform-admin has no mutation/direct-table path;
- CDK synthesizes no speculative queue/event/schedule/provider resource before a named Ready use case.

## References

- domain baseline: `docs/domains/subscription-billing.md`
- product decisions: `docs/domains/product-decisions.md`
- technical reconciliation: `docs/architecture/product-decision-technical-reconciliation.md`
- ADR-004: trusted Tenant authority
- ADR-005: DynamoDB module ownership/access patterns
- ADR-006: reliable integration
- ADR-007: HTTP/idempotency conventions
- ADR-009: onboarding completion/recovery