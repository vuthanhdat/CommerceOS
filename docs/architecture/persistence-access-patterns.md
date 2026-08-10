# CommerceOS — Persistence Ownership and Access Patterns

_DynamoDB/consistency baseline refreshed on 2026-08-10 after final resolution of `PD-004`, `PD-023`, and `PD-044`._

## 1. Decision and scope

CommerceOS starts with **one DynamoDB table per implementation module**, introduced only when that module has a Ready persistence task.

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

This is not table-per-aggregate and not one platform-wide single table. Multiple aggregates owned by one implementation module may share a table and bounded module-local transactions.

No module reads another module's records, indexes, stream, key codec, serialization model, or repository.

ADR authority: [ADR-005](../adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md).

## 2. Universal persistence rules

1. Tenant-owned repository methods require trusted Tenant scope and expose no unscoped merchant overload.
2. Tenant-owned aggregate base keys/queries include immutable TenantId.
3. Explicit technical cross-Tenant lookup is allowed only when architecture approves it, it is minimal/IAM-scoped, and it never becomes ordinary tenant repository access.
4. Aggregate IDs, SKU, slug, email, source/provider IDs, route/query/body/header/cursor values remain untrusted inputs.
5. Application paths use documented `GetItem`, `BatchGetItem`, `Query`, conditional writes, or bounded transactions; application `Scan` is prohibited.
6. `FilterExpression` never substitutes for an access key/index.
7. GSI exists only for an approved eventual query and never becomes sole authorization/uniqueness/revision/invariant authority.
8. Strong/transactional reads are used where current correctness requires them; eventual reads are allowed only by contract.
9. Optimistic mutations carry expected revision where lost update is unsafe.
10. Cross-domain DynamoDB transactions and foreign-table reads/writes are not approved.
11. Idempotency, work/event outbox, inbox/source, and technical process records belong to the accepting/producing/consuming module.
12. TTL may clean only technical records whose expiration cannot weaken business history, permanent uniqueness, or source deduplication.
13. Every persistence task documents isolation proof, transaction/index amplification, recovery, and cost.

## 3. Access-pattern ledger requirement

Every DynamoDB implementation task maintains a module-local ledger with:

| Field | Required content |
|---|---|
| ID | stable access-pattern identifier |
| Owner | module + aggregate/projection/technical record |
| Use case | command/query/process |
| Trusted scope | source of Tenant/platform/subject authority |
| Key/query | base key or approved index |
| Cardinality | one/bounded page/bounded batch |
| Consistency | transactional/strong/eventual + rationale |
| Write protection | revision/condition/transaction/idempotency/source rule |
| Pagination/order | stable order + opaque cursor |
| Isolation proof | cross-Tenant/unscoped-call tests |
| Recovery | duplicate/reconciliation/migration expectations |
| Cost note | read/write/index/storage amplification |

A task that says only “store it in DynamoDB” is not persistence-ready.

## 4. Symbolic key grammar

Examples communicate access intent, not final attribute names:

```text
Tenant partition:       PK = TENANT#<TenantId>
Aggregate:              SK = <TYPE>#<AggregateId>
Guard/counter:          SK = <TYPE>-GUARD
Claim:                  SK = <CLAIM-TYPE>#<NormalizedValue>
Command/idempotency:    SK = COMMAND#<Operation>#<ScopedKeyDigest>
Technical process:      SK = PROCESS#<ProcessId>
Work outbox:            SK = WORKOUTBOX#<WorkId>
Event outbox:           SK = OUTBOX#<EventId>
Inbox/source marker:    SK = INBOX#<SourceType>#<SourceId>
```

Module-private codecs normalize/escape user-controlled strings before key construction.

## 5. Tenancy table

Tenancy hosts Tenant Management and Merchant Access as distinct model areas but one implementation module/table, allowing their approved local atomic invariants.

### Logical records

| Record | Symbolic access intent | Purpose |
|---|---|---|
| Tenant + BusinessProfile | `PK=TENANT#t, SK=TENANT` | Tenant aggregate, Active/Suspended status/revision |
| Membership | `PK=TENANT#t, SK=MEMBERSHIP#m` | Membership aggregate |
| current authority lookup | `PK=TENANT#t, SK=AUTHORITY#SUBJECT#s` | selected-Tenant current authority |
| subject discovery | `PK=SUBJECT#s, SK=MEMBERSHIP#m#TENANT#t` | strongly consistent candidate discovery only |
| Active Owner guard | `PK=TENANT#t, SK=OWNER-GUARD` | last Active Owner invariant |
| Active Membership counter/guard | `PK=TENANT#t, SK=MEMBERSHIP-COUNT-GUARD` | authoritative `MaxActiveMemberships` enforcement |
| Invitation | `PK=TENANT#t, SK=INVITATION#i` | invitation aggregate |
| pending-email claim | tenant partition normalized-email claim | one Pending Invitation per Tenant/email |
| onboarding operation | trusted onboarding scope + stable operation id | durable completion/recovery |
| Trial bootstrap work outbox | colocated with onboarding operation | guaranteed one-worker recovery |
| command/audit/event outbox | owner scope | idempotency/reliable delivery |

Subject discovery never authorizes a business command by itself. Final selected Tenant + Membership is revalidated.

### Access patterns

| ID | Use case | Access/protection |
|---|---|---|
| `TEN-AP-01R` | registration local commit | one bounded transaction: operation/idempotency + Active Tenant + initial Owner + authority + subject discovery + Owner guard + Membership count guard + Trial work outbox + required Audit intent |
| `TEN-AP-02` | get Tenant/Profile | strong when Tenant status gates command/read classification; eventual safe display only by contract |
| `TEN-AP-03` | update Profile | Tenant expected revision; TenantId immutable |
| `TEN-AP-04R` | resolve read/mutation authority | current Tenant + authority records; no JWT/GSI/cache fallback |
| `TEN-AP-05` | get/manage Membership | strong for mutation; expected revision |
| `TEN-AP-06` | list Memberships | bounded Tenant `Query`; never authorization authority |
| `TEN-AP-07R` | activate/disable/reactivate/change role | transaction Membership + authority + subject discovery + Owner guard + Membership count guard as applicable |
| `TEN-AP-08R` | create/manage Invitation | tenant records + pending-email claim; expiry/state revision |
| `TEN-AP-09R` | accept Invitation | transaction Pending/not-expired/single-use/verified-recipient + Membership/authority/discovery/guards; respects current Membership limit |
| `TEN-AP-10R` | suspend/reactivate Tenant | expected Tenant revision/state + required reason metadata + source-owned durable Audit intent; no Membership/Subscription rewrite |
| `TEN-AP-11R` | subject discovery | strongly consistent `Query` on `SUBJECT#s`; final selected-Tenant validation required |
| `TEN-AP-12` | platform support Tenant query/search | explicit platform context + dedicated bounded application/index design; never generic unscoped merchant repository |
| `TEN-AP-13` | onboarding operation status | stable operation get/update + idempotent Trial result reference |

### Suspended persistence rules

- `TenantStatus=Suspended` is business state, not deletion marker.
- No Tenant/Membership/Order/Accounting business evidence is TTL-deleted because of suspension.
- Read authority and mutation authority both read current Tenant status; mutation requires Active, read may accept Active or Suspended with Active Membership.
- Reactivate updates Tenant status only; it does not rewrite disabled Membership or Ended Subscription.

### Membership hard-limit guard

Before an activation/create, the application obtains current `MaxActiveMemberships` from SubscriptionBilling. Tenancy transaction conditionally increments/sets the module-owned active count with the Membership lifecycle write.

The counter counts all Active roles. It is an invariant aid, not a copied Subscription authority; current limit is never stored as Tenancy authority.

## 6. Catalog table

Current patterns remain:

| ID | Use case | Access/protection |
|---|---|---|
| `CAT-AP-01R` | create Draft | conditional Product put; optional initial SKU claim; idempotent create where required |
| `CAT-AP-02` | get Product | Tenant-scoped strong read for mutation/checkout |
| `CAT-AP-03` | list Products | bounded Tenant query + opaque cursor |
| `CAT-AP-04` | change fields | expected revision; Archived rejects ordinary mutation |
| `CAT-AP-05R` | change Draft SKU | claim new normalized SKU + Product revision + release old Draft claim only before first publication |
| `CAT-AP-06R` | resolve SKU | strong SKU claim + Product validation |
| `CAT-AP-07R` | Category | Category + normalized-name uniqueness claim |
| `CAT-AP-08R` | Brand | Brand + normalized-name uniqueness claim |
| `CAT-AP-09R` | assign Category/Brand | same-Tenant Catalog record validation + Product revision |
| `CAT-AP-10R` | publish/unpublish/archive | expected lifecycle/revision; first publish fixes SKU; Archive never releases permanent SKU claim |
| `CAT-AP-11R` | public Product | only after Storefront supplies approved public Tenant context |
| `CAT-AP-12` | approved filters | keyed/sparse bounded index only |
| `CAT-AP-13R` | apply ImportCandidate | explicit source-idempotent Catalog command; no PDI table read |
| `CAT-AP-14` | change slug | atomically claim new Tenant slug + Product revision; release old current slug; no redirect record |
| `CAT-AP-15` | Category rename | normalized-name claim + revision; historical-name reuse remains domain-owned if needed |
| `CAT-AP-16` | Brand rename | same as Category |
| `CAT-AP-17` | media association | same-Tenant FilesMedia validation through application contract; no FilesMedia table read |
| `CAT-AP-18` | source mapping | Tenant-scoped source-product claim + Product provenance |

## 7. SubscriptionBilling table

Platform-global Plan catalog and tenant Subscription truth share the module table but remain logically separated.

### Records

- stable Plan;
- immutable PlanVersion;
- immutable dedicated Trial terms version;
- catalog bootstrap/source claim;
- Subscription;
- immutable EntitlementSet/history;
- PlatformCharge;
- provider evidence/inbox/reconciliation;
- UsageMeter;
- plan-change/downgrade transition;
- command/outbox records.

### Access patterns

| ID | Use case | Access/protection |
|---|---|---|
| `SUB-AP-00` | bootstrap/query sellable PlanVersions + Trial terms | platform-scoped key/query; immutable version identity; idempotent seed; no runtime Scan |
| `SUB-AP-01R` | current Subscription | strong/current Tenant base access; expected revision |
| `SUB-AP-02R` | EvaluateEntitlement | coherent current Subscription + effective EntitlementSet provenance |
| `SUB-AP-03` | immutable history | bounded Tenant history; eventual display allowed |
| `SUB-AP-04R` | Trial/upgrade/downgrade/renew/reactivate | expected revision + command idempotency + immutable accepted history |
| `SUB-AP-05R` | order UsageMeter | Tenant + period meter + `OrderConfirmed` source claim |
| `SUB-AP-06R` | PlatformCharge | separate aggregate/revision + stable logical charge identity |
| `SUB-AP-07R` | provider evidence | verify/dedupe/non-regression |
| `SUB-AP-08R` | due reconciliation | sparse bounded/sharded operational index only when needed |
| `SUB-AP-09R` | platform support query | explicit app query, read-only context, no direct storage |
| `SUB-AP-10` | Trial source | one logical Trial per onboarding/Tenant source |
| `SUB-AP-11` | restrictive downgrade | durable transition + owner assessment/fence acknowledgements |

Initial Plan/Trial values are defined by resolved `PD-044`; no placeholder/deferred package record remains.

## 8. Sales table

### Core Order records/access

| ID | Use case | Access/protection |
|---|---|---|
| `SAL-AP-01` | accept Order | Tenant+Order immutable snapshot + checkout-intent idempotency |
| `SAL-AP-02` | get Order | tenant-scoped base read; non-disclosing absence |
| `SAL-AP-03` | list Orders | bounded Tenant/time/status query + opaque cursor |
| `SAL-AP-04` | confirm after capture | expected state/revision + PaymentCaptured source idempotency |
| `SAL-AP-05` | mark allocated | expected Confirmed + full reservation evidence/source |
| `SAL-AP-06` | cancel | expected pre-Fulfilled state; Sales fact only |
| `SAL-AP-07` | fulfillment/completion | only with refined fulfillment contract |
| `SAL-AP-08` | technical order process | module-owned process/reference separate from business state |
| `SAL-AP-09` | workflow-start outbox | atomic with OrderPlaced/process for deterministic ADR-010 start |
| `SAL-AP-10` | integration outbox | named Sales facts only when consumers exist |

### Refund records/access

| ID | Use case | Access/protection |
|---|---|---|
| `SAL-AP-11R` | request refund | Tenant+Order/refund request record; eligibility snapshot/reference + idempotent request identity |
| `SAL-AP-12R` | approve/reject refund | expected refund revision/Pending state; terminal decision; approved amount/line quantities bounded by eligible original evidence |
| `SAL-AP-13R` | publish RefundApproved | approval + versioned RefundApproved outbox + required Audit intent in same Sales transaction |
| `SAL-AP-14` | refund review list | bounded Tenant/status/time query/index; display only, never approval authority |

`RefundRequested` has no Inventory/Payments/Accounting outbox effect. `RefundRejected` emits no downstream refund authorization.

## 9. Inventory table

### Records

- Warehouse;
- active-Warehouse count/guard;
- StockItem by Tenant+Product+Warehouse;
- StockReservation;
- immutable StockMovement;
- InventoryAdjustment;
- source/inbox/idempotency/outbox records.

### Access patterns

| ID | Use case | Protection |
|---|---|---|
| `INV-AP-00R` | create/reactivate Warehouse | current `MaxWarehouses` entitlement + Inventory module-local active count/guard + expected state/transaction |
| `INV-AP-01` | get StockItem | trusted Tenant+Product+Warehouse; strong for mutation decisions |
| `INV-AP-02` | reserve Order stock | source-idempotent conditional/transaction preserving `Available>=0`; whole-order result only |
| `INV-AP-03` | release reservation | expected reservation state; decrement Reserved once |
| `INV-AP-04` | issue stock | expected reservation/source; apply `OnHand-=q`, `Reserved-=q` once |
| `INV-AP-05` | receive | source-idempotent `OnHand+=q`; one movement |
| `INV-AP-06R` | apply approved refund return | `RefundApproved` source claim + eligible issue provenance + `OnHand+=q` + immutable StockReturned movement exactly once |
| `INV-AP-07` | adjust | reason/source + conditions preserving `OnHand>=Reserved` |
| `INV-AP-08` | low-stock query/projection | keyed/bounded operational path; not quantity authority |
| `INV-AP-09` | integration outbox | StockIssued/Returned/Adjusted/Received when named consumers exist |

`StockReturned` outbox includes stable original issue provenance/reference, but Accounting cost amount remains Accounting-owned.

If all-line reservation cannot safely fit one DynamoDB transaction for real approved order cardinality, a later technical task must introduce a durable Inventory reservation coordinator. Architecture does not invent a product line-count limit.

## 10. Payments table

Payments owns capture and refund provider truth.

### Records

- Payment obligation;
- immutable PaymentAttempts;
- approved refund operation(s)/logical refund identity;
- provider operation/evidence records;
- reconciliation state;
- source/idempotency/inbox/outbox records.

### Access patterns

| ID | Use case | Protection |
|---|---|---|
| `PAY-AP-01` | get Payment | Tenant+Payment strong/current read |
| `PAY-AP-02R` | start capture | one obligation; stable attempt/provider operation id; no new attempt while prior Unknown |
| `PAY-AP-03R` | apply capture evidence | verified/deduped/non-regressing provider evidence |
| `PAY-AP-04R` | reconcile capture | bounded due work/query; Unknown preserved |
| `PAY-AP-05R` | start approved refund | stable `refundApprovalId`/logical operation; amount/currency match approval; cumulative refund <= captured; persist operation before unsafe retry |
| `PAY-AP-06R` | apply refund evidence | verified/deduped; only verified commit creates PaymentRefunded |
| `PAY-AP-07R` | reconcile refund | no unsafe second operation while Unknown; bounded due-work access |
| `PAY-AP-08` | integration outbox | PaymentCaptured/Declined/Unknown/PaymentRefunded named facts |

Provider IDs stay inside Payments. Timeout/queue retry/elapsed time never proves no commit.

## 11. Procurement table

Procurement remains owner of Supplier, PurchaseOrder, GoodsReceipt, SupplierInvoice, SupplierPayment evidence and source/outbox records.

Important persistence constraints:

- submitted PO snapshot immutable;
- confirmed GoodsReceipt immutable; correction is separate evidence;
- SupplierInvoice/SupplierPayment evidence append/transition through Procurement-owned rules;
- integration outbox is atomic with accepted source fact;
- Procurement does not write Inventory/Accounting tables.

## 12. Accounting table

Accounting owns ChartOfAccounts/control accounts, Journal/lines, source claims, ledger query structures, and valuation provenance/state.

### Required access patterns

| ID | Use case | Protection |
|---|---|---|
| `ACC-AP-01R` | post source journal | atomic source claim + balanced immutable Journal/lines |
| `ACC-AP-02` | journal by id | tenant-scoped base read |
| `ACC-AP-03` | journal/ledger by effective date | bounded tenant-scoped index/query; stable pagination |
| `ACC-AP-04` | GL/Trial Balance | Accounting-owned journal truth only |
| `ACC-AP-05R` | StockIssued valuation/posting | source claim + Accounting-owned valuation/provenance update + COGS/Inventory journal |
| `ACC-AP-06R` | GoodsReceipt valuation/posting | Procurement accepted cost evidence + Accounting-owned valuation state + Inventory/GRNI journal |
| `ACC-AP-07` | moving-average cost pool | **DOMAIN DECISION REQUIRED** before key dimension is finalized |
| `ACC-AP-08R` | RefundApproved revenue correction | refund approval source claim + linked immutable Dr Sales Revenue / Cr Customer Deposits journal |
| `ACC-AP-09R` | StockReturned COGS reversal | StockReturned source claim + original StockIssued Accounting provenance + immutable Dr Inventory / Cr COGS journal + valuation correction |
| `ACC-AP-10R` | PaymentRefunded Cash settlement | payment-refund source claim + immutable Dr Customer Deposits / Cr Cash journal |

Accounting never reads Sales/Inventory/Payments persistence. Refund events must carry stable source/provenance references sufficient to locate Accounting's own historical records.

Posted journals are never edited. A replay of one source produces one logical posting.

## 13. Reporting / Audit / Notification / PDI / FilesMedia

### Reporting

Projections are rebuildable/non-authoritative. Every projection query requires bounded tenant/time keys/indexes. Reporting counts never authorize writes.

### Audit

Audit stores append-only evidence from source-owned durable Audit intents. Tenant-visible reads are scoped to approved roles; platform support/admin uses explicit privileged context. Audit never becomes source business state.

### Notification

Recipient state/read/ack is Notification-owned. Delivery failures never roll back source facts.

### ProductDataIngestion

PDI owns source policy, schedule/run, snapshot, candidate truth. Scheduled execution state/history is retained when entitlement is lost; dispatch rechecks SubscriptionBilling capability through application contract, not copied persistence.

### FilesMedia

Metadata table holds asset identity/ownership/safe metadata. Binary data belongs in private S3 when Ready. Catalog stores only same-Tenant asset references after FilesMedia application validation.

## 14. Retention and TTL

Current MVP business truth does not use TTL for:

- Suspended Tenant data;
- Membership history needed for authority/audit;
- accepted PlanVersion/Trial terms/Subscription history;
- Orders/refund decisions;
- stock movements;
- Payment capture/refund evidence needed for reconciliation/history;
- posted Accounting journals/source provenance.

TTL may be used for narrow technical replay/cache/work records only when expiry cannot weaken permanent idempotency/history/uniqueness.

## 15. Remaining persistence gates

Builders must stop for domain refinement before hardening:

- Storefront Tenant public-address key/index model;
- Accounting moving-weighted-average cost-pool key dimension;
- Category/Brand historical normalized-name reuse if a task needs claim-release semantics;
- non-restock refund persistence semantics.

## 16. Stop condition

**PERSISTENCE ACCESS-PATTERN BASELINE RECONCILED.**
