# TASK-0091 — Extend domain baseline for Subscription & Billing

Status: Completed
Specification maturity: Completed
Owner: Domain Architect
Recommended model: Strong reasoning model
Created: 2026-08-10
Completed: 2026-08-10
Depends on: completed TASK-0087, `docs/08-subscription-billing-product-scope.md`
Execution gate satisfied: TASK-0088 was completed/merged before TASK-0091 execution.

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

The execution gate was satisfied before work began: the TASK-0088 architecture baseline had already been marked Completed on `main`. TASK-0091 therefore extended the business baseline without changing an architecture baseline underneath an active Technical Architect run.

TASK-0091 did not use TASK-0088 as authority for business meaning.

## Required reads completed

- `AGENTS.md`
- `README.md`
- `docs/development/15-planning-factory-and-task-maturity.md`
- `docs/agents/domain-architect.md`
- `docs/00-product-definition.md`
- `docs/08-subscription-billing-product-scope.md`
- `docs/02-business-domains.md`
- `docs/domains/tenant-identity.md`
- `docs/domains/commerce-operations.md`
- `docs/domains/product-decisions.md`
- `docs/01-non-functional-requirements.md`
- `docs/development/03-architecture-rules.md`
- completed TASK-0088 status for scheduling/impact awareness only
- `tasks/backlog/TASK-0092-subscription-billing-technical-architecture-reconciliation.md` for handoff alignment

## Domain conclusions

### Bounded-context boundary

CommerceOS starts with **one `Subscription & Billing` bounded context**.

This is the smallest coherent current boundary for the merchant's commercial relationship with CommerceOS. It keeps Plan/commercial terms, Subscription lifecycle, effective Entitlements, approved UsageMeter truth, and PlatformCharge evidence under one language while preserving internal aggregate separation. It does not split contexts merely to imitate microservices.

The baseline explicitly permits a later business-driven split if independent teams, scale, regulation, lifecycle, or product evolution justify it.

### Source-of-truth ownership

Subscription & Billing owns:

- merchant subscription identity/lifecycle;
- accepted plan/version/commercial terms for an effective period;
- plan-change and cancellation intent/history;
- effective EntitlementSet history and provenance;
- accumulated UsageMeter truth where an approved metered entitlement requires it;
- CommerceOS SaaS PlatformCharge truth and external billing evidence/references when introduced.

It does not own:

- Tenant identity/status/profile;
- Memberships/roles/active staff truth;
- Warehouses/locations/stock;
- Sales Orders;
- shopper/order Payments or the Mock Payment Provider;
- merchant Accounting journals/ledger;
- Product Data Ingestion snapshots/runs;
- Audit evidence or Reporting projections.

### Aggregates and consistency concepts

The refined business model uses:

- `Plan` aggregate with versioned/accepted commercial terms;
- `Subscription` aggregate for the Tenant's current base CommerceOS commercial relationship;
- immutable effective `EntitlementSet` snapshots/value with provenance and effective interval;
- optional `UsageMeter` aggregate only for accumulated metered limits that require duplicate-safe source counting;
- separate `PlatformCharge` aggregate because SaaS billing attempts/ambiguity/reconciliation can evolve independently from Subscription state.

These are business consistency boundaries, not table/service decisions.

### Lifecycle dimensions

The stable subscription conditions before human policy is resolved are:

- `PendingActivation`;
- `Active`;
- `Ended`.

Plan-change intent and cancellation intent are orthogonal state dimensions. Trial, Grace, PastDue, delinquency suspension, reactivation, and similar labels are not adopted unless the human product owner defines their business semantics.

Subscription condition, PlatformCharge outcome, TenantStatus, and MembershipStatus are explicitly independent.

### Upgrade/downgrade invariants

- `PlanChangeRequested` never means the plan/EntitlementSet changed.
- Higher entitlements become effective only when the human-approved upgrade condition occurs.
- Downgrade never silently deletes/disables foreign-domain business data.
- Safe interim downgrade rule: when authoritative current usage exceeds a target **hard** limit, downgrade does not become effective and remains `BlockedByUsage` / remediation-required until approved policy/remediation makes it valid.
- Any remediation in another domain is a request to that owner; only that context can accept the resource/membership/location change.

### Entitlement semantics

Other contexts consume trusted capability/limit meaning rather than marketing plan-name checks.

The domain can answer:

- may this Tenant use capability X now?;
- which limit applies to resource/usage Y?;
- which Subscription/accepted terms produced the decision?;
- when did/will the entitlement become effective?;
- which historical EntitlementSet was effective earlier?

A hard-limit write requires current trusted entitlement plus authoritative owning-domain usage/state. UI/Reporting projections cannot authorize the write. `Unlimited` is explicit policy, never a missing value.

### SaaS billing versus merchant Payments

```text
Shopper  ── pays merchant order ──► Payments
Merchant ── pays CommerceOS plan ─► Subscription & Billing / PlatformCharge
```

Existing merchant-order Payments and Mock Payment Provider remain unchanged in business ownership. PlatformCharge uses separate business identity/outcome/evidence semantics. External SaaS billing provider evidence is external evidence; timeout/missing callback remains unknown when commit cannot be proven and is not converted to failure/delinquency/cancellation by time passage.

## Human product decisions surfaced

TASK-0091 added the following unresolved decisions with mandatory safe interim constraints:

- `PD-043` — subscription acquisition, trial, and Tenant-without-subscription policy;
- `PD-044` — plan catalog, versioning, accepted terms, commercial packages;
- `PD-045` — monthly/annual billing-cycle and effective-period policy;
- `PD-046` — CommerceOS SaaS currency, tax, invoice, and proration policy;
- `PD-047` — upgrade effective-time and charge-precondition policy;
- `PD-048` — downgrade timing and excess-resource remediation policy;
- `PD-049` — cancellation, expiry, grace, delinquency, reactivation, suspension, retention policy;
- `PD-050` — hard/soft/overage/unlimited limit and enforcement policy;
- `PD-051` — order-volume meter and shopper-checkout impact;
- `PD-052` — SaaS billing-provider strategy for learning/MVP versus later real operation;
- `PD-053` — platform-admin subscription/billing support and override authority.

No common SaaS convention was treated as human approval.

## Repository outputs

1. `docs/02-business-domains.md`
   - canonical bounded-context map now includes Subscription & Billing;
   - responsibility/source-of-truth tables distinguish SaaS subscription billing from merchant Payments/Accounting;
   - cross-cutting subscription/entitlement/history/downgrade invariants are explicit;
   - first-frontier planning explicitly avoids silently coupling Tenant onboarding to trial/default-plan behavior.
2. `docs/domains/subscription-billing.md`
   - new detailed domain baseline covering ownership, aggregates, lifecycle dimensions, entitlements, usage, PlatformCharge, cross-domain interactions, facts/errors, human decisions, and downstream handoffs.
3. `docs/domains/product-decisions.md`
   - added `PD-043` through `PD-053` with decision gates and safe interim constraints.
4. `docs/domains/tenant-identity.md`
   - TenantStatus/MembershipStatus are reconciled with the new Subscription boundary;
   - staff-count entitlement interaction preserves Merchant Access ownership/last-owner invariants;
   - subscription state cannot implicitly suspend Tenant or disable Memberships.
5. Technical Architecture handoff is explicit in `docs/domains/subscription-billing.md` for TASK-0092.

No application code, AWS service choice, persistence model/key/schema, HTTP/API contract, CDK/IaC, or real billing provider was introduced.

## Acceptance criteria

### AC01 — New capability has explicit ownership: PASS

The canonical bounded-context map now identifies Subscription & Billing as the owner of merchant CommerceOS subscription/commercial-term/entitlement/platform-charge truth and explains why it is one initial bounded context.

### AC02 — SaaS billing is not confused with shopper Payments: PASS

`PlatformCharge` / merchant-to-CommerceOS SaaS billing is explicitly separate from the existing shopper/order `Payment` and Mock Payment Provider responsibility.

### AC03 — Entitlements are modeled independently of marketing plan-name checks: PASS

Effective immutable EntitlementSets carry capability/limit values, provenance, and effective intervals. Downstream domains consume trusted capability/limit decisions and must not scatter checks such as `if plan == Growth`.

### AC04 — Upgrade/downgrade semantics preserve business data: PASS

A plan-change request is not an effective change. When target hard limits are below current authoritative usage, the safe interim rule blocks downgrade effectivity and requires remediation/product policy; no foreign-domain data may be silently deleted/disabled/rewritten.

### AC05 — Lifecycle dimensions are not conflated: PASS

Subscription commercial condition, PlatformCharge/billing outcome, TenantStatus, MembershipStatus, plan-change intent, and cancellation intent are documented as independent dimensions.

### AC06 — Human decisions are explicit: PASS

All material unresolved Subscription/Billing product policies were added as `PD-043`–`PD-053` with decision gates and safe interim constraints rather than inferred defaults.

### AC07 — Existing contexts remain authoritative for their own truth: PASS

Merchant Access owns Memberships/staff count, Inventory owns Warehouses/stock, Sales owns Orders, Ingestion owns source/run/snapshot truth, Payments owns shopper/order payments, Accounting owns merchant books, Audit owns evidence, and Reporting owns projections. Subscription & Billing owns only the commercial subscription/entitlement/usage-meter/platform-charge policy/truth required by its boundary.

### AC08 — Technical handoff is actionable: PASS

`docs/domains/subscription-billing.md` includes a dedicated TASK-0092 handoff covering module mapping, trusted entitlement boundary, persistence/access needs to be decided technically, aggregate consistency/idempotency, cross-domain interactions, downgrade coordination, external-provider seam, security/audit, reliability/observability, and ADR triggers without prescribing the technical solution.

## Documentary/adversarial verification

- Product-scope trace against `docs/08-subscription-billing-product-scope.md`: PASS — acquisition/trial, plan/version, subscription period, upgrade/downgrade, cancellation/delinquency, entitlement, usage, PlatformCharge, provider uncertainty, and all listed human decision areas are owned or explicitly gated.
- AWS/persistence/API contamination review: PASS — domain artifacts do not select Lambda/API Gateway/EventBridge/SQS/Step Functions, databases/tables/keys/indexes, caching/token mechanisms, HTTP contracts, or a billing provider.
- Historical-truth review: PASS — plan edits/changes cannot rewrite prior EntitlementSets, Orders, Membership history, Inventory, source snapshots, or merchant Accounting truth.
- Downgrade destructive-behavior review: PASS — safe interim block/remediation rule prohibits silent foreign-domain deletion/deactivation.
- Tenant/security review: PASS — entitlement authority requires trusted Tenant context; client plan/entitlement/limit claims and stale projections are never authority.
- Merchant Payments separation review: PASS — existing Payments/Mock Payment Provider remain merchant-order contexts and cannot be reused as SaaS billing owners by convenience.
- Human-decision coverage review: PASS — `PD-043` through `PD-053` cover all ten product-scope decision areas plus platform-admin override authority.
- Cloud verification: N/A — documentation/domain task; no AWS resource was created and no teardown is required.
- `python3 scripts/harness_check.py`: **not executed in this connector-only session** because there is no runnable repository checkout available to the session. Repository-level harness status is therefore not represented as green by TASK-0091; the next runnable checkout should execute the harness before treating repository verification as passing.

## Architecture, security, and cost implications

- Architecture: TASK-0091 changes business-domain authority only. TASK-0092 must reconcile implementation module/contracts/persistence/integration choices afterward.
- Security/tenant: trusted Tenant context remains mandatory; Subscription/Entitlement does not replace authentication/Membership authorization or TenantStatus.
- Reliability: duplicate logical plan-change/usage/PlatformCharge effects are forbidden; external billing uncertainty remains explicit and requires later technical reconciliation if provider execution enters scope.
- Cost: zero runtime/cloud cost from TASK-0091; no AWS service or paid billing platform was selected.

## Follow-up handoff

1. **TASK-0092 — Technical Architect** must reconcile this extended domain baseline into module/contracts/persistence/integration/security/reliability/AWS architecture without resolving `PD-043`–`PD-053` through technical convenience.
2. **TASK-0089 — Backlog Planner** must then generate/reconcile canonical Backlog V2 and keep Subscription/Billing implementation work non-Ready whenever a required PD remains unresolved.
3. **Human Product Owner** must resolve `PD-043`–`PD-053` selectively before dependent implementation tasks can pass their Ready gate. Domain modeling can proceed safely without guessing those policies.

**Stop condition: DOMAIN BASELINE EXTENDED.**
