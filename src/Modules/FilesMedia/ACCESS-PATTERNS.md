# FilesMedia access patterns

`MED-AP-01` creates a PendingUpload metadata record under trusted Tenant scope. `MED-AP-02` finalizes it only after the object gateway confirms exact content type and length. `MED-AP-03` producer-owned lookup returns only Ready asset identity/type to Catalog. No external URL becomes a managed asset.
