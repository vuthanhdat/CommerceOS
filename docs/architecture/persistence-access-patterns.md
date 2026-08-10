# CommerceOS — Persistence Ownership and Access Patterns

_DynamoDB/consistency baseline originally reconciled by TASK-0088 and refreshed on 2026-08-10 after the product/domain decision pass._

## 1. Decision and scope

CommerceOS starts with **one DynamoDB table per implementation module**, introduced only when that module has a Ready persistence task.

Examples:

```text
Tenancy              -> Tenancy table
Catalog              -> Catalog table
SubscriptionBilling  -> SubscriptionBilling table
Sales                -> Sales table
Inventory            -> Inventory table
Payments             -> Payments table
Procurement           -> Procurement table
Accounting            -> Accounting table
Audit                 -> Audit table when introduced
Notification          -> Notification table when introduced
FilesMedia            -> FilesMedia metadata table when introduced
```

This is not table-per-aggregate and not one platform-wide single table. Multiple aggregates owned by one module may share a table and bounded module-local transactions.

No other module reads its records, indexes, stream, key codec, serialization model, or repository.

ADR authority: [ADR-005](../adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md).

The 2026-08-10 [technical reconciliation](product-decision-technical-reconciliation.md) closes historical product gates where domain semantics are now approved.

## 2. Universal repository rules

1. Tenant-owned repository methods require trusted Tenant scope and have no unscoped tenant overload.
2. Tenant-owned aggregate base keys/queries include immutable TenantId.
3. A technical cross-Tenant authorization/operations lookup is allowed only when explicitly approved, minimal, narrowly IAM-scoped, and never exposed as a normal tenant repository.
4. Aggregate IDs, SKU, slug, email, source IDs, provider IDs, route/query/body/header/cursor values remain untrusted inputs and cannot replace Tenant scope.
5. Application paths use documented `GetItem`, `BatchGetItem`, `Query`, conditional writes, or bounded transactions; application `Scan` is prohibited.
6. `FilterExpression` is not a substitute for a key/index and cannot hide an unbounded access path.
7. A GSI exists only for an approved query and never becomes the sole authority/uniqueness/revision/invariant check.
8. Strong/transactional reads are used where current correctness requires them; bounded lists/rebuildable projections may be eventual when the contract says so.
9. IDs are opaque/server-assigned. Mutable names/SKU/slug/email/source identifiers are not aggregate identity.
10. Optimistic mutations carry aggregate revision/expected revision where lost update is unsafe.
11. A transaction attaches the invariant condition to the same item write whenever possible; do not issue redundant condition-check + mutation actions for the same item.
12. Cross-domain DynamoDB transactions and foreign-table writes/reads are not approved.
13. Command/idempotency, work/outbox, event outbox, inbox/source, and technical process records belong to the accepting/producing/consuming module.
14. TTL cleans technical replay/cache records only where expiry cannot weaken permanent business uniqueness/history/source deduplication.
15. Every persistence task documents isolation proof and cost amplification for table/index/transaction/stream usage.

## 3. Required access-pattern ledger

Every DynamoDB implementation task maintains a module-local ledger with:

| Field | Required content |
|---|---|
| ID | stable access-pattern identifier |
| Owner | module + aggregate/projection/technical record owner |
| Use case | command/query/process that needs it |
| Trusted scope | source of Tenant/platform/subject scope |
| Key/query | base key or approved index; no schema dump |
| Cardinality | one, bounded page, known bounded batch |
| Consistency | transactional/strong/eventual with rationale |
| Write protection | revision/condition/transaction/idempotency/source rule |
| Pagination/order | stable order + opaque cursor |
| Isolation proof | cross-Tenant/unscoped-call tests |
| Recovery | duplicate/reconciliation/migration expectations |
| Cost note | request/write/index/storage amplification |

A task that says only “store it in DynamoDB” is not persistence-ready.

## 4. Symbolic key grammar

Examples communicate access intent, not final attribute names:

```text
tenant aggregate partition: PK = TENANT#<TenantId>
aggregate item:             SK = <TYPE>#<AggregateId>
singleton/guard:            SK = <TYPE>
claim:                      SK = <CLAIM-TYPE>#<NormalizedValue>
command/idempotency:        SK = COMMAND#<Operation>#<ScopedKeyDigest>
technical process:          SK = PROCESS#<ProcessId>
work outbox:                SK = WORKOUTBOX#<WorkId>
event outbox:               SK = OUTBOX#<EventId>
inbox/source marker:        SK = INBOX#<SourceType>#<SourceId>
```

A module-private codec normalizes/escapes user-controlled strings before key construction.

`ScopedKeyDigest` covers canonical operation + trusted actor/principal/Tenant scope + bounded idempotency key. It is never a hash of the raw header alone.

## 5. Tenancy table

`Tenancy` hosts Tenant Management and Merchant Access as distinct model areas and can transact across their records because they share one implementation module/table.

### 5.1 Logical records

| Record | Symbolic access intent | Owner/purpose |
|---|---|---|
| Tenant + BusinessProfile | `PK=TENANT#t, SK=TENANT` | Tenant Management aggregate |
| Membership | `PK=TENANT#t, SK=MEMBERSHIP#m` | Merchant Access aggregate |
| current authority lookup | `PK=TENANT#t, SK=AUTHORITY#SUBJECT#s` | current selected-Tenant authority lookup |
| active-owner guard | `PK=TENANT#t, SK=OWNER-GUARD` | last Active Owner invariant |
| Invitation | `PK=TENANT#t, SK=INVITATION#i` | Merchant Access invitation aggregate |
| subject Membership discovery | `PK=SUBJECT#s, SK=MEMBERSHIP#m#TENANT#t` | strongly consistent candidate discovery across Tenants; technical lookup only |
| onboarding operation | trusted pre-Tenant/onboarding scope + stable operation id | durable registration completion/recovery status |
| command/idempotency record | trusted pre/post-Tenant scope | duplicate-safe command result |
| Trial-bootstrap work outbox | colocated with onboarding operation | guaranteed recovery command to one worker |
| integration/Audit outbox | module-owned source scope | durable delivery intent, not Audit business state |

The subject discovery record is deliberately not Tenant business data and never authorizes a business command by itself. Final authority always reads current selected Tenant + Membership authority.

### 5.2 Tenancy access patterns

| ID | Use case | Access / protection |
|---|---|---|
| `TEN-AP-01R` | merchant registration local commit | one `TransactWriteItems`: onboarding operation/idempotency claim + Active Tenant + initial Owner + authority lookup + subject discovery record + owner guard + Trial work-outbox + accepted-state Audit intent when applicable |
| `TEN-AP-02` | get Tenant/Profile | strong/current base `GetItem` when status gates a command; eventual permitted only for safe display contract |
| `TEN-AP-03` | update Profile | Tenant item expected revision; TenantId immutable |
| `TEN-AP-04R` | resolve selected Tenant authority | transactionally/currently read Tenant + authority record from Tenant partition; no JWT/GSI/cache fallback |
| `TEN-AP-05` | get/manage Membership | base `GetItem`; strong for mutation; expected revision |
| `TEN-AP-06` | list Tenant Memberships | bounded `Query` on Tenant partition; eventual permitted for management list, never authorization |
| `TEN-AP-07R` | disable/reactivate/change role | transaction Membership + authority lookup + subject discovery + owner guard as applicable; expected revisions/last-owner condition |
| `TEN-AP-08R` | create/manage Invitation | tenant partition; one Pending per normalized recipient enforced by an authoritative claim/record strategy; explicit expiry/state revision |
| `TEN-AP-09R` | accept Invitation | all-or-nothing transaction for Pending/not-expired/single-use/verified-recipient proof + resulting Membership/authority/discovery/owner guard changes; stable acceptance identity |
| `TEN-AP-10` | suspend/reactivate Tenant | expected revision/state condition; no Membership/Subscription rewrite; exact suspended read/support semantics remain `PD-004` gated |
| `TEN-AP-11R` | subject Membership discovery | strongly consistent base-table `Query` on `SUBJECT#s`; minimal data; final Tenant/Membership revalidation required |
| `TEN-AP-12` | platform Tenant support list/search | no generic unscoped merchant access; introduce only with explicit platform-admin contract/security/index design |
| `TEN-AP-13` | onboarding operation status/Trial completion | stable operation `Get/Update` with expected technical state and idempotent result reference; no copied Subscription authority |

### 5.3 Onboarding persistence boundary

ADR-009 replaces the old Tenancy-only “completed onboarding” assumption.

Tenancy transaction:

```text
onboarding operation/idempotency claim
+ Active Tenant
+ Active initial Owner
+ current authority lookup
+ subject discovery link
+ owner guard
+ Trial-bootstrap work-outbox
+ accepted mutation Audit intent where required
```

SubscriptionBilling Trial creation occurs in its own table/transaction through an idempotent application command. No cross-table transaction is used.

If the Trial call is interrupted, the durable work-outbox supports Stream-relayed SQS recovery. Tenancy operation becomes Completed only after Trial acceptance is proven.

## 6. Catalog table

### 6.1 Logical records

| Record | Symbolic access intent | Purpose |
|---|---|---|
| Product | `PK=TENANT#t, SK=PRODUCT#p` | Catalog aggregate |
| normalized SKU claim | `PK=TENANT#t, SK=SKU#<normalized>` | authoritative Tenant uniqueness/permanent post-publication history |
| public slug claim | `PK=TENANT#t, SK=SLUG#<normalized>` | authoritative current public slug uniqueness |
| Category | `PK=TENANT#t, SK=CATEGORY#c` | flat Tenant reference aggregate |
| Category normalized-name claim | `PK=TENANT#t, SK=CATEGORY-NAME#<normalized>` | authoritative uniqueness |
| Brand | `PK=TENANT#t, SK=BRAND#b` | Tenant reference aggregate |
| Brand normalized-name claim | `PK=TENANT#t, SK=BRAND-NAME#<normalized>` | authoritative uniqueness |
| source-product mapping claim | `PK=TENANT#t, SK=SOURCEPRODUCT#<source>#<externalId>` | one source identity → zero/one Product |
| command/outbox | module-owned tenant scope | retry/event delivery |

Product-owned specifications/media associations may be embedded or split into Product-part items only after bounded cardinality/item-size needs are known. That split does not create another aggregate or partial lifecycle authority.

### 6.2 Catalog access patterns

| ID | Use case | Access / protection |
|---|---|---|
| `CAT-AP-01R` | create Draft | conditional Product put; optional initial SKU claim in same module transaction; stable create idempotency where route requires it |
| `CAT-AP-02` | get Product | tenant-scoped strong base read for mutation/checkout; non-disclosing absence |
| `CAT-AP-03` | list Products | bounded Tenant partition query; opaque Tenant/query-bound cursor; eventual permitted for management list |
| `CAT-AP-04` | change Product fields | expected revision; Archived rejects ordinary mutation |
| `CAT-AP-05R` | change Draft SKU | transaction claims new normalized SKU + updates Product revision + releases prior Draft claim only while domain lifecycle allows mutation |
| `CAT-AP-06R` | resolve Product by SKU | strong tenant SKU-claim read + Product read/validation; ProductId remains canonical identity |
| `CAT-AP-07R` | Category create/read/list | Category + normalized-name claim; conditional uniqueness; bounded Tenant list |
| `CAT-AP-08R` | Brand create/read/list | Brand + normalized-name claim; conditional uniqueness; bounded Tenant list |
| `CAT-AP-09R` | assign Category/Brand | same-Tenant reference validation via Catalog-owned base records + Product expected revision; no cascade |
| `CAT-AP-10R` | publish/unpublish/archive | expected revision/lifecycle condition; first publish requires SKU and makes SKU immutable; Archive never releases permanent historical SKU claim |
| `CAT-AP-11R` | public Product read/list | Catalog public projection only after Storefront Tenant-address contract supplies public Tenant scope; Product slug claim is ready but Tenant address is not |
| `CAT-AP-12` | approved filters | each filter needs keyed/sparse bounded index; no broad FilterExpression/Scan substitute |
| `CAT-AP-13R` | apply ImportCandidate | explicit Catalog command using stable candidate/source references; Product transaction only; no PDI table access |
| `CAT-AP-14` | change Product slug | atomically claim new tenant slug + Product revision and release old current slug; no redirect record required |
| `CAT-AP-15` | create/rename Category name | authoritative normalized-name claim + Category revision; historical-name reuse behavior remains domain-owned if implementation needs it |
| `CAT-AP-16` | create/rename Brand name | authoritative normalized-name claim + Brand revision; historical-name reuse behavior remains domain-owned if implementation needs it |
| `CAT-AP-17` | attach/detach media | Catalog Product update references validated same-Tenant `MediaAssetId` via FilesMedia application contract; no FilesMedia table read |
| `CAT-AP-18` | link external source Product | authoritative tenant source-product mapping claim + Product provenance change; one mapping only |

### 6.3 SKU permanence

Draft SKU mutation may use:

```text
Update Product expected revision
+ Put new SKU claim if absent
+ Delete old Draft claim if still owned by Product
```

After first publication there is **no SKU mutation path**. Unpublish/Archive retains the SKU claim permanently.

Architecture must not implement “archive frees SKU”.

### 6.4 Public projection

Query-time mapping from authoritative Product is the safe initial default. A persisted Catalog-owned public projection is introduced only if a Ready task proves it is needed.

Any persisted projection:

- remains Catalog-owned;
- cannot be checkout authority;
- updates atomically/recoverably with Product lifecycle;
- removes Unpublished/Archived visibility deterministically;
- does not finalize Tenant public address keys before Storefront Tenant-address domain semantics exist.

## 7. Sales table

A Sales table is introduced with the first Ready Order persistence task.

### Logical records/access needs

| ID | Use case | Access / protection |
|---|---|---|
| `SAL-AP-01` | accept Order | Tenant+Order item with immutable line/price/customer/fulfillment snapshot + state/revision; checkout-intent idempotency claim |
| `SAL-AP-02` | get Order | tenant-scoped base read; non-disclosing cross-Tenant absence |
| `SAL-AP-03` | list Orders | bounded Tenant/time/status query through an approved key/index; opaque cursor |
| `SAL-AP-04` | confirm after PaymentCaptured | expected state/revision + source-idempotency against accepted Payment capture identity |
| `SAL-AP-05` | mark allocated | expected Confirmed state + accepted full reservation evidence/source identity; idempotent |
| `SAL-AP-06` | cancel | expected pre-Fulfilled state/revision; Sales fact only; foreign release/refund effects separate |
| `SAL-AP-07` | fulfillment/completion | introduced only with refined fulfillment contract; no automatic shipping semantics invented here |
| `SAL-AP-08` | technical order process | module-owned process/reference record keyed to Order; separate from SalesOrder business state; supports ADR-010 support/recovery view |
| `SAL-AP-09` | workflow-start outbox | atomic with OrderPlaced/process record; deterministic Step Functions start recovery |
| `SAL-AP-10` | integration outbox | named facts such as OrderConfirmed/OrderFulfilled only when consumers exist |

`OrderPlaced` may commit even while downstream process is pending; API/order status must not claim reservation/payment/confirmation until the owning facts exist.

## 8. Inventory table

Inventory owns physical quantity truth and reservation/movement history.

### Logical records

- Warehouse/Location aggregate;
- StockItem by Tenant + Product + Warehouse/Location;
- StockReservation/source lifecycle;
- immutable StockMovement;
- InventoryAdjustment evidence;
- command/source-idempotency records;
- integration outbox.

### Required access patterns

| ID | Use case | Protection |
|---|---|---|
| `INV-AP-01` | get StockItem | trusted Tenant + Product + Warehouse base key; strong/current for mutation decisions |
| `INV-AP-02` | reserve Order stock | source-idempotent conditional/transactional quantity changes preserving `Available >= 0`; expose accepted whole-order result only |
| `INV-AP-03` | release reservation | source/reservation expected state; decrement Reserved once; immutable movement/effect |
| `INV-AP-04` | issue stock | reservation/source expected state; atomically apply `OnHand -= q`, `Reserved -= q`; one movement |
| `INV-AP-05` | receive | source-idempotent `OnHand += q`; one immutable movement |
| `INV-AP-06` | return | source-idempotent `OnHand += q`; one movement; no implied refund/accounting |
| `INV-AP-07` | adjust | explicit reason/source; decrease condition must preserve `OnHand >= Reserved`; one movement |
| `INV-AP-08` | low-stock state/query | derived from current Available/threshold; notification/projection may be eventual but quantity truth is base state |
| `INV-AP-09` | Warehouse growth/activation | current Subscription entitlement + owner-authoritative count/state + owner-local condition; no subscription table read |

Quantity invariants:

```text
OnHand >= 0
Reserved >= 0
Available = OnHand - Reserved
Available >= 0
```

### All-line reservation cardinality

If all StockItem/reservation actions fit one bounded DynamoDB transaction, Inventory may implement all-line reservation atomically inside its table.

If real Order cardinality can exceed that safe bound, a later architecture task must introduce a durable Inventory-local reservation coordinator/compensation process that never exposes partial reservation as the accepted whole-order result.

Do **not** invent a product max-order-line limit to fit DynamoDB.

`OutcomeUnknown` Payment never drives reservation expiry/release by time.

## 9. Payments table

Payments owns merchant-order Payment truth.

### Logical records/access needs

| ID | Use case | Protection |
|---|---|---|
| `PAY-AP-01` | one Payment obligation per Order | Tenant+Payment/Order claim; accepted Sales obligation amount/currency; idempotent create |
| `PAY-AP-02` | create PaymentAttempt | immutable attempt identity; no next attempt while prior Unknown |
| `PAY-AP-03` | record provider operation/result | stable logical operation id + expected Payment revision; verified evidence only for definitive result |
| `PAY-AP-04` | ingest callback/evidence | provider-evidence dedup claim; authenticate/verify first; out-of-order non-regression |
| `PAY-AP-05` | reconcile Unknown | current Payment/attempt + provider reference; bounded due-work index only if required; no Scan |
| `PAY-AP-06` | refund | cumulative verified refunds cannot exceed capture; refund business/accounting routes follow their own decisions |
| `PAY-AP-07` | integration outbox | PaymentCaptured/Declined/Unknown/Refunded only for named consumers |

Timeout/network/missing callback creates/retains Unknown observation where outcome cannot be proven. Persistence must never store a transport timeout as definitive NoCommit unless provider semantics prove it.

## 10. SubscriptionBilling table

ADR-008 owns this module strategy.

### Required records/access patterns

| ID | Use case | Protection |
|---|---|---|
| `SUB-AP-01R` | get current Tenant Subscription | strong/current tenant base access; no GSI authority |
| `SUB-AP-02R` | evaluate Entitlement | coherent current Subscription + effective EntitlementSet provenance; strong/transactional as needed |
| `SUB-AP-03` | history | bounded immutable tenant history; eventual permitted for display with freshness semantics |
| `SUB-AP-04R` | Trial/create/upgrade/downgrade/renew/reactivate transition | expected Subscription revision + stable command identity + immutable accepted history; intent never equals effectivity |
| `SUB-AP-05R` | Order volume UsageMeter | tenant + current billing-period meter; `OrderConfirmed` source-idempotency; warning-only |
| `SUB-AP-06R` | PlatformCharge | separate aggregate/revision + logical charge identity; VND whole-đồng amount; Unknown preserved |
| `SUB-AP-07R` | provider evidence | verified/deduplicated evidence mapping to PlatformCharge; non-regression |
| `SUB-AP-08R` | due reconciliation | sparse bounded/sharded operations index only when simulated provider execution needs periodic/due work; never tenant authority |
| `SUB-AP-09R` | platform-admin support visibility | explicit application query; separate admin context; no mutation/direct table access |
| `SUB-AP-10` | onboarding Trial source | stable onboarding/Tenant source claim ensures one logical Trial |
| `SUB-AP-11` | restrictive downgrade process | durable transition + per-owner assessment/fence acknowledgements; no foreign resource copy/mutation |

Platform-global Plan/PlanVersion records remain SubscriptionBilling-owned. Accepted PlanVersions are immutable. Exact sellable price/entitlement package values remain deferred under `PD-044`.

## 11. Procurement table

Procurement owns Supplier/PO/receipt/invoice/payment evidence.

Required patterns include:

- Supplier by Tenant+SupplierId, Active/Archived revision;
- PurchaseOrder Draft/Submitted snapshots; Submitted immutable;
- cancellation condition only while no confirmed downstream Procurement evidence exists;
- GoodsReceipt immutable confirmed evidence + explicit correction record referencing original;
- derived received quantity from accepted receipt/correction evidence;
- one SupplierInvoice and one SupplierPayment per PO claims under the MVP cardinality;
- invoice/payment idempotency/source evidence;
- integration outbox for `GoodsReceiptRecorded`, `SupplierInvoiceRecorded`, `SupplierPaymentRecorded`.

Procurement never writes Inventory or Accounting tables.

## 12. Accounting table

Accounting owns chart, valuation/posting state, immutable journals, and ledger derivations.

### Logical records/access needs

| ID | Use case | Protection |
|---|---|---|
| `ACC-AP-01` | control/non-control Account | Tenant-scoped stable identity/code uniqueness claim; deactivation rules; no reuse after posted reference |
| `ACC-AP-02` | apply authoritative source | source/inbox claim + balanced Journal + affected Accounting-owned state in one module transaction where possible |
| `ACC-AP-03` | get Journal | tenant-scoped base read; immutable after posting |
| `ACC-AP-04` | General Ledger | Tenant + EffectiveDate/account keyed/indexed bounded query; no foreign table lookup |
| `ACC-AP-05` | Trial Balance | derived from Accounting-owned journal truth/maintained projection; source data never queried directly |
| `ACC-AP-06` | reversal/correction | new linked compensating Journal; original never edited/deleted |
| `ACC-AP-07` | valuation position | key/scope **not final** until Domain Architecture states moving-weighted-average cost-pool dimension |
| `ACC-AP-08` | source reconciliation | query module-owned source/posting records to find missing/failed expected effect; never producer table read |

Approved posting sources:

- PaymentCaptured;
- OrderFulfilled;
- StockIssued;
- GoodsReceiptRecorded;
- SupplierInvoiceRecorded;
- SupplierPaymentRecorded;
- StockAdjusted.

`PaymentRefunded`/`StockReturned` have **no Accounting posting access path** until `PD-023` is resolved.

The source event contract must carry the stable business data needed to post; Accounting must not query producer persistence for missing data.

### Moving weighted average gap

The domain approves moving weighted-average inventory valuation but does not state whether the authoritative cost pool is Tenant+Product, Tenant+Product+Warehouse, or another dimension.

`ACC-AP-07` therefore remains `DOMAIN DECISION REQUIRED`. Architecture does not choose a key that silently defines Accounting policy.

## 13. Audit table

Audit is append-oriented and not source business state.

When introduced:

- Audit append is idempotent by durable audit-intent/source identity;
- tenant-visible records are Tenant-scoped and queryable only under Owner/Admin policy;
- cross-Tenant/security investigation detail is not exposed to tenant-visible queries;
- successful covered mutations write source-owned audit intent atomically with source state;
- covered rejected attempts with no business-state transaction persist a standalone source-owned audit-delivery intent before completing the application result where practical;
- Audit consumers never read source tables.

## 14. Notification table

Notification persistence is per recipient.

Required concepts:

- immutable notification/source identity;
- recipient-specific state `Unread/Read/Acknowledged`;
- delivery attempt/evidence separate from read/ack state;
- one recipient mutation never touches another recipient state;
- acknowledgement never mutates source-domain exception state;
- source replay must not duplicate the same logical notification effect.

## 15. FilesMedia persistence and S3 boundary

When FilesMedia is introduced:

- DynamoDB metadata records use trusted Tenant scope and stable `MediaAssetId`;
- binary objects live in private S3 under server-generated tenant/asset keys;
- object key is an implementation detail, not public asset identity;
- upload authorization is issued through a FilesMedia application contract and cannot select another Tenant's prefix;
- Catalog stores only same-Tenant MediaAsset references/association metadata;
- arbitrary external image hotlink/copy is not a supported public media path;
- delete/retention lifecycle must not break referenced Product history without an approved FilesMedia/domain policy.

CloudFront/public projection delivery remains non-authoritative for Catalog lifecycle/checkout.

## 16. Technical records

### Command/idempotency record

- conditionally claimed inside same transaction as first durable effect where possible;
- stores canonical operation, trusted scope digest, semantic request fingerprint, state/result reference;
- incompatible reuse conflicts;
- cleanup TTL only where permanent uniqueness/source history is unaffected.

### Work outbox

Use when a required one-worker command must survive source commit, e.g. onboarding Trial recovery.

- written atomically with source operation/state;
- complete stable work contract, not foreign-table pointer;
- Stream relay targets named SQS queue directly;
- duplicate relay/worker delivery safe;
- pending record remains queryable after stream retention.

### Integration outbox

- written atomically with producing business state;
- contains producer-owned versioned fact envelope/payload;
- not raw table/domain serialization;
- delivered through ADR-006 when a named fact consumer exists.

### Inbox/source marker

- owned by consuming module;
- records event/source version/identity/fingerprint as needed;
- commits with consumer-owned side effect where possible;
- equivalent replay returns prior effect; incompatible reuse rejects.

### Technical process record

- tracks application/workflow recovery/progress only;
- never replaces owning aggregate state;
- may reference workflow execution/foreign result identities for support/recovery without becoming foreign business authority.

## 17. Capacity, indexes, security, and cost

- initial tables are single-region `ap-southeast-1`;
- no Global Tables without later recovery/data-residency ADR;
- small provisioned learning capacity may be used inside aggregate Free Tier target; on-demand may be selected intentionally for preview/burst/production-like behavior;
- every GSI needs an approved query, item projection, tenant/platform scope, cardinality, consistency note, cost estimate, and deletion/migration plan;
- no speculative PITR/customer-managed KMS/global index;
- AWS-managed/default encryption unless a later threat/compliance decision requires otherwise;
- CDK grants explicit table/index/stream actions only to named runtime roles;
- the initial shared Lambda can physically have grants to several module tables, so code boundaries/tests must still prohibit foreign access;
- no raw token/credential/provider secret/card data in items or keys.

## 18. Remaining persistence gates

Do not finalize keys/access behavior for:

- exact Suspended/closure/retention/privacy lifecycle under `PD-004`;
- refund/return Accounting under `PD-023`;
- exact sellable Plan package values under `PD-044`;
- Storefront Tenant-address index/key until domain semantics exist;
- Accounting weighted-average valuation cost-pool key until domain scope exists;
- Category/Brand historical normalized-name reuse if a task reaches it without a domain rule.

## 19. Validation

Dependent tasks must include applicable tests for:

- no application Scan/unscoped tenant repository;
- Tenant A/B known-ID/cursor/claim/provider/source isolation;
- strong subject discovery + current authority validation;
- last-owner/invitation/idempotency races;
- onboarding crash after Tenancy commit and Trial recovery without cross-module transaction;
- Draft SKU race, post-publication immutability, Archive claim permanence;
- slug/Category/Brand/source uniqueness races;
- Inventory reserve/issue/release/adjust quantity races and zero floor;
- Payment duplicate/Unknown/provider evidence non-regression;
- Subscription downgrade fence/current usage race and Trial duplicate safety;
- Accounting one-source-one-balanced-posting and no producer-table access;
- outbox/inbox duplicate/redrive/reconciliation behavior;
- CDK ownership/least-privilege/capacity/index/stream/removal assertions;
- no persistence design that fills an explicit domain gate by convenience.

## 20. References

- [Technical baseline](technical-baseline.md)
- [Product-decision technical reconciliation](product-decision-technical-reconciliation.md)
- [ADR-004](../adr/ADR-004-trusted-tenant-authority-and-authorization-boundary.md)
- [ADR-005](../adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md)
- [ADR-006](../adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md)
- [ADR-008](../adr/ADR-008-subscription-billing-module-entitlement-and-provider-boundary.md)
- [ADR-009](../adr/ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md)
- [ADR-010](../adr/ADR-010-order-payment-allocation-durable-orchestration.md)