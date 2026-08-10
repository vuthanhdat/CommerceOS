# Product-Decision → Domain-Baseline Reconciliation

_Date: 2026-08-10_

## 1. Purpose

This document records the Domain Architect propagation of the human-approved/delegated decisions in [`product-decisions.md`](product-decisions.md) into the canonical CommerceOS business-domain baseline.

The decision register remains authoritative for the **decision text, rationale, approver, and decision status**. This document is authoritative for whether those decisions have been propagated into Domain Architect artifacts.

Some individual decision entries still contain historical text such as `Affected baseline documents updated: Pending Domain Architect reconciliation.` Those propagation markers are superseded by this reconciliation record; they do **not** mean the product decision itself is unresolved.

This pass changes business/domain documentation only. It introduces no application code, AWS choice, persistence technology/schema, transport, API schema, or deployment decision.

## 2. Reconciliation status

| Decision group | Domain artifacts reconciled | Domain status |
|---|---|---|
| `PD-001`–`PD-004`, `PD-033`, `PD-034`, `PD-036` | `tenant-identity.md`, `02-business-domains.md` | Reconciled; only deferred scope in `PD-004` remains gated |
| `PD-002`, `PD-003`, `PD-005`–`PD-010`, `PD-037`, `PD-040` | `catalog.md`, `02-business-domains.md` | Reconciled |
| `PD-011`–`PD-032`, `PD-035`, `PD-038`, `PD-039`, `PD-041`, `PD-042` | `commerce-operations.md`, `02-business-domains.md` | Reconciled; `PD-023` refund approval/return/accounting policy is resolved and propagated |
| `PD-043`–`PD-053` | `subscription-billing.md`, `tenant-identity.md`, `02-business-domains.md` | Reconciled; only exact commercial plan catalog/pricing in `PD-044` remains gated |

The product-decision register currently contains **no** entry with status `HUMAN PRODUCT DECISION REQUIRED`. The only intentionally deferred product-policy areas are `PD-004` and the exact commercial-catalog portion of `PD-044`.

## 3. Remaining human-decision gates and resolved refund policy

### `PD-004` — Tenant suspension detail, closure, retention, privacy lifecycle

Approved interim domain policy:

- Suspended denies ordinary merchant mutations and public commerce.
- Suspension does not delete Tenant data, Memberships, Subscription, Orders, accounting history, or other evidence.
- Reactivation does not silently reactivate independent Membership/Subscription state.

Still requires human product decision before implementation needs:

- exact Suspended read/support policy beyond the safe interim rule;
- Tenant closure/deletion;
- retention/recovery windows;
- privacy/legal erasure semantics.

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

`PD-023` is no longer a human-product-decision gate. Technical Architecture and Backlog Planning still need to propagate the newly approved workflow/contracts before affected tasks become Ready.

### `PD-044` — Exact sellable SaaS plan catalog/pricing

Approved structural domain policy:

- stable Plan identity;
- immutable accepted PlanVersion/terms;
- accepted versions never edited in place;
- withdrawal from new sale never rewrites existing Subscription history;
- `Enterprise`/custom pricing is out of MVP.

Still requires human product decision before implementation needs exact `Starter`/`Growth`/`Business` prices and entitlement/limit packages.

## 4. Major baseline changes produced by the decision pass

### Tenant & Merchant Access

- one authenticated identity may belong to multiple Tenants;
- explicit Tenant selection is required when more than one eligible Membership exists;
- Membership has exactly one MVP role: `Owner`, `Admin`, `Staff`, or `Viewer`;
- multiple Owners are supported and the last Active Owner invariant is mandatory;
- registration is open self-service for verified email and creates initial Owner;
- successful onboarding also requires a 30-day Trial Subscription outcome;
- Invitations bind to verified normalized email, expire after 7 days, are single-use, and resend rotates the credential.

### Catalog

- VND-only Money, zero-price Product allowed;
- SKU optional in Draft, required before first publication, case-insensitive Tenant uniqueness, immutable after first publication, never reused after Archive;
- Published Product edits are live; Published may Archive directly; Archive is terminal;
- public Tenant-scoped slug is mutable and unique, with no redirect requirement;
- zero/one flat Category and zero/one Brand, non-destructive retirement;
- public media uses merchant-managed uploads only;
- Product specifications/public fields are now explicit;
- external-source mapping and ImportCandidate lifecycle are explicit.

### Sales / Inventory / Payments / Procurement / Accounting

- any checkout price change requires shopper reconfirmation;
- whole-unit quantities only;
- no manual discount in public checkout;
- canonical flow is `OrderPlaced → all-line reserve → full capture → OrderConfirmed → OrderAllocated → whole fulfillment`;
- all-or-nothing allocation/fulfillment, no backorder;
- one Payment obligation per Order with multiple attempts;
- definitive decline is attempt-terminal only; `OutcomeUnknown` requires reconciliation and keeps stock held;
- refund is now an explicit `RefundRequested → RefundApproved/RefundRejected` workflow with a dedicated merchant approval experience;
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
- refund approval/rejection and resulting privileged accounting corrections are auditable without Audit becoming refund/accounting source truth.

### Subscription & Billing

- onboarding starts automatic 30-day no-card Trial;
- paid billing is monthly with explicit anchor/month-end behavior;
- SaaS Money is VND-only with no tax/statutory invoice/proration in learning MVP;
- upgrade becomes effective only after verified successful PlatformCharge and starts a fresh monthly period;
- downgrade is next-renewal and blocks on authoritative excess usage without destructive remediation;
- definitive renewal failure creates `PastDue` with 7-day grace; Unknown does not;
- Ended preserves authenticated read/history/export/recovery access and data;
- capability limits, hard resource-growth limits, and soft order-volume warning are distinct;
- SaaS billing uses a dedicated simulated provider seam separate from merchant-order Payments;
- platform-admin Subscription/Billing is read-only support visibility, not override authority.

## 5. Downstream planning state

### Technical Architect reconciliation required

The completed TASK-0092 technical baseline predates the final product-decision propagation and the subsequent resolution of `PD-023`. Therefore its architecture decisions/contracts must be rechecked against the updated domain meaning before affected implementation tasks become Ready.

This is **not** permission for the Domain Architect to choose:

- module/deployment boundaries;
- persistence schema/access mechanisms;
- sync/async transport;
- AWS services;
- API/event wire schemas;
- payment/refund provider implementation details.

The Technical Architect should preserve the business semantics recorded in the detailed domain documents and explicitly reconcile any earlier preserved alternatives that are now closed by product policy, including the refund approval boundary, exactly-once cross-domain effects, and provider-evidence separation.

### Backlog Planner reconciliation required

After Technical Architecture reconciliation, Backlog Planning should:

- remove obsolete product-decision gates for decisions now approved and propagated, including `PD-023`;
- refine only the first safe dependency frontier;
- ensure refund tasks explicitly include the dedicated approval experience and do not treat `RefundRequested` as an automatic refund;
- preserve Sales/Inventory/Payments/Accounting ownership and the `RefundApproved` versus `PaymentRefunded` distinction;
- keep work touching `PD-004` or exact `PD-044` scope non-Ready until its human-decision gate is actually reached/resolved;
- repair tasks whose assumptions conflict with the approved role model, onboarding Trial, Catalog lifecycle, commerce sequence, accounting semantics, refund workflow, or subscription lifecycle.

The Domain Architect does not mark implementation tasks Ready.

## 6. Domain acceptance check

This reconciliation pass satisfies the Domain Architect contract because:

- bounded-context ownership remains explicit;
- authoritative source facts are separated from projections/evidence;
- aggregates/state dimensions/invariants needed by approved decisions are recorded;
- cross-domain effects retain owning-context authority;
- resolved product decisions are no longer left as Builder guesses in the detailed domain baseline;
- refund approval/return/accounting semantics are now explicit without conflating provider refund truth;
- the two intentionally deferred product-policy areas remain explicit rather than guessed;
- downstream Technical Architect and Backlog Planner reconciliation is stated;
- no application/AWS/persistence decision was introduced.

**Stop condition: DOMAIN BASELINE EXTENDED AND RECONCILED.**
