# Product Data Ingestion access patterns

Platform source policy is separate from Tenant enrollment. `PDI-AP-01` reads one source policy by SourceId; `PDI-AP-02` reads/writes enrollment under trusted Tenant scope with revision; scheduled work rechecks both plus the entitlement contract.
