# Tenancy persistence access-pattern ledger

| ID | Owner / use case | Trusted scope | Key/query and consistency | Write protection / isolation / recovery | Cost note |
|---|---|---|---|---|---|
| TEN-AP-01R | Tenant + initial Owner registration | trusted onboarding context; never client TenantId | bounded transaction in Tenant partition; transactional | operation claim, Tenant, Membership, authority, discovery, owner/count guards and Trial work intent commit together | multiple module-local writes |
| TEN-AP-02 | Tenant/Profile read | `TrustedTenantPersistenceScope` after authority resolution | `TENANT#<encoded tenant> / TENANT`; strong | no unscoped merchant read; missing is non-disclosing at delivery | one strong read |
| TEN-AP-04R | current authority | authenticated subject plus selected Tenant candidate | strong authority record then Membership record | discovery is candidate-only; final Tenant/Membership validation required | two strong reads |
| TEN-AP-05 | Membership read/mutation | `TrustedTenantPersistenceScope` | Tenant partition Membership key; strong for mutation | expected revision; lifecycle task transacts authority/discovery/guards | one strong read; mutation may be transactional |
| TEN-AP-05W | Membership persistence supporting current authority | `TrustedTenantPersistenceScope` matching the Membership Tenant | bounded transaction: Tenant Membership + authority-by-Subject + Subject discovery record | conditional creation/update preserves Tenant/Subject binding and keeps authority/discovery representations in one commit; lifecycle rules remain Application-owned | three transactional writes |
| TEN-AP-06 | membership list | resolved tenant read context | bounded base-table Query | display only, never authority | paged Query; no Scan/GSI |
| TEN-AP-11R | subject discovery | authenticated Subject identity only | strong Query on Subject partition | candidates never authorize; all candidates are revalidated | one strong Query |

The DynamoDB key codec is module-private and base64url-encodes user-controlled identifiers. No other module may use this table, its keys, or this ledger as a persistence contract.
