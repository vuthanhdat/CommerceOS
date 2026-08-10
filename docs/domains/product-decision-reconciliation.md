# Product-Decision → Domain-Baseline Reconciliation

_Date: 2026-08-10_

## 1. Purpose

This document records the Domain Architect propagation of the human-approved/delegated decisions in [`product-decisions.md`](product-decisions.md) into the canonical CommerceOS business-domain baseline.

The decision register remains authoritative for the **decision text, rationale, approver, and decision status**. This document is authoritative for whether those decisions have been propagated into Domain Architect artifacts.

Some individual older decision entries may still contain historical propagation wording such as `Pending Domain Architect reconciliation`; this reconciliation record is the current propagation authority. It does not change the Product Owner's decision text.

This pass changes business/domain documentation only. It introduces no application code, AWS choice, persistence technology/schema, transport, API schema, or deployment decision.

## 2. Reconciliation status

| Decision group | Domain artifacts reconciled | Domain status |
|---|---|---|
| `PD-001`–`PD-004`, `PD-033`, `PD-034`, `PD-036` | `tenant-identity.md`, `02-business-domains.md` | Reconciled; `PD-004` is resolved for MVP with platform-only suspension/reactivation and non-destructive read-only Suspended semantics |
| `PD-002`, `PD-003`, `PD-005`–`PD-010`, `PD-037`, `PD-040` | `catalog.md`, `02-business-domains.md` | Reconciled |
| `PD-011`–`PD-032`, `PD-035`, `PD-038`, `PD-039`, `PD-041`, `PD-042` | `commerce-operations.md`, `02-business-domains.md` | Reconciled; `PD-023` refund approval/return/accounting policy is resolved and propagated |
| `PD-043`–`PD-053` | `subscription-billing.md`, `tenant-identity.md`, `02-business-domains.md` | Reconciled; `PD-044` initial Starter/Growth/Business catalog and Trial terms are resolved and propagated |

The current product-decision register contains **no unresolved or deferred `PD-*` entry for the approved MVP scope**.

## 3. Final follow-up resolutions

### `PD-004` — Tenant suspension, reactivation, and lifecycle — Resolved for MVP

Approved MVP domain policy:

```text
Active ──platform Suspend(reason)──► Suspended
   ▲                                     │
   └────platform Reactivate(reason)──────┘
```

- MVP supports only `Active` and `Suspended` Tenant states.
- Suspension/reactivation are explicit authorized platform-administration actions, not merchant self-service.
- Both require reason + Audit evidence.
- Suspended disables public storefront/checkout and all ordinary merchant mutations.
- Membership records remain unchanged.
- Owner/Admin retain controlled read-only history, operational, billing/support/recovery, and otherwise-authorized Audit visibility.
- Staff/Viewer retain only normal-role read visibility and no operational mutations.
- Platform support may use an explicit privileged read-only investigation path without becoming a Tenant Membership.
- Reactivation restores Tenant eligibility only; Disabled Memberships, Ended Subscriptions, and other independent lifecycle states are not rewritten.
- Tenant closure, hard deletion, automatic retention expiry, and privacy/legal erasure are not supported in MVP.
- Suspended Tenant data is retained indefinitely until a future explicit privacy/retention decision supersedes this rule.

`PD-004` is no longer a current human-decision gate. A future destructive/privacy lifecycle is new product scope rather than a hidden continuation of MVP suspension.

### `PD-023` — Refund approval, return, and accounting correction — Resolved

Approved MVP domain policy:

```text
RefundRequested
      ↓ dedicated merchant refund-approval experience
RefundApproved
      ├────► Inventory StockReturned
      ├────► Accounting revenue compensating journal
      ├────► Accounting COGS reversal after StockReturned
      └────► Payments refund operation
                    ↓
          verified PaymentRefunded
                    ↓
          Accounting Cash settlement
```

- `RefundRequested` alone has no stock/accounting/payment effect.
- The request must be explicitly approved or rejected through a dedicated merchant refund-approval experience.
- `RefundApproved` means the MVP return is accepted as restockable and authorizes exactly one logical `StockReturned` for the approved quantity.
- For an already recognized sale, approval creates a linked revenue compensating journal rather than editing posted history: `Dr Sales Revenue / Cr Customer Deposits`.
- Accepted `StockReturned` reverses the applicable original issue-cost COGS effect: `Dr Inventory / Cr COGS`.
- Approval authorizes the Payments refund operation but is not proof the provider committed it.
- Only verified provider evidence creates `PaymentRefunded`; that fact clears the refund liability against Cash: `Dr Customer Deposits / Cr Cash`.
- `RefundRejected` produces no StockReturned, payment-refund authorization, or refund accounting correction.
- Non-restock refund semantics are outside this MVP policy and require a future explicit product decision if introduced.

### `PD-044` — Initial SaaS Plan catalog and Trial terms — Resolved for MVP

Approved paid monthly catalog:

| Plan | Price/month | MaxActiveMemberships | MaxWarehouses | Scheduled product ingestion | Order-volume warning |
|---|---:|---:|---:|---|---:|
| Starter | 199,000 VND | 3 | 1 | Disabled | 500 |
| Growth | 499,000 VND | 10 | 3 | Enabled | 2,000 |
| Business | 999,000 VND | 30 | 10 | Enabled | 10,000 |

Shared paid-plan policy:

- Catalog, Storefront, Orders/Sales, Inventory, Procurement, Accounting, and Reporting are core capabilities on all three Plans.
- Plans differentiate primarily through resource scale and scheduled automation.
- `MaxActiveMemberships` counts all Active Owner/Admin/Staff/Viewer Memberships.
- `MaxActiveMemberships` and `MaxWarehouses` are hard growth/activation limits; they never auto-delete/disable existing resources.
- order-volume thresholds remain warning-only under `PD-051`; they never block shopper checkout or create overage billing.
- Plan identity is stable; accepted PlanVersion/terms are immutable and future price/limit changes create a new PlanVersion.
- Enterprise/custom pricing is outside MVP.

Approved 30-day Trial terms:

- all core CommerceOS capabilities enabled;
- `MaxActiveMemberships = 3`;
- `MaxWarehouses = 1`;
- scheduled product ingestion enabled;
- order-volume warning threshold = 500;
- Trial is dedicated terms, not a Starter/Growth alias;
- Trial expiry does not silently convert to Starter; a paid Plan must be explicitly accepted.

`PD-044` is no longer a current human-decision gate. Future pricing experiments use new immutable PlanVersions; a materially different commercial strategy requires a new product decision.

## 4. Major baseline changes produced by the decision pass

### Tenant & Merchant Access

- one authenticated identity may belong to multiple Tenants;
- explicit Tenant selection is required when more than one eligible Membership exists;
- Membership has exactly one MVP role: `Owner`, `Admin`, `Staff`, or `Viewer`;
- multiple Owners are supported and the last Active Owner invariant is mandatory;
- registration is open self-service for verified email and creates initial Owner;
- successful onboarding also requires a 30-day Trial Subscription outcome;
- Invitations bind to verified normalized email, expire after 7 days, are single-use, and resend rotates the credential;
- Tenant suspension is now platform-only, reasoned, auditable, non-destructive, and read-only for merchant operations;
- the current plan catalog uses `MaxActiveMemberships` as the Membership growth limit, counting all Active roles.

### Catalog

- VND-only Money, zero-price Product allowed;
- SKU optional in Draft, required before first publication, case-insensitive Tenant uniqueness, immutable after first publication, never reused after Archive;
- Published Product edits are live; Published may Archive directly; Archive is terminal;
- public Tenant-scoped slug is mutable and unique, with no redirect requirement;
- zero/one flat Category and zero/one Brand, non-destructive retirement;
- public media uses merchant-managed uploads only;
- Product specifications/public fields are explicit;
- external-source mapping and ImportCandidate lifecycle are explicit.

### Sales / Inventory / Payments / Procurement / Accounting

- any checkout price change requires shopper reconfirmation;
- whole-unit quantities only;
- no manual discount in public checkout;
- canonical flow is `OrderPlaced → all-line reserve → full capture → OrderConfirmed → OrderAllocated → whole fulfillment`;
- all-or-nothing allocation/fulfillment, no backorder;
- one Payment obligation per Order with multiple attempts;
- definitive decline is attempt-terminal only; `OutcomeUnknown` requires reconciliation and keeps stock held;
- refund is an explicit `RefundRequested → RefundApproved/RefundRejected` workflow with a dedicated merchant approval experience;
- `RefundApproved` authorizes restockable `StockReturned`, linked revenue/COGS compensating entries, and the Payments refund attempt, while `PaymentRefunded` remains verified provider truth;
- Inventory cannot go negative and adjustments cannot consume Reserved stock;
- Procurement submitted/receipt evidence is immutable with explicit correction;
- Accounting uses cash/deposit-at-capture, revenue-at-fulfillment, moving weighted-average valuation, COGS-at-StockIssued, GRNI procurement accounting, explicit journal date semantics, and append-only refund corrections.

### Reporting / Ingestion / Notification / Audit

- operational KPIs have source-fact formulas;
- Tenant IANA timezone defines operational business day;
- source policy is platform-governed with Tenant opt-in;
- Notification read/acknowledgement state is per recipient;
- privileged Audit coverage and Owner/Admin tenant visibility are explicit and non-disclosing cross-Tenant;
- refund approval/rejection and resulting privileged accounting corrections are auditable without Audit becoming refund/accounting source truth;
- Tenant suspension/reactivation reason and outcome are auditable platform actions.

### Subscription & Billing

- onboarding starts automatic 30-day no-card Trial with an approved Trial EntitlementSet;
- initial paid catalog is Starter 199k, Growth 499k, Business 999k VND monthly;
- all paid Plans share core CommerceOS capabilities;
- approved hard growth limits are `MaxActiveMemberships` and `MaxWarehouses`;
- scheduled product ingestion differentiates Starter from Growth/Business while Trial enables it for evaluation;
- order-volume thresholds are soft warning only;
- paid billing is monthly with explicit anchor/month-end behavior;
- SaaS Money is VND-only with no tax/statutory invoice/proration in learning MVP;
- upgrade becomes effective only after verified successful PlatformCharge and starts a fresh monthly period;
- downgrade is next-renewal and blocks on authoritative excess usage without destructive remediation;
- definitive renewal failure creates `PastDue` with 7-day grace; Unknown does not;
- Ended preserves authenticated read/history/export/recovery access and data;
- SaaS billing uses a dedicated simulated provider seam separate from merchant-order Payments;
- platform-admin Subscription/Billing is read-only support visibility, not override authority.

## 5. Downstream planning state

### Technical Architect reconciliation required

The completed TASK-0092 technical baseline predates the final product-decision propagation and the follow-up resolutions of `PD-004`, `PD-023`, and `PD-044`. Therefore its architecture decisions/contracts must be rechecked against the updated domain meaning before affected implementation tasks become Ready.

This is **not** permission for the Domain Architect to choose:

- module/deployment boundaries;
- persistence schema/access mechanisms;
- sync/async transport;
- AWS services;
- API/event wire schemas;
- payment/refund/billing-provider implementation details;
- storage/configuration mechanism for PlanVersions or entitlement matrices.

The Technical Architect should preserve the business semantics recorded in the detailed domain documents and explicitly reconcile earlier alternatives now closed by product policy, including:

- platform-only reasoned Tenant suspension/reactivation and controlled read-only access;
- `MaxActiveMemberships` authority and hard-growth enforcement;
- approved Trial EntitlementSet;
- sellable Starter/Growth/Business PlanVersions/prices;
- scheduled-ingestion entitlement and order-warning semantics;
- refund approval boundary, exactly-once cross-domain effects, and provider-evidence separation.

### Backlog Planner reconciliation required

After Technical Architecture reconciliation, Backlog Planning should:

- remove obsolete product-decision gates for all currently resolved `PD-*` decisions;
- refine only the first safe dependency frontier;
- ensure Tenant suspension/admin tasks use platform-only reasoned actions and preserve read-only/non-destructive semantics;
- ensure Membership tasks use `MaxActiveMemberships`, counting every Active role;
- ensure plan-selection, Trial, entitlement, usage, upgrade/downgrade, and billing tasks use the approved `PD-044` matrix rather than placeholders;
- ensure refund tasks include the dedicated approval experience and do not treat `RefundRequested` as an automatic refund;
- preserve Sales/Inventory/Payments/Accounting ownership and the `RefundApproved` versus `PaymentRefunded` distinction;
- represent future Tenant deletion/privacy, non-restock refunds, Enterprise/custom pricing, and materially new commercial semantics as new product work rather than current hidden assumptions.

The Domain Architect does not mark implementation tasks Ready.

## 6. Domain acceptance check

This reconciliation pass satisfies the Domain Architect contract because:

- bounded-context ownership remains explicit;
- authoritative source facts are separated from projections/evidence;
- aggregates/state dimensions/invariants needed by approved decisions are recorded;
- cross-domain effects retain owning-context authority;
- resolved product decisions are no longer left as Builder guesses in the detailed domain baseline;
- Tenant suspension/read/retention MVP behavior is explicit without inventing destructive privacy lifecycle;
- refund approval/return/accounting semantics are explicit without conflating provider refund truth;
- the initial paid Plan catalog and Trial entitlement matrix are explicit without using plan-name authorization;
- the current register has no unresolved/deferred human product-decision gate for approved MVP scope;
- downstream Technical Architect and Backlog Planner reconciliation is stated;
- no application/AWS/persistence decision was introduced.

**Stop condition: DOMAIN BASELINE EXTENDED AND RECONCILED.**
