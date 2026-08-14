# Catalog access patterns

| ID | Use case | Trusted scope | Key / consistency | Protection |
|---|---|---|---|---|
| CAT-AP-01 | Get Product | Merchant Access mutation/read context | `TENANT#id` / `PRODUCT#id`, strong | Tenant partition is fixed by trusted context |
| CAT-AP-02 | Create/edit/publish/archive | Merchant Access mutation context | bounded transaction | Product revision plus tenant SKU/slug claims |
| CAT-AP-03 | SKU / slug uniqueness | same | `SKU#normalized` / `SLUG#normalized` claim records | transactional conditional claims; no index authority |

No Catalog operation scans, or reads Catalog-adjacent module persistence.
