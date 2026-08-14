# Procurement access patterns

`PROC-AP-01` gets a Supplier and PurchaseOrder under trusted Tenant scope with strong reads. `PROC-AP-02` submits the Draft PO using expected revision after an explicit Catalog eligibility contract query. Catalog persistence is never read. Submitted snapshots are immutable.
