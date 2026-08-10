# CommerceOS — Subscription & Billing Product Scope

_Status: product-scope addendum for domain refinement. Created 2026-08-10._

## 1. Why this document exists

CommerceOS is a multi-tenant SaaS product, but the current product/domain baseline only mentions tenant `plan metadata` and does not yet model how a merchant acquires, changes, pays for, or loses access to a CommerceOS subscription.

This document makes **Subscription & Billing** an explicit product capability so that Domain Architect and later Technical Architect work cannot silently omit it.

This is a product/domain input, not an implementation design. It deliberately does not choose AWS services, persistence, payment providers, or billing vendors.

## 2. Product intent

A merchant should be able to:

- register a CommerceOS tenant/business;
- start an approved trial or select an available CommerceOS plan;
- see the current plan/subscription status and effective billing period;
- upgrade to a higher plan;
- request downgrade to a lower plan;
- cancel renewal / cancel the subscription according to approved policy;
- understand which capabilities and limits are currently available;
- receive clear handling when a downgrade would violate current usage limits;
- retain safe access/recovery behavior during any approved grace, past-due, suspended, or cancelled state;
- view enough subscription/billing history to understand plan changes and platform charges.

The product must distinguish **CommerceOS SaaS billing** from the merchant's own shopper payments.

```text
Shopper ──pays for merchant order──► Merchant commerce Payments

Merchant ──pays for CommerceOS plan──► Subscription & Billing
```

The existing `Payments` bounded context remains about the merchant's commercial order/payment truth. It must not become the owner of CommerceOS subscription billing merely because both involve money.

## 3. Candidate commercial packaging

Current product discussion uses three candidate merchant packages:

- `Starter`
- `Growth`
- `Business`

An `Enterprise`/custom package may exist later.

Current price ideas discussed are **provisional commercial hypotheses**, not accepted domain constants. Domain design must model plan identity, versioning, pricing/terms, and entitlements without hard-coding today's marketing names or prices into unrelated business domains.

Likely differentiation axes include:

- staff/member allowance;
- warehouse/location allowance;
- order-volume allowance or soft threshold;
- audit-history retention;
- product-ingestion/automation capabilities;
- accounting/reporting capability depth;
- API/webhook access;
- other future entitlements.

A pricing package is a commercial policy. Other domains should not contain scattered checks such as `if plan == Growth`.

## 4. Entitlement intent

CommerceOS needs a stable way to answer questions such as:

```text
May Tenant X use capability Y now?
What is Tenant X's current limit for resource Z?
What usage counts toward that limit?
What happens when the limit is reached or exceeded?
```

The domain model should separate:

- **Plan/catalog definition** — what a sellable CommerceOS package promises;
- **Subscription** — the tenant's commercial relationship and lifecycle;
- **Entitlement** — effective capability/limit derived from the subscription and approved policy;
- **Usage** — measured tenant consumption when a plan limit requires it.

Downstream domains may consume an entitlement decision but should not become the source of truth for plan/subscription state.

## 5. Required lifecycle questions

Domain refinement must model or explicitly defer these behaviors rather than let a Builder invent them:

### Acquisition

- Is registration followed by an automatic trial, explicit plan selection, or another admission path?
- May a tenant exist without an active subscription?
- When does the first entitlement set become effective?

### Trial

- Is a trial supported?
- Trial duration?
- Does trial require a payment method?
- Which plan/entitlements does trial emulate?
- What happens at trial expiry?

### Upgrade

- Is upgrade immediate or next-cycle?
- Is proration required?
- When do higher entitlements become effective?

### Downgrade

Downgrade must not blindly destroy or disable merchant data.

Example:

```text
Business
  current: 8 staff, 4 warehouses
        ↓ request downgrade
Starter
  allows: 3 staff, 1 warehouse
        ↓
PENDING_DOWNGRADE / remediation required
        ↓
merchant resolves or approved policy handles excess usage
        ↓
downgrade effective
```

Domain refinement must define whether excess resources are blocked, grandfathered, read-only, scheduled for later action, or handled by another approved policy.

### Cancellation / expiry / delinquency

- Is cancellation immediate or end-of-cycle?
- Is there a grace period?
- What is readable versus mutable during grace/past-due/suspended/cancelled states?
- What retention/reactivation behavior is promised?
- Subscription cancellation must not silently delete tenant business data.

## 6. Billing boundary

Subscription billing may later use a real external payment/billing provider, but that provider is **not selected by this document**.

The domain must preserve at least these truths:

- the subscription's current lifecycle state;
- the commercial plan/version/terms accepted by the tenant;
- platform charge/invoice/payment references needed for traceability;
- unknown/external payment outcomes must not be converted into success or failure without evidence;
- retries must not create duplicate subscription charges or duplicate lifecycle effects;
- provider records are external evidence, not the CommerceOS subscription source of truth.

If the project continues to avoid real money initially, the domain may still be modeled while billing-provider execution is deferred or simulated.

## 7. Entitlement enforcement rules

The product should prefer capability/limit decisions over plan-name checks.

Conceptually:

```text
Subscription
     ↓
Effective Entitlements
     ↓
Capability / Limit Decision
     ↓
Tenant operation
```

Examples:

- `CanUseAdvancedAccounting`
- `CanUseScheduledIngestion`
- `MaxActiveStaff`
- `MaxWarehouses`
- `ApiAccessEnabled`

The names above are examples, not approved published contracts.

Rules to preserve:

- entitlement checks use trusted Tenant context;
- client-supplied plan/entitlement claims are never authority;
- historical business records are not rewritten when a plan changes;
- a plan downgrade must not corrupt invariants in Merchant Access, Inventory, Catalog, Accounting, or other domains;
- limits that affect writes must be revalidated at the authoritative command boundary;
- reporting/UI projections may show usage, but projections do not authorize an over-limit write.

## 8. Relationship to existing bounded contexts

Expected domain-analysis questions include:

- Should `Subscription & Billing` be one bounded context initially or split later into Plan Catalog / Subscription / Billing?
- Does Tenant Management only reference subscription state, or is tenant activation itself gated by subscription policy?
- Merchant Access owns memberships; Subscription & Billing may limit active members but must not own membership identity/lifecycle.
- Inventory owns warehouses/locations; Subscription & Billing may limit creation/activation but must not own stock/location business truth.
- Accounting for the merchant's books must remain separate from CommerceOS's own SaaS revenue/accounting concerns.
- Audit may record subscription/plan administration actions but does not own subscription state.
- Platform administration needs visibility/support actions without becoming the business source of truth.

The Domain Architect must decide ownership and interaction semantics before Technical Architecture chooses mechanisms.

## 9. Human product decisions required

The Domain Architect must add unresolved material questions to `docs/domains/product-decisions.md` instead of guessing. At minimum review:

1. trial existence, duration, and expiry behavior;
2. exact plan catalog and whether plan versions are immutable once subscribed;
3. monthly/annual billing support;
4. currency, taxes, invoicing, and proration policy for CommerceOS SaaS charges;
5. upgrade effective-time policy;
6. downgrade effective-time and excess-resource policy;
7. cancellation, grace, delinquency, suspension, reactivation, and retention policy;
8. which limits are hard, soft-warning, grace/overage, or operationally unlimited;
9. whether order-volume limits may ever block shopper checkout (default assumption is **do not silently break shopper checkout** until product owner explicitly approves another behavior);
10. billing-provider strategy for the learning/MVP phase versus later real SaaS operation.

## 10. Planning consequence

The completed TASK-0087 baseline predates this product capability. Therefore it is no longer sufficient as the complete CommerceOS domain baseline for Backlog V2 generation.

Required planning sequence:

```text
TASK-0088 currently in progress
        ↓ complete/merge current architecture baseline first
TASK-0091 Domain Architect — extend domain baseline for Subscription & Billing
        ↓
TASK-0092 Technical Architect — reconcile Subscription & Billing into architecture
        ↓
TASK-0089 Backlog Planner — generate canonical Backlog V2
```

TASK-0091 must not merely add task names. It must establish ownership, invariants, lifecycle semantics, interactions, and unresolved human decisions so downstream technical design and backlog generation do not guess.
