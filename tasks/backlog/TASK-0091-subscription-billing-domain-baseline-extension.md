# TASK-0091 — Extend domain baseline for Subscription & Billing

Status: Completed
Specification maturity: Completed
Owner: Domain Architect
Recommended model: Strong reasoning model
Created: 2026-08-10
Completed: 2026-08-10
Canonical completed record: `../completed/TASK-0091-subscription-billing-domain-baseline-extension.md`
Depends on: completed TASK-0087, `docs/08-subscription-billing-product-scope.md`
Execution gate satisfied: TASK-0088 was completed/merged before TASK-0091 execution.

## Goal

Extend the completed CommerceOS business-domain baseline to incorporate the newly explicit **Subscription & Billing** SaaS capability defined in `docs/08-subscription-billing-product-scope.md`.

Determine the correct business ownership and boundaries for merchant plan/subscription lifecycle, effective entitlements/limits, platform billing evidence, upgrades, downgrades, cancellation/expiry/delinquency semantics, and their interactions with Tenant Management, Merchant Access, Inventory, Accounting, Audit, and platform administration.

This task updates domain knowledge only. It must not implement code, choose AWS services, choose persistence, or select a real billing/payment provider.

## Domain conclusion

CommerceOS starts with **one `Subscription & Billing` bounded context** because Plan/commercial terms, the merchant Subscription, effective Entitlements, approved accumulated UsageMeter truth, and CommerceOS PlatformCharge evidence are one coherent merchant-to-platform commercial language today. They are kept as separate internal business concepts/consistency boundaries so a later business-driven split remains possible without creating premature context/microservice boundaries now.

Source-of-truth ownership is explicit in `docs/02-business-domains.md` and `docs/domains/subscription-billing.md`. Subscription & Billing owns the commercial relationship and entitlement policy/truth but never takes ownership of TenantStatus, Memberships, Warehouses/stock, Orders, merchant-order Payments, merchant Accounting, Ingestion snapshots/runs, Audit evidence, or Reporting projections.

## Core business model

The refined model uses:

- `Plan` aggregate with stable plan identity plus versioned/accepted commercial terms;
- `Subscription` aggregate for the Tenant's current base CommerceOS commercial relationship;
- immutable effective `EntitlementSet` snapshots with provenance/effective interval rather than marketing-plan-name checks;
- optional `UsageMeter` aggregate only for approved accumulated metered limits that require duplicate-safe source counting;
- separate `PlatformCharge` aggregate because SaaS billing attempts, unknown outcomes, and reconciliation can evolve independently from the commercial Subscription state.

These are business consistency concepts only. TASK-0091 does not choose table schemas, databases, keys, module boundaries, transports, or AWS services.

## Lifecycle and invariant conclusions

Stable subscription conditions before unresolved policy is decided are `PendingActivation`, `Active`, and `Ended`. Plan-change intent, cancellation intent, PlatformCharge outcome, TenantStatus, and MembershipStatus are independent dimensions.

Trial, Grace, PastDue, delinquency suspension, reactivation, proration, and similar business meanings are not invented. They are product-decision gated.

Key invariants:

- `PlanChangeRequested` is not `SubscriptionPlanChanged`;
- `CancellationRequested` is not `SubscriptionEnded`;
- a PlatformCharge outcome does not automatically imply a subscription transition;
- client plan/entitlement/limit claims and stale UI/Reporting projections are never entitlement authority;
- every effective entitlement decision has trusted Tenant scope and provenance to accepted Subscription/terms;
- historical EntitlementSets and foreign-domain business history are not rewritten by later plan changes;
- duplicate logical plan-change, usage, or PlatformCharge effects are prohibited;
- external billing timeout/missing callback remains unknown when commit cannot be proven and is not converted into failure/delinquency/cancellation by time passage.

### Safe upgrade/downgrade rule

An upgrade request does not grant higher entitlements until the human-approved effective condition occurs.

For downgrade, the safe interim rule is:

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

Subscription & Billing must never silently delete Products, disable Memberships, remove Warehouses, erase source snapshots, mutate Orders, or rewrite Accounting history to make a tenant fit a lower plan. If remediation requires another domain action, that owning domain must accept it and preserve its own invariants.

## Entitlement semantics

Downstream domains consume trusted capability/limit meaning, not plan names. The domain can answer business questions equivalent to:

- may Tenant X use capability Y now?;
- what limit applies to resource/usage Z?;
- which accepted Subscription/plan terms produced that decision?;
- when did/will the entitlement become effective?;
- which historical EntitlementSet was effective earlier?

A hard-limit write must combine a current trusted entitlement with authoritative owning-domain usage/state. `Unlimited` is explicit policy, never a missing record.

## SaaS billing boundary

```text
Shopper  ── pays merchant order ──► Payments
Merchant ── pays CommerceOS plan ─► Subscription & Billing / PlatformCharge
```

Existing merchant-order `Payments` and the Mock Payment Provider remain unchanged in business ownership. They do not become CommerceOS subscription-billing owners merely because both domains involve money.

## Cross-domain interaction conclusions

- **Tenant Management:** owns Tenant identity/Profile/Active-Suspended status. Subscription state is independent; trial/plan/billing are not Tenant states.
- **Merchant Access:** owns Membership identity/status/roles and active-member count. A staff-count entitlement is only a policy input; Subscription & Billing cannot disable Memberships. Last-owner invariants still apply.
- **Inventory:** owns Warehouse/Location and stock truth. A warehouse limit may gate future create/activate behavior under approved policy but cannot delete existing Warehouses during downgrade.
- **Product Data Ingestion:** owns source policy/run/snapshot/candidate truth. Subscription may govern a capability such as scheduled ingestion but cannot bypass source-policy rules or erase evidence.
- **Sales:** owns Orders. Approved Sales facts may feed an idempotent usage meter, but until `PD-051` an order-volume threshold must not silently reject otherwise valid shopper checkout.
- **Payments:** shopper/order payment only; no SaaS billing ownership.
- **Accounting:** merchant books only; a CommerceOS PlatformCharge does not become a merchant Journal by implication.
- **Audit:** owns append-oriented action evidence, not subscription state.
- **Reporting/platform admin:** projections/support visibility do not authorize entitlement or direct state mutation.

## Human product decisions surfaced

TASK-0091 added these unresolved decisions with explicit safe interim constraints rather than defaults:

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

1. `docs/02-business-domains.md` — canonical map/responsibilities/source-of-truth/invariants extended.
2. `docs/domains/subscription-billing.md` — detailed Subscription & Billing domain baseline.
3. `docs/domains/product-decisions.md` — `PD-043` through `PD-053` decision gates and safe interim constraints.
4. `docs/domains/tenant-identity.md` — Tenant/Membership interaction reconciled with subscription/entitlement semantics, including staff-count/last-owner protection.
5. `docs/domains/subscription-billing.md` contains an explicit **Technical Architecture handoff** for TASK-0092 and a Backlog Planner handoff for TASK-0089.

No application code, frontend/API contract, AWS service choice, persistence schema/key/index, CDK/IaC, or real billing provider was introduced.

## Acceptance criteria

### AC01 — New capability has explicit ownership: PASS

The canonical domain map represents Subscription & Billing and identifies it as owner of merchant CommerceOS subscription/commercial-term/entitlement/platform-charge truth.

### AC02 — SaaS billing is not confused with shopper Payments: PASS

PlatformCharge / merchant-to-CommerceOS SaaS billing is explicitly distinct from shopper/order Payment and the Mock Payment Provider.

### AC03 — Entitlements are modeled independently of marketing plan-name checks: PASS

Immutable effective EntitlementSets carry capability/limit values, provenance, and effective intervals; unrelated domains consume trusted entitlement meaning rather than `if plan == ...` logic.

### AC04 — Upgrade/downgrade semantics preserve business data: PASS

Plan-change request is not effectivity. Downgrade over a target hard limit is blocked/remediation-required and may not destroy or rewrite another domain's state/history.

### AC05 — Lifecycle dimensions are not conflated: PASS

Subscription commercial condition, PlatformCharge/billing outcome, TenantStatus, MembershipStatus, plan-change intent, and cancellation intent are explicitly independent.

### AC06 — Human decisions are explicit: PASS

`PD-043`–`PD-053` record unresolved material policy with safe interim constraints and planning gates.

### AC07 — Existing contexts remain authoritative for their own truth: PASS

Membership, Warehouse/stock, Order, Ingestion, shopper Payment, merchant Accounting, Audit, and Reporting ownership remains with the existing contexts while Subscription & Billing owns only commercial subscription/entitlement/approved metering/platform-charge truth.

### AC08 — Technical handoff is actionable: PASS

The detailed domain document tells TASK-0092 exactly which module/contracts/persistence/integration/security/reliability/provider-seam questions must be resolved technically without prescribing their solutions or rediscovering basic business semantics.

## Documentary/adversarial verification

- Product-scope trace against `docs/08-subscription-billing-product-scope.md`: PASS — acquisition/trial, plan/version, period, upgrade/downgrade, cancellation/delinquency, entitlement, usage, PlatformCharge, provider uncertainty, and every material decision area are owned or explicitly gated.
- AWS/persistence/API contamination review: PASS — no cloud service, database/table/key/index, caching/token mechanism, API schema, or billing provider was selected to answer a product-policy question.
- Historical-truth review: PASS — later plan edits/changes cannot rewrite prior EntitlementSets, Orders, Membership history, Inventory, source snapshots, or merchant Accounting truth.
- Downgrade destructive-behavior review: PASS — safe interim rule prohibits silent foreign-domain deletion/deactivation.
- Tenant/security review: PASS — entitlement authority uses trusted Tenant context; client plan/entitlement/limit claims and stale projections are never authority.
- Merchant Payments separation review: PASS — merchant-order Payments/Mock Payment Provider remain separate business responsibilities.
- Human-decision coverage review: PASS — `PD-043` through `PD-053` cover all ten product-scope decision areas plus platform-admin override authority.
- Cloud verification: N/A — documentation/domain task; no AWS resource was created and no teardown is required.
- `python3 scripts/harness_check.py`: **not executed in this connector-only session** because there is no runnable repository checkout available to the session. Repository-level harness status is not represented as green by TASK-0091; the next runnable checkout should execute the harness before repository verification is treated as passing.

## Architecture, security, and cost implications

- Architecture: business-domain authority changed; TASK-0092 must perform the technical reconciliation afterward.
- Security/tenant: trusted Tenant context remains mandatory; Subscription entitlement does not replace authentication/Membership authority or TenantStatus.
- Reliability: duplicate logical plan-change/usage/PlatformCharge effects are forbidden; external billing uncertainty remains explicit.
- Cost: zero runtime/cloud cost from TASK-0091; no AWS service or paid billing platform was selected.

## Follow-up handoff

1. **TASK-0092 — Technical Architect**: reconcile this extended domain baseline into module/contracts/persistence/integration/security/reliability/AWS architecture while preserving `PD-043`–`PD-053` as product gates.
2. **TASK-0089 — Backlog Planner**: after TASK-0092, reconcile canonical Backlog V2 and keep Subscription/Billing implementation work non-Ready wherever a required PD remains unresolved.
3. **Human Product Owner**: resolve `PD-043`–`PD-053` selectively before dependent implementation tasks can pass the Ready gate.

**Stop condition: DOMAIN BASELINE EXTENDED.**
