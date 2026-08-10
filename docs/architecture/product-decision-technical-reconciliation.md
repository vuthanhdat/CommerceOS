# CommerceOS — Product-Decision Technical Reconciliation

_Reconciled by the Technical Architect on 2026-08-10 after the human product-decision pass and the Domain Architect reconciliation._

## 1. Authority and purpose

This document is the authoritative technical-architecture delta over the TASK-0088/TASK-0092 baseline for product decisions reconciled on 2026-08-10.

Business meaning remains authoritative in:

1. `docs/domains/product-decisions.md`;
2. `docs/02-business-domains.md` and the detailed domain baselines;
3. this document and accepted ADRs for implementation mechanisms only.

Where an older architecture document still labels a now-resolved `PD-*` item as product-gated, this reconciliation supersedes that stale gate. The older architectural mechanism remains valid unless this document explicitly changes it.

The only intentionally deferred product-policy areas in the current domain baseline are:

- `PD-004` — exact Suspended-Tenant read/support behavior and closure/deletion/retention/recovery/privacy semantics;
- `PD-023` — exact refund/return accounting treatment;
- `PD-044` — exact sellable `Starter`/`Growth`/`Business` prices and entitlement/limit packages.

This pass introduces no business feature implementation and deploys no AWS resource.

## 2. Architectural changes caused by the decision pass

The previous baseline preserved alternatives that are now closed by approved business policy. The material technical consequences are:

1. Merchant identity is many-to-many with Tenant Membership, so current tenant discovery/selection needs an authoritative Merchant Access lookup that cannot rely on JWT claims or an eventually consistent GSI.
2. Successful onboarding now requires three accepted outcomes owned by two implementation modules: Active Tenant, Active initial Owner Membership, and a 30-day Trial Subscription/EntitlementSet. It can no longer be described as one Tenancy-only ACID transaction.
3. Catalog lifecycle, SKU, slug, Category/Brand, media, public-field, and import-candidate rules are sufficiently concrete to finalize their near-term access-pattern requirements.
4. The order/payment/allocation business sequence is now approved through `OrderAllocated`; payment `OutcomeUnknown` may require durable waiting/reconciliation while stock remains held. A durable workflow mechanism is now justified for that process.
5. Accounting source triggers are now explicit for capture, fulfillment, stock issue/adjustment, goods receipt, supplier invoice, and supplier payment. Refund/return posting remains blocked by `PD-023`.
6. Subscription/Billing Trial, period, upgrade/downgrade, grace, entitlement categories, order-volume warning, simulated-provider seam, and read-only platform-admin semantics are approved. Only exact commercial packages/prices remain gated by `PD-044`.

## 3. Implementation module boundaries

CommerceOS remains a modular monolith by default. A bounded context is not automatically a deployment unit.

| Implementation module | Business ownership hosted | Initial runtime/deployment decision |
|---|---|---|
| `Platform` | no merchant business truth | shared composition/readiness only |
| `Tenancy` | Tenant Management + Merchant Access | shared `commerce-api`; one module-owned DynamoDB table |
| `Catalog` | Catalog | shared `commerce-api`; one module-owned DynamoDB table |
| `SubscriptionBilling` | Subscription & Billing | shared `commerce-api` for synchronous application surface; worker/provider ingress only when introduced |
| `Storefront` | public tenant experience/address binding/cart composition | shared public API composition when its missing Tenant-address domain decision is resolved |
| `Sales` | Sales & Order Management | shared API plus order workflow task handler composition when introduced |
| `Inventory` | Inventory | shared application module; workflow/task handlers may invoke its application contracts |
| `Payments` | merchant-order Payment | shared application module plus provider adapter/callback/reconciliation handlers |
| `Procurement` | Procurement | shared application module; async consumers/producers as required |
| `Accounting` | merchant Accounting | module-owned table; side-effecting event consumer worker when posting starts |
| `Reporting` | rebuildable projections | async worker when named projections exist |
| `ProductDataIngestion` | source policy/run/snapshot/candidate truth | crawler dispatcher/workers according to source runtime profile |
| `Notification` | delivery/read/acknowledgement truth | async worker and module-owned persistence when introduced |
| `Audit` | append-oriented audit evidence | async append worker plus query surface when introduced |
| `FilesMedia` | merchant-uploaded asset identity/safe metadata | shared API for metadata/upload authorization; S3 object boundary when introduced |
| `Customer`, `Pricing` | their approved bounded contexts | introduced only by a Ready task |
| merchant-order Mock Provider | simulated shopper payment provider | separate external-like application/deployment |
| SaaS Billing Mock Provider | dedicated simulated CommerceOS billing provider | separate external-like application/deployment; never reuse merchant-order Mock Provider |

No module may read/write another module's table, item model, index, stream, key codec, or repository. Cross-module collaboration uses producer-owned application/contracts or versioned integration facts.

## 4. Trusted tenant context after `PD-001` and `PD-003`

### 4.1 Authentication remains identity evidence only

Cognito/API Gateway proves the authenticated subject. Tenant, Membership, role, capability, Subscription, entitlement, and limit claims in a token/client/session are never current business authority.

### 4.2 Tenant discovery and selection

One subject may hold Memberships in multiple Tenants.

Merchant Access owns a strongly consistent subject-to-membership discovery representation in the Tenancy table. It is a module-private authorization lookup, not a second Membership aggregate and not tenant business data.

Conceptual access shape:

```text
PK = SUBJECT#<SubjectId>
SK = MEMBERSHIP#<MembershipId>#TENANT#<TenantId>
```

The record contains only the minimum membership/Tenant references and current Membership-status/revision data needed for discovery. It is updated atomically with the owning Membership change.

Rules:

- no Subject GSI is authority;
- discovery uses a strongly consistent base-table `Query`;
- if the request supplies an explicit selected Tenant, the selector is still untrusted until Merchant Access validates the current Tenant + Membership;
- if no selection is supplied, CommerceOS may auto-select only when discovery yields one candidate and the final current Tenant/Membership validation succeeds;
- when discovery is ambiguous/multiple, return `TENANT_SELECTION_REQUIRED` and require intentional selection;
- a suspended/inactive target fails the final authority check; discovery never bypasses `TenantStatus` or `MembershipStatus`.

This is intentionally conservative: product policy says one eligible Tenant **may** be auto-selected, not that auto-selection is mandatory.

### 4.3 Trusted merchant execution context

Conceptual transport-neutral result:

```text
TrustedTenantContext
  tenantId
  subjectId
  membershipId
  role                 # Owner | Admin | Staff | Viewer
  tenantRevision?
  membershipRevision?
  correlationId
```

The owning application applies the approved domain-specific role policy. A generic client-supplied `capability` never becomes authority. Subscription entitlements remain a separate synchronous authority and are not copied into this context.

Protected mutation eligibility remains the conjunction of:

```text
authenticated identity
+ TrustedTenantContext
+ TenantStatus policy
+ role/domain authorization
+ current Subscription entitlement when applicable
+ owning aggregate invariant
```

Public storefront, onboarding, background-worker, and platform-administration contexts remain separate types. No `bypassTenant`/`isAdmin` flag converts one into another.

## 5. Cross-domain onboarding completion

### 5.1 Business outcome preserved

Completed onboarding means all of these accepted outcomes exist:

```text
Tenancy:
  Active Tenant
  Active initial Owner Membership

SubscriptionBilling:
  30-day Trial Subscription
  Trial EntitlementSet
```

The Trial is not moved into Tenancy and SubscriptionBilling does not write the Tenancy table.

### 5.2 Technical consistency model

Cross-domain DynamoDB ACID is not used. Onboarding uses an idempotent durable application process:

1. delivery establishes `TrustedOnboardingContext` from an authenticated subject whose email is verified;
2. Tenancy atomically commits its module-owned registration outcome: durable onboarding intent/operation, Active Tenant, Active initial Owner, authority lookup, subject-membership discovery link, owner guard, and required source-owned Audit intent when applicable;
3. the application coordinator immediately invokes `SubscriptionBilling.StartTrialSubscription` with the stable onboarding/Tenant identity as the logical source;
4. SubscriptionBilling atomically creates/replays one Trial Subscription + Trial EntitlementSet for that source;
5. the coordinator marks the Tenancy-owned registration operation `Completed` only after Trial acceptance is proven;
6. if the synchronous Trial call is interrupted/unavailable, a durable work-outbox written with the Tenancy registration outcome is relayed to a single-purpose SQS recovery worker; the API exposes the durable registration-operation status rather than claiming success;
7. retry/recovery invokes the same idempotent Trial command; it never creates a second Trial and never deletes a committed Tenant/Owner as compensation.

HTTP guidance:

- return normal synchronous creation success only after all required outcomes are accepted;
- return `202 Accepted` with durable operation identity when Tenancy committed but Trial completion is still being recovered;
- equivalent onboarding retry returns the same logical operation/result;
- incompatible idempotency-key reuse remains conflict.

Until Trial exists, ordinary subscription-governed mutations fail closed because no effective operational entitlement can be established. This is not Tenant suspension or Membership disablement.

This decision is recorded by ADR-009.

## 6. Contract consequences

### Tenancy contracts now final enough for near-term refinement

- `DiscoverMerchantTenants(AuthenticatedPrincipal)` → bounded candidate Tenant/Membership references suitable for tenant selection; not authority for a business command.
- `ResolveTenantAuthority(AuthenticatedPrincipal, RequestedTenantSelection?, RequestMetadata)` → `TrustedTenantContext | AuthorityFailure`.
- onboarding request requires verified authenticated identity plus merchant display name and IANA business timezone; VND is product-wide and is not a selectable onboarding currency.
- Invitation issue/resend/accept/revoke contracts preserve normalized verified-email binding, 7-day expiry, single-use acceptance, and credential rotation.
- Membership commands carry expected revision where lost update is unsafe and must preserve last-Active-Owner.

### Catalog contracts now final enough for near-term refinement

- Draft creation may omit SKU; publication requires Name + SKU + valid VND Money.
- `AssignProductSku` is legal only before first publication; post-publication change returns `SKU_IMMUTABLE_AFTER_PUBLICATION`.
- Product lifecycle supports Draft/Published/Unpublished/Archived with direct Published→Archived and terminal Archived.
- public product lookup uses immutable ProductId plus Tenant-scoped mutable slug after public Tenant addressing is approved.
- Category/Brand are zero-or-one references, flat, tenant-owned, non-destructively retired.
- public media accepts only `FilesMedia` asset references owned by the same trusted Tenant.
- import apply is an explicit Catalog command and `Applied` is acknowledged only after Catalog accepts the canonical mutation.

### Sales/Inventory/Payments contracts

The now-approved purchase-confirmation interaction is:

```text
Sales PlaceOrder
  -> Catalog/Pricing authoritative validation
  -> price changed? reject with reconfirm-required and create no Order
  -> OrderPlaced
  -> Inventory ReserveOrderStock
  -> Payments CaptureOrderPayment
  -> verified PaymentCaptured
  -> Sales ConfirmOrder
  -> Sales MarkOrderAllocated
```

Rules:

- one logical checkout intent creates at most one Order;
- reservation command is source-idempotent by Order/operation identity;
- one Payment obligation exists per Order with immutable attempts;
- definitive decline terminates the attempt only; it does not auto-cancel the Order;
- `OutcomeUnknown` blocks another capture attempt and keeps stock held until provider evidence resolves it;
- time, retry exhaustion, workflow timeout, DLQ placement, or operator impatience never means Payment failure;
- cancellation remains a separate Sales command and may require separate Inventory release/Payments refund effects;
- refund Accounting remains blocked by `PD-023`.

Fulfillment starts only from an explicit approved fulfillment command/interaction. This reconciliation does not invent warehouse/shipping UX or an automatic fulfillment trigger.

### SubscriptionBilling contracts

- `StartTrialSubscription` is an idempotent onboarding-owned source command.
- `EvaluateEntitlement` remains the synchronous authority for subscription-governed mutations.
- upgrade execution requires verified successful PlatformCharge before new terms become effective and starts a fresh monthly period.
- downgrade is scheduled for renewal, revalidates owner-authoritative hard-limit usage, and remains blocked without destructive remediation when usage is too high.
- renewal `OutcomeUnknown` remains Unknown; definitive failure creates PastDue with the approved seven-day grace period.
- `Ended` removes ordinary operational entitlements but does not remove authenticated read/history/export/recovery access.
- order-volume metering consumes `OrderConfirmed` idempotently for the current billing period and is warning-only.
- platform-admin contracts are read-only visibility in MVP.
- exact package price/entitlement matrices remain gated by `PD-044`.

## 7. Persistence ownership and access-pattern updates

ADR-005 remains the physical strategy: one DynamoDB table per implementation module, trusted tenant keying, no application `Scan`, no foreign table access, and no cross-domain DynamoDB transaction.

### 7.1 Tenancy additions

| ID | Use case | Required access/protection |
|---|---|---|
| `TEN-AP-01R` | registration + Owner bootstrap | one Tenancy transaction writes onboarding operation/intent, Tenant, initial Owner, authority lookup, subject discovery link, owner guard, and durable work/audit intents as applicable |
| `TEN-AP-04R` | resolve explicit selected Tenant | transactionally/currently read Tenant + authority record from the selected tenant partition; fail closed |
| `TEN-AP-11R` | discover memberships for tenant selection | strongly consistent subject-partition Query; no GSI/JWT/client authority; final selected Tenant still revalidated |
| `TEN-AP-13` | complete/recover Trial bootstrap | get/update onboarding operation by stable id with expected state/idempotency; never stores Subscription business state as authority |

Membership disable/reactivate/role changes atomically update Membership, tenant authority lookup, subject discovery representation, and owner guard where applicable.

### 7.2 Catalog additions/closures

| ID | Use case | Required access/protection |
|---|---|---|
| `CAT-AP-05R` | assign/change Draft SKU | claim normalized tenant SKU + Product revision atomically; old Draft claim may be released only as allowed by the Product lifecycle; after first publication no SKU mutation path exists |
| `CAT-AP-10R` | publish/unpublish/archive | expected revision + lifecycle condition; first publish fixes SKU; Archive never releases the permanent historical SKU claim |
| `CAT-AP-14` | slug uniqueness/change | tenant-scoped normalized slug claim changed atomically with Product; no redirect record required by MVP |
| `CAT-AP-15` | Category normalized-name uniqueness | tenant-scoped authoritative name claim; GSI is not uniqueness authority |
| `CAT-AP-16` | Brand normalized-name uniqueness | tenant-scoped authoritative name claim; GSI is not uniqueness authority |
| `CAT-AP-17` | media association | Product transaction validates same-Tenant `MediaAssetId` through a FilesMedia application contract; Catalog never reads FilesMedia persistence |
| `CAT-AP-18` | source-product mapping | tenant-scoped claim for one external source-product identity → zero/one Product; apply remains explicit and idempotent |

If Category/Brand rename/retirement implementation needs a rule for historical normalized-name reuse that is not stated by the domain baseline, the task must obtain a domain decision rather than infer claim-release semantics.

### 7.3 Sales

A Sales table is introduced only with a Ready Sales persistence task. Required patterns include:

- Order by trusted Tenant + OrderId;
- checkout-intent/idempotency claim with semantic fingerprint;
- immutable Order-line/total snapshots;
- expected-revision lifecycle writes;
- technical order-process/orchestration reference/state separate from Sales business state;
- outbox facts for named consumers such as `OrderConfirmed` and `OrderFulfilled`.

### 7.4 Inventory

Inventory owns Warehouse, StockItem, StockReservation, StockMovement, and source-idempotency records.

Required properties:

- every quantity mutation uses conditions/transactions that preserve `OnHand >= 0`, `Reserved >= 0`, and `Available >= 0`;
- reserve/release/issue/receive/return/adjust source identities are duplicate-safe;
- adjustment decrease condition prevents `OnHand` falling below `Reserved`;
- `OutcomeUnknown` does not trigger an expiry/release write;
- all-line reservation must expose only an accepted whole-order reservation result.

If an Order can exceed the number of StockItem/reservation actions safely covered by one DynamoDB transaction, a later technical decision must introduce a durable Inventory reservation coordinator/compensation mechanism. Architecture must not invent a product max-order-line limit merely to fit DynamoDB transaction limits.

### 7.5 Payments

Payments owns Payment, immutable PaymentAttempts, provider-operation/evidence records, reconciliation status, idempotency records, and integration outbox.

- Payment amount/currency comes from the accepted Sales obligation;
- provider operation identity is stable across transport retries;
- no second capture attempt starts while the prior attempt is Unknown;
- callback/query evidence is verified/deduplicated and cannot regress known state;
- merchant-order provider references stay inside Payments.

### 7.6 SubscriptionBilling

The existing `SUB-AP-*` patterns remain valid with gates closed as follows:

- current Subscription/Entitlement evaluation is authoritative and strongly/currently read;
- PlanVersion/accepted terms are immutable once accepted;
- Trial creation is now an approved idempotent persistence path;
- monthly period/anchor history is explicit;
- plan-change intent and effectivity are separate records/states;
- PlatformCharge remains a separate aggregate with Unknown/reconciliation evidence;
- `OrderConfirmed` usage meter uses a source-idempotency record and current billing-period bucket;
- provider-evidence inbox and a bounded due-reconciliation index are permitted only when the dedicated simulated provider task introduces the provider execution path;
- platform-admin reads use a separate admin application query and never direct table access.

Exact sellable package/price/entitlement definitions remain absent until `PD-044` is resolved.

### 7.7 Accounting

Accounting owns one table when posting begins. Required access patterns are:

- ChartOfAccounts/control-account records;
- Journal aggregate + immutable lines;
- one logical source-posting claim/inbox keyed by authoritative source fact identity;
- atomic `source claim + balanced Journal + affected Accounting-owned valuation state` where one source requires valuation mutation;
- journal queries by effective date through a documented tenant-scoped index/access pattern;
- General Ledger/Trial Balance derived from Accounting-owned journal truth, never foreign tables.

Approved reliable source routes are:

- `PaymentCaptured` → Cash / Customer Deposits posting;
- `OrderFulfilled` → Customer Deposits / Sales Revenue posting;
- `StockIssued` → COGS / Inventory posting;
- `GoodsReceiptRecorded` → Inventory / GRNI posting using Procurement-owned accepted receipt-cost evidence;
- `SupplierInvoiceRecorded` → GRNI / Accounts Payable (+ approved variance evidence);
- `SupplierPaymentRecorded` → Accounts Payable / Cash;
- `StockAdjusted` → approved Inventory Adjustment gain/loss posting.

`PaymentRefunded`/`StockReturned` do not receive an Accounting consumer until `PD-023` is resolved.

The domain baseline approves moving weighted-average valuation but does not state whether the cost pool is Tenant+Product, Tenant+Product+Warehouse, or another scope. Persistence keying for the valuation position therefore remains `DOMAIN DECISION REQUIRED`; Technical Architecture will not invent that business meaning.

## 8. Synchronous versus asynchronous integration after reconciliation

### Synchronous application contracts

Use synchronous calls inside the shared runtime when the caller cannot truthfully complete without the owner result:

- API → Merchant Access authority resolution;
- protected command → SubscriptionBilling entitlement decision when governed;
- Back Office/API → Catalog commands/queries;
- checkout → Catalog/Pricing final sellability/price validation;
- order workflow task → Inventory reservation command;
- order workflow task → Payments capture/reconciliation command/query;
- order workflow task → Sales confirm/allocation command;
- import application → Catalog apply command;
- platform-admin support → SubscriptionBilling read-only query.

A synchronous call never permits foreign repository access.

### Durable work queue

Use direct SQS for one known retryable worker where fan-out is not the problem:

- onboarding Trial-bootstrap recovery work;
- Product Data Ingestion acquisition work;
- provider callback/reconciliation work when a queue is needed for backpressure.

When a work item must be guaranteed after a source transaction, write a module-owned transactional work-outbox and relay it idempotently to the named queue. Do not perform `commit -> best-effort SendMessage` for a required recovery path.

### Reliable integration facts

Use producer transaction/outbox → DynamoDB Stream relay → EventBridge → consumer-specific SQS/DLQ for committed owner facts with independent side-effecting consumers.

Named routes now approved in principle include:

| Producer fact | Named consumer/effect |
|---|---|
| `OrderConfirmed` | SubscriptionBilling UsageMeter warning count; Reporting projection |
| `PaymentCaptured` | Accounting posting; Sales convergence if capture resolves asynchronously |
| `OrderFulfilled` | Accounting revenue posting; Reporting |
| `StockIssued` | Accounting COGS posting |
| `GoodsReceiptRecorded` | Inventory receipt application; Accounting GRNI/inventory posting |
| `SupplierInvoiceRecorded` | Accounting AP/GRNI posting |
| `SupplierPaymentRecorded` | Accounting cash/AP posting |
| `StockAdjusted` | Accounting adjustment posting |
| approved privileged action/rejection audit intent | Audit append |
| relevant owned facts | Notification/Reporting projections only where a named projection/recipient rule exists |

Every consumer is at-least-once/idempotent and persists its source/inbox identity with its owned effect where possible.

### Audit rejection evidence

For an approved privileged action that is rejected before a business-state transaction, the rejecting owning module persists a standalone durable, idempotent audit-delivery intent in its own table before completing the application result where practical. Audit later appends its evidence through the same reliable delivery boundary. This avoids a best-effort log being mistaken for Audit and avoids synchronous foreign-table writes.

## 9. Order payment/allocation orchestration

The approved sequence plus unbounded-in-time payment uncertainty now creates a demonstrated durable orchestration need.

Use **AWS Step Functions Standard** for the cross-domain order placement/payment/allocation process when that implementation frontier becomes Ready.

Scope of the first state machine:

```text
accepted OrderPlaced
  -> reserve all required stock
  -> start/query merchant-order capture attempt
  -> if Captured: Confirm Order -> verify/mark OrderAllocated
  -> if Declined/NoCommit: retain Order/reservation and expose payment-retry-needed process state
  -> if OutcomeUnknown: durable reconciliation/wait path; never infer failure from timeout
  -> if technical recovery exhausts bounded automatic attempts: NeedsAttention process state; business facts remain unchanged
```

The state machine does **not**:

- create an Order before shopper price reconfirmation when price changed;
- auto-cancel an Order on decline/technical error;
- release stock because time elapsed, retries exhausted, or a workflow entered NeedsAttention;
- call the provider directly; it calls Payments application contracts;
- mutate Inventory/Payments/Sales persistence directly;
- post Accounting;
- encode refund accounting;
- invent automatic fulfillment/shipping behavior.

Workflow execution identity is deterministic from the Sales-owned Order/process identity; duplicate start/continuation is idempotent. Sales keeps a module-owned process reference/status suitable for user support; Step Functions execution history is operational evidence, not Sales business state.

This decision is recorded by ADR-010 and is an explicit specialization of ADR-006's general “Step Functions only with an approved durable sequence” rule.

## 10. AWS and CDK mapping

No service outside the existing approved serverless set is required.

| Need | AWS mapping | Status after reconciliation |
|---|---|---|
| merchant authentication | Cognito + API Gateway HTTP API JWT authorizer | accepted with protected API |
| shared synchronous API/application surface | Lambda `commerce-api` | accepted modular-monolith default |
| module persistence | one DynamoDB table per introduced module | accepted by ADR-005 |
| tenant-selection discovery | Tenancy base-table strongly consistent Query | accepted; no GSI required for authority |
| onboarding Trial recovery | Tenancy transactional work-outbox → Stream relay → SQS + worker Lambda | conditional with onboarding implementation |
| reliable business facts | module outbox → DynamoDB Stream → EventBridge | conditional with first named fact consumer |
| critical side-effect consumers | consumer-specific SQS + DLQ + Lambda | conditional with consumer implementation |
| order payment/allocation durable process | Step Functions Standard + Lambda task handlers | accepted for the named process when Ready; ADR-010 |
| merchant-order provider | separate API Gateway/Lambda/DynamoDB stack/app | conditional with Payments provider task |
| SaaS Billing Mock Provider | separate API Gateway/Lambda/DynamoDB stack/app | conditional; distinct from merchant-order provider |
| provider reconciliation schedule | EventBridge Scheduler | conditional only if bounded periodic inquiry is required |
| media binary storage | private S3 + CloudFront delivery through Web/FilesMedia policy | conditional with FilesMedia/Web task |
| crawler acquisition | SQS/DLQ + Lambda workers; Scheduler only for approved schedules | conditional |
| observability | CloudWatch built-ins/logs/alarms | accepted with bounded retention/low-cardinality policy |
| IaC | AWS CDK/CloudFormation | accepted |

No NAT Gateway, ALB, EC2, RDS/Aurora, Redis/ElastiCache, OpenSearch, Kafka/MSK, EKS, always-on service, or provisioned Lambda concurrency is introduced.

### Step Functions cost guardrail

The order state machine must have a task-level transition budget before deployment. Normal successful flow should remain small; Unknown/reconciliation paths must use bounded operational retry/wait policy and must not create high-frequency polling loops. Learning/dev cloud tests remain low-volume under the repository Free Tier/credit guardrails.

## 11. Architecture decisions and ADR impact

### Existing ADRs retained

- ADR-003 modular runtime/deployment boundaries remains valid.
- ADR-004 trusted Tenant authority remains valid but its historical `PD-001`/`PD-003` gates are closed by this reconciliation; subject membership discovery and one-role context are now concrete.
- ADR-005 module-owned DynamoDB strategy remains valid; onboarding no longer attempts cross-module ACID.
- ADR-006 reliable integration remains valid; it now has named business routes and ADR-010 supplies the first justified Step Functions specialization.
- ADR-007 HTTP/version/idempotency conventions remain valid.
- ADR-008 SubscriptionBilling module/entitlement/provider boundary remains valid; its historical `PD-043`–`PD-053` gate list is superseded except exact commercial packages under `PD-044`.

### New ADRs required by this reconciliation

- ADR-009 — cross-domain onboarding completion and Trial-bootstrap recovery.
- ADR-010 — durable order payment/allocation orchestration with Step Functions Standard.

A later Accounting valuation ADR is required before implementing weighted-average persistence if the domain clarifies the missing cost-pool scope and the resulting model materially affects keys/consistency.

## 12. Remaining non-architecture product/domain gates

This Technical Architect pass intentionally does not invent:

1. `PD-004` exact Suspended read/support and Tenant closure/retention/privacy lifecycle.
2. `PD-023` refund/return Accounting postings.
3. `PD-044` exact sellable package prices and entitlement/limit matrix.
4. Storefront Tenant-address ownership/lifecycle/uniqueness (the refreshed domain baseline still does not define it). Public Tenant route/index/cache-key contracts remain non-final.
5. Accounting moving-weighted-average cost-pool scope. The business rule says moving weighted average but not the aggregation dimension used for the authoritative cost position.
6. Any Category/Brand historical normalized-name reuse behavior not explicitly stated by the domain model.
7. Any product max-order-line constraint. If DynamoDB transaction limits become relevant, Architecture must solve the process without inventing a business maximum.

These are explicit stop conditions for affected work, not defaults.

## 13. Backlog Planner handoff

The Backlog Planner may now remove obsolete product gates from tasks that depended only on the approved decisions above, but it still must apply the normal Ready gate.

Near-term task refinement may rely on:

- multi-Tenant Membership discovery/selection and one-role authority;
- verified-email self-service onboarding with display name + IANA timezone;
- cross-domain Trial bootstrap recovery architecture;
- finalized Catalog lifecycle/SKU/slug/media/import rules and their access-pattern requirements;
- canonical purchase-confirmation sequence and Step Functions orchestration boundary through `OrderAllocated`;
- Payments Unknown/reconciliation semantics;
- approved accounting source routes except refund/return;
- SubscriptionBilling lifecycle/enforcement/provider simulation semantics except exact commercial packages;
- named reliable integration routes and conditional AWS resources above.

Tasks touching the remaining domain/product gaps must stay non-Ready until the owning decision is supplied.

## 14. Verification requirements for dependent implementation

When code-bearing tasks are later introduced, verify the applicable subset:

- subject with memberships in Tenant A and Tenant B cannot gain authority from client selection/JWT claims; multi-membership selection requires intentional choice;
- Membership/Tenant state changes affect the next authority resolution and subject discovery cannot become stale authority;
- onboarding cannot report completed while Trial is absent; interrupted Trial bootstrap is recoverable and duplicate-safe;
- no cross-module DynamoDB transaction/read/write is used for onboarding;
- Draft SKU can change under concurrency, first publication fixes it, and Archive never releases its historical claim;
- slug/name/source claims preserve tenant isolation and uniqueness without GSI authority;
- checkout price change creates no Order until reconfirmed;
- order workflow duplicate starts/retries do not duplicate reservation, Payment attempt, Sales transition, or integration fact;
- Payment Unknown never becomes failure/release/cancel from timeout, Step Functions timeout, retry exhaustion, or DLQ placement;
- Accounting consumers create one balanced immutable posting per logical source and never read producer tables;
- `PaymentCaptured`, `OrderFulfilled`, and `StockIssued` cannot double-post revenue/COGS effects;
- `OrderConfirmed` usage-meter replay increments at most once and never blocks shopper checkout;
- dedicated SaaS billing provider state cannot leak into merchant-order Payments;
- tenant-visible Audit/Subscription/public errors remain non-disclosing;
- CDK synthesizes only resources introduced by named Ready consumers/workflows and preserves Free Tier/cost guardrails.

## 15. Stop condition

**TECHNICAL BASELINE RECONCILED WITH THE 2026-08-10 PRODUCT-DECISION PASS.**

The architecture no longer requires a Builder to choose module ownership, tenant authority, onboarding consistency/recovery, Catalog uniqueness mechanics, purchase-confirmation sync/async boundaries, Payment uncertainty handling, named accounting integration routes, SubscriptionBilling trust/provider boundaries, or the AWS mechanism for the approved order process.

The remaining gaps are explicitly product/domain-owned and must not be filled by implementation convenience.