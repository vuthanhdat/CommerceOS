# TASK-0091 — Extend domain baseline for Subscription & Billing

Status: Backlog
Specification maturity: Ready
Execution permission: NO while TASK-0088 is actively being authored; execute after the current TASK-0088 work is completed/merged so the active architecture task is not chasing a moving domain baseline
Owner: Domain Architect
Recommended model: Strong reasoning model
Created: 2026-08-10
Depends on: completed TASK-0087, `docs/08-subscription-billing-product-scope.md`

## Goal

Extend the completed CommerceOS business-domain baseline to incorporate the newly explicit **Subscription & Billing** SaaS capability defined in `docs/08-subscription-billing-product-scope.md`.

Determine the correct business ownership and boundaries for merchant plan/subscription lifecycle, effective entitlements/limits, platform billing evidence, upgrades, downgrades, cancellation/expiry/delinquency semantics, and their interactions with Tenant Management, Merchant Access, Inventory, Accounting, Audit, and platform administration.

This task updates domain knowledge only. It must not implement code, choose AWS services, choose persistence, or select a real billing/payment provider.

## Why this task exists after TASK-0087

TASK-0087 was correct for the product scope known at the time, but the product definition previously contained only a vague `plan metadata` placeholder. CommerceOS is itself a SaaS product, so the merchant's relationship with CommerceOS needs explicit domain modeling.

A completed baseline is not immutable when approved product scope grows.

```text
Product scope addendum
        ↓
TASK-0091 Domain Architect
        ↓
extended business-domain baseline
        ↓
TASK-0092 Technical Architect reconciliation
        ↓
TASK-0089 canonical Backlog V2
```

## Scheduling constraint

This task is a **business-domain** task and does not depend semantically on TASK-0088.

However, TASK-0088 is currently being authored against the pre-Subscription baseline. To avoid changing the source domain baseline underneath an active Technical Architect run:

1. finish/merge the currently active TASK-0088 work first;
2. run TASK-0091 from latest `main`;
3. then run TASK-0092 to reconcile the new domain capability into the technical architecture.

TASK-0091 must not read TASK-0088 as authority for business meaning.

## Required reads

- `AGENTS.md`
- `docs/agents/domain-architect.md`
- `docs/00-product-definition.md`
- `docs/08-subscription-billing-product-scope.md`
- `docs/02-business-domains.md`
- `docs/domains/tenant-identity.md`
- `docs/domains/commerce-operations.md`
- `docs/domains/product-decisions.md`
- relevant NFR/security/tenant-isolation requirements

Read technical architecture only for impact awareness if useful; do not let it dictate business ownership.

## Required domain questions

### 1. Bounded-context boundary

Determine whether the initial product should model:

- one `Subscription & Billing` bounded context;
- separate Plan Catalog / Subscription / Billing contexts;
- or another explicitly justified boundary.

Prefer the smallest coherent business boundary that preserves ownership and can evolve later. Do not split only to imitate microservices.

### 2. Source-of-truth ownership

Explicitly define who owns:

- merchant subscription identity/lifecycle;
- commercial plan/version accepted by the merchant;
- effective subscription period;
- trial state, if approved;
- upgrade/downgrade/cancellation intent and accepted state;
- effective entitlements and limits;
- usage evidence needed for limits;
- CommerceOS SaaS charge/invoice/payment references;
- external billing-provider references/evidence if a provider is introduced later.

Explicitly distinguish these truths from:

- Tenant lifecycle;
- Merchant Access memberships;
- merchant warehouses/locations;
- merchant shopper/order Payments;
- merchant bookkeeping Accounting;
- Audit records;
- Reporting projections.

### 3. Aggregates/entities/value objects

Refine enough business model to make Technical Architecture possible. Candidate concepts to analyze include, without assuming they are all separate aggregates:

- Plan / PlanVersion / Offer;
- Subscription;
- SubscriptionPeriod;
- Entitlement / EntitlementSet;
- UsageCounter / UsageWindow;
- PlatformCharge / InvoiceReference / BillingAttempt reference;
- Money/price terms;
- effective dates and lifecycle reasons.

Choose aggregate boundaries by consistency/invariants, not by nouns or database tables.

### 4. Subscription lifecycle

Model the business states/transitions required by approved scope or explicitly record decisions still required.

Candidate lifecycle concerns include:

```text
Trial / PendingActivation
        ↓
Active
        ↓
PendingPlanChange
        ↓
Active on new terms

Active
  ├─ PastDue / Grace?    (only if approved)
  ├─ PendingCancellation
  ├─ Cancelled / Expired
  └─ Reactivated?        (only if approved)
```

Do not adopt these exact states without reasoning from product policy.

State semantics must not conflate:

- commercial subscription status;
- external charge/payment outcome;
- tenant platform lifecycle/suspension;
- individual membership authorization.

### 5. Upgrade and downgrade invariants

Specify what the domain must know/preserve when plan capability changes.

At minimum address the semantic problem:

```text
Current usage > target plan limit
        ↓
downgrade cannot silently delete/corrupt business state
```

Define whether the domain owns a pending downgrade/remediation state or delegates a request/evidence relationship to affected contexts.

Do not invent the final excess-resource policy if it is a human product decision; add it to the decision register and preserve a safe interim constraint.

### 6. Entitlement semantics

Define the business meaning of effective entitlements so other contexts do not scatter plan-name checks.

Domain design should support questions equivalent to:

- may this tenant use capability X now?;
- what limit applies to resource/usage Y?;
- which subscription/plan terms produced this entitlement?;
- when does a changed entitlement become effective?;
- how is historical business truth preserved after the plan changes?

Do not define HTTP middleware, caching, tokens/claims, database keys, or service calls here.

### 7. Cross-domain interactions

Explicitly analyze at least:

- Tenant Management — tenant existence/lifecycle versus subscription commercial eligibility;
- Merchant Access — membership ownership versus staff-count entitlement;
- Inventory — warehouse/location ownership versus warehouse-count entitlement;
- Product Data Ingestion — scheduled/crawler capability entitlement;
- Accounting — merchant books versus CommerceOS's own SaaS charge/billing truth;
- Payments — shopper/order payment versus merchant-to-CommerceOS subscription charge;
- Audit — evidence of privileged subscription/plan actions;
- Reporting/platform admin — projections/support visibility only.

For each interaction, distinguish request, accepted business fact, projection, and policy decision where relevant.

### 8. Business events/facts

Define semantic candidate facts only after ownership is clear. Examples to evaluate, rename, split, or reject include:

- SubscriptionStarted
- TrialStarted
- TrialExpired
- PlanChangeRequested
- SubscriptionUpgraded
- DowngradeScheduled
- SubscriptionCancelled
- SubscriptionExpired
- EntitlementsChanged
- PlatformChargeRecorded
- SubscriptionPaymentOutcomeChanged

Do not publish schemas or select transports in this task.

### 9. Human product decisions

Update `docs/domains/product-decisions.md` with every unresolved material policy question discovered.

At minimum assess the ten decision areas listed in `docs/08-subscription-billing-product-scope.md`:

- trial policy;
- plan/version policy;
- monthly/annual cycle;
- SaaS billing currency/tax/invoice/proration policy;
- upgrade timing;
- downgrade/excess-resource policy;
- cancellation/grace/delinquency/reactivation/retention policy;
- hard versus soft limits/overage;
- order-volume limit behavior;
- billing-provider strategy for learning/MVP versus real SaaS.

Do not resolve them by common industry convention.

## Required repository outputs

At minimum:

1. update `docs/02-business-domains.md` so Subscription & Billing is represented in the canonical bounded-context map, responsibility table, source-of-truth rules, invariants/runway, and first/later planning consequences as appropriate;
2. create `docs/domains/subscription-billing.md` (or an equally explicit domain-local document if the final boundary naming differs);
3. update `docs/domains/product-decisions.md` with unresolved material Subscription/Billing decisions;
4. update any existing domain document only where the new capability materially changes its responsibility/interactions;
5. include a **Technical Architecture handoff** section identifying exactly what TASK-0092 must resolve without prescribing the technical solution.

## Out of scope

- application code;
- frontend screens;
- APIs/HTTP contracts;
- DynamoDB tables/keys/indexes;
- Lambda/API Gateway/EventBridge/SQS/Step Functions choices;
- CDK/IaC;
- choosing Stripe, Paddle, Lemon Squeezy, AWS Marketplace, or another provider;
- implementing real-money billing;
- setting final commercial prices for Starter/Growth/Business;
- changing merchant order-payment semantics owned by the existing Payments context;
- modifying TASK-0088 output to make it fit the new domain.

## Acceptance criteria

### AC01 — New capability has explicit ownership

Given `docs/08-subscription-billing-product-scope.md`
when TASK-0091 completes
then the canonical domain map explicitly represents where merchant Subscription & Billing business truth is owned and why.

### AC02 — SaaS billing is not confused with shopper Payments

Given CommerceOS processes merchant-order payment simulations and also needs merchant subscription billing
when the domain baseline is extended
then these are explicitly separate business responsibilities with no accidental shared source of truth.

### AC03 — Entitlements are modeled independently of marketing plan-name checks

Given plans may change names/prices/versions over time
when entitlement semantics are documented
then downstream domains can reason about effective capabilities/limits without requiring scattered `if plan == ...` business logic.

### AC04 — Upgrade/downgrade semantics preserve business data

Given a tenant can request a plan change
when the target plan has lower limits than current usage
then the domain baseline explicitly prevents silent destructive handling and identifies the approved rule or human decision needed.

### AC05 — Lifecycle dimensions are not conflated

Given subscription, provider billing outcome, tenant lifecycle, and membership access can change independently
when states/invariants are documented
then one dimension does not falsely imply another.

### AC06 — Human decisions are explicit

Given product policy is incomplete
when TASK-0091 completes
then unresolved material choices are recorded in the product-decision register with safe interim constraints and planning gates rather than being invented.

### AC07 — Existing contexts remain authoritative for their own truth

Given Subscription & Billing introduces limits affecting staff, warehouses, ingestion, accounting, or other areas
when boundaries are documented
then it owns subscription/entitlement policy while existing contexts continue to own membership, inventory/location, operational commerce, and bookkeeping truth.

### AC08 — Technical handoff is actionable

Given the domain baseline is extended
when TASK-0091 completes
then TASK-0092 can identify module/contracts/persistence/integration/security/reliability design work without needing to rediscover basic business semantics.

## Test/review plan

This is a planning/domain task, so verification is documentary and adversarial:

- trace every product requirement in `docs/08-subscription-billing-product-scope.md` to domain ownership or an explicit deferred decision;
- verify no AWS/persistence/API mechanism is used to resolve a business-policy question;
- verify plan changes cannot silently rewrite historical commerce/accounting truth;
- verify cross-tenant entitlement authority cannot come from client input;
- verify shopper/order Payments and CommerceOS subscription billing remain distinct;
- verify all unresolved material policy choices appear in `product-decisions.md`.

Cloud verification: No.

## Stop conditions

- `DOMAIN BASELINE EXTENDED` — ownership, state/invariants, interactions, unresolved decisions, and Technical Architect handoff are complete;
- `HUMAN PRODUCT DECISION REQUIRED` — a decision is required before even a safe domain boundary/invariant can be established; record it and identify whether the remaining safe domain work can still proceed.
