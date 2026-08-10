# ADR-008 — Subscription & Billing Module, Entitlement Decision, and Provider Boundary

Status: Accepted
Date: 2026-08-10
Last reconciled: 2026-08-10 after resolved PD-044
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS has a distinct Subscription & Billing bounded context that owns the merchant Tenant's commercial relationship with CommerceOS:

- stable Plan identity and immutable accepted PlanVersion terms;
- dedicated Trial terms;
- Subscription lifecycle;
- immutable effective EntitlementSets;
- approved UsageMeters;
- PlatformCharge obligations/evidence;
- interpretation of the dedicated simulated SaaS billing provider.

Resolved `PD-044` now defines the initial sellable MVP catalog and Trial terms:

| Terms | Monthly price | MaxActiveMemberships | MaxWarehouses | ScheduledProductIngestion | OrderVolumeWarningThreshold |
|---|---:|---:|---:|---|---:|
| Trial | n/a | 3 | 1 | true | 500 |
| Starter | 199,000 VND | 3 | 1 | false | 500 |
| Growth | 499,000 VND | 10 | 3 | true | 2,000 |
| Business | 999,000 VND | 30 | 10 | true | 10,000 |

All paid Plans include the same core CommerceOS capabilities: Catalog, Storefront, Orders/Sales, Inventory, Procurement, Accounting, and Reporting. Trial also enables all core capabilities. Plan name itself is never authority outside SubscriptionBilling.

Architecture must now provide an immutable/versioned catalog storage/bootstrap mechanism and concrete hard-limit/capability enforcement without copying commercial truth into Tenancy, Inventory, ProductDataIngestion, JWTs, frontend constants, or Reporting.

## Decision

### Module and deployment boundary

Keep one `SubscriptionBilling` implementation module initially:

```text
CommerceOS.SubscriptionBilling.Domain
CommerceOS.SubscriptionBilling.Application
CommerceOS.SubscriptionBilling.Contracts
CommerceOS.SubscriptionBilling.Infrastructure
```

Synchronous application contracts run in the shared `commerce-api` Lambda by default. Separate worker/provider-ingress Lambdas are introduced only for named async/provider use cases. The bounded-context boundary alone does not justify a separate microservice.

SubscriptionBilling remains separate from merchant-order Payments, Tenancy, Sales, Inventory, Accounting, Reporting, and ProductDataIngestion.

### Plan catalog persistence and bootstrap

Plan/PlanVersion and Trial-terms versions are SubscriptionBilling-owned platform-global commercial truth stored in the SubscriptionBilling DynamoDB table when that module's persistence task is Ready.

Use a **version-controlled catalog seed artifact plus an idempotent SubscriptionBilling bootstrap/migration command** to create the initial catalog. Do not introduce AppConfig, SSM Parameter Store, a separate configuration database, or frontend hard-coded commercial authority.

Required properties:

- `Plan` has stable identity (`Starter`, `Growth`, `Business` are current business names/identities, not authorization keys);
- each published `PlanVersion` is immutable once accepted by any Subscription;
- a PlanVersion can be withdrawn from new sale without deleting it;
- price/entitlement changes create a new PlanVersion;
- Trial uses a dedicated immutable Trial-terms version and is not a Starter alias;
- bootstrap is safe to re-run: equivalent version content is `AlreadyApplied`; incompatible reuse of an immutable version identity is conflict;
- old accepted versions remain queryable for historical Subscription explanation;
- no TTL applies to accepted commercial terms/history.

The initial seed contains exactly the resolved MVP catalog values from the domain baseline. Future changes use new version records rather than mutating accepted records.

### EntitlementSet remains runtime authority

Accepted Trial/PlanVersion terms are materialized into an immutable effective `EntitlementSet` owned by the Subscription aggregate.

Foreign modules call:

```text
EvaluateEntitlement(
  TrustedTenantScope,
  EntitlementKey,
  DecisionMetadata)
    -> EffectiveEntitlementDecision | Failure
```

The decision may expose:

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

Foreign modules never authorize by:

- Plan name;
- frontend pricing table;
- JWT/custom claim;
- copied plan/limit field;
- Reporting projection;
- provider-private state.

No cross-request entitlement cache is authoritative initially.

### Initial entitlement keys

The initial technical entitlement vocabulary is the business vocabulary already approved by `PD-044`:

```text
CoreCommerceCapabilities
MaxActiveMemberships
MaxWarehouses
ScheduledProductIngestion
OrderVolumeWarningThreshold
```

`CoreCommerceCapabilities` must be explicit in the accepted EntitlementSet even though all current paid Plans/Trial enable it; missing entitlement never means implicitly enabled.

`OrderVolumeWarningThreshold` is warning-only and is not a hard capability or billing amount.

### MaxActiveMemberships enforcement

Merchant Access owns Membership state/count; SubscriptionBilling owns the current limit.

Before creating or reactivating a Membership:

1. Tenancy resolves current merchant mutation authority;
2. Tenancy synchronously evaluates `MaxActiveMemberships` in SubscriptionBilling;
3. Tenancy uses a module-local authoritative active-membership count/guard updated transactionally with Membership lifecycle changes;
4. the Tenancy transaction accepts the activation only when `currentActiveCount + delta <= limit` while preserving the last-Active-Owner invariant.

All Active Owner/Admin/Staff/Viewer Memberships count. SubscriptionBilling never reads the Tenancy table and never disables Memberships to fit a lower plan.

### MaxWarehouses enforcement

Inventory owns Warehouse lifecycle/count; SubscriptionBilling owns the current limit.

Before creating/reactivating a Warehouse:

1. Inventory receives trusted mutation context;
2. Inventory synchronously evaluates `MaxWarehouses`;
3. Inventory conditionally updates a module-local active-Warehouse count/guard with the Warehouse write;
4. the write rejects if the resulting authoritative count would exceed the current limit.

SubscriptionBilling never reads/writes Inventory persistence.

### ScheduledProductIngestion enforcement

ProductDataIngestion owns source policy, schedule/run state, and source safety. SubscriptionBilling owns whether the Tenant currently has the scheduled-ingestion capability.

- schedule creation/enablement requires `ScheduledProductIngestion=true` plus PDI source-policy approval;
- dispatch/execution re-evaluates the current entitlement before starting scheduled acquisition so downgrade/Ended takes effect without relying on stale schedule state;
- loss of entitlement suppresses future scheduled execution but does not silently delete PDI history/configuration;
- source-policy denial still wins even when the subscription capability is enabled.

Starter therefore cannot execute scheduled product ingestion; Growth, Business, and Trial can, subject to PDI policy.

### Order-volume warning meter

SubscriptionBilling consumes `OrderConfirmed` idempotently for the current billing period.

The meter reads the threshold from the current effective EntitlementSet/period terms:

- Trial/Starter: 500;
- Growth: 2,000;
- Business: 10,000.

Threshold crossing produces warning/visibility only. It never rejects shopper checkout, cancels an Order, or creates automatic overage billing.

Upgrade starts a fresh monthly period under approved policy, so the new period uses the new PlanVersion threshold. Downgrade takes effect only at its approved renewal boundary.

### Trial onboarding

`StartTrialSubscription` is idempotent by stable onboarding/Tenant source identity and creates:

- 30-day Trial Subscription;
- dedicated Trial terms provenance;
- Trial EntitlementSet with all core capabilities, `MaxActiveMemberships=3`, `MaxWarehouses=1`, `ScheduledProductIngestion=true`, `OrderVolumeWarningThreshold=500`.

ADR-009 owns cross-domain onboarding completion/recovery. Trial expiry does not auto-convert to Starter.

### Upgrade, downgrade, renewal, and Ended

Existing accepted rules remain:

- upgrade effectivity requires verified successful PlatformCharge and starts a fresh monthly period;
- Declined/NoCommit/OutcomeUnknown does not grant the higher EntitlementSet;
- downgrade is scheduled for renewal and uses authoritative owner assessment/fencing for hard limits;
- usage above target keeps downgrade `BlockedByUsage/RemediationRequired` and current terms continue;
- no destructive auto-remediation;
- definitive renewal failure may create PastDue with seven-day grace; OutcomeUnknown does not;
- Ended removes ordinary operational entitlements/scheduled automation/public commerce while approved authenticated read/history/export/recovery remains;
- reactivation creates new accepted terms/history rather than rewriting old history.

### Dedicated simulated SaaS provider

Keep the dedicated external-like provider seam separate from merchant-order Payments.

It supports deterministic success, definitive no-commit/decline, OutcomeUnknown, duplicate/out-of-order evidence, idempotency, and query/reconciliation.

No real card/bank data is stored. A real billing provider later requires a new product/security/compliance/architecture decision.

### Platform administration

Platform-admin Subscription/Billing remains read-only support visibility. It cannot override PlanVersion, EntitlementSet, charge outcome, cancellation/reactivation, or hard limits.

### Persistence access patterns

SubscriptionBilling owns one DynamoDB table when Ready.

| ID | Use case | Protection |
|---|---|---|
| `SUB-AP-00` | bootstrap/list sellable PlanVersions + Trial terms | platform-scoped base records; immutable version identities; idempotent seed; no Scan for runtime catalog query |
| `SUB-AP-01R` | current Subscription | strong/current Tenant base access; expected revision |
| `SUB-AP-02R` | EvaluateEntitlement | coherent current Subscription + effective EntitlementSet provenance; no GSI authority |
| `SUB-AP-03` | immutable history | bounded Tenant history; eventual allowed for display |
| `SUB-AP-04R` | Trial/upgrade/downgrade/renew/reactivate | expected revision + command idempotency + immutable accepted history |
| `SUB-AP-05R` | Order volume UsageMeter | Tenant + current billing-period meter + `OrderConfirmed` source idempotency |
| `SUB-AP-06R` | PlatformCharge | separate aggregate/revision + stable logical charge identity |
| `SUB-AP-07R` | provider evidence | verified/deduplicated/non-regressing evidence |
| `SUB-AP-08R` | due reconciliation | sparse bounded operational index only when provider execution requires it |
| `SUB-AP-09R` | platform support read | explicit application query; separate admin context; no direct table access |
| `SUB-AP-10` | Trial source claim | one logical Trial per stable onboarding/Tenant source |
| `SUB-AP-11` | restrictive downgrade process | durable transition + per-owner assessment/fence acknowledgements |

Tenancy and Inventory maintain their own authoritative resource-count guard records; those counters do not move into SubscriptionBilling.

### AWS/deployment mapping

No new managed service is needed for resolved `PD-044`.

- Plan catalog, Trial terms, Subscription/EntitlementSet/UsageMeter/PlatformCharge use the module DynamoDB table.
- `commerce-api` hosts synchronous catalog query/entitlement/application commands initially.
- EventBridge + SubscriptionBilling SQS/DLQ handles the named `OrderConfirmed` meter consumer when Ready.
- the dedicated simulated SaaS provider uses its already-approved conditional API Gateway/Lambda/DynamoDB external-like stack.
- EventBridge Scheduler is introduced only if a named due renewal/reconciliation process requires periodic triggering.

Do not add AppConfig/SSM/RDS/Redis or an always-on service merely to store plan prices/limits.

## Alternatives considered

### Hard-code plan prices/limits in frontend or consuming modules

Rejected because it creates duplicated stale commercial authority and makes historical PlanVersion semantics impossible to enforce consistently.

### Use AppConfig/SSM as runtime Plan catalog authority

Rejected for MVP because SubscriptionBilling already needs transactional immutable accepted terms/history in DynamoDB. A second runtime configuration authority adds cost and consistency problems without a demonstrated need.

### Store only Plan name on Subscription and derive current limits from latest catalog

Rejected because historical accepted terms would change when the catalog changes.

### Copy effective limits into Tenancy/Inventory/PDI and authorize locally

Rejected as current authority. Owner-local counts/state remain local, but current commercial limit/capability comes from SubscriptionBilling.

### Cross-domain transaction for resource limit checks

Rejected. The owner conditionally protects its own resource/count after obtaining current commercial limit; no cross-module ACID is used.

### Version-controlled seed + immutable PlanVersion records + EntitlementSet snapshot

Chosen because it provides repeatable bootstrap, historical explanation, no additional AWS service, and clean authority boundaries.

## Consequences

Positive consequences:

- the approved 199k/499k/999k catalog is usable without hard-coded authority outside SubscriptionBilling;
- future pricing experiments create new immutable PlanVersions without rewriting accepted history;
- hard resource limits remain concurrency-safe in their owning modules;
- Trial can deliberately expose scheduled ingestion while Starter does not;
- order-volume warnings remain operational rather than accidental checkout/billing gates;
- no extra managed configuration service is required.

Trade-offs:

- deployment/bootstrap must seed catalog versions before plan-selection flows are enabled;
- synchronous governed writes add an entitlement lookup before owner-local conditional commit;
- Tenancy/Inventory must maintain authoritative local count guards;
- restrictive downgrade requires owner assessment/fencing rather than a single local Subscription update.

## Security and tenant impact

- Plan/PlanVersion records are platform-global but never grant tenant authority by caller input.
- Entitlement evaluation always uses trusted Tenant scope derived from current merchant authority.
- foreign modules cannot directly read SubscriptionBilling persistence.
- platform admin cannot mutate commercial truth through support queries.
- provider evidence and merchant-order Payment state remain isolated.
- no real payment credentials are stored in plan/provider fixtures.

## Reliability and operability impact

- catalog bootstrap is idempotent and detects incompatible immutable-version reuse;
- accepted PlanVersions/Trial terms are retained indefinitely for historical Subscription explanation;
- hard-limit writes fail closed if current entitlement cannot be established;
- owner-local count guards protect concurrent resource activation/creation;
- PDI dispatch rechecks capability so stale schedules cannot continue after entitlement loss;
- `OrderConfirmed` meter consumption is at-least-once/idempotent;
- provider OutcomeUnknown remains reconciled rather than guessed.

## Cost impact

No new AWS managed service is introduced by resolving `PD-044`.

Costs are bounded DynamoDB reads/writes/storage for catalog versions/EntitlementSets/count guards and existing EventBridge/SQS/Lambda usage for named meter/provider workflows. The initial catalog is tiny and platform-global, so catalog storage/read cost is negligible relative to the existing serverless baseline.

## Reversibility / migration

- future price/limit changes add PlanVersions rather than mutating current accepted versions;
- frontend plan-selection UI may change without changing authority semantics because it queries SubscriptionBilling;
- a future external catalog/config service would require migration behind SubscriptionBilling application contracts and a new consistency/availability ADR;
- removing an entitlement key later requires a versioned commercial/domain migration; absence must never silently mean Unlimited/enabled.

## Validation

Dependent implementation must verify:

- initial seed creates exactly Trial terms + Starter/Growth/Business versions with approved values and is safe to re-run;
- accepted PlanVersion cannot be edited in place;
- withdrawn version cannot be newly selected but remains readable for existing Subscription history;
- Trial does not alias Starter and does not auto-convert on expiry;
- Starter denies scheduled ingestion while Growth/Business/Trial allow it only when PDI source policy also allows it;
- Membership activation/create counts every Active role and cannot exceed 3/3/10/30 according to effective terms;
- Warehouse creation/reactivation cannot exceed 1/1/3/10 according to effective terms;
- concurrent resource creation cannot bypass owner-local count guard;
- downgrade below current usage remains blocked without destructive remediation;
- order-volume replay increments once and threshold crossing never blocks checkout or creates overage billing;
- no consuming module branches on Plan name or reads SubscriptionBilling table directly;
- no AppConfig/SSM/extra database is introduced solely for the MVP catalog.
