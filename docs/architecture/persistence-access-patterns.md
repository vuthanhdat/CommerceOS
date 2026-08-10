# CommerceOS — Persistence Ownership and Access Patterns

_DynamoDB and consistency baseline reconciled by TASK-0088._

## 1. Decision and scope

CommerceOS starts with one DynamoDB table per implementation module, created only when that module has a Ready persistence task.

Near-term physical owners:

```text
Tenancy module  ──owns──► Tenancy table
Catalog module  ──owns──► Catalog table
Storefront module──owns──► Storefront table only when approved persistent Storefront state exists
Audit module    ──owns──► Audit table when introduced
```

This is not table-per-aggregate and not one platform-wide single table. Several aggregates owned by the same implementation module may share that module's table and a small transaction. No other module reads its records, indexes, stream, or serialization model.

The symbolic keys below make access and tenant isolation explicit enough for task refinement. Exact attribute names, encoding helpers, length limits, and item serialization are implementation details that must preserve this grammar and be documented in the introducing task.

ADR authority: [ADR-005](../adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md).

## 2. Universal repository rules

1. A tenant-owned repository method accepts a trusted TenantId or a module-owned scope derived from `TrustedTenantContext`; it has no unscoped overload.
2. Tenant scope is present in the base-table partition key used by every tenant-owned aggregate read/write/query. Every tenant-facing GSI partition/query is Tenant-prefixed and has the same trusted-scope/no-unscoped-repository rule.
3. Target aggregate IDs, SKU values, invitation values, filters, and cursors remain untrusted inputs and cannot replace the partition scope.
4. Another module never receives a table name, item DTO, key builder, DynamoDB context/client, stream record, or index name.
5. Application paths use `GetItem`, `BatchGetItem`, `Query`, conditional writes, or bounded transactions for a documented pattern. `Scan` is prohibited.
6. A `FilterExpression` does not satisfy an access pattern when it reads an unbounded partition or substitutes for a required key/index.
7. GSIs are added only for an approved query. Their eventual consistency is documented and they are never the sole authority, uniqueness, revision, or invariant check.
8. Strong/transactional reads and conditions are used only where current correctness requires them; ordinary lists and rebuildable projections may be eventually consistent when the contract states it.
9. IDs are server-assigned opaque values. Mutable names, SKUs, slugs, email addresses, and source IDs are not aggregate identity.
10. Every write records an aggregate revision where optimistic concurrency applies and enough safe source/correlation metadata for diagnosis.
11. A transaction applies an invariant condition on the same `Put`, `Update`, or `Delete` action that mutates that item. DynamoDB must not receive a separate `ConditionCheck` and write action for the same item in one transaction; standalone condition checks are only for records not mutated by that transaction.
12. A cross-Tenant operational index is permitted only for an approved service/operations access pattern. It has a minimal projection, narrow non-merchant IAM, bounded/sharded query shape, and explicit isolation, audit, retention, and cost tests; it is never exposed through a tenant repository.

## 3. Required access-pattern ledger

Every DynamoDB implementation task adds or updates a module-local ledger with these columns:

| Field | Required content |
|---|---|
| ID | stable pattern identifier such as `TEN-AP-03` |
| Owner | module and aggregate/projection owner |
| Use case | application command/query that needs it |
| Trusted scope | how Tenant/source scope is obtained |
| Key/query | base key or approved index query; no schema dump |
| Cardinality | one, bounded page, or known bounded batch |
| Consistency | transactional, strong, or eventual with rationale |
| Write protection | condition/transaction/idempotency rule |
| Pagination/order | stable order and opaque cursor behavior |
| Isolation proof | cross-tenant and unscoped-call test |
| Cost note | read/write amplification, index/storage implication |

A task is not persistence-ready when it says only “store in DynamoDB.”

## 4. Symbolic key grammar

Examples use `PK` and `SK` only to communicate access intent:

```text
tenant partition:       PK = TENANT#<TenantId>
aggregate item:         SK = <TYPE>#<AggregateId>
singleton/guard item:   SK = <TYPE>
owned lookup claim:     SK = <CLAIM-TYPE>#<NormalizedValue>
command record:         SK = COMMAND#<Operation>#<ScopedKeyDigest>
outbox record:          SK = OUTBOX#<EventId>
```

Values are encoded through one module-private key codec. User strings are normalized/escaped before key construction under an approved domain/contract rule; raw request text is never concatenated into a key ad hoc.

`ScopedKeyDigest` covers the canonical operation, trusted actor/principal scope, and bounded `Idempotency-Key`; it is never a digest of the unscoped header alone. Equivalent keys used by two actors therefore cannot claim or replay each other's command record.

Pre-Tenant onboarding intent uses a separate platform-scoped partition derived from the trusted admission principal and idempotency key selected after `PD-034`. Its exact key cannot be finalized before that decision. It is never keyed by a caller-selected TenantId.

## 5. Tenancy table

The `Tenancy` implementation module hosts Tenant Management and Merchant Access as distinct model areas but can atomically persist their approved onboarding outcome in its one table.

### Item ownership intent

| Logical record | Symbolic base key | Owner | Notes |
|---|---|---|---|
| Tenant aggregate including its one BusinessProfile | `PK=TENANT#t`, `SK=TENANT` | Tenant Management | BusinessProfile is not a separate aggregate/table |
| Membership aggregate | `PK=TENANT#t`, `SK=MEMBERSHIP#m` | Merchant Access | Immutable Tenant/Subject binding once active |
| current authority lookup | `PK=TENANT#t`, `SK=AUTHORITY#SUBJECT#s` | Merchant Access | Same-owner lookup projection updated atomically with Membership |
| active-owner guard | `PK=TENANT#t`, `SK=OWNER-GUARD` | Merchant Access | Count/revision or equivalent condition protects last-owner invariant |
| Invitation aggregate | `PK=TENANT#t`, `SK=INVITATION#i` | Merchant Access | Recipient/token lookup waits for `PD-036` |
| command/idempotency record | same tenant partition when post-onboarding | accepting Tenancy use case | Request fingerprint + stable result reference |
| integration/audit outbox record | same transaction/partition as applicable state | source model area's application boundary | Technical delivery record, not an Audit record or domain aggregate |

The authority lookup is not a second Membership authority. It is a Merchant Access-owned, transactionally maintained access representation for the required `(TenantId, SubjectId)` lookup. It contains only fields needed to resolve current membership identity/status/revision and approved capability inputs.

### Tenancy access-pattern baseline

| ID | Use case | Key/access | Consistency and protection | Product gate |
|---|---|---|---|---|
| `TEN-AP-01` | complete merchant onboarding | transaction across pre-Tenant intent claim, Tenant item, initial Owner Membership, authority lookup, and owner guard | one `TransactWriteItems`; all absent/expected; equivalent accepted intent returns prior result | request/admission fields: `PD-002`, `PD-034` |
| `TEN-AP-02` | get Tenant/BusinessProfile | `GetItem(TENANT#t, TENANT)` | strong for protected commands/status gates; query read may use strong when freshness matters | none for base ownership |
| `TEN-AP-03` | update BusinessProfile | same Tenant item | conditional expected revision; TenantId immutable | fields: `PD-002`, `PD-034` |
| `TEN-AP-04` | resolve current authority | transactionally read Tenant item + `AUTHORITY#SUBJECT#s` from selected Tenant partition | `TransactGetItems`; no GSI/cache/JWT role authority; fail closed | selector/capabilities: `PD-001`, `PD-003` |
| `TEN-AP-05` | get/manage Membership by id | `GetItem(TENANT#t, MEMBERSHIP#m)` | strong for status/role mutation; expected revision | permissions/cardinality: `PD-003` |
| `TEN-AP-06` | list Memberships in Tenant | `Query PK=TENANT#t, SK begins MEMBERSHIP#` | bounded page; eventual permitted for management list, never authorization | display/filter details: refinement |
| `TEN-AP-07` | disable/reactivate/change ownership-sensitive role | transaction across Membership, authority lookup, and owner guard | expected revisions + guard condition; no read-then-write last-owner check | `PD-003` |
| `TEN-AP-08` | create/get/manage Invitation | tenant partition by Invitation identity | conditional create/revision; expiry stored explicitly | recipient/duplicates: `PD-036` |
| `TEN-AP-09` | accept Invitation | transaction checks Pending/not expired/single-use/recipient proof, creates or changes only the approved Membership path, updates authority/owner guard, and finalizes Invitation | one all-or-nothing transaction; stable acceptance identity | `PD-003`, `PD-036` |
| `TEN-AP-10` | suspend/reactivate Tenant | update Tenant with expected revision and state condition; reactivation transaction also condition-checks the distinct owner guard | suspension does not change Membership; reactivation requires at least one Active Owner and does not reactivate one; durable Audit intent joins the transaction only when `PD-033` requires it | authorization/support detail: `PD-003`, `PD-004`, `PD-033` |
| `TEN-AP-11` | subject membership discovery across Tenants | no index approved | do not add Subject GSI or infer Tenant until `PD-001` defines whether discovery/selection exists | `PD-001` |
| `TEN-AP-12` | platform Tenant list/support search | no index approved | platform-admin contract/security/access pattern deferred | `PD-004`, `PD-033` plus platform-admin task |

### Onboarding transaction

The transaction boundary is technical and fixed; its product inputs remain gated:

```text
Condition/put durable onboarding intent/admission claim
+ Put Active Tenant with BusinessProfile
+ Put Active initial Owner Membership
+ Put current authority lookup for the trusted Owner subject
+ Put owner guard initialized to one active Owner
+ Put required durable audit intent only after PD-033 designates coverage
────────────────────────────────────────────────────────────────────
all commit, or no completed onboarding result exists
```

An interrupted caller can retrieve the accepted result through the idempotency/admission claim. No cleanup workflow deletes a committed Tenant because the response was lost.

### Authority read freshness

The resolver transactionally reads the Tenant and current authority lookup so one coherent snapshot determines ordinary access. An eventually consistent Subject GSI, token custom claim, or in-memory/Lambda cache cannot authorize a request.

If the read fails or is unavailable, authorization fails closed with a safe dependency-unavailable outcome. It does not report that a Membership is absent and never uses a client target as fallback.

## 6. Catalog table

### Item ownership intent

| Logical record | Symbolic base key | Owner | Notes |
|---|---|---|---|
| Product aggregate | `PK=TENANT#t`, `SK=PRODUCT#p` | Catalog | Contains Product-owned value/entities appropriate to item-size limits |
| normalized SKU claim | `PK=TENANT#t`, `SK=SKU#<normalized>` | Catalog | Claim points to ProductId; authoritative uniqueness check |
| Category aggregate | `PK=TENANT#t`, `SK=CATEGORY#c` | Catalog | Lifecycle/indexes wait for `PD-009` |
| Brand aggregate | `PK=TENANT#t`, `SK=BRAND#b` | Catalog | Lifecycle/indexes wait for `PD-009` |
| command/idempotency record | `PK=TENANT#t`, `SK=COMMAND#...` | accepting Catalog use case | Added only for a documented retryable command |
| integration outbox record | same module table | Catalog application boundary | Written only for a named published integration fact |

Product-owned specifications/media/source associations may be embedded or split into Product-part items only after size/cardinality access needs are known. Splitting storage does not create another aggregate or allow partial lifecycle commits.

### Catalog access-pattern baseline

| ID | Use case | Key/access | Consistency and protection | Product gate |
|---|---|---|---|---|
| `CAT-AP-01` | create Draft Product | put `PRODUCT#p`; add SKU claim in same transaction when assigned | conditional absent; optional idempotency claim in same transaction | fields/SKU: `PD-002`, `PD-005`, `PD-037` |
| `CAT-AP-02` | get tenant Product by ProductId | `GetItem(TENANT#t, PRODUCT#p)` | strong for edit/checkout validation; non-disclosing 404 on absence | none for base identity |
| `CAT-AP-03` | list tenant Products | `Query PK=TENANT#t, SK begins PRODUCT#` | stable bounded page; eventual permitted for management list; opaque tenant-bound cursor | sort/filter refinement |
| `CAT-AP-04` | change Product fields | same Product item | conditional expected revision; no lost update | field/lifecycle gates as applicable |
| `CAT-AP-05` | assign/change SKU | transaction conditionally creates new tenant SKU claim, updates Product/revision, and removes old claim only under expected ownership | no GSI/read-then-write uniqueness | `PD-005` |
| `CAT-AP-06` | resolve Product by SKU | strong `GetItem` of tenant SKU claim, then strong Product base read | claim carries a technical binding token copied atomically to Product; return only when Product normalized SKU + binding token still match, otherwise bounded retry/not-visible; ProductId remains identity | `PD-005` |
| `CAT-AP-07` | create/get/list Category | tenant partition Category keys | conditional create and bounded query; name/lifecycle indexes not approved | `PD-009` |
| `CAT-AP-08` | create/get/list Brand | tenant partition Brand keys | conditional create and bounded query; name/lifecycle indexes not approved | `PD-009` |
| `CAT-AP-09` | assign Category/Brand | transaction checks same-tenant reference item(s) and conditionally updates Product revision | no cross-tenant/global lookup and no cascade | `PD-009` |
| `CAT-AP-10` | publish/unpublish/archive | conditional Product transition; atomically update persisted public projection only if one is approved | query-time projection is the safe default; no external event without consumer | `PD-006`–`PD-010`, `PD-037` |
| `CAT-AP-11` | list/read public Products | no public index/address key approved | add only after public fields, Tenant address, Product address, order/filter, and freshness contract exist | missing Tenant-address decision; `PD-006`–`PD-010`, `PD-037` |
| `CAT-AP-12` | filter by lifecycle/Category/Brand | logical access need recorded; no GSI approved yet | each approved filter gets a sparse/bounded index or is excluded; no broad filter scan | `PD-007`, `PD-009` and API refinement |
| `CAT-AP-13` | apply ImportCandidate | explicit Catalog command; Catalog table only | validate supplied immutable source/candidate reference through contract, then Product transaction; no Ingestion table read/write | `PD-040` |

### SKU transaction

The mechanism is fixed without choosing SKU business semantics:

```text
Update Product display/normalized SKU/binding + revision
  (condition: Product revision == expected)
+ Put new claim -> ProductId + binding token
  (condition: new tenant SKU claim does not exist)
+ Delete old claim
  (condition: old claim is owned by the same Product and binding)
───────────────────────────────────────────────────
all commit or Product and claims remain unchanged
```

Each condition is attached to the write for that same item; no duplicate transaction action targets one item. Whether the transaction runs at creation, publication, or later change and whether the old claim may be deleted are controlled by `PD-005`.

### Public projection

Until a Ready task proves that a persisted projection/index is required, Catalog maps an authoritative Published Product to its approved public DTO at query time. This avoids a second write model and cache invalidation before the public contract exists.

If a persisted projection is later justified:

- Catalog still owns it;
- Product transition and projection update are one Catalog transaction or have an explicit repair invariant;
- a stale projection cannot authorize checkout or publication;
- Unpublished/Archived removal is deterministic;
- cache/index keys cannot be chosen before the Tenant and Product addressing decisions.

## 7. Idempotency, inbox, outbox, and uniqueness records

These records are technical persistence owned by the module whose use case accepts or consumes the operation. They are not shared global tables.

### Command record

- conditionally claimed inside the same transaction as the first durable effect where possible;
- stores operation, trusted scope, key digest, semantic request hash, state, and result reference;
- incompatible reuse fails;
- TTL is allowed only when expiry cannot violate permanent business uniqueness.

### Integration outbox record

- written atomically with the producer's accepted state;
- contains a complete versioned integration envelope/payload, publication status/attempt metadata, and no database row dump;
- relayed from a module-table stream filter that accepts only new `PENDING` outbox inserts after a named consumer exists; relay status updates do not qualify and cannot create a stream loop;
- bounded below the smallest end-to-end DynamoDB/EventBridge/SQS payload limit with headroom; large content is represented only by an approved safe object reference;
- participates, when the first async contract is introduced, in the approved sparse recovery access pattern `OUTBOX-AP-01`: index partition `OUTBOX#PENDING#<bounded-shard>` and sort key `<NextAttemptAt>#<EventId>` (or a behaviorally equivalent module dispatch ledger);
- is queried by known bounded shards and due-time range, never Scan; shard count and per-shard throughput are calculated in the implementing task, and the relay strongly rechecks the base outbox state before publishing because the recovery index is eventually consistent;
- removes the sparse pending-index attributes when conditionally marked published; published-record retention/archive and unrecoverable-permanent-failure handling are explicit and cannot delete required source/audit evidence;
- does not become the producer's business-event history or the consumer's inbox.

`OUTBOX-AP-01` is the sole approved cross-Tenant recovery exception in this baseline. Its projection contains dispatch identity/status/due-time only, is available only to the owning relay/reconciler role, and is created with the first named asynchronous producer—not speculatively. A one-worker work-dispatch record may use the same bounded recovery shape without EventBridge.

### Consumer inbox/source record

- keyed by consumer identity plus event/logical source ID;
- checked/written atomically with the consumer-owned side effect where possible;
- equivalent replay returns the prior result; incompatible source reuse is a conflict;
- critical inventory/payment/accounting source uniqueness is retained for as long as the corresponding business record, not expired as a generic cache.

Reliable publication details are in [ADR-006](../adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md).

## 8. Capacity, indexes, and cost

- Persistent learning/dev tables may use a deliberately small provisioned profile within the repository's aggregate Free Tier target when traffic assumptions fit; preview or burst experiments may intentionally use on-demand.
- A table and every GSI have explicit capacity/cost settings in CDK. A GSI is not free write capacity and adds storage/write amplification.
- No index is created for a hypothetical dashboard, search box, platform operation, or future filter.
- Item sizes and transaction item counts are bounded; large raw payloads/media belong in policy-approved S3 objects, not DynamoDB items.
- Preview data is synthetic and removable. Production-like retention/PITR/backup is enabled only through environment policy with its cost tracked.
- DynamoDB encryption uses AWS-managed/default encryption unless a later threat/compliance decision justifies customer-managed keys and their cost/operational burden.
- Table and stream metrics/alarms use built-in low-cardinality metrics first.

## 9. IAM and tenant isolation

The initial shared `commerce-api` Lambda may require IAM actions on both Tenancy and Catalog tables. This is an accepted modular-monolith trade-off, not permission for cross-module persistence access.

Controls:

- table grants are explicit in CDK; no wildcard account/table access;
- queue/worker functions receive only their owning table/queue and required event target;
- an outbox relay/reconciler can query only its module's recovery index and get/update only its module's outbox records; tenant APIs cannot query that operational index;
- Infrastructure projects keep table clients and item types internal;
- architecture tests reject cross-module Domain/Infrastructure references;
- repository signatures require Tenant scope;
- integration tests exercise known Tenant A IDs under Tenant B context at API and repository levels;
- a later deployment split can reduce IAM scope because physical tables already align to modules.

IAM condition keys are defense in depth only when the selected key/session model can enforce them correctly. They do not replace application authorization or tenant-key tests.

## 10. Transaction boundary rule

Use a conditional write for a one-item invariant and a DynamoDB transaction for a small set of records owned by one implementation module that must commit together.

Attach each condition to its corresponding mutating action when that item is written. A separate `ConditionCheck` is reserved for a distinct record that the transaction does not also mutate, such as the owner guard checked during Tenant reactivation.

Do not create a cross-domain distributed ACID transaction. A later context:

- calls a synchronous owner contract when it needs an immediate accepted/rejected result; or
- commits its fact and uses the reliable asynchronous integration pattern.

The shared runtime and DynamoDB's ability to transact across tables do not authorize a business module to coordinate arbitrary foreign table writes.

## 11. Required persistence verification when implemented

- create/get/query uses documented keys and performs no Scan;
- repository APIs cannot be invoked without tenant scope;
- Tenant A cannot read/write Tenant B even with known IDs, claims, cursors, or SKU/reference values;
- onboarding transaction has failure injection at every write and never leaves a visible partial result;
- duplicate/concurrent onboarding intent produces one logical Tenant result;
- authority resolution observes current disable/suspension changes on the next resolution;
- last-owner and invitation acceptance races preserve their invariants;
- suspension uses the expected Tenant revision; reactivation fails unless the owner guard proves an Active Owner and never changes Membership status;
- Product update race returns stale precondition without lost update;
- SKU same-tenant race yields one owner; another Tenant may hold the same approved normalized value;
- a SKU change/reuse between claim read and Product read cannot return a Product whose normalized SKU/binding no longer matches the claim;
- Category/Brand cross-tenant references fail without disclosure;
- GSI-based eventual lists are never used for authorization or uniqueness;
- outbox/inbox replay, relay interruption, DLQ redrive, and reconciliation preserve event/source identity when those resources are introduced;
- recovery-index tests prove due pending records are found after stream expiry without Scan, published records leave the sparse index, shards stay bounded, and tenant-facing roles cannot query the operational index;
- the same idempotency header used by two trusted actors produces separate records and cannot replay or poison the other actor's result;
- selected access patterns receive bounded real-AWS verification because DynamoDB transaction, consistency, index, IAM, and stream semantics are cloud-sensitive.
