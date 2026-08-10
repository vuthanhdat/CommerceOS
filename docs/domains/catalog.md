# Catalog Domain Baseline

_Reconciled after the 2026-08-10 human product-decision pass. This document incorporates approved decisions `PD-002`, `PD-003`, `PD-005`–`PD-010`, `PD-037`, and `PD-040`._

## 1. Responsibility

Catalog owns the Tenant's canonical merchant Product and the rules that make it eligible for public presentation.

Catalog answers:

- What Product does this merchant own?
- Which merchant SKU identifies it operationally?
- What base selling price is currently configured?
- Is the Product Draft, Published, Unpublished, or Archived?
- Which Category, Brand, media assets, specifications, and external-source references has the merchant accepted?
- What public Product facts may be projected?

Catalog does not own:

- stock or reservation truth;
- shopper-agreed Order price snapshots;
- accounting inventory value/COGS;
- external source snapshots/candidates before merchant acceptance;
- subscription-plan pricing;
- storefront delivery/caching mechanics.

## 2. Product aggregate

`Product` is the aggregate root.

Immutable identity:

- `ProductId`;
- owning `TenantId`.

Owned Product facts include:

- optional/current SKU plus normalized comparison value;
- name and optional description;
- current Catalog base selling price;
- optional advisory cost reference with no accounting authority;
- lifecycle status;
- zero or one Category reference;
- zero or one Brand reference;
- ordered Product specifications;
- ordered merchant-managed media associations;
- tenant-scoped public slug after publication;
- approved external-source association/import provenance;
- revision/history needed to preserve accepted changes and lifecycle meaning.

`ProductVariant` remains outside MVP.

## 3. Money policy (`PD-002`, `PD-006`)

CommerceOS MVP is VND-only.

- Every monetary value is still a `Money(amount, currency)` value.
- Merchant-facing VND amounts use whole đồng with no fractional minor unit.
- Catalog performs no currency conversion.
- A Product price may be **zero**.
- Publication requires a valid Money value but does not require a positive amount.

Changing Catalog price never rewrites an existing SalesOrder snapshot.

## 4. SKU policy (`PD-005`)

SKU is a merchant-operational identifier, never canonical Product identity.

Rules:

1. SKU is optional when the initial Draft is created.
2. SKU becomes mandatory before first publication.
3. SKU uniqueness is case-insensitive within one Tenant using a stable normalized representation.
4. The merchant-visible display form may be preserved while normalized value determines uniqueness.
5. Once a Product has been published for the first time, its SKU is immutable.
6. An Archived Product permanently retains its normalized SKU claim; that SKU is not reusable.
7. Another Tenant may independently use the same SKU.
8. Cross-domain references use immutable ProductId and may snapshot the displayed SKU; they never depend on SKU remaining editable.

A conflicting SKU assignment is rejected without changing either Product.

## 5. Product lifecycle (`PD-007`)

Recognized states:

- `Draft` — canonical Product that has never been published and may be incomplete;
- `Published` — public-eligible canonical Product;
- `Unpublished` — retained canonical Product that is not public-eligible;
- `Archived` — terminal retired Product, never public-eligible.

Approved transitions:

```text
Create ─► Draft ──Publish──► Published ──Unpublish──► Unpublished
             │                 │                         │
             └────Archive──────┼────────Archive─────────┘
                               │
                               └────────Archive────────► Archived

Unpublished ──Publish──► Published
```

Rules:

1. editing a Published Product updates the canonical Product and its public projection directly in MVP; no hidden draft-revision/approval workflow exists;
2. Published may be Archived directly; explicit Unpublish first is not required;
3. Archived is terminal in MVP and cannot be restored or republished;
4. publication is independent of current stock availability;
5. Product change never rewrites historical Orders or accounting evidence.

## 6. Publication eligibility (`PD-006`)

A Product may be Published only when:

- it belongs to the trusted Tenant;
- it is not Archived;
- Name is valid;
- SKU is present and satisfies Tenant uniqueness;
- base selling price is valid VND Money;
- referenced Category, Brand, specifications, and media associations satisfy same-Tenant/policy rules.

Category, Brand, description, and media are optional for publication.

A Published Product may still be unavailable for sale because Inventory has no available stock or another independent commerce entitlement/state blocks checkout. Catalog publication itself does not require stock.

## 7. Public slug/address (`PD-008`)

- `ProductId` remains immutable canonical identity.
- A Published Product has a Tenant-scoped public slug.
- CommerceOS may propose a slug from Product name; the merchant may edit it.
- normalized slug is unique within the Tenant.
- slug change does not require historical redirects in MVP.
- slug never grants Tenant authority and is not a cross-Tenant identity key.

## 8. Category and Brand (`PD-009`)

`Category` and `Brand` are independent Tenant-owned reference aggregates with immutable identity.

### Category

- Product may reference zero or one Category;
- Category is non-hierarchical in MVP;
- normalized Category name is unique case-insensitively within the Tenant;
- Category may be retired rather than hard-deleted;
- retirement preserves existing Product references and history.

### Brand

- Product may reference zero or one Brand;
- normalized Brand name is unique case-insensitively within the Tenant;
- Brand may be retired rather than hard-deleted;
- retirement preserves existing Product references and history.

Retirement never silently cascades destructive Product mutation.

## 9. Product specifications and public fields (`PD-037`)

Each `ProductSpecification` has:

- normalized name unique within that Product;
- one text value;
- optional unit;
- merchant-controlled display order.

Private specifications are not supported in MVP. When the Product is Published, its specifications are public.

### PublicProduct projection

MVP public projection may expose:

- ProductId;
- slug;
- name;
- description when present;
- VND Money/base price or a separately resolved Pricing offer when that capability exists;
- SKU;
- Category when present;
- Brand when present;
- approved media;
- specifications;
- derived availability supplied from the appropriate commerce projection.

It must not expose:

- advisory cost;
- raw ingestion snapshots;
- source-review/policy evidence;
- internal change history/revision data;
- merchant-private metadata.

Ingestion source attribution is not public by default and requires a later explicit publication-policy decision if ever introduced.

## 10. Media policy (`PD-010`)

Public Product media in MVP must come from merchant uploads managed through CommerceOS's Files/Media capability.

Rules:

- Catalog owns whether a Product associates with a media asset, its order, and display metadata;
- Files/Media owns reusable merchant-uploaded binary asset identity/safe metadata when introduced;
- Product Data Ingestion may observe external image references but those observations do not authorize public use;
- CommerceOS does not copy arbitrary external binaries for public Product media;
- external-media hotlinking is not supported;
- merchant remains responsible for rights to uploaded content.

Storage/CDN mechanics are outside the domain model.

## 11. Product Data Ingestion relationship (`PD-040`)

```text
Ingestion SourceSnapshot
        ↓ normalize
ImportCandidate Ready
        ↓ merchant review
Approved
        ↓ explicit Catalog apply command
Catalog accepts canonical change
        ↓
Applied
```

Within one Tenant, one external source-product identity may map to zero or one canonical Product in MVP.

ImportCandidate lifecycle:

```text
Ready ──Approve──► Approved ──Catalog accepted apply──► Applied
   │                    │
   ├──Reject───────────► Rejected
   └──newer candidate──► Superseded

Approved ──newer candidate before apply──► Superseded
```

Rules:

- `Applied`, `Rejected`, and `Superseded` are terminal historical states;
- there is no time-only Expired transition in MVP;
- `Approved` means merchant approval only, not that Catalog changed;
- `Applied` exists only after Catalog confirms the canonical mutation;
- newer source evidence never updates a Product automatically;
- imported values pass the same Catalog validation/lifecycle rules as manual edits;
- source provenance remains traceable without transferring continuing authority to the external source.

## 12. Authorization (`PD-003`)

- Owner and Admin may manage Catalog and publish Products in MVP.
- Staff does not receive Catalog administration/publishing merely from the Staff role; any narrower read/operational use must follow the owning feature's explicit policy.
- Viewer is read-only.
- anonymous shoppers receive only public projections through a resolved Storefront Tenant context.
- cross-Tenant Product visibility remains non-disclosing.

There is no separate “catalog manager” role in MVP.

## 13. Commands, queries, and facts

### Command candidates

- `CreateProduct`
- `ChangeProductDetails`
- `AssignProductSKU`
- `SetCatalogBasePrice`
- `ChangeProductSlug`
- `AssignProductCategory`
- `AssignProductBrand`
- `SetProductSpecifications`
- `AttachProductMedia`
- `DetachProductMedia`
- `PublishProduct`
- `UnpublishProduct`
- `ArchiveProduct`
- `ApplyImportCandidate`
- `LinkExternalProduct`
- `CreateCategory` / `RenameCategory` / `RetireCategory`
- `CreateBrand` / `RenameBrand` / `RetireBrand`

### Query intents

- get/list/filter Tenant-owned Products;
- resolve one current Product by ProductId/SKU/slug within trusted Tenant scope;
- resolve current sellable Catalog facts needed for checkout revalidation;
- read public projection for Published Products;
- list active/retired Category and Brand references.

### Owned fact candidates

- `ProductCreated`
- `ProductDetailsChanged`
- `ProductSKUAssigned`
- `ProductBasePriceChanged`
- `ProductSlugChanged`
- `ProductPublished`
- `ProductUnpublished`
- `ProductArchived`
- `ProductImported`
- `ExternalProductLinked`
- `CategoryCreated` / `CategoryChanged` / `CategoryRetired`
- `BrandCreated` / `BrandChanged` / `BrandRetired`

These are business meanings, not published event-schema decisions.

## 14. Business error semantics

| Outcome | Meaning |
|---|---|
| `PRODUCT_NOT_VISIBLE` | absent or not visible in trusted Tenant context; no cross-Tenant disclosure |
| `SKU_REQUIRED` | publication requires SKU and none is assigned |
| `SKU_CONFLICT` | normalized Tenant-scoped SKU already has a permanent claim |
| `SKU_IMMUTABLE_AFTER_PUBLICATION` | requested SKU change targets a Product already published at least once |
| `PRODUCT_INCOMPLETE_FOR_PUBLICATION` | Name/SKU/Money or another required policy fact is invalid |
| `PRODUCT_STATE_TRANSITION_INVALID` | requested lifecycle transition is not approved |
| `PRODUCT_ARCHIVED` | ordinary edit/publish targets terminal Archived Product |
| `SLUG_CONFLICT` | normalized public slug is already used in the Tenant |
| `CATALOG_REFERENCE_INVALID` | Category/Brand/media/source reference violates Tenant/lifecycle policy |
| `CATEGORY_NAME_CONFLICT` / `BRAND_NAME_CONFLICT` | normalized reference name already exists in Tenant scope |
| `SPECIFICATION_NAME_CONFLICT` | duplicate normalized specification name within Product |
| `CATALOG_CURRENCY_INVALID` | value is not supported VND Money policy |
| `SOURCE_IMPORT_STALE_OR_INVALID` | candidate cannot be applied under approved candidate/mapping lifecycle |
| `CATALOG_REVISION_STALE` | concurrent accepted change is newer; no silent overwrite |

## 15. Accounting boundary

Optional Catalog advisory cost remains planning/reference data only. It is never:

- Inventory valuation truth;
- COGS authority;
- supplier cost evidence;
- journal amount authority.

Accounting valuation policy is defined in the Commerce Operations domain baseline.

## 16. Downstream reconciliation handoff

### Technical Architect

Reconcile the completed technical baseline against:

- optional-at-Draft but mandatory-before-publish SKU;
- case-insensitive permanent Tenant SKU claims and post-first-publication immutability;
- direct live edits to Published Product;
- direct Published→Archived and terminal Archived behavior;
- Tenant-scoped slug uniqueness;
- single flat Category plus single Brand references and non-destructive retirement;
- managed merchant-upload media only;
- public specification/public-field policy;
- one-to-one Tenant source-product mapping and explicit ImportCandidate lifecycle.

Do not choose storage/CDN/schema mechanisms in this domain document.

### Backlog Planner

Candidate tasks must no longer treat `PD-002`, `PD-003`, `PD-005`–`PD-010`, `PD-037`, or `PD-040` as unresolved. Tasks that still assume a catalog-manager role, mutable post-publication SKU, external hotlinking, hierarchical/multi-category catalog, draft-revision CMS workflow, or implicit source application require reconciliation before Ready.

**Stop condition: DOMAIN BASELINE READY for current Catalog MVP semantics.**
