# Inventory access patterns

| ID | Use case | Trusted scope | Key / consistency | Protection |
|---|---|---|---|---|
| INV-AP-00R | Create/reactivate Warehouse | Merchant Access mutation context | Tenant guard + Warehouse transaction | current entitlement and conditional active count |
| INV-AP-01 | Get StockItem | trusted Tenant + Product + Warehouse | base key, strong | identifiers cannot choose Tenant |

`StockItem` enforces `OnHand >= Reserved >= 0`; mutation operations/reservations are intentionally deferred to TASK-0162.
