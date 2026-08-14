# CommerceOS — Product-Decision Technical Reconciliation

_Reconciled by the Technical Architect on 2026-08-10 after the final Domain Architect propagation of resolved `PD-004`, `PD-023`, and `PD-044`._

## 1. Authority and purpose

This document is the authoritative Technical Architect delta over the TASK-0088/TASK-0092 architecture baseline after the final 2026-08-10 product/domain reconciliation.

Business meaning remains authoritative in:

1. `docs/domains/product-decisions.md`;
2. `docs/02-business-domains.md` and detailed domain baselines;
3. this document and accepted ADRs for implementation mechanisms only.

The current product-decision register contains **no unresolved/deferred `PD-*` gate for the approved MVP scope**. Any older architecture text saying `PD-004`, `PD-023`, or `PD-044` is still deferred is superseded by this reconciliation.

This pass changes architecture documentation only. It implements no business feature and deploys no AWS resource.

## 2. Final architecture consequences of the resolved decisions

### `PD-004` — Tenant suspension/reactivation

Resolved business meaning requires Architecture to distinguish authenticated read authority from merchant mutation authority:

- Suspended Tenant remains selectable for approved authenticated read-only access when the Membership is still Active;
- ordinary merchant mutation requires Tenant `Active`;
- public storefront/checkout must fail for Suspended Tenant even if public Catalog data is cached;
- platform suspend/reactivate is a separate privileged Tenancy command requiring reason + Audit evidence;
- platform support investigation is a separate read-only trust path and never a Tenant Membership;
- Suspended data has no MVP TTL/deletion lifecycle.

ADR-004 is updated accordingly.

### `PD-023` — Refund approval/return/accounting

Resolved business meaning establishes the authoritative fact chain:

```text
Sales RefundRequested
   -> merchant review
   -> RefundApproved | RefundRejected

RefundApproved
   -> Inventory StockReturned
   -> Accounting revenue compensation
   -> Payments provider refund operation

StockReturned
   -> Accounting COGS/inventory reversal

verified PaymentRefunded
   -> Accounting Customer Deposits/Cash clearing
```

Technical Architecture selects reliable event choreography after `RefundApproved`, not a global refund Step Functions workflow. Owner-local source idempotency makes every effect duplicate-safe. Payments retains provider ambiguity/reconciliation authority.

ADR-011 records this decision.

### `PD-044` — initial Plan catalog and Trial terms

The initial immutable commercial terms are now implementation inputs:

| Terms | Price/month | MaxActiveMemberships | MaxWarehouses | ScheduledProductIngestion | OrderVolumeWarningThreshold |
|---|---:|---:|---:|---|---:|
| Trial | n/a | 3 | 1 | true | 500 |
| Starter | 199,000 VND | 3 | 1 | false | 500 |
| Growth | 499,000 VND | 10 | 3 | true | 2,000 |
| Business | 999,000 VND | 30 | 10 | true | 10,000 |

All current paid Plans and Trial enable the approved core CommerceOS capabilities. Architecture stores Plan/PlanVersion and dedicated Trial terms in SubscriptionBilling DynamoDB using a version-controlled idempotent catalog bootstrap. Accepted EntitlementSet, not Plan name, is runtime authority.

ADR-008 is updated accordingly.

## 3. Implementation module boundaries

CommerceOS remains a modular serverless monolith by default. A bounded context is not automatically a deployment unit.

| Implementation module | Business ownership hosted | Initial runtime/deployment decision |
|---|---|---|
| `Platform` | no merchant business truth | shared composition/readiness only |
| `Tenancy` | Tenant Management + Merchant Access | shared `commerce-api`; module-owned DynamoDB table |
| `Catalog` | Catalog | shared `commerce-api`; module-owned table |
| `SubscriptionBilling` | Plan/PlanVersion, Trial terms, Subscription, EntitlementSet, UsageMeter, PlatformCharge | shared API/application surface; named workers/provider ingress only when required |
| `Sales` | SalesOrder, cancellation/refund-review truth | shared API plus order workflow task composition |
| `Inventory` | Warehouse, StockItem, Reservation, StockMovement | shared application module; async refund/procurement consumers when Ready |
| `Payments` | merchant-order Payment, attempts, provider capture/refund evidence | shared application module + provider adapter/callback/reconciliation handlers |
| `Procurement` | Supplier/PO/receipt/invoice/payment evidence | shared application module + named consumers/producers |
| `Accounting` | chart, valuation state, immutable journals/ledger | module table + side-effecting fact consumers |
| `Reporting` | rebuildable projections | async worker when named projections exist |
| `ProductDataIngestion` | source policy/run/snapshot/candidate/schedule | crawler dispatcher/workers according to source runtime profile |
| `Notification` | delivery/read/acknowledgement truth | async worker + module persistence when introduced |
| `Audit` | append-only privileged/security evidence | async append + query surface when introduced |
| `FilesMedia` | merchant-uploaded asset identity/metadata | shared API + S3 object boundary when introduced |
| `Storefront`, `Customer`, `Pricing` | matching approved contexts | introduced only by Ready work |
| merchant-order Mock Provider | shopper payment simulation | separate external-like application/deployment |
| SaaS Billing Mock Provider | CommerceOS billing simulation | separate external-like application/deployment |

No module reads/writes another module's table, repository, private index, stream, key codec, or domain type. Collaboration uses producer-owned application/contracts or versioned integration facts.

## 4. Trusted execution contexts after `PD-004`

### Authentication remains identity evidence

Cognito/API Gateway proves authenticated subject only. Tenant, Membership, role, plan, entitlement, and limit claims in token/client/session state are not current business authority.

### Merchant discovery/selection

Merchant Access retains the strongly consistent subject-membership discovery representation:

```text
PK = SUBJECT#<SubjectId>
SK = MEMBERSHIP#<MembershipId>#TENANT#<TenantId>
```

- discovery is candidate selection only;
- one candidate may be auto-selected only after current validation;
- multiple candidates require intentional selection;
- selected Tenant remains untrusted until current Tenant/Membership validation;
- Suspended Tenant may remain discoverable/selectable for approved read-only access;
- Disabled Membership does not gain ordinary protected authority.

### Separate read and mutation authority

Conceptual contracts:

```text
ResolveTenantReadAuthority(...)     -> TrustedTenantReadContext | Failure
ResolveTenantMutationAuthority(...) -> TrustedTenantMutationContext | Failure
```

`TrustedTenantReadContext` requires Active Membership and permits Tenant `Active` or `Suspended`, then the owning domain applies normal role read visibility.

`TrustedTenantMutationContext` requires Tenant `Active` plus normal role/domain authorization. A subscription-governed mutation separately asks SubscriptionBilling for current entitlement.

This split prevents accidental reuse of a Suspended read context for mutation.

### Platform paths

- `TrustedPlatformAdminContext` is required for `SuspendTenant`/`ReactivateTenant`; reason + expected revision + durable Audit intent/evidence are mandatory.
- `TrustedPlatformSupportReadContext` is used for explicit producer-owned read-only support queries across modules.
- no `bypassTenant`, no automatic Owner Membership, and no direct foreign-table support reads.

### Public path

`PublicTenantContext` remains separate. PD-052 resolves public Tenant addressing as a globally unique Tenant-owned `/{storefrontSlug}` binding; public Tenant resolution must check current Tenant status, and Suspended denies storefront/checkout.

## 5. Onboarding remains ADR-009

Completed onboarding still means:

```text
Tenancy:
  Active Tenant
  Active initial Owner

SubscriptionBilling:
  30-day Trial Subscription
  Trial EntitlementSet
```

The Trial EntitlementSet is now concrete:

```text
CoreCommerceCapabilities = enabled
MaxActiveMemberships = 3
MaxWarehouses = 1
ScheduledProductIngestion = true
OrderVolumeWarningThreshold = 500
```

Cross-domain DynamoDB ACID is not used. Tenancy commits local registration + durable Trial-bootstrap work; SubscriptionBilling creates/replays Trial through idempotent `StartTrialSubscription`; incomplete Trial bootstrap is recovered through the ADR-009 work-outbox/SQS path.

Trial is dedicated terms and never aliases/auto-converts to Starter.

## 6. SubscriptionBilling catalog and entitlement contracts

### Plan catalog bootstrap/query

SubscriptionBilling owns platform-global Plan/PlanVersion and Trial-terms records.

Use a version-controlled catalog seed artifact consumed by an idempotent SubscriptionBilling bootstrap/migration command.

Rules:

- accepted PlanVersion/Trial terms are immutable;
- a sellable version may be withdrawn without deleting historical records;
- future price/limit changes create a new PlanVersion;
- frontend/other modules query SubscriptionBilling rather than hard-code authority;
- no AppConfig/SSM/separate runtime configuration authority is introduced for this catalog.

### Runtime authority

`EvaluateEntitlement` remains the synchronous authority for governed operations.

Approved keys are:

```text
CoreCommerceCapabilities
MaxActiveMemberships
MaxWarehouses
ScheduledProductIngestion
OrderVolumeWarningThreshold
```

Missing entitlement never means Unlimited/enabled. Current immutable EntitlementSet is authority, not Plan name.

### `MaxActiveMemberships`

Tenancy owns the authoritative Active Membership count and maintains a module-local count/guard transactionally with Membership lifecycle writes.

Before create/reactivate:

```text
ResolveTenantMutationAuthority
-> SubscriptionBilling.EvaluateEntitlement(MaxActiveMemberships)
-> Tenancy local count + last-owner invariant
-> conditional Tenancy transaction
```

Every Active Owner/Admin/Staff/Viewer counts.

### `MaxWarehouses`

Inventory owns the authoritative active-Warehouse count/guard and conditionally protects create/reactivate using current `MaxWarehouses` entitlement.

### `ScheduledProductIngestion`

PDI checks both:

```text
SubscriptionBilling ScheduledProductIngestion entitlement
AND
PDI platform/source/Tenant source-policy permission
```

Check on schedule enable/create and again before scheduled dispatch. Losing entitlement suppresses future scheduled execution but does not delete PDI history/configuration.

### Order warning

`OrderConfirmed` feeds SubscriptionBilling UsageMeter idempotently. Threshold comes from the current period's EntitlementSet and is warning-only.

## 7. Refund contracts after `PD-023`

### Sales

Sales owns refund request/review state and expected revision/idempotency.

Conceptual commands:

```text
RequestRefund(...)
ApproveRefund(refundRequestId, expectedRevision, ...)
RejectRefund(refundRequestId, expectedRevision, ...)
```

`RefundRequested` has no downstream refund effect. Approval/rejection is terminal for the logical request and duplicate-safe.

Trusted Merchant Access resolves the refund capability: Owner/Admin/Staff may
request; Owner/Admin alone may approve or reject; Viewer has neither capability.
Builders must deny before Sales mutation when the trusted capability is absent;
screen visibility and client-supplied roles remain non-authoritative.

### `RefundApproved` integration fact

Sales writes `RefundApproved` + outbox atomically. Stable event data includes the consumer-required approved amount/currency, returned-line quantities, Order/Payment/refund identifiers, and original issue/source references required for validation/provenance. It does not expose database serialization or provider secrets.

### Inventory

Inventory consumes `RefundApproved` idempotently, applies one `StockReturned` effect, and emits `StockReturned` with original issue provenance/reference. It owns quantity truth and never infers provider refund success.

### Payments

Payments consumes `RefundApproved` and starts/replays one logical provider refund operation.

- provider operation identity is stable before unsafe retry;
- timeout/network ambiguity is `OutcomeUnknown`;
- no duplicate unsafe refund while prior logical operation is Unknown;
- only verified provider evidence creates `PaymentRefunded`;
- cumulative verified refund cannot exceed captured amount.

### Accounting

Accounting consumes three facts independently and never reads producer tables:

| Source fact | Accounting effect |
|---|---|
| `RefundApproved` | linked revenue compensation `Dr Sales Revenue / Cr Customer Deposits` for already recognized sale |
| `StockReturned` | original-issue-cost COGS reversal `Dr Inventory / Cr COGS` |
| `PaymentRefunded` | refund settlement `Dr Customer Deposits / Cr Cash` |

Each consumer atomically persists its source claim with one balanced immutable journal/effect. Original posted journals are never edited.

For `StockReturned`, Accounting uses the event's original issue reference to locate its **own** original StockIssued posting/valuation provenance. Inventory does not become accounting-cost authority.

### No global refund workflow state

Architecture does not invent a cross-domain `RefundCompleted` business state. Sales approval remains committed while downstream effects recover independently. Reporting/support may project per-domain progress but projection is not source truth.

ADR-011 owns this integration choice.

## 8. Persistence updates

ADR-005 remains the physical strategy: one DynamoDB table per implementation module, trusted Tenant keying, no application Scan, no foreign-table access, and no cross-domain DynamoDB transaction.

### Tenancy

Required additions/closures:

- Tenant lifecycle stores only Active/Suspended in MVP; suspend/reactivate uses expected revision and durable Audit delivery intent;
- no suspended business record is TTL-deleted under current MVP retention policy;
- subject Membership discovery includes enough current Tenant/Membership reference/status data for selection but is never final authority;
- authoritative Active Membership count/guard is updated atomically with activation/disable/reactivation changes;
- read versus mutation authority uses current Tenant status from base-table authority reads.

### SubscriptionBilling

Required records:

- platform-global Plan + immutable PlanVersion records;
- immutable Trial-terms version;
- catalog bootstrap source/idempotency/version conflict protection;
- current/historical Subscription + EntitlementSet;
- UsageMeter and `OrderConfirmed` source claims;
- PlatformCharge/provider evidence/reconciliation;
- restrictive downgrade transition/owner assessment acknowledgements.

### Inventory

Required additions:

- authoritative active-Warehouse count/guard for hard limit;
- `RefundApproved` source claim + one immutable StockReturned movement/effect;
- StockReturned carries original issue provenance/reference in integration event.

### Sales

Required additions:

- RefundRequest/refund-review state keyed to Tenant/Order/refund identity;
- expected revision + terminal decision protection;
- `RefundApproved`/`RefundRejected` Audit intent;
- `RefundApproved` integration outbox atomic with approval.

### Payments

Required additions:

- approved refund operation identity/status/evidence separate from capture attempts;
- refund source claim by `refundApprovalId`/logical operation;
- provider refund evidence + reconciliation state;
- `PaymentRefunded` outbox only after verified evidence.

### Accounting

Required additions:

- source posting claims for revenue refund compensation, StockReturned COGS reversal, and PaymentRefunded Cash settlement;
- durable linkage to original journal/source/issue provenance required for append-only reversals;
- no foreign table lookup for refund postings.

Moving-weighted-average valuation state keys by trusted Tenant + Product. Warehouse remains Inventory-only for quantity/location; transfer has no Accounting valuation effect. `StockReturned` locates the original Accounting StockIssued provenance and reverses that recorded issue cost, never a current moving-average estimate.

## 9. Integration matrix updates

### Synchronous

Use synchronous producer-owned application contracts for immediate owner decisions:

- merchant read/mutation authority resolution;
- SubscriptionBilling entitlement/catalog queries;
- Tenancy/Inventory resource-limit guarded writes;
- PDI schedule enable/dispatch entitlement checks;
- checkout Catalog/Pricing validation;
- ADR-010 Inventory/Payments/Sales order tasks;
- platform support read-only module queries.

### Reliable facts

Use producer outbox -> Stream relay -> EventBridge -> consumer-specific SQS/DLQ for committed facts with independent consumers.

Named routes now include:

| Producer fact | Consumer/effect |
|---|---|
| `OrderConfirmed` | SubscriptionBilling UsageMeter; Reporting |
| `PaymentCaptured` | Accounting; Sales convergence |
| `OrderFulfilled` | Accounting revenue; Reporting |
| `StockIssued` | Accounting COGS |
| `RefundApproved` | Inventory return; Payments refund execution; Accounting revenue compensation |
| `StockReturned` | Accounting COGS/inventory reversal |
| `PaymentRefunded` | Accounting Customer Deposits/Cash settlement |
| `GoodsReceiptRecorded` | Inventory; Accounting |
| `SupplierInvoiceRecorded` | Accounting |
| `SupplierPaymentRecorded` | Accounting |
| `StockAdjusted` | Accounting |
| covered privileged actions/rejections | Audit |

Consumers are at-least-once/idempotent and persist source identity with owned effect where possible.

### Refund does not use Step Functions by default

ADR-010 remains scoped to OrderPlaced -> OrderAllocated. Refund approval is durable Sales state; post-approval effects are independent fact consumers and Payments owns its own provider reconciliation. No refund state machine is created unless a future approved global process demonstrates a separate orchestration need.

## 10. AWS/CDK mapping after final decisions

No new managed service is required by resolving `PD-004`, `PD-023`, or `PD-044`.

| Need | AWS mapping | Status |
|---|---|---|
| protected merchant identity | Cognito + API Gateway HTTP API JWT authorizer | accepted |
| shared synchronous application surface | Lambda `commerce-api` | accepted modular-monolith default |
| module persistence | one DynamoDB table per introduced module | ADR-005 |
| merchant subject discovery | Tenancy base-table strong Query | accepted |
| onboarding Trial recovery | Tenancy work-outbox/Stream -> SQS worker | ADR-009 |
| reliable business facts including refund | module outbox/Stream -> EventBridge | conditional with named consumer |
| side-effecting fact consumers | consumer-specific SQS/DLQ + Lambda | conditional |
| order payment/allocation | Step Functions Standard + application task handlers | ADR-010 only |
| refund propagation | EventBridge + Inventory/Payments/Accounting queues/workers | ADR-011; no Step Functions |
| merchant-order provider | separate API Gateway/Lambda/DynamoDB app | conditional |
| SaaS Billing Mock Provider | separate API Gateway/Lambda/DynamoDB app | conditional |
| provider reconciliation schedule | EventBridge Scheduler | conditional only with named due-work need |
| PDI acquisition | SQS/DLQ + Lambda; Scheduler only for approved schedules | conditional |
| media binaries | private S3 + controlled CloudFront delivery | conditional |
| observability | CloudWatch built-ins/logs/alarms | accepted with bounded retention |
| IaC | AWS CDK/CloudFormation | accepted |

Do not introduce AppConfig/SSM merely for Plan catalog, or NAT Gateway/ALB/EC2/RDS/Redis/OpenSearch/MSK/EKS/always-on infrastructure without a new demonstrated problem and ADR.

## 11. ADR reconciliation

Current accepted architecture decisions include:

- ADR-003 — modular runtime/deployment boundaries;
- ADR-004 — trusted Tenant authority, now including Suspended read versus mutation separation and explicit platform paths;
- ADR-005 — module-owned DynamoDB/access-pattern strategy;
- ADR-006 — reliable integration/outbox/EventBridge/SQS pattern;
- ADR-007 — HTTP/idempotency conventions;
- ADR-008 — SubscriptionBilling boundary, now including resolved MVP catalog bootstrap and concrete entitlement enforcement;
- ADR-009 — onboarding Trial completion/recovery;
- ADR-010 — Order payment/allocation Step Functions Standard workflow;
- ADR-011 — refund approval propagation and Accounting correction integration.

## 12. Remaining domain/architecture stop conditions

The final three PDs are no longer stop conditions. The following independent gaps remain and must not be guessed by Builders:

1. Public Tenant route/index/cache-key implementation for the approved `/{storefrontSlug}` binding; owner/lifecycle/uniqueness are finalized by PD-052.
2. Category/Brand historical normalized-name reuse after rename/retirement if a task requires it.
3. Any non-restock refund semantics; current MVP refund approval means accepted restockable return.
4. Any product max-order-line limit. Architecture must solve DynamoDB transaction cardinality without inventing a commercial/business maximum.

## 13. Backlog Planner handoff

Backlog Planner may remove obsolete `PD-004`, `PD-023`, and `PD-044` gates from affected candidate tasks after verifying this architecture reconciliation.

Task refinement may now rely on:

- platform-only reasoned Tenant suspend/reactivate;
- Suspended merchant read-only versus Active mutation authority split;
- approved Trial/Starter/Growth/Business commercial terms;
- concrete `MaxActiveMemberships`, `MaxWarehouses`, scheduled-ingestion, and order-warning entitlement behavior;
- versioned immutable SubscriptionBilling catalog bootstrap;
- `RefundRequested -> RefundApproved/RefundRejected` boundary;
- `RefundApproved` fan-out to Inventory/Payments/Accounting;
- `StockReturned` and verified `PaymentRefunded` Accounting routes;
- no refund Step Functions workflow by default.

Backlog Planner still owns task maturity/Ready status.

## 14. Verification requirements for dependent implementation

Verify the applicable subset:

- Suspended Tenant can perform approved role-scoped reads but no ordinary merchant mutation;
- platform suspend/reactivate requires authorized platform context, reason, expected revision, and durable Audit evidence;
- public storefront/checkout fails for Suspended Tenant despite cached Catalog data;
- plan catalog seed is idempotent and accepted versions cannot be mutated;
- Trial/Starter/Growth/Business values match the approved matrix exactly;
- consumers never authorize by Plan name/frontend/JWT/copied limit;
- Membership and Warehouse concurrent growth cannot bypass hard limits;
- PDI rechecks scheduled-ingestion entitlement at dispatch;
- order-volume warning replay counts once and never blocks checkout/charges overage;
- RefundRequested/Rejected create no stock/payment/accounting effect;
- RefundApproved replay produces at most one logical StockReturned, provider refund operation, and revenue compensation;
- StockReturned and PaymentRefunded replay cannot double-post Accounting;
- Payment refund OutcomeUnknown never becomes verified refund from timeout/retry/DLQ;
- Accounting uses own original issue/journal provenance and never reads producer tables;
- no refund Step Functions resource is synthesized absent a later approved ADR;
- CDK creates only resources introduced by named Ready tasks and preserves Free Tier/pay-per-use constraints.

## 15. Stop condition

**TECHNICAL BASELINE RECONCILED WITH RESOLVED PD-004, PD-023, AND PD-044.**

The three former product-decision gates no longer require a Builder to choose technical semantics. Remaining independent domain gaps are explicit above rather than hidden behind stale PD status text.
