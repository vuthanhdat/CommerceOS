# TASK-0092 — Reconcile Subscription & Billing into technical architecture

Status: Backlog
Specification maturity: Refined
Execution permission: NO — blocked until TASK-0088 is completed/approved and TASK-0091 has extended the domain baseline
Owner: Technical Architect
Recommended model: Strong reasoning model
Created: 2026-08-10
Depends on: completed/approved TASK-0088, completed/approved TASK-0091

## Goal

Reconcile the Subscription & Billing domain extension produced by TASK-0091 into the accepted CommerceOS technical architecture baseline without rewriting unrelated architecture or implementing business features.

Resolve the technical boundaries needed so the Backlog Planner can later generate canonical Backlog V2 with Subscription & Billing coverage that does not force Builders to invent module ownership, persistence, integration, entitlement enforcement, security, reliability, or external billing-provider boundaries.

## Context

TASK-0088 is already in progress against the domain baseline produced by TASK-0087. Rather than changing the active architecture task underneath its author, CommerceOS intentionally finishes TASK-0088 first, extends the domain baseline through TASK-0091, and then uses this task as a focused reconciliation pass.

```text
TASK-0088 current architecture baseline
        +
TASK-0091 Subscription & Billing domain extension
        ↓
TASK-0092 technical reconciliation
        ↓
TASK-0089 canonical Backlog V2
```

## Required reads

- `AGENTS.md`
- `docs/agents/technical-architect.md`
- completed TASK-0088 outputs and accepted ADRs
- completed TASK-0091 outputs
- `docs/08-subscription-billing-product-scope.md`
- `docs/02-business-domains.md`
- `docs/domains/subscription-billing.md` or the final equivalent created by TASK-0091
- `docs/domains/product-decisions.md`
- current Phase 0 skeleton and architecture rules

Business meaning from TASK-0091 is authoritative. Do not use technical convenience to rewrite subscription semantics.

## In scope

### 1. Module/project ownership

Define how the accepted Subscription & Billing bounded context maps into repository modules/layers while preserving:

- Domain independence from AWS/framework/persistence implementation;
- explicit ownership of subscription/entitlement state;
- no direct cross-domain persistence access;
- no leakage of plan-name checks into unrelated modules.

Do not create modules in code in this task.

### 2. Trusted entitlement decision boundary

Define how protected application use cases obtain a trusted effective entitlement/limit decision.

At minimum resolve:

- trusted TenantContext relationship;
- where entitlement evaluation belongs;
- whether immediate write-path checks are synchronous;
- how stale projections/caches may or may not be used;
- how a command revalidates a hard limit at the authoritative boundary;
- failure/error semantics for entitlement denied, limit reached, stale/unknown subscription state, and temporary external billing ambiguity.

Client-supplied plan/entitlement claims are never authority.

### 3. Persistence ownership and access patterns

Based on TASK-0091 business access/consistency needs, define near-term persistence ownership/access patterns for:

- plan/version/offer data if in initial scope;
- tenant subscription lifecycle;
- effective plan/entitlement state;
- usage/limit evidence where required;
- platform charge/billing-provider references where required;
- idempotency/reconciliation records for external billing interactions if introduced.

Do not invent speculative single-table complexity. Preserve module ownership and known access patterns.

### 4. Integration matrix

Define technical interactions with at least:

- Tenant Management;
- Merchant Access;
- Inventory/location creation or activation where plan limits apply;
- Product Data Ingestion capability gating;
- platform administration/support;
- Audit/Reporting;
- external billing/payment provider boundary if/when introduced.

For each interaction, choose synchronous/application call versus durable asynchronous fact only after considering the business semantics from TASK-0091.

Do not publish events without named consumers/use cases.

### 5. Plan-change consistency

Resolve the technical strategy required to implement safe plan changes without distributed transactions or destructive cross-domain writes.

At minimum address:

- immediate upgrade entitlement changes if product policy allows them;
- scheduled/pending downgrade state;
- validation of excess staff/warehouse/other usage;
- eventual consistency and recovery when multiple domain projections must react;
- idempotency if a plan-change request or external billing callback is delivered more than once;
- how failure/reconciliation remains observable.

Do not choose business policy where TASK-0091 left a PD entry unresolved.

### 6. External billing-provider boundary

If real or simulated CommerceOS SaaS billing is within the approved near-term scope, define the adapter/boundary pattern without choosing a provider unless the product owner already approved one.

Preserve:

- request idempotency;
- timeout/unknown-outcome semantics;
- verified callback/webhook handling;
- duplicate/out-of-order delivery tolerance;
- reconciliation/status query path;
- provider reference mapping;
- no storage of prohibited payment secrets/card data.

If provider execution is deferred, document the seam and keep implementation tasks appropriately Outline/blocked.

### 7. AWS mapping and cost

Map approved technical needs to AWS services only where needed for near-term tasks, under existing cost/free-tier guardrails.

Do not introduce a new always-on service, NAT, relational database, or paid platform solely for Subscription & Billing without an ADR and explicit cost rationale.

### 8. ADR and baseline updates

Update existing architecture documents where Subscription & Billing materially extends them, and create a focused ADR only when a durable architecture choice is material enough to require one.

Do not rewrite unrelated accepted architecture just to make the documents look uniform.

## Required outputs

At minimum:

1. Subscription & Billing module/dependency mapping in the technical baseline;
2. trusted entitlement enforcement/decision boundary;
3. persistence ownership/access-pattern additions;
4. integration matrix additions;
5. reliability/idempotency/reconciliation strategy for plan changes and external billing boundary;
6. AWS/CDK implications for near-term work or explicit deferral;
7. ADR(s) only if required;
8. explicit handoff to TASK-0089 listing which Subscription/Billing work is sufficiently resolved for V2 task generation and which remains blocked by product decisions.

## Out of scope

- application/business implementation;
- changing TASK-0091 business semantics;
- setting commercial prices;
- choosing unresolved product policy;
- silently resolving PD entries;
- modifying the merchant-order Payments bounded context into a SaaS billing owner;
- deploying AWS resources;
- generating the canonical Backlog V2 itself.

## Acceptance criteria

### AC01 — Domain extension is represented technically

Given TASK-0091 is complete
when TASK-0092 completes
then accepted technical architecture explicitly represents Subscription & Billing without erasing its business boundary.

### AC02 — Trusted entitlement checks are implementable

Given another module needs to enforce an entitlement or hard limit
when the technical design is read
then a Builder can identify the trusted decision path and cannot satisfy the check from client-supplied plan metadata.

### AC03 — Persistence ownership is explicit

Given subscription/entitlement/billing state must be stored
when access patterns are documented
then ownership and cross-domain read/write rules are clear enough that no Builder needs direct access to another module's private persistence.

### AC04 — Plan change does not require unsafe distributed mutation

Given upgrade/downgrade affects capabilities across domains
when architecture is documented
then orchestration/interaction/reconciliation preserves domain ownership and failure visibility without pretending one distributed transaction can atomically rewrite all domains.

### AC05 — External billing uncertainty is safe

Given an external provider call or callback may timeout, duplicate, or arrive out of order
when the boundary is defined
then unknown outcome, idempotency, verification, and reconciliation are explicit rather than treated as ordinary failure.

### AC06 — Cost remains bounded

Given CommerceOS is still cost-sensitive/serverless
when Subscription & Billing architecture is added
then no unnecessary always-on infrastructure is introduced and any material new AWS service has an explicit rationale/ADR where required.

### AC07 — Backlog handoff is explicit

Given TASK-0089 must generate a clean V2 task graph
when TASK-0092 completes
then it can trace Product → Domain → Architecture for Subscription & Billing and distinguish Ready-able work from PD-blocked work.

## Verification

- architecture/document review against TASK-0091 domain outputs;
- verify no cross-domain persistence ownership violation;
- verify tenant/entitlement authority is trusted and server-side;
- verify provider timeout/duplicate/reconciliation semantics if provider boundary is in scope;
- verify cost guardrails;
- verify no implementation code or AWS deployment occurs.

Cloud verification: No.

## Stop conditions

- `TECHNICAL BASELINE RECONCILED` — architecture artifacts are sufficient for TASK-0089 V2 generation;
- `PRODUCT DECISION BLOCKED` — a TASK-0091 PD entry prevents a specific technical choice; preserve alternatives and identify exactly which downstream tasks must remain non-Ready.
