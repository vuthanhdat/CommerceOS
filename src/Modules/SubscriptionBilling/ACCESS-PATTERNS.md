# SubscriptionBilling persistence access-pattern ledger

| ID | Owner / use case | Scope | Key/query and consistency | Write protection / recovery | Cost note |
|---|---|---|---|---|---|
| SUB-AP-00 | Bootstrap immutable Trial terms and paid PlanVersions | platform catalog; no caller-selected Tenant authority | direct `CATALOG / <terms identity>` strong read; bounded `CATALOG` PlanVersion prefix Query for sellable catalog | conditional create; equal replay is `AlreadyApplied`; same identity with different contents is `VersionConflict`; no TTL | four strong reads/writes at bootstrap; one bounded Query for catalog display |

The initial catalog source is `catalog/initial-catalog.v1.json`. It is SubscriptionBilling-owned platform commercial truth, not a frontend, JWT, foreign-module, AppConfig, or SSM authority. The DynamoDB key schema is module-private; no other module may read this table or depend on its record shape.
