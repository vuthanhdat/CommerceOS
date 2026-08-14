# CommerceOS — First-Frontier Contracts and Trusted Context

_Technical contract baseline refreshed on 2026-08-10 after final resolution of `PD-004`, `PD-023`, and `PD-044`._

## 1. Contract ownership

Contracts are owned by the module that accepts a command, answers a query, or states a fact.

| Contract family | Owner | Consumers | Rule |
|---|---|---|---|
| merchant authentication input | API/Cognito adapter | Merchant Access | identity evidence only |
| `DiscoverMerchantTenants` | Tenancy / Merchant Access | merchant delivery | candidate discovery only |
| `ResolveTenantReadAuthority` | Tenancy / Merchant Access | merchant query composition | Active Membership; Active or Suspended Tenant |
| `ResolveTenantMutationAuthority` | Tenancy / Merchant Access | merchant command composition | Active Membership + Active Tenant |
| platform Tenant lifecycle commands | Tenancy | platform admin delivery | reasoned/audited suspend/reactivate only |
| platform support query boundary | owning module | platform support composition | explicit privileged read-only path |
| onboarding operation/result | Tenancy process boundary | onboarding delivery/recovery | complete only after Tenant + Owner + Trial |
| `StartTrialSubscription` | SubscriptionBilling | onboarding coordinator/worker | idempotent dedicated Trial |
| Plan catalog/entitlement query | SubscriptionBilling | Back Office/owner modules | immutable terms; EntitlementSet authority |
| Catalog commands/queries | Catalog | API/Storefront/Sales/PDI | Catalog-owned truth only |
| Sales Order/refund contracts | Sales | Storefront/Back Office/workers | Sales commercial/refund-review truth |
| Inventory stock/return contracts | Inventory | order/refund/procurement processes | physical quantity truth |
| Payments capture/refund/reconcile contracts | Payments | order/refund processes | provider interpretation/ambiguity truth |
| Accounting integration consumers | Accounting | fact delivery | immutable balanced own effects only |
| integration facts | producing module | named async consumers | producer-owned/versioned under ADR-006/011 |
| Storefront Tenant addressing | Tenant-owned `/{storefrontSlug}` contract | public delivery | PD-052; globally unique normalized binding, permanent retirement/no reuse |
| HTTP DTO/problems/pagination | API delivery | clients | transport only, never Domain entities |

No consumer imports another module's Domain/Infrastructure or persistence representation.

## 2. Authentication and Tenant discovery

API Gateway/Cognito validates merchant identity. Backend uses stable SubjectId as identity evidence only.

Never Tenant authority:

- route/query/header/body TenantId;
- email claim;
- JWT Tenant/Membership/role/plan/entitlement/limit claims;
- browser/session cache;
- aggregate ID knowledge.

Conceptual discovery:

```text
DiscoverMerchantTenants(
  AuthenticatedPrincipal,
  RequestMetadata)
    -> MerchantTenantCandidates | DiscoveryFailure
```

Rules:

- result comes from strongly consistent subject-membership discovery;
- result is candidate selection only;
- one candidate may be auto-selected only after current validation;
- multiple/ambiguous candidates require intentional selection;
- Suspended Tenant may remain a candidate for approved merchant read-only access;
- client cannot supply another SubjectId to discover Tenants.

## 3. Merchant read authority

Conceptual contract:

```text
ResolveTenantReadAuthority(
  AuthenticatedPrincipal,
  RequestedTenantSelection?,
  RequestMetadata)
    -> TrustedTenantReadContext | AuthorityFailure
```

The resolver:

1. resolves/selects candidate Tenant;
2. validates current selected Tenant + Membership from authoritative Tenancy records;
3. requires Active Membership bound to authenticated Subject;
4. accepts Tenant `Active` or `Suspended` for the read context;
5. returns current role/status from accepted records;
6. fails closed when current authority cannot be established.

Conceptual result:

```text
TrustedTenantReadContext
  tenantId
  subjectId
  membershipId
  role
  tenantStatus          # Active | Suspended
  tenantRevision?
  membershipRevision?
  correlationId
```

Owning-domain query contracts then apply normal role read visibility. Suspended does not expand role visibility.

## 4. Merchant mutation authority

Conceptual contract:

```text
ResolveTenantMutationAuthority(
  AuthenticatedPrincipal,
  RequestedTenantSelection?,
  RequestMetadata)
    -> TrustedTenantMutationContext | AuthorityFailure
```

Requires:

- current Active Membership;
- current Tenant `Active`;
- owning-domain role authorization;
- separate Subscription entitlement where governed;
- owning aggregate invariant.

Conceptual context:

```text
TrustedTenantMutationContext
  tenantId
  subjectId
  membershipId
  role
  tenantRevision?
  membershipRevision?
  correlationId
```

A `TrustedTenantReadContext` is not accepted by mutation application contracts. This is a deliberate type/contract boundary for resolved `PD-004`.

## 5. Platform administration and support

Platform administration is never `TrustedTenantMutationContext + bypass`.

Conceptual trust types:

```text
TrustedPlatformAdminContext
TrustedPlatformSupportReadContext
```

Tenancy-owned commands:

```text
SuspendTenant(
  TrustedPlatformAdminContext,
  tenantId,
  reason,
  expectedRevision,
  idempotencyIdentity,
  requestMetadata)
    -> TenantSuspended | AlreadyApplied | Failure

ReactivateTenant(...)
    -> TenantReactivated | AlreadyApplied | Failure
```

Rules:

- only explicit authorized platform administration may invoke them;
- reason required;
- expected revision protects current lifecycle state;
- accepted transition writes durable Audit delivery intent;
- transition does not alter Membership/Subscription state.

Platform support uses explicit producer-owned read-only queries per module. It never receives Owner Membership authority and never reads another module's DynamoDB table directly.

## 6. Subscription entitlement and Plan catalog

Conceptual entitlement contract:

```text
EvaluateEntitlement(
  TrustedTenantScope,
  EntitlementKey,
  DecisionMetadata)
    -> EffectiveEntitlementDecision | EntitlementFailure
```

Approved keys:

```text
CoreCommerceCapabilities
MaxActiveMemberships
MaxWarehouses
ScheduledProductIngestion
OrderVolumeWarningThreshold
```

Current approved terms:

| Terms | Price/month | MaxActiveMemberships | MaxWarehouses | ScheduledProductIngestion | Warning |
|---|---:|---:|---:|---|---:|
| Trial | n/a | 3 | 1 | true | 500 |
| Starter | 199,000 VND | 3 | 1 | false | 500 |
| Growth | 499,000 VND | 10 | 3 | true | 2,000 |
| Business | 999,000 VND | 30 | 10 | true | 10,000 |

Plan-selection/catalog query is SubscriptionBilling-owned. Clients may display Plan names/prices, but consuming modules never authorize by Plan name.

`EffectiveEntitlementDecision` exposes only the current value/provenance needed by the consumer. Missing entitlement does not imply enabled/Unlimited.

## 7. Hard resource-limit contracts

### Membership

Before creating/reactivating an Active Membership:

```text
merchant mutation authority
-> EvaluateEntitlement(MaxActiveMemberships)
-> Merchant Access authoritative active count/guard
-> conditional Tenancy commit
```

All Active roles count. Last-Active-Owner remains an independent invariant.

### Warehouse

Before creating/reactivating Warehouse:

```text
merchant mutation authority
-> EvaluateEntitlement(MaxWarehouses)
-> Inventory authoritative active-Warehouse count/guard
-> conditional Inventory commit
```

SubscriptionBilling does not own either count.

## 8. Onboarding

`TrustedOnboardingContext` is built from authenticated Subject with verified email and no caller-selected Tenant authority.

Conceptual command:

```text
RegisterMerchant(
  TrustedOnboardingContext,
  merchantDisplayName,
  businessTimeZoneIana,
  idempotencyIdentity,
  requestMetadata)
    -> OnboardingCompleted | OnboardingAcceptedPending | Failure
```

Completed outcome:

```text
Active Tenant
+ Active initial Owner Membership
+ 30-day Trial Subscription / Trial EntitlementSet
```

Trial terms are concrete: core enabled, memberships 3, warehouses 1, scheduled ingestion true, warning 500.

ADR-009 behavior:

- Tenancy commits local registration + durable Trial work intent;
- coordinator calls idempotent `StartTrialSubscription`;
- completed response only after Trial accepted;
- interruption returns `202 Accepted` with durable operation identity;
- recovery uses same logical source command;
- no cross-module transaction or destructive rollback.

Trial never auto-converts to Starter.

## 9. Membership and Invitation contracts

Membership rules:

- one role: Owner/Admin/Staff/Viewer;
- expected revision for unsafe lost update;
- last Active Owner cannot be disabled/demoted/removed;
- Admin cannot grant/revoke Owner authority;
- Viewer is read-only;
- Staff mutation scope must be explicitly approved by owning domain;
- create/reactivate respects `MaxActiveMemberships`;
- downgrade never auto-disables Membership.

Invitation issue/resend/accept/revoke preserves:

- Tenant-bound normalized recipient email;
- verified-email acceptance match;
- at most one active Pending invitation per Tenant/email;
- resend rotates credential;
- 7-day expiry;
- single use;
- Active member => harmless already-member result;
- Disabled member is not silently reactivated;
- resulting activation still respects last-owner and Membership hard-limit invariants.

## 10. Catalog contracts

Current approved constraints remain:

- Draft may omit SKU; first publish requires SKU;
- VND Money, zero price allowed;
- SKU Tenant-unique case-insensitively, mutable only before first publication, never reused after Archive;
- Published edits are live canonical/public edits;
- Archived terminal;
- ProductId canonical, Tenant-scoped mutable public slug;
- zero/one flat Category and Brand, non-destructive retirement;
- media references only same-Tenant FilesMedia assets;
- ImportCandidate application is explicit Catalog command.

Public Product lookup may implement the approved Storefront Tenant-address semantics; Product slug policy remains independently Tenant-scoped.

## 11. Public storefront

Public request uses `PublicTenantContext`, never merchant context.

Storefront Tenant addressing is Tenant-owned `/{storefrontSlug}` binding (PD-052). Public Tenant resolution must establish:

- target Tenant identity from approved public address binding;
- current Tenant status;
- `Suspended` => storefront/checkout unavailable.

Catalog cache/projection cannot override current Tenant suspension or authorize checkout.

## 12. Sales Order contracts

Order placement remains:

```text
PlaceOrder
 -> authoritative Catalog/Pricing validation
 -> changed price? reconfirm-required, no Order
 -> OrderPlaced
 -> ADR-010 order process
```

Rules:

- one logical checkout intent creates at most one Order;
- browser totals/TenantId are untrusted;
- reservation/capture/confirmation/allocation remain owner facts;
- Payment OutcomeUnknown is not failure;
- order workflow technical state is not Sales business truth.

## 13. Refund review contracts

Sales owns:

```text
RequestRefund(...)
  -> RefundRequested

ApproveRefund(
  TrustedTenantMutationContext,
  refundRequestId,
  expectedRevision,
  idempotencyIdentity,
  requestMetadata)
  -> RefundApproved | AlreadyApplied | Failure

RejectRefund(...)
  -> RefundRejected | AlreadyApplied | Failure
```

Architecture requirements:

- request alone causes no stock/payment/accounting effect;
- approval/rejection terminal for the logical request;
- approval requires an explicitly domain-authorized refund-approval capability; exact role wiring is not invented here;
- accepted approval writes `RefundApproved` integration outbox atomically;
- accepted approval/rejection writes required Audit intent.

Conceptual `RefundApproved` integration data includes:

```text
refundApprovalId
orderId
paymentId
approvedAmount/currency
approved returned line quantities
original issue/source references
correlation/causation/occurredAt
```

No persistence serialization/provider secrets.

## 14. Inventory refund contract

Conceptual consumer command:

```text
ApplyApprovedReturn(
  tenantId,
  refundApprovalId,
  orderId,
  approvedLines,
  sourceMetadata)
    -> StockReturned | AlreadyApplied | Failure
```

Inventory owns validation of eligible physical return quantity and one logical `OnHand += q` effect. `StockReturned` includes original issue provenance/reference needed by Accounting without transferring accounting-cost authority to Inventory.

## 15. Payments refund contract

Conceptual command:

```text
StartApprovedRefund(
  tenantId,
  refundApprovalId,
  paymentId,
  amount,
  currency,
  sourceMetadata)
    -> Refunded | DefinitiveNoCommit | OutcomeUnknown | AlreadyApplied
```

Rules:

- stable logical/provider operation identity before unsafe retry;
- cumulative verified refunds cannot exceed captured amount;
- timeout/network ambiguity => OutcomeUnknown;
- unsafe duplicate refund is blocked while outcome Unknown;
- only verified provider evidence creates `PaymentRefunded`;
- reconciliation/query remains Payments-owned.

## 16. Accounting refund source contracts

Accounting consumes producer facts only:

| Fact | Accounting-owned effect |
|---|---|
| `RefundApproved` | Dr Sales Revenue / Cr Customer Deposits for already recognized sale |
| `StockReturned` | Dr Inventory / Cr COGS using Accounting-owned original issue-cost provenance |
| `PaymentRefunded` | Dr Customer Deposits / Cr Cash |

Each fact is source-idempotent. Accounting never reads Sales/Inventory/Payments persistence and never edits original journals.

No global `RefundCompleted` contract is approved; support/reporting may compose progress as a projection only.

## 17. Scheduled ingestion contract

PDI schedule create/enable and scheduled dispatch require current `ScheduledProductIngestion=true` plus independent PDI source-policy approval.

Dispatch rechecks entitlement so an old schedule cannot continue after downgrade/Ended. Losing entitlement does not delete PDI configuration/history.

## 18. Background facts and execution context

Integration envelope retains:

```text
eventId
eventType
eventVersion
tenantId
aggregateId
occurredAt
correlationId
causationId
producer
data
```

Background handlers use validated service/message context, not merchant role context. They scope their own persistence by event/work Tenant, deduplicate by source identity, and never access producer tables.

Named refund routes are defined by ADR-011.

## 19. HTTP conventions

ADR-007 still governs:

- major-versioned JSON;
- transport DTOs separate from domain/application types;
- safe RFC 9457 problem details;
- opaque IDs/cursors;
- ETag/`If-Match` where revision-sensitive;
- scoped idempotency for unsafe retries;
- `202` only with durable status resource;
- Unknown/timeout never mapped to definitive business failure;
- non-disclosing cross-Tenant behavior.

## 20. Remaining contract gaps

Builders must stop rather than invent:

- Storefront Tenant-address owner/lifecycle/uniqueness;
- moving-weighted-average cost-pool scope;
- Category/Brand historical normalized-name reuse if needed;
- exact refund approval role/capability mapping if not supplied by task/domain refinement;
- any non-restock refund behavior.

## 21. Stop condition

**FIRST-FRONTIER CONTRACT BASELINE RECONCILED.**
