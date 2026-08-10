# Subscription & Billing Domain Baseline

_Reconciled after the 2026-08-10 human product-decision pass. This document incorporates approved `PD-043`–`PD-053`; only the exact commercial plan catalog/pricing sub-decision in `PD-044` remains intentionally deferred._

## 1. Boundary and business language

CommerceOS models **Subscription & Billing** as one bounded context for the current MVP.

It owns the merchant Tenant's commercial relationship with CommerceOS:

- stable Plan identity and versioned commercial terms;
- Subscription lifecycle and accepted terms;
- Trial and paid effective periods;
- immutable effective EntitlementSets;
- approved usage-meter truth;
- CommerceOS `PlatformCharge` obligations/evidence;
- verified interpretation of the dedicated simulated SaaS billing-provider evidence;
- enough history to explain why a capability/limit/charge was effective at any point in time.

It does not own:

- Tenant identity, Business Profile, or TenantStatus;
- Membership identity/status/roles;
- Product, Warehouse, Inventory, Order, merchant-order Payment, source snapshot, or journal truth;
- merchant bookkeeping/accounting entries;
- Audit evidence or Reporting projections.

A subscription restriction may make an operation in another bounded context ineligible, but Subscription & Billing never becomes owner of that aggregate.

## 2. Separation from merchant-order Payments

These are different commercial relationships:

```text
Shopper  ── pays merchant Order ──► Payments / merchant-order Mock Payment Provider
Merchant ── pays CommerceOS SaaS ─► Subscription & Billing / PlatformCharge
```

MVP SaaS billing uses a **dedicated simulated SaaS billing-provider seam** (`PD-052`). It is not the existing merchant-order Mock Payment Provider.

No real card/bank data is stored and no real money is charged in the learning/MVP billing path.

## 3. Core business model

### 3.1 Plan aggregate (`PD-044` structural policy)

`Plan` is the aggregate root for a stable CommerceOS commercial offering identity.

A Plan has immutable/versioned `PlanVersion` terms. Once a PlanVersion has been accepted by any Subscription:

- it is never edited in place;
- later commercial changes require a new PlanVersion;
- a version may be withdrawn from new purchase without changing existing Subscriptions or history.

`Starter`, `Growth`, and `Business` remain **candidate marketing packages only** until a deliberate commercial-pricing exercise approves exact prices and entitlements. `Enterprise`/custom pricing is out of MVP.

The exact sellable plan catalog/prices/entitlement matrix is the only intentionally deferred part of `PD-044` and must not be invented by implementation.

### 3.2 Subscription aggregate

`Subscription` is the aggregate root for one Tenant's base CommerceOS subscription relationship.

A Tenant has at most one current base Subscription relationship governing ordinary CommerceOS entitlements at a time.

The Subscription owns:

- immutable `SubscriptionId` and Tenant reference;
- accepted Trial or paid terms/version;
- current commercial condition;
- current effective period;
- immutable EntitlementSet history;
- plan-change intents and outcomes;
- cancellation-renewal intent;
- delinquency/grace history where evidence establishes it;
- enough accepted history to explain every commercial transition.

Retry of the same logical command creates one logical effect. Incompatible reuse of an intent identity is conflict.

### 3.3 EntitlementSet

`EntitlementSet` is an immutable business snapshot with:

- Tenant + Subscription provenance;
- source Trial terms or PlanVersion;
- explicit capabilities/limits;
- effective-from/effective-until interval;
- policy/version provenance;
- transition that made the set effective.

Marketing plan names are never authority outside this context.

`Unlimited` is an explicit entitlement value, never the absence of an entitlement record.

### 3.4 UsageMeter

A `UsageMeter` exists only for an approved accumulated usage policy that needs duplicate-safe counting over a window.

For MVP order volume (`PD-051`):

- source fact is idempotent `OrderConfirmed`;
- window is the current Subscription billing period;
- reaching/exceeding threshold is warning/operational follow-up only;
- it never rejects an otherwise valid shopper checkout;
- it never cancels an Order;
- it never creates an automatic overage charge.

Current-resource counts such as Active Memberships or Warehouses remain authoritative in their owning contexts rather than being blindly copied into a meter.

### 3.5 PlatformCharge

`PlatformCharge` is a separate aggregate root because charge attempts/evidence may become ambiguous or reconcile independently from Subscription state.

It owns conceptually:

- immutable charge identity;
- Tenant/Subscription reference;
- charge reason/period/accepted terms reference;
- VND Money amount;
- simulated provider references/attempt observations;
- verified known outcome, including explicit `OutcomeUnknown`;
- traceability to the Subscription transition/renewal it supports.

A PlatformCharge is not a merchant-order Payment and does not become a merchant Accounting Journal by implication.

## 4. Acquisition and automatic Trial (`PD-043`)

Successful merchant registration automatically creates a **30-day Trial Subscription** with a dedicated Trial terms/EntitlementSet.

Rules:

- Trial requires no payment method;
- Trial semantics are explicit and are not inferred from a marketing plan name;
- Tenant/Membership/Trial facts remain owned by their respective bounded contexts;
- onboarding must not pretend complete success while knowingly omitting the required Trial outcome;
- technical coordination/recovery is outside this domain document.

At Trial expiry without a paid/subsequent Subscription:

- Subscription becomes `Ended`;
- ordinary merchant mutations are disabled by missing effective operational entitlements;
- scheduled automation is disabled;
- public commerce is disabled;
- authenticated merchant read/history/export/recovery access remains available;
- Tenant identity/data and Memberships are not deleted or disabled.

## 5. Commercial lifecycle and independent dimensions

Subscription commercial state, billing outcome, TenantStatus, MembershipStatus, plan-change intent, and cancellation intent are independent dimensions.

### 5.1 Commercial conditions

Approved MVP commercial conditions include:

- `Trial` — 30-day dedicated Trial terms effective;
- `Active` — paid Subscription terms effective;
- `PastDue` — definitive renewal failure established and grace period active;
- `Ended` — no current normal operational Subscription entitlement remains effective.

`OutcomeUnknown` is a PlatformCharge/billing-evidence condition, not `PastDue`.

### 5.2 Cancellation intent

Merchant cancellation means **cancel renewal**, not immediate termination (`PD-049`).

```text
Active/PastDue eligible subscription
        └─ CancelRenewalRequested
                  ↓
current effective terms continue until paid period end
                  ↓
Ended at period boundary if no subsequent accepted continuation
```

A cancellation request never rewrites the already-effective period.

### 5.3 Plan-change intent

Plan change is distinct from current Subscription condition:

```text
RequestPlanChange
      ├─ upgrade pending required charge evidence
      └─ downgrade scheduled for renewal boundary
```

Requested is never equivalent to effective.

## 6. Billing periods (`PD-045`)

Paid MVP subscriptions are **monthly only**. Annual billing is out of scope.

Rules:

- activation records an explicit billing anchor;
- each next paid period advances one calendar month from the anchor;
- if equivalent day does not exist in the target month, use the last valid day of that month;
- effective periods are explicit, non-overlapping intervals;
- period semantics do not depend on server/reporting timezone assumptions;
- Trial remains a separate fixed 30-day period.

## 7. SaaS Money, tax, invoice, and proration (`PD-046`)

CommerceOS SaaS learning/MVP charges are:

- VND-only;
- whole đồng;
- explicit `Money` values.

MVP does not support:

- currency conversion;
- tax calculation;
- tax-inclusive/exclusive logic;
- statutory/tax invoices;
- proration.

CommerceOS may present a billing statement/PlatformCharge record for traceability, but it must not be represented as a legally compliant tax invoice.

## 8. Upgrade policy (`PD-047`)

An upgrade becomes effective **only after** the required new-plan PlatformCharge has a verified successful outcome.

When a successful mid-period upgrade occurs:

1. current old terms stop at the approved upgrade boundary;
2. a fresh monthly paid Subscription period begins at upgraded terms;
3. a new immutable EntitlementSet becomes effective at that same boundary;
4. unused old-period value is not credited because MVP has no proration.

If PlatformCharge is Declined/definitive no-commit or remains `OutcomeUnknown`:

- higher entitlements do not become effective;
- existing Subscription/EntitlementSet remains authoritative;
- an unknown charge is not silently converted into failure or success.

## 9. Downgrade policy (`PD-048`)

Downgrade is scheduled for the **next renewal boundary**, never immediate in MVP.

Before effectivity, authoritative owning-domain usage/state is revalidated against target hard limits.

If current usage exceeds a target hard limit:

```text
scheduled downgrade
      ↓
revalidate authoritative usage
      ↓
usage > target hard limit
      ↓
BlockedByUsage / RemediationRequired
      ↓
no lower EntitlementSet becomes effective
      ↓
current plan/terms continue for next period
```

Merchant remediation uses normal owning-domain commands.

Subscription & Billing never auto-deletes Products, disables Memberships, removes Warehouses, erases snapshots, mutates Orders, or rewrites Accounting history to fit a lower plan.

The downgrade itself grants no temporary overage/grandfathered write expansion.

## 10. Renewal failure, grace, end, and reactivation (`PD-049`)

A **definitive** renewal-charge failure creates `PastDue` with a **7-day grace period**.

During grace:

- existing effective operational entitlements continue;
- merchant should be able to see the billing exception through appropriate projections/notifications;
- business history remains unchanged.

A billing `OutcomeUnknown` does **not** become PastDue until reconciliation establishes a definitive failed renewal outcome.

If grace ends without successful renewal:

- Subscription becomes Ended;
- ordinary merchant mutations, scheduled automation, and public commerce are disabled;
- authenticated merchant read/history/export/recovery access remains;
- no Tenant/business data is automatically deleted.

Reactivation starts a **new** Subscription period/accepted terms and does not rewrite ended history.

Data retention/deletion remains governed by the broader future privacy/Tenant-lifecycle decision, not Subscription end.

## 11. Entitlement enforcement categories (`PD-050`)

MVP does not use one generic limit rule.

### Hard capability gates

Examples: scheduled ingestion/API access when present.

- checked at the owning business command boundary;
- capability absent/disabled rejects the protected operation;
- read/history/recovery access remains where approved;
- missing entitlement is never interpreted as Unlimited.

### Hard counted-resource growth/activation limits

Examples: `MaxActiveStaff`, `MaxWarehouses`.

- creation/activation that would exceed current trusted limit is rejected;
- existing resources caused to be over a target downgrade limit are not destroyed;
- existing resources remain readable/manageable for remediation;
- authoritative current count comes from the owning bounded context;
- stale Reporting/UI usage is not command authority.

### Soft usage warning

Order-volume threshold follows `PD-051`: warning only, no shopper checkout rejection and no overage billing.

Overage billing is out of MVP.

## 12. Provider simulation and uncertainty (`PD-052`)

The dedicated SaaS billing simulation must be able to represent business evidence for:

- success;
- definitive decline/no-commit;
- timeout/`OutcomeUnknown`;
- duplicate delivery;
- retry/idempotency;
- provider query/reconciliation;
- out-of-order evidence.

Domain rules:

1. equivalent retry creates one logical charge effect;
2. callbacks/provider records are evidence, not direct Subscription truth;
3. only verified evidence may establish definitive charge outcome;
4. timeout/network failure/missing callback/caller cancellation never proves no commit;
5. `OutcomeUnknown` remains explicit until reconciled;
6. time passage alone never converts Unknown into success/failure/PastDue/Ended;
7. duplicate/out-of-order evidence cannot duplicate or regress accepted charge/subscription effects;
8. no real card/bank secrets belong in fixtures/history.

Choosing a real billing provider later is a new product/architecture/compliance decision behind this provider boundary.

## 13. Platform administration (`PD-053`)

MVP platform-admin Subscription & Billing capability is **read/support visibility only**.

Authorized platform administrators may inspect appropriate subscription, entitlement, usage, PlatformCharge, and reconciliation projections.

They may **not**:

- manually comp/assign/change plans;
- cancel/reactivate a Tenant Subscription;
- mutate charge outcomes;
- bypass entitlements/limits;
- create a hidden cross-Tenant override.

Any future platform-admin mutation must be introduced as an explicit Subscription & Billing business command with Product Owner decision, authorization/notice policy, and Audit evidence.

## 14. Cross-domain interaction rules

| Context | Subscription & Billing provides/owns | Other context remains authoritative for | Approved rule |
|---|---|---|---|
| Tenant Management | Subscription commercial eligibility | Tenant identity/Profile/Active-Suspended status | Subscription end/delinquency does not mutate TenantStatus. |
| Merchant Access | MaxActiveStaff entitlement/target limit | Membership identity/status/role/current count | hard growth gate; no automatic Membership disable; last-owner preserved. |
| Inventory | MaxWarehouses or other approved capability/limit | Warehouse/stock truth | hard growth/activation gate; downgrade cannot delete Warehouses. |
| Product Data Ingestion | scheduled-ingestion capability | source policy/run/snapshot/candidate truth | both entitlement and PDI source-policy eligibility must pass. |
| Sales | order-volume metering policy | SalesOrder/OrderConfirmed truth | OrderConfirmed may feed duplicate-safe meter; threshold never blocks shopper checkout. |
| Payments | none for SaaS billing | merchant-order Payment | do not reuse merchant-order Payment as PlatformCharge. |
| Accounting | no merchant-journal authority | merchant books | PlatformCharge is not automatically a merchant journal. |
| Reporting | projection inputs | projections only | Reporting/UI never authorizes entitlement/state mutation. |
| Audit | auditable business-action references | Audit evidence | Subscription/admin actions produce required Audit evidence without Audit owning state. |

## 15. Commands, queries, and facts

### Command candidates

- `StartTrialSubscription`
- `ActivatePaidSubscription`
- `RequestPlanUpgrade`
- `RequestPlanDowngrade`
- `CancelSubscriptionRenewal`
- `AttemptSubscriptionRenewal`
- `ReactivateSubscription`
- `RecordPlatformChargeAttempt`
- `ReconcilePlatformCharge`
- `ApplyUsageFact`

These are business intent names, not API schemas.

### Query intents

- resolve current Subscription and accepted terms for Tenant;
- resolve current effective EntitlementSet/capability/limit with provenance;
- explain historical entitlement period;
- list current/historical PlatformCharge outcomes;
- resolve current order-volume meter/window;
- surface downgrade remediation status;
- support read-only platform-admin investigation.

### Owned fact candidates

- `TrialSubscriptionStarted`
- `SubscriptionActivated`
- `SubscriptionPlanChanged`
- `SubscriptionDowngradeScheduled`
- `SubscriptionDowngradeBlockedByUsage`
- `SubscriptionRenewalCancellationRequested`
- `SubscriptionEnteredPastDue`
- `SubscriptionEnded`
- `SubscriptionReactivated`
- `EntitlementSetBecameEffective`
- `UsageCountApplied`
- `UsageThresholdCrossed`
- `PlatformChargeRecorded`
- `PlatformChargeSettled`
- `PlatformChargeDefinitivelyNotSettled`
- `PlatformChargeOutcomeBecameUnknown`
- `PlatformChargeReconciled`

A requested plan change/cancellation/charge attempt is never proof of the resulting effect.

## 16. Business error semantics

| Outcome | Meaning |
|---|---|
| `SUBSCRIPTION_REQUIRED` | no current entitlement exists for an operation that requires one; read/recovery access may still remain |
| `ENTITLEMENT_DENIED` | current trusted EntitlementSet does not grant capability |
| `ENTITLEMENT_LIMIT_REACHED` | hard growth/activation limit rejects proposed increase; existing resources unchanged |
| `PLAN_CHANGE_CONFLICT` | logical plan-change identity reused incompatibly |
| `UPGRADE_CHARGE_NOT_SETTLED` | higher terms cannot become effective because required verified successful charge is absent |
| `PLATFORM_CHARGE_OUTCOME_UNKNOWN` | charge commit cannot currently be proven; not success/failure |
| `DOWNGRADE_BLOCKED_BY_USAGE` | authoritative current usage exceeds target hard limit |
| `SUBSCRIPTION_ENDED` | operational entitlements ended; read/history/export/recovery semantics remain as approved |
| `PLAN_VERSION_NOT_AVAILABLE_FOR_NEW_PURCHASE` | version withdrawn/not currently sellable but historical accepted Subscriptions remain valid |
| `PLAN_CATALOG_NOT_YET_APPROVED` | requested exact commercial package/price/entitlement definition depends on deferred `PD-044` |

Transport mapping belongs to Technical Architecture.

## 17. Remaining human product decision

All Subscription & Billing structural/lifecycle/enforcement/provider/admin semantics needed for the current domain model are approved except the exact commercial sellable catalog in `PD-044`:

- exact `Starter`/`Growth`/`Business` prices;
- exact entitlement/limit matrix for those marketing packages;
- deliberate availability/launch commercial choices beyond the approved immutable-version structure.

**HUMAN PRODUCT DECISION REQUIRED** before a task must expose, sell, seed, or hard-code exact plan prices/entitlement packages. This deferment does not authorize a Builder to invent placeholders as product truth.

## 18. Downstream reconciliation handoff

### Technical Architect

TASK-0092 was completed before this product-decision pass. Reconcile the technical baseline against the now-approved semantics, especially:

- automatic 30-day Trial as part of merchant onboarding while preserving bounded-context ownership;
- monthly billing anchors and explicit periods;
- charge-success-first immediate upgrade with fresh monthly period;
- next-renewal downgrade + revalidation/block-by-usage;
- PastDue only after definitive renewal failure and 7-day grace;
- Ended read/history/export/recovery access;
- hard capability vs hard growth vs soft order-volume semantics;
- dedicated SaaS billing-provider simulation distinct from merchant-order Mock Payment Provider;
- explicit `OutcomeUnknown` reconciliation;
- read-only platform-admin support with no override.

Do not resolve exact plan pricing/entitlement packages through persistence, API, seed data, AWS, or provider convenience.

### Backlog Planner

Remove obsolete `PD-043`, `PD-045`–`PD-053` unresolved gates from affected candidate tasks once technical reconciliation is completed. Keep exact plan-catalog/pricing/entitlement-package work gated by deferred `PD-044`.

**Stop condition: DOMAIN BASELINE READY for approved Subscription & Billing MVP semantics; HUMAN PRODUCT DECISION REQUIRED only when exact `PD-044` commercial catalog/pricing is needed.**
