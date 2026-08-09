# Catalog Domain Baseline

_Deep baseline for the first delivery frontier. Reconciled by TASK-0087._

## 1. Responsibility

Catalog owns the tenant's canonical description of what the merchant may sell.

Catalog answers:

- What is this merchant's Product?
- Which merchant SKU identifies it?
- What base selling price and merchandise facts are current?
- Is the Product eligible for public presentation?
- Which Category, Brand, media, and external-source references has the merchant associated with it?

Catalog does not answer:

- how much stock is available;
- what price a shopper ultimately agreed to;
- whether a payment succeeded;
- what inventory cost or accounting value should be recognized;
- what an external source currently says without merchant acceptance.

## 2. Aggregate and concept ownership

### Aggregate: Product

`Product` is the aggregate root for the canonical merchant product.

Immutable identity:

- system-assigned `ProductId`;
- owning `TenantId`.

Owned product facts:

- merchant SKU and its normalized comparison value;
- name and description;
- current Catalog base selling price;
- optional advisory cost reference;
- Product lifecycle status;
- Category and Brand references;
- Product-owned specifications;
- ordered Product-to-media associations;
- Product-to-external-source associations and merchant import provenance;
- accepted revision/history information needed for concurrency and lifecycle evidence.

Product-owned entities/value objects:

- `SKU` — merchant-facing identifier with a normalized uniqueness value;
- `Money` — amount plus currency;
- `ProductSpecification` — at minimum a merchant-approved name/value pair owned by Product; duplicate-name, unit, ordering, and public-visibility semantics are `PD-037`;
- `ProductMediaReference` — ordered association and display/rights metadata, not necessarily a binary asset;
- `ExternalProductLink` — merchant decision to associate this Product with an ingestion-owned source identity;
- `ProductStatus` — lifecycle value.

`ProductVariant` is not part of the initial Product aggregate. It remains a later product capability.

### Aggregate: Category

Category is a tenant-owned reference aggregate with immutable identity and display name.

Baseline rules:

- a Product may reference only a Category owned by the same Tenant;
- lifecycle/retirement, hierarchy, name uniqueness, single-versus-multiple categorization, and existing-reference behavior all remain `PD-009`;
- no delete/archive/retire behavior is approved until that decision is resolved.

### Aggregate: Brand

Brand is a tenant-owned reference aggregate with immutable identity and display name.

Baseline rules:

- a Product may reference only a Brand owned by the same Tenant;
- lifecycle/retirement, aliases, name uniqueness, and existing-reference behavior remain `PD-009`;
- no delete/archive/retire behavior is approved until that decision is resolved; initial ownership remains tenant-local.

## 3. Product lifecycle

Recognized states:

- `Draft` — canonical merchant record that has never been published and may be incomplete;
- `Published` — Catalog has accepted it as eligible for a public projection;
- `Unpublished` — retained canonical record that is not currently public-eligible;
- `Archived` — intentionally retired from ordinary catalog management and never public-eligible.

Guaranteed transitions:

```text
Create ──► Draft ──Publish──► Published ──Unpublish──► Unpublished
                ▲                                  │          │
                └────────────────Publish───────────┘          │
Draft / Unpublished ─────────────────────────Archive────────► Archived
```

Rules:

1. Creation produces `Draft`; creation alone is not publication.
2. Publishing requires all approved publication fields and creates public eligibility.
3. Unpublishing removes public eligibility but preserves the canonical Product and history.
4. Archived is never public-eligible and cannot be published directly.
5. Publication is independent of current stock. A Published Product may be out of stock.
6. A public projection includes only fields approved for public display; advisory cost, private source-review data, and private merchant metadata are excluded.
7. Editing an historical order is never a consequence of Product change.

The following lifecycle choices cannot be safely inferred and remain `PD-007`: whether a Published Product is edited live or through a draft revision, whether a Published Product may be archived without an explicit unpublish action, and whether Archived can ever be restored. Until decided, candidate implementation tasks must not invent those transitions.

## 4. Publication eligibility

The universal baseline is:

- Product belongs to the trusted Tenant;
- Product is not Archived;
- name is present and merchant-valid;
- SKU satisfies the tenant uniqueness policy;
- base selling price is a valid Money value under the tenant currency policy;
- every referenced Category, Brand, and media association belongs to the same Tenant or satisfies its explicit external-reference rule;
- the public projection contains no advisory cost or restricted source content.

Whether zero price is sellable, and whether Category, Brand, description, or at least one image is mandatory, remain explicit decision `PD-006`. Product-slug/addressing rules remain `PD-008`.

`Published` means Catalog eligibility. It does not by itself promise that an anonymous HTTP route, storefront deployment, cache, or search projection is available; those are later Storefront/technical concerns.

### Public Product projection

The Catalog-owned public projection contains canonical merchant-approved display facts, not a second Product:

- immutable ProductId;
- public Product address only after `PD-008` is resolved;
- name and, when present, description;
- current Catalog base selling price with currency (or an explicitly separate Pricing-resolved offer when Pricing is later active);
- associated Category/Brand display references when present;
- merchant-approved public specifications under `PD-037`;
- ordered policy-safe media references when present.

It excludes advisory cost, private source-review data, raw snapshots, internal revision/history, and non-public merchant metadata. Whether SKU/source attribution is public, and specification duplicate/unit/order/visibility rules, remain `PD-037`; TASK-0012 cannot finalize `PublicProduct` before that decision.

## 5. SKU policy

SKU is a merchant identifier, not Product identity.

Baseline invariants:

1. When assigned, SKU is nonblank after trimming.
2. Comparison uses one human-approved normalization and case-sensitivity rule; the merchant's display form may be preserved.
3. The normalized value is unique among one Tenant's Products and may be reused by another Tenant.
4. SKU uniqueness is a Catalog-wide invariant, not a best-effort Product-local check.
5. Product references across domains use immutable ProductId and may snapshot the displayed SKU; they do not depend on SKU remaining unchanged.
6. A conflicting SKU change is rejected without changing either Product.

Whether SKU is required at Draft creation or only before publication, its case/normalization rule, when it may be changed, and whether an Archived Product permanently retains its claim are pending `PD-005`.

After the human chooses merchant-visible case/normalization semantics, exact Unicode handling, length limit, and allowed characters are refined as validation/contract details and applied consistently.

## 6. Prices and cost references

### Catalog base selling price

Catalog owns the Product's current base selling price.

- It is Money, never an unqualified number.
- Pricing may consume it and produce a resolved offer; Pricing does not become another owner of the base value.
- Sales resolves current eligible commercial facts and captures the accepted value in an immutable order snapshot.
- Updating Catalog price never changes an existing order.

### Advisory cost reference

The optional Catalog cost reference is merchant planning data only.

It is not:

- Inventory's stock cost basis;
- an authoritative COGS amount;
- an Accounting journal amount;
- proof of supplier cost;
- a value that may silently revalue stock or historical sales.

Inventory valuation and COGS source policy require human accounting decision `PD-021`.

### Currency

The supported tenant functional currency, price currency choices, precision, and rounding policy are pending `PD-002`. Until decided, no task may hard-code VND merely because examples use VND, and no task may introduce currency conversion.

## 7. Category and Brand behavior

Category and Brand organize the canonical catalog but are not Product aggregate children because they can be independently referenced by multiple Products.

Common invariants:

- immutable identity and immutable Tenant ownership;
- nonblank display name;
- only same-tenant references;
- no lifecycle operation may silently cascade into hidden Product mutation.

Delete/archive/retire semantics are not approved. Candidate tasks must remain blocked on `PD-009` rather than choose one.

## 8. Media ownership

Catalog owns the decision that a Product uses a media reference, its order, public alt/display metadata, and the rights/attribution assertion required by product policy.

Files/Media may later own a reusable merchant-uploaded binary asset. Product Data Ingestion owns an external source image observation/reference. Neither context may attach or publish an asset on a Product without an explicit Catalog decision.

A `ProductMediaReference` distinguishes at least conceptually:

- merchant-owned asset/reference;
- external content the merchant is permitted to display or reference;
- source attribution and rights-review evidence where required;
- display order and public alt text.

A URL being fetchable is not permission to copy or republish its content. Exact external hotlink, license-attestation, and evidence policy is pending `PD-010`.

## 9. Product Data Ingestion relationship

```text
Ingestion-owned external snapshot
            ↓ candidate + provenance
Merchant reviews selected fields
            ↓ explicit Catalog command
Catalog creates/updates canonical Product
            ↓
Catalog owns resulting values and source link
```

Rules:

1. Ingestion never owns or mutates a canonical Product.
2. A source change never changes a Product without a new explicit merchant-approved Catalog action or a future explicit pricing/import rule.
3. Catalog owns the ExternalProductLink from a Product to an ingestion source identity; Ingestion owns the referenced source record/snapshot.
4. Imported values pass the same Catalog validation and lifecycle rules as manually entered values.
5. Import provenance identifies the source snapshot and selected fields but does not transfer continuing authority to that source.
6. `ProductImported` is a Catalog fact only when Catalog actually accepts creation or selected changes. `ImportCandidateCreated` is an Ingestion fact.

Mapping cardinality and candidate application/supersession/expiry remain `PD-040`.

## 10. Authorization baseline

- `catalog.view`, `catalog.manage`, and `catalog.publish` are distinct business capabilities whose role mapping remains `PD-003`.
- A role name alone does not grant any of those capabilities.
- No UI role label such as “catalog manager” creates a new role or permission by implication.
- Anonymous shoppers receive only the public projection of a Published Product through a resolved storefront Tenant context.
- Tenant mismatch is non-disclosing and cannot be overridden by a Product or Tenant id in input.

Catalog tasks cannot become Ready until the human approves the applicable role/cardinality mapping in `PD-003`.

## 11. Commands, queries, and business facts

Business command candidates:

- `CreateProduct`
- `ChangeProductDetails`
- `AssignProductSKU`
- `SetCatalogBasePrice`
- `AssignProductCategory`
- `AssignProductBrand`
- `AttachProductMediaReference`
- `PublishProduct`
- `UnpublishProduct`
- `ArchiveProduct`
- `ApplyImportCandidate`
- `LinkExternalProduct`

Business query intents:

- retrieve one tenant-owned Product;
- list/filter tenant-owned Products by lifecycle, SKU, Category, or Brand;
- resolve the current published commercial Product facts required for checkout;
- read the public projection for a Published Product.

Owned fact candidates:

- `ProductCreated`
- `ProductDetailsChanged`
- `ProductSKUChanged`
- `ProductBasePriceChanged`
- `ProductPublished`
- `ProductUnpublished`
- `ProductArchived`
- `ProductImported`
- `ExternalProductLinked`
- `CategoryCreated` / `CategoryChanged` under the approved `PD-009` lifecycle
- `BrandCreated` / `BrandChanged` under the approved `PD-009` lifecycle

These names describe accepted facts. TASK-0088 decides contract shapes and whether/when any need cross-boundary publication.

## 12. Business error semantics

| Code | Meaning and required effect |
|---|---|
| `PRODUCT_NOT_VISIBLE` | absent or not visible in trusted tenant context; no cross-tenant existence disclosure |
| `SKU_REQUIRED` | action requires SKU and none is assigned |
| `SKU_CONFLICT` | normalized SKU is already claimed under the approved lifecycle policy; Product remains unchanged |
| `PRODUCT_INCOMPLETE_FOR_PUBLICATION` | required publication facts are missing/invalid; state remains unchanged |
| `PRODUCT_STATE_TRANSITION_INVALID` | requested lifecycle transition is not approved from current state |
| `PRODUCT_ARCHIVED` | ordinary edit/publish action targets Archived Product and is rejected |
| `CATALOG_REFERENCE_INVALID` | Category, Brand, media, or external reference is absent, disallowed under its approved lifecycle policy, or from another Tenant |
| `CATALOG_REVISION_STALE` | attempted edit used an old Product revision; newer accepted state is preserved |
| `CATALOG_CURRENCY_INVALID` | price/currency violates the approved tenant currency policy |
| `SOURCE_IMPORT_STALE_OR_INVALID` | candidate/snapshot cannot be applied under approved import policy; canonical Product remains unchanged |

Safe UI recovery may reload current state or let the merchant deliberately reapply edits, but must never overwrite a newer revision automatically.

## 13. Inputs to TASK-0088 and TASK-0089

Technical Architecture must preserve:

- Product aggregate consistency and tenant-wide SKU uniqueness;
- non-disclosing tenant isolation;
- immutable Product/Tenant identity and immutable Sales snapshots;
- lifecycle transition concurrency;
- Catalog-owned public projection boundary;
- explicit PDI-to-Catalog review/apply handoff;
- no path from cost reference to accounting value without approved policy.

Backlog Planning must reconcile:

- TASK-0010 omitting SKU while TASK-0011 assumes its lifecycle;
- TASK-0012's “becomes readable” with Storefront-owned anonymous exposure;
- TASK-0013's undefined “catalog manager” role;
- task readiness against `PD-002`, `PD-003`, `PD-005` through `PD-010`, `PD-037`, and `PD-040` at their stated gates.
