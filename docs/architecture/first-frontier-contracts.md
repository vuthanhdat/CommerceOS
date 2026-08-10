# CommerceOS — First-Frontier Contracts and Trusted Context

_Technical contract baseline originally reconciled by TASK-0088 and refreshed on 2026-08-10 after the product/domain decision pass._

## 1. Contract ownership

Contracts are owned by the module that accepts the request, answers the query, or states the fact.

| Contract family | Owner | Consumers | Rule |
|---|---|---|---|
| merchant authentication input | API/Cognito adapter | Merchant Access authority resolver | identity evidence only; no Tenant/role/plan authority |
| `DiscoverMerchantTenants` | Tenancy / Merchant Access | protected API/back office | current subject Membership discovery; not final command authority |
| `ResolveTenantAuthority` | Tenancy / Merchant Access | protected delivery/application composition | validates current selected Tenant + Membership |
| `TrustedTenantContext` | Tenancy Contracts | protected applications | constructed only from accepted authority records |
| onboarding operation/result | Tenancy process/application boundary | onboarding delivery/recovery worker | completed only after Tenant + initial Owner + Trial are proven |
| `StartTrialSubscription` | SubscriptionBilling | onboarding coordinator/recovery worker | idempotent Trial creation; separate business owner |
| profile/membership/invitation contracts | Tenancy | protected API/back office | one-role, last-owner, verified-email invitation policy |
| Catalog commands/queries/results | Catalog | API, later Storefront/Sales/Ingestion | no persistence models or external-source authority |
| entitlement decision | SubscriptionBilling | subscription-governed applications | current commercial authority only; no plan record exposure |
| order command/result | Sales | Storefront/API/order workflow | immutable accepted commercial snapshot; browser price/Tenant untrusted |
| inventory reservation/issue/release contracts | Inventory | Sales/order/fulfillment process | Inventory owns stock truth and source idempotency |
| payment capture/reconcile contracts | Payments | order process | provider uncertainty remains Payments truth |
| public Product query | Catalog | Storefront/public delivery | Published/public-eligible Product facts only |
| Storefront Tenant addressing | Storefront/domain contract | public delivery | **not final** until Tenant-address domain semantics are supplied |
| HTTP DTO/problem/pagination | API delivery | clients | versioned transport only; not Domain entities |
| integration facts | producing module | named async consumers | producer-owned/versioned under ADR-006 |

No consumer imports another module's Domain/Infrastructure assembly or private table representation. A contract shape does not transfer fact ownership.

## 2. Authentication and merchant authority

### 2.1 Authentication boundary

API Gateway's Cognito JWT authorizer validates configured access-token issuer, audience/client, signature, lifetime, and coarse OAuth scopes.

The backend treats the stable external subject identifier as identity evidence.

Never current Tenant authority:

- route/query/header/body TenantId;
- email claim;
- JWT custom Tenant/Membership/role/capability/plan/entitlement claims;
- browser/session cache;
- previously cached authority result;
- knowledge of an aggregate ID.

### 2.2 Subject membership discovery

Because one Subject may belong to multiple Tenants, Merchant Access exposes a bounded discovery contract conceptually equivalent to:

```text
DiscoverMerchantTenants(
  AuthenticatedPrincipal,
  RequestMetadata)
    -> MerchantTenantCandidates | DiscoveryFailure
```

The result contains only safe Tenant/Membership references/display information required for selection. It is produced from the strongly consistent subject-membership discovery path in the Tenancy table.

Rules:

- discovery is not permission to access a Tenant aggregate;
- more than one/ambiguous Membership requires intentional selection;
- one candidate may be auto-selected only after current Tenant/Membership validation;
- a subject selector never comes from client-supplied SubjectId;
- result pagination, if ever needed, remains bounded and opaque.

### 2.3 Current authority resolution

Conceptual input:

```text
AuthenticatedPrincipal
  subjectId
  issuer
  audience/client
  authenticationTime?

RequestedTenantSelection
  tenant reference?     # untrusted target; optional only where auto-selection may apply

RequestMetadata
  requestId
  correlationId
  occurredAt
```

Conceptual contract:

```text
ResolveTenantAuthority(
  AuthenticatedPrincipal,
  RequestedTenantSelection?,
  RequestMetadata)
    -> TrustedTenantContext | AuthorityFailure
```

The resolver:

1. determines/accepts a candidate Tenant using Merchant Access discovery/selection rules;
2. reads current Tenant + Membership authority through authoritative base-table access;
3. requires an Active Tenant for ordinary work and an Active Membership bound to that Tenant/Subject;
4. returns values from accepted records, not request data;
5. fails closed when current authority cannot be established.

Conceptual result:

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

The owning application applies the approved domain-specific role rule. The context is not a general RBAC engine and does not contain current Subscription entitlement authority.

### 2.4 Application enforcement

For every protected command/query:

1. delivery obtains `TrustedTenantContext`;
2. the owning application checks the approved role/domain policy;
3. when subscription-governed, it separately asks SubscriptionBilling for current entitlement;
4. target aggregate identifiers remain untrusted input;
5. repository scope comes only from trusted Tenant context;
6. persistence key/query contains TenantId;
7. absent/cross-Tenant target uses non-disclosing not-visible behavior.

Resolution occurs on every protected request initially. No cross-request Membership/Tenant authorization cache is authoritative.

## 3. Subscription entitlement decision

Subscription entitlement is independent from authentication/Membership/Tenant status.

Conceptual contract:

```text
EvaluateEntitlement(
  TrustedTenantScope,
  EntitlementKey,
  DecisionMetadata)
    -> EffectiveEntitlementDecision | EntitlementFailure
```

A decision exposes only current business meaning/provenance needed by the consumer, such as:

```text
tenantId
entitlementKey
value
entitlementSetId
subscriptionId
sourceTermsId
effectiveFrom
effectiveUntil?
decisionRevision
```

Rules:

- missing entitlement does not mean Unlimited;
- Reporting/UI/cache/JWT/client values are never write authority;
- hard counted-resource limits combine this decision with owner-authoritative current count/state;
- Ended/Trial/PastDue semantics follow the Subscription domain; read/history/export/recovery routes do not accidentally require ordinary mutation entitlements;
- exact sellable package keys/values remain absent until the deferred portion of `PD-044` is resolved.

## 4. Distinct execution paths

### 4.1 Onboarding

Onboarding occurs before ordinary merchant authority exists.

`TrustedOnboardingContext` is built from an authenticated Subject with verified email and contains no caller-selected Tenant authority.

Approved onboarding input is final enough to include:

- merchant display name;
- explicit IANA business timezone;
- stable idempotency identity;
- authenticated verified Subject identity from the trust boundary.

VND is product-wide policy; onboarding does not accept a selectable functional currency.

Completed business outcome:

```text
Active Tenant
+ Active initial Owner Membership
+ 30-day Trial Subscription / Trial EntitlementSet
```

ADR-009 technical behavior:

1. Tenancy atomically accepts the registration operation plus Tenant/Owner/authority/discovery/owner-guard and durable Trial-recovery work intent;
2. coordinator calls `SubscriptionBilling.StartTrialSubscription` idempotently;
3. completed response only after Trial acceptance;
4. interruption after Tenancy commit returns `202 Accepted` with durable operation identity/status;
5. SQS recovery retries the same Trial logical source;
6. no cross-module transaction and no destructive Tenant rollback.

Conceptual onboarding status contract:

```text
MerchantOnboardingOperation
  operationId
  tenantId
  initialOwnerMembershipId
  state              # technical: CompletingTrial | Completed | NeedsAttention
  trialSubscriptionId?  # result reference only, not Tenancy commercial authority
  correlationId
```

Technical process state must not be exposed as a fake Subscription/Tenant business state.

### 4.2 Public storefront

A public request uses a separate `PublicTenantContext` and can never call merchant-protected commands.

Catalog owns public Product eligibility/projection. Storefront may compose that projection with derived availability but cannot authorize checkout from a cache.

The Product public slug contract is approved, but the **Tenant storefront address owner/lifecycle/uniqueness contract is still missing**. Therefore no final public Tenant route/index/cache key is approved yet.

### 4.3 Background consumer/workflow task

A background handler uses a service/message execution context built from validated versioned input.

It:

- validates type/version/source/Tenant identifiers;
- scopes consumer-owned persistence by the message/workflow Tenant;
- deduplicates by inbox/source identity;
- never acts as a merchant role;
- never reads/writes producer persistence;
- preserves correlation/causation identities.

Step Functions task handlers obey the same rule. Workflow input is process evidence, not business-state authority.

### 4.4 Platform administration

Platform administration is a separate authenticated/authorized/audited path.

For SubscriptionBilling, current MVP platform-admin scope is read-only support visibility. It is not `TrustedTenantContext + bypass` and has no direct table access.

## 5. Tenancy application contracts

### Merchant registration

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

Rules:

- server assigns Tenant/Membership identities;
- same logical intent + equivalent input returns same logical operation/result;
- incompatible replay conflicts;
- completed result cannot omit Trial;
- Tenant name/SubjectId are not global business-uniqueness keys.

### Membership and roles

MVP role is exactly one of Owner/Admin/Staff/Viewer.

Contract rules:

- expected revision for unsafe lost-update operations;
- last Active Owner cannot be disabled/demoted/removed;
- Admin cannot grant/revoke Owner authority;
- Viewer is read-only;
- Staff receives only explicitly approved operational mutation authority;
- hard staff limit uses current Subscription entitlement + Merchant Access authoritative count;
- downgrade never auto-disables a Membership.

### Invitations

Invitation issue/resend/accept/revoke contracts preserve:

- Tenant-bound normalized recipient email;
- acceptance only by authenticated identity with matching verified email;
- at most one active Pending invitation per Tenant + normalized email;
- resend rotates/reissues credential and invalidates prior credential;
- seven-day expiry;
- single-use acceptance;
- existing Active member -> harmless already-member result;
- Disabled member is not silently reactivated;
- last-owner and Subscription hard-limit invariants still apply to resulting Membership changes.

Secrets/credentials never appear in logs/problem responses.

## 6. Catalog contracts

### Draft/create/update

- ProductId/TenantId server/authority controlled;
- Draft may omit SKU;
- VND Money is structured and price may be zero;
- expected revision used for unsafe lost updates;
- Owner/Admin may mutate Catalog under the approved role policy.

### SKU

- case-insensitive Tenant uniqueness through stable normalization;
- SKU required before first publication;
- Draft SKU may change under the lifecycle rule;
- after first publication SKU is immutable;
- Archived Product permanently retains its normalized SKU claim;
- cross-domain references use ProductId and may snapshot displayed SKU.

### Lifecycle

```text
Draft -> Published <-> Unpublished
  |         |
  +-------> Archived
Unpublished -> Archived
```

`Archived` is terminal. Published edits affect current canonical/public projection directly; no hidden draft-revision workflow exists.

### Public slug

- ProductId remains canonical identity;
- Published Product has Tenant-scoped mutable normalized slug;
- slug collision rejects without changing Product;
- slug change requires no redirect history in MVP;
- slug is not Tenant authority.

### Category/Brand

- zero/one flat Category;
- zero/one Brand;
- normalized names Tenant-unique case-insensitively;
- records retire non-destructively and existing Product references remain;
- no cascade delete.

If a rename/retirement task needs historical normalized-name reuse semantics not stated by Domain Architecture, it stops for a domain decision.

### Specifications/media/public projection

Public Product projection may expose approved ProductId/slug/name/description/VND price/SKU/Category/Brand/specifications/media and derived availability.

It never exposes advisory cost, raw ingestion snapshots, internal revision/history, or merchant-private metadata.

Media association accepts only same-Tenant `FilesMedia` asset references. Catalog does not accept arbitrary external image URL/hotlink authority.

### Import application

`ApplyImportCandidate` is an explicit Catalog command carrying stable source/candidate references and approved values.

- PDI owns candidate/snapshot evidence;
- Catalog applies its own validation/lifecycle rules;
- PDI marks candidate Applied only after Catalog accepts the canonical mutation;
- one external source-product identity maps to at most one Product per Tenant;
- no PDI table read from Catalog.

## 7. Sales / Inventory / Payments contracts

### 7.1 Place Order

Conceptual behavior:

```text
PlaceOrder(checkoutIntent, shopperSnapshot, lines, requestMetadata)
  -> authoritative Catalog/Pricing validation
  -> if any price changed: ReconfirmRequired; no Order
  -> else accept immutable SalesOrder snapshot
```

Rules:

- whole-unit quantities only;
- no authoritative client totals/discounts;
- one logical checkout intent creates at most one Order;
- later Catalog/Customer changes never rewrite accepted Order snapshots.

### 7.2 Reserve stock

Inventory owns a source-idempotent `ReserveOrderStock` contract.

Result is either an accepted whole-order reservation or a rejection/process exception. It must preserve Inventory quantity invariants under concurrency.

Architecture does not invent a product max-order-line limit. If required transaction cardinality exceeds DynamoDB's bounded transaction capability, the implementation needs a durable Inventory reservation coordinator that still exposes only whole-order accepted reservation semantics.

### 7.3 Payment capture/reconciliation

Payments owns one Payment obligation per Order with multiple immutable attempts.

Conceptual contracts:

```text
CaptureOrderPayment(orderObligation, operationIdentity)
ReconcilePayment(paymentId/attemptId, sourceIdentity)
GetPaymentOutcome(paymentId)
```

Rules:

- amount/currency comes from accepted Sales obligation;
- equivalent retry creates one logical provider/payment effect;
- definitive decline/no-commit terminates the attempt only;
- no next attempt while prior attempt is `OutcomeUnknown`;
- timeout/missing callback/transport error is Unknown unless provider evidence proves otherwise;
- provider callbacks/query evidence are verified/deduplicated and cannot regress known state;
- no raw provider state leaks to Sales.

### 7.4 Confirm/allocate

After verified `PaymentCaptured`:

- Sales confirms Order idempotently using stable Payment source evidence;
- Sales marks OrderAllocated only after full reservation evidence is accepted;
- duplicate immediate result plus later integration event cannot double-transition Sales;
- stale/out-of-order evidence cannot regress later state.

### 7.5 Durable orchestration

ADR-010 selects Step Functions Standard from already accepted `OrderPlaced` through `OrderAllocated`.

Workflow technical states such as `AwaitingPaymentRetry`/`NeedsAttention` are not Sales business states.

The workflow never infers cancellation/release/failure from timeout/retry exhaustion and does not include automatic fulfillment or Accounting.

## 8. HTTP contract conventions

Governed by ADR-007.

### Versioning/representation

- consistent major version prefix, initially `/api/v1` unless a refined task records an equivalent gateway mapping;
- transport DTOs separate from application/Domain types;
- additive compatible response fields may appear within a major version;
- breaking semantic/required-field changes use a new major version/migration window;
- UTC ISO-8601 for timestamps; explicit business dates/timezones where business meaning requires them;
- Money is `{amount,currency}`-style structured data, never a bare currencyless number;
- identifiers are opaque strings;
- request TenantId/selector may identify a target but never become authorization scope.

### Success status baseline

| Result | HTTP behavior |
|---|---|
| synchronously created resource | `201 Created` + stable body/Location where a read route exists |
| completed command | `200 OK` or `204 No Content` fixed by route contract |
| query | `200 OK` |
| durable operation still progressing | `202 Accepted` only with operation identity/status query |
| equivalent idempotent replay | same logical result/status contract as original accepted operation |

Onboarding specifically uses `202` only when Tenancy accepted the durable operation but Trial completion is pending/recovering.

### Problem details

Errors use `application/problem+json` compatible with RFC 9457 and safe stable context codes/correlation.

Never include stack traces, DynamoDB keys, AWS payloads, token/claims, invitation credentials, provider secrets, raw payment/source payloads, or cross-Tenant disclosure.

### Stable transport mapping

| Outcome family | Status | Notes |
|---|---:|---|
| malformed contract/encoding | 400 | no domain command attempted |
| unauthenticated/invalid token | 401 | generally edge rejection |
| authenticated but current role/Membership/Tenant policy denies | 403 | non-disclosing |
| aggregate absent/not visible | 404 | same shape for foreign Tenant target |
| business validation | 422 | stable owner code |
| uniqueness/invalid transition/incompatible replay | 409 | no accepted state overwritten |
| required precondition omitted | 428 | revision-sensitive routes |
| stale ETag/revision | 412 | context stale-revision code |
| bounded rate/concurrency guard | 429 | safe Retry-After when known |
| required authority/dependency unavailable | 503 | fail closed; no stale fallback |
| unexpected failure | 500 | generic safe code + correlation |

`OutcomeUnknown` is not mapped to definitive failure. When exposed as still-progressing durable work, use a status resource/202; when it is a current resource state, return that state truthfully under the route contract.

## 9. Optimistic concurrency

For unsafe lost-update aggregate mutations:

1. GET returns opaque ETag derived from aggregate revision;
2. mutation requires `If-Match`;
3. delivery maps ETag to application expected revision;
4. Infrastructure uses conditional write/transaction;
5. missing precondition -> 428;
6. losing concurrent update -> 412 with stable stale-revision code.

Server does not silently retry stale business intent against a newer state.

## 10. Idempotency

`Idempotency-Key` is required only for a route/command whose retry could duplicate unsafe effects/resources and is documented accordingly.

Scope:

```text
operation
+ trusted Tenant/actor scope
  or approved pre-Tenant onboarding principal scope
+ idempotency key
```

Record stores semantic request fingerprint, processing/accepted status, stable result references, and bounded cleanup metadata where safe.

| Existing logical record | Fingerprint | Result |
|---|---|---|
| none | valid | claim and execute |
| accepted | equivalent | replay original logical result |
| accepted/processing | incompatible | 409 conflict; no effect |
| interrupted/unknown | equivalent | resume/reconcile same logical operation |

TTL is storage cleanup only and never removes a permanent business uniqueness/source invariant.

Correlation ID is not an idempotency key.

## 11. Correlation and causation

Keep these identities distinct:

| Identifier | Meaning |
|---|---|
| requestId | one transport attempt |
| command/idempotency identity | one logical requested effect |
| correlationId | related end-to-end flow |
| workflow/process id | one durable technical orchestration instance |
| eventId | one immutable published integration fact |
| causationId | direct causing command/event |
| aggregateId | owning aggregate related to the fact |

Retry normally gets a new requestId but preserves logical command/source identity. Event redrive preserves eventId/source identity.

## 12. Pagination and query safety

List contracts use bounded page size and opaque versioned cursor bound to:

- trusted Tenant scope;
- route/query contract version;
- approved filter/sort shape;
- owner repository continuation key.

Never expose raw DynamoDB `LastEvaluatedKey`, trust TenantId inside cursor, or add arbitrary filter/sort/search without an access pattern.

No route promises total count unless a maintained count access pattern exists.

## 13. Remaining contract gates

The old broad first-frontier product gates are closed by the 2026-08-10 product/domain pass. Current incomplete contracts are limited to actual remaining business gaps:

| Contract | Remaining blocker |
|---|---|
| exact Suspended Tenant read/support/closure/privacy routes | `PD-004` |
| refund/return Accounting command/event/result shape | `PD-023` |
| exact SaaS sellable package/price/entitlement contract | deferred exact portion of `PD-044` |
| public Storefront Tenant address route/binding | missing domain ownership/lifecycle/uniqueness semantics |
| Accounting weighted-average valuation-position scope | missing domain cost-pool dimension |
| Category/Brand historical normalized-name reuse if required | missing domain rule |

A Builder must not select placeholder defaults for these gaps.

## 14. Required verification when implemented

- public HTTP/event serialization/compatibility fixtures;
- Tenant A/Tenant B/anonymous/inactive/suspended authorization cases;
- multi-Membership explicit selection cases;
- request/JWT/cursor attempts to override Tenant scope;
- onboarding crash between Tenancy commit and Trial response plus SQS recovery/idempotent replay;
- invitation expiry/recipient/resend/single-use races;
- last-owner and staff-entitlement-limit races;
- Catalog revision/SKU/slug/name/source uniqueness/lifecycle races;
- price-change reconfirmation creates no Order;
- duplicate workflow start/reservation/payment/Sales convergence creates one logical effect;
- Payment Unknown remains Unknown and retains reservation despite technical timeout/retry exhaustion;
- Accounting source replay posts once and remains balanced;
- problem responses/logs remain non-disclosing and secret-safe;
- cursor cannot cross Tenant/query shape;
- no raw DynamoDB/provider/AWS type leaks through contracts.

## 15. References

- [Technical architecture baseline](technical-baseline.md)
- [Product-decision technical reconciliation](product-decision-technical-reconciliation.md)
- [ADR-004](../adr/ADR-004-trusted-tenant-authority-and-authorization-boundary.md)
- [ADR-007](../adr/ADR-007-versioned-http-contract-and-command-safety-conventions.md)
- [ADR-008](../adr/ADR-008-subscription-billing-module-entitlement-and-provider-boundary.md)
- [ADR-009](../adr/ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md)
- [ADR-010](../adr/ADR-010-order-payment-allocation-durable-orchestration.md)