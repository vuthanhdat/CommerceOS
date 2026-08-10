# Subscription & Billing Domain Baseline

_Extension to the CommerceOS business-domain baseline produced by TASK-0091 on 2026-08-10._

## 1. Boundary and business language

CommerceOS models **Subscription & Billing** as one bounded context initially.

This is the smallest coherent business boundary for the current product scope because plan terms, the merchant's subscription lifecycle, effective entitlements, usage evidence needed by those entitlements, and CommerceOS SaaS charge evidence all describe one commercial relationship between CommerceOS and a merchant Tenant. Splitting Plan Catalog, Subscription, Entitlements, Usage Metering, and Billing into separate bounded contexts now would add coordination boundaries before the product has independent teams, scale, regulatory needs, or materially different lifecycles that justify them.

The internal model still keeps these concepts distinct so they can split later without changing business ownership:

```text
Commercial Plan / accepted terms
            ↓
       Subscription
            ↓
 Effective Entitlement Set
            ↓
 capability / limit decisions
            ↓
 tenant-owned domains keep their own truth

Subscription ── may create/relate to ──► Platform Charge
                                      └─ external billing evidence later
```

A future architecture may map this bounded context to one or more modules or deployed components. That is not decided here.

### What this context owns

Subscription & Billing owns:

- the merchant Tenant's CommerceOS subscription identity and commercial lifecycle;
- the specific CommerceOS plan/version/terms accepted for an effective period;
- plan-change and cancellation intent/history;
- the effective entitlement set produced by accepted subscription terms and approved policy;
- usage-meter truth only where a limit requires an accumulated metered window;
- CommerceOS platform charge obligations/references and the outcome CommerceOS currently knows about them;
- references/evidence from any future external SaaS billing provider;
- enough history to explain why a capability or limit was effective at a point in time.

### What this context does not own

It does **not** own:

- Tenant identity, Business Profile, or TenantStatus — Tenant Management;
- authentication credentials or staff Membership identity/lifecycle — Merchant Access;
- Products, Orders, stock, Warehouses, source snapshots, or merchant operational transactions;
- the merchant-order `Payment` aggregate or Mock Payment Provider semantics used for shopper checkout;
- the merchant's bookkeeping journals/ledger — Accounting;
- Audit evidence or Reporting projections;
- another context's current resource count merely because that count is compared with a subscription limit.

A subscription restriction may make an operation in another context ineligible, but it does not transfer ownership of that context's aggregate to Subscription & Billing.

## 2. Source-of-truth rules

| Business question | Authoritative owner | Other contexts may hold only |
|---|---|---|
| Which CommerceOS commercial subscription currently governs this Tenant? | Subscription & Billing | subscription reference/status projection |
| Which plan/version/terms did the Tenant accept for a period? | Subscription & Billing | immutable reference/projection needed for the consumer's own history |
| Which capability/limit is effective for this Tenant now? | Subscription & Billing | trusted decision/result and provenance |
| Why was a capability/limit effective at a historical time? | Subscription & Billing | copied business snapshot where required for the consumer's own historical meaning |
| How many active staff Memberships exist? | Merchant Access | usage evidence/projection; never Membership truth |
| How many Warehouses/Locations exist or are active? | Inventory | usage evidence/projection; never Warehouse truth |
| How many Orders were accepted in a metered window? | Sales owns the source Orders; Subscription & Billing may own an idempotent usage meter derived from accepted source facts when an approved plan requires it | reporting projection |
| May scheduled external product ingestion be used? | Subscription & Billing owns the capability entitlement; Product Data Ingestion still owns whether a source/run is otherwise policy-eligible | trusted entitlement result |
| What CommerceOS SaaS charge is due/recorded and what outcome does CommerceOS currently know? | Subscription & Billing | billing-history projection/reference |
| What did an external SaaS billing provider commit? | the future provider is external evidence; Subscription & Billing owns CommerceOS's verified interpretation | provider reference/evidence |
| What did a shopper pay for a merchant Order? | Payments | payment reference/projection |
| What has the merchant posted in its own books? | Accounting | journal reference/financial projection |

External provider evidence, tenant usage projections, and UI plan labels never become the subscription source of truth.

## 3. Core model and aggregate boundaries

Aggregate boundaries are chosen by business consistency, not by nouns or storage tables.

### 3.1 Plan aggregate

`Plan` is the aggregate root for a stable CommerceOS commercial offering identity.

A Plan may have one or more `PlanVersion` / commercial-term revisions. A version contains or references the business terms needed to determine what a tenant accepted, including:

- stable plan/version identity;
- merchant-facing label/description where applicable;
- price terms and billing-cycle terms when approved;
- effective/availability period when approved;
- the entitlement definitions promised by that version;
- commercial-policy version/provenance needed to interpret the terms later.

The exact Starter/Growth/Business catalog, version mutability policy, price values, availability/retirement behavior, and whether accepted versions become immutable records are human decision `PD-044`.

Safe baseline invariant:

> Once a Tenant has accepted commercial terms, later catalog editing must not retroactively change the historical meaning of that accepted subscription period.

The implementation may preserve this through immutable versions or an accepted-terms snapshot, but that technical representation is not chosen here.

### 3.2 Subscription aggregate

`Subscription` is the aggregate root for the merchant Tenant's base CommerceOS commercial relationship.

For the approved product scope, a Tenant has at most one current base subscription relationship governing its ordinary CommerceOS plan entitlements at a time. Future add-ons, multiple concurrent products, reseller contracts, or enterprise side agreements are not modeled by this task.

The Subscription owns conceptually:

- immutable `SubscriptionId`;
- immutable Tenant reference;
- current commercial subscription condition;
- current accepted Plan/PlanVersion or accepted-terms reference;
- current effective subscription period;
- current effective EntitlementSet reference/history;
- accepted plan-change intents and their state/reason;
- accepted cancellation intent and effective-end information when policy permits it;
- trial phase/terms if and only if `PD-043` approves trial behavior;
- history sufficient to explain each effective commercial-term change.

A retry of the same accepted logical subscription command produces one logical effect. Reusing the same command identity for materially different target terms is a conflict.

### 3.3 Effective EntitlementSet

`EntitlementSet` is an immutable business snapshot/value associated with an effective interval and provenance. It is not a marketing-plan name and does not become another domain's aggregate.

An entitlement expresses one of these business meanings:

- capability: allowed/not allowed;
- bounded limit: an explicit numeric/unit limit;
- operationally unlimited: an explicit semantic value, not a very large magic number;
- optional policy metadata needed to interpret a limit, such as a usage window after that policy is approved.

Each effective set identifies conceptually:

- Tenant and Subscription;
- source PlanVersion/accepted terms;
- entitlement keys and values;
- effective-from and, where known, effective-until;
- policy/version provenance;
- the subscription change that made the set effective.

Historical EntitlementSets are not rewritten when the merchant later upgrades, downgrades, cancels, or a Plan's marketing name changes.

### 3.4 UsageMeter aggregate, only where needed

A `UsageMeter` is a separate consistency boundary only for an approved entitlement that requires accumulated usage over a window, for example an order-volume allowance.

It may own:

- Tenant + entitlement/meter identity;
- approved usage window identity/bounds;
- counted quantity/value;
- exact logical source identities already applied;
- current threshold/over-limit condition as defined by approved policy.

Rules:

1. source domains still own the business facts being counted;
2. replay of the same logical source fact cannot increment usage twice;
3. a Reporting total is not command authority for a hard limit;
4. current-resource cardinality such as active staff or Warehouses need not be copied into a UsageMeter merely to check a limit;
5. exact order-volume blocking/overage behavior is `PD-051`.

### 3.5 PlatformCharge aggregate

`PlatformCharge` is a separate aggregate root from Subscription because billing execution can retry, become ambiguous, reconcile later, or fail independently from the current subscription state.

It owns conceptually:

- immutable platform-charge identity and Tenant/Subscription reference;
- charge reason/period/accepted commercial-term reference;
- amount and currency when SaaS charging is approved;
- CommerceOS invoice/reference identifier where applicable;
- external billing-provider customer/subscription/invoice/payment references when a provider exists;
- charge attempts/observations needed for idempotency and reconciliation;
- the outcome CommerceOS currently knows, including an explicit unknown/ambiguous condition;
- traceability back to the subscription period/change that caused the charge.

A `PlatformCharge` is **not** the existing merchant-order `Payment` aggregate.

## 4. Subscription lifecycle semantics

The domain must not collapse subscription commercial state, charge/payment outcome, TenantStatus, or MembershipStatus into one enum.

### 4.1 Stable commercial conditions

The baseline needs only these stable meanings before the remaining human policy is resolved:

- `PendingActivation` — a subscription intent/record exists but no normal entitlement set has yet become effective;
- `Active` — one accepted commercial term set is effective for the Tenant now;
- `Ended` — no current normal subscription term set remains effective under the accepted end/expiry policy.

`Trialing`, `PastDue`, `Grace`, `SuspendedForBilling`, `PendingCancellation`, `Reactivated`, and similar labels are **not** adopted as unconditional states by this baseline. They are introduced only after the corresponding human decisions approve their business meaning.

### 4.2 Orthogonal lifecycle dimensions

Plan change is modeled separately from commercial subscription condition:

```text
Active Subscription
      │
      └─ PlanChangeIntent
            ├─ Requested
            ├─ BlockedByUsage / RemediationRequired
            └─ Scheduled      only if approved timing policy requires it
                  ↓
              PlanChanged
```

Cancellation intent is also separate:

```text
Active Subscription
      │
      └─ CancellationRequested
                  ↓
          Ended at approved effective time
```

A cancellation request does not by itself prove the subscription ended. A billing failure or unknown billing outcome does not by itself prove the subscription ended. A Tenant suspension does not by itself cancel the subscription. A Membership disablement does not change the subscription.

### 4.3 Trial, grace, delinquency, and reactivation

If the product owner approves a trial, the trial is modeled as an explicit subscription phase/period with accepted trial terms and an effective EntitlementSet. It is never modeled as a TenantStatus or authentication state.

If the product owner approves delinquency/grace behavior, billing standing and any resulting commercial restriction remain explicit and evidence-based. Timeout/missing callback alone cannot create `PastDue`, suspension, cancellation, or failure semantics.

`PD-043` and `PD-049` are required before these flows become implementation-ready.

## 5. Entitlement semantics and invariants

Other contexts must consume entitlement meaning, not marketing plan names.

Conceptually the domain answers:

```text
May Tenant X use capability Y now?
What limit applies to resource/usage Z now?
Which accepted terms produced this decision?
When did/will the current value become effective?
```

Invariants:

1. entitlement authority is always tenant-scoped and evaluated from trusted Tenant context; client-supplied plan names, limits, claims, or cached browser state are never authority;
2. every effective entitlement decision has provenance to an accepted Subscription and commercial term set;
3. changing marketing plan labels/prices does not rewrite historical entitlement truth;
4. a requested upgrade/downgrade does not change entitlements until the approved effective condition occurs;
5. removed entitlements do not delete the affected domain's historical business data;
6. a domain write governed by a hard limit must be evaluated against a current authoritative entitlement and authoritative local usage/state at the business command boundary; a stale Reporting/UI projection cannot authorize it;
7. inability to establish a trusted current entitlement for a protected paid capability is not silently treated as an unlimited grant;
8. `Unlimited` is explicit policy, not the absence of a record;
9. entitlement evaluation does not replace TenantStatus or Membership authorization; an operation may require all applicable independent conditions to be satisfied.

Exact hard/soft/overage behavior is `PD-050`.

## 6. Upgrade and downgrade invariants

### Upgrade

`RequestPlanChange` is an intent, not evidence that higher entitlements or a billing effect already occurred.

Until `PD-047` resolves timing/proration prerequisites:

- no upgrade request grants higher entitlements immediately by assumption;
- any required PlatformCharge outcome remains separate evidence;
- once the approved effective condition is satisfied, one new immutable EntitlementSet becomes effective and the prior set remains historical;
- retry/duplicate plan-change intent cannot create duplicate commercial or charge effects.

### Downgrade

Downgrade must preserve existing domain-owned business data and invariants.

Before a lower-limit change becomes effective, Subscription & Billing must be able to compare target hard limits against required authoritative usage evidence from the owning contexts or an approved metered UsageMeter.

Safe interim rule while `PD-048` remains unresolved:

```text
current authoritative usage > target hard limit
              ↓
    downgrade does not become effective
              ↓
BlockedByUsage / RemediationRequired
              ↓
human-approved policy or merchant remediation
              ↓
only then may lower terms become effective
```

Subscription & Billing must **not** silently delete Products, disable Memberships, remove Warehouses, erase source snapshots, mutate Orders, or rewrite Accounting history to make the tenant fit a lower plan.

If future policy requires remediation in another context, Subscription & Billing may request that context to attempt an approved action, but only the owning context can accept and report that action's fact. A request is never treated as proof that the resource was removed/deactivated.

## 7. Platform billing truth and uncertainty

CommerceOS SaaS billing remains a separate business responsibility from merchant shopper-order Payments.

```text
Shopper ──pays merchant order──► Payments
Merchant ──pays CommerceOS plan──► Subscription & Billing / PlatformCharge
```

Rules:

1. a PlatformCharge has one logical effect for the same charge identity/period/reason;
2. retries cannot create duplicate charges or duplicate subscription transitions;
3. external provider callbacks/records are evidence, not Subscription truth;
4. only verified evidence may establish a definitive provider payment outcome;
5. timeout, network error, missing callback, or caller cancellation creates/retains an unknown observation where commit status cannot be proven;
6. unknown billing outcome is not converted into success, failure, delinquency, cancellation, or Tenant suspension merely because time passes;
7. subscription effects caused by billing outcome require an explicit approved product rule and a verified business fact;
8. provider references are retained for traceability without making provider-private state authoritative for other CommerceOS domains;
9. no real card data or secrets belong in domain fixtures or business history.

Candidate PlatformCharge outcome facts may include:

- `PlatformChargeRecorded`;
- `PlatformChargeOutcomeBecameUnknown`;
- `PlatformChargeSettled`;
- `PlatformChargeDefinitivelyNotSettled` when verified evidence proves no commit under the selected provider semantics;
- `PlatformChargeReconciled` when later evidence resolves a prior unknown observation.

Exact invoice/tax/proration/provider semantics remain `PD-046` and `PD-052`.

## 8. Cross-domain interactions

This section defines business ownership and required semantics only. It does not select synchronous calls, events, caches, databases, or workflows.

| Context | Subscription & Billing responsibility | Existing context remains authoritative for | Safe interaction rule / pending policy |
|---|---|---|---|
| Tenant Management | associate a subscription with an existing Tenant and expose commercial eligibility/entitlements | Tenant identity, BusinessProfile, Active/Suspended TenantStatus | TenantStatus and subscription state are independent. Whether a Tenant may exist/use ordinary commerce without an Active subscription is `PD-043`; billing delinquency must not mutate TenantStatus by implication (`PD-049`). |
| Merchant Access | provide staff-related entitlement such as `MaxActiveStaff` and target-plan limit during downgrade analysis | Invitation, Membership identity/status/role, active-member count | Merchant Access enforces its own Membership invariants. Subscription & Billing never disables members to force compliance. Enforcement timing/mode is `PD-050`; downgrade excess behavior is `PD-048`. |
| Inventory | provide Warehouse/location-related entitlement/target limit | Warehouse/Location identity/status and all stock truth | Inventory decides whether its own create/activate command is eligible after trusted entitlement evaluation. Existing Warehouses are never deleted by plan change. `PD-048`/`PD-050`. |
| Product Data Ingestion | provide capability entitlement for scheduled/automated ingestion and any approved quota | DataSource policy, source/run/snapshot/candidate truth | Entitlement removal does not delete snapshots/candidates or bypass source-policy rules. Whether running/scheduled work is stopped, allowed to finish, or merely prevents new starts is governed by `PD-050`. |
| Sales | own plan-defined order-volume entitlement/usage-meter policy when approved | SalesOrder truth and accepted checkout | Sales facts may contribute idempotently to a UsageMeter. Until `PD-051`, an order-volume threshold must not silently reject an otherwise valid shopper checkout. |
| Payments | none for SaaS billing; remain completely separate | shopper/order payment obligations, attempts, captures/refunds | Existing `Payment` and Mock Payment Provider must not become SaaS-billing source of truth by convenience. |
| Accounting | keep CommerceOS platform charge truth outside the merchant's operational ledger | merchant chart, journals, ledger and financial reports | A merchant may later choose to record a CommerceOS subscription expense only through an explicit Accounting integration/product rule; SaaS charge is not a merchant Journal by implication. |
| Audit | identify privileged subscription/plan/billing actions that require evidence | append-oriented actor/action/outcome evidence | Audit records do not own Subscription state. Exact platform-admin mutation authority is `PD-053`; existing audit coverage policy `PD-033` still applies. |
| Reporting | provide authoritative source facts/queries for subscription/usage/billing projections | rebuildable dashboards and aggregate views | Projection lag cannot grant entitlement or change billing truth. |
| Platform administration/support | expose business commands/visibility from this context | no new transactional source of truth | Support UI/projection does not authorize direct state mutation. Manual plan/charge/entitlement override authority requires `PD-053`. |

### Independent eligibility dimensions

A protected tenant operation can be blocked for different reasons that must stay distinguishable:

```text
Authenticated identity
      +
Active Merchant Access authority
      +
TenantStatus allows operation
      +
Subscription entitlement allows capability/limit
      +
owning domain invariant accepts the command
```

Failure in one dimension does not rewrite the others.

## 9. Commands, queries, and business facts

Names below are semantic candidates for domain refinement/technical contracts; they are not API or message schemas.

### Commands

Plan/commercial catalog:

- `DefineCommercialPlanVersion`
- `MakeCommercialPlanVersionAvailable`
- `RetireCommercialPlanVersion`

Subscription:

- `StartSubscription`
- `ActivateSubscription`
- `RequestPlanChange`
- `ConfirmPlanChangeEffective`
- `RequestCancellation`
- `EndSubscription`
- `StartTrial` only if `PD-043` approves a trial
- `ReactivateSubscription` only if `PD-049` defines it

Usage/billing:

- `RecordMeteredUsage`
- `RecordPlatformCharge`
- `RecordPlatformBillingEvidence`
- `ReconcilePlatformChargeOutcome`

### Queries/decisions

- `GetCurrentSubscription`
- `GetEffectiveEntitlements`
- `EvaluateCapability`
- `GetEffectiveLimit`
- `GetUsageStatus`
- `GetSubscriptionHistory`
- `GetPlatformBillingHistory`

### Candidate owned business facts

- `SubscriptionStarted` / `SubscriptionActivated`
- `TrialStarted`, `TrialExpired` only after trial policy is approved
- `PlanChangeRequested`
- `PlanChangeBlockedByUsage`
- `PlanChangeScheduled` only if approved timing policy needs a scheduled state
- `SubscriptionPlanChanged`
- `CancellationRequested`
- `SubscriptionEnded`
- `SubscriptionReactivated` only if approved
- `EffectiveEntitlementsChanged`
- `UsageRecorded`
- `UsageLimitReached` / `UsageLimitExceeded` only where approved policy gives those occurrences business meaning
- `PlatformChargeRecorded`
- `PlatformChargeOutcomeBecameUnknown`
- `PlatformChargeSettled`
- `PlatformChargeDefinitivelyNotSettled`
- `PlatformChargeReconciled`

`PlanChangeRequested` is not `SubscriptionPlanChanged`. `CancellationRequested` is not `SubscriptionEnded`. `PlatformChargeSettled` is not automatically `SubscriptionActivated`. `EffectiveEntitlementsChanged` means a new effective entitlement snapshot was accepted, not merely that a plan record was edited.

## 10. Business error semantics

Domain-level meanings include:

- `SUBSCRIPTION_REQUIRED_OR_INACTIVE` — the requested subscription-governed capability has no applicable effective subscription under approved policy;
- `ENTITLEMENT_NOT_GRANTED` — capability is not present/enabled in the current effective EntitlementSet;
- `ENTITLEMENT_LIMIT_REACHED` — a hard limit selected by approved policy rejects the requested increase;
- `PLAN_CHANGE_NOT_ALLOWED` — current subscription/target terms do not allow the requested transition under approved policy;
- `DOWNGRADE_BLOCKED_BY_USAGE` — current authoritative usage exceeds a target hard limit and the safe interim/approved policy prevents effectivity;
- `SUBSCRIPTION_CHANGE_ALREADY_APPLIED` — equivalent retry returns/references the previous logical result;
- `SUBSCRIPTION_CHANGE_CONFLICT` — the same logical intent identity is reused for incompatible target terms;
- `PLATFORM_CHARGE_OUTCOME_UNKNOWN` — CommerceOS cannot yet prove whether the independent billing boundary committed;
- `PLATFORM_CHARGE_OPERATION_CONFLICT` — billing operation identity is reused incompatibly;
- `PLATFORM_BILLING_EVIDENCE_INVALID` — evidence cannot be verified/matched to the expected charge/terms.

Transport mapping belongs to Technical Architecture.

## 11. Human product decisions and safe planning gates

TASK-0091 records the material unresolved policy in `product-decisions.md`:

- `PD-043` — acquisition/trial/tenant-without-subscription policy;
- `PD-044` — plan catalog/version/accepted-terms policy;
- `PD-045` — monthly/annual billing-cycle and period policy;
- `PD-046` — SaaS currency, tax, invoice, proration policy;
- `PD-047` — upgrade effective-time policy;
- `PD-048` — downgrade effective-time and excess-resource remediation policy;
- `PD-049` — cancellation, grace, delinquency, suspension, reactivation, retention policy;
- `PD-050` — hard/soft/overage/unlimited entitlement-limit behavior;
- `PD-051` — order-volume limit behavior and shopper-checkout impact;
- `PD-052` — billing-provider strategy for learning/MVP versus later real SaaS;
- `PD-053` — platform-admin subscription/billing override/support authority.

These decisions do not prevent the bounded context, aggregate ownership, entitlement semantics, historical-truth invariants, and safe downgrade rule from being modeled now. They **do** prevent downstream implementation tasks from becoming Ready when those tasks require the unresolved behavior.

## 12. Technical Architecture handoff — TASK-0092

TASK-0092 must reconcile this domain extension into the accepted technical baseline without changing its business meaning.

It must decide, at minimum:

1. how this one bounded context maps to implementation module/project boundaries and dependencies;
2. how trusted TenantContext reaches subscription and entitlement decisions without accepting client plan/limit authority;
3. how another domain obtains a current entitlement/capability/limit decision and how a hard-limit write revalidates against authoritative local usage;
4. persistence ownership and access patterns for Plan/version terms, Subscription/history, immutable effective EntitlementSets, optional UsageMeter, and PlatformCharge evidence;
5. transaction/consistency boundaries inside each aggregate, especially plan-change idempotency, EntitlementSet effectivity, UsageMeter duplicate counting, and PlatformCharge ambiguity;
6. technical interaction choices with Tenant Management, Merchant Access, Inventory, Product Data Ingestion, Sales, Audit, Reporting, and platform administration while preserving no-cross-domain persistence access;
7. how downgrade usage evidence/remediation is coordinated without destructive distributed writes or pretending one cross-domain transaction exists;
8. how effective-entitlement changes become visible consistently enough for command authorization, including stale/unknown-state behavior;
9. the external SaaS billing adapter seam, idempotency, verified evidence, timeout/unknown outcome, duplicate/out-of-order callback handling, and reconciliation path **only if `PD-052` places provider execution in scope**;
10. security/audit requirements for privileged plan/subscription actions and any platform-admin path, preserving `PD-033` and `PD-053`;
11. reliability/observability requirements for ambiguous billing and partially completed plan changes;
12. any material architecture/ADR changes required by these needs, without preselecting AWS services or persistence in this domain task.

TASK-0092 must preserve all `PD-043`–`PD-053` alternatives until the human resolves them. A technical mechanism may not become a hidden product decision.

## 13. Backlog Planner handoff

After TASK-0092, TASK-0089 must ensure that:

- Subscription & Billing work is represented in canonical Backlog V2;
- implementation tasks are split by safe dependency/frontier rather than by every noun in this document;
- no task requiring unresolved `PD-043`–`PD-053` behavior is marked Ready;
- existing merchant Payments/Accounting tasks are not repurposed as SaaS billing work;
- entitlement-enforced Merchant Access/Inventory/Ingestion/Sales tasks depend on the appropriate subscription/entitlement prerequisites only when the approved product policy actually requires them;
- downgrade/remediation work cannot instruct a Builder to delete/deactivate foreign-domain data as a shortcut;
- platform billing can remain provider-deferred while domain modeling/plan/subscription/entitlement work proceeds if the approved product strategy allows that sequence.

**Stop condition: DOMAIN BASELINE EXTENDED.**
