# Customer/CRM access patterns

| ID | Use case | Trusted scope | Key / consistency | Protection |
|---|---|---|---|---|
| CUS-AP-01 | Create customer profile | Merchant Access trusted tenant | `PK=TENANT#t, SK=CUSTOMER#id` conditional create | client tenant selector is never authority |
| CUS-AP-02 | Get/update profile | Merchant Access trusted tenant | strong point read / expected revision | tenant key and revision prevent cross-tenant/stale writes |
| CUS-AP-03 | List/search profile | Merchant Access trusted tenant | tenant partition query, bounded page | no global contact lookup or automatic matching |

Guest checkout remains Sales-owned immutable snapshot data. Customer never consumes guest email/phone to create, deduplicate or merge a profile.
