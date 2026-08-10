# ADR-005 — DynamoDB Module Ownership and Access-Pattern Strategy

Status: Accepted
Date: 2026-08-09
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS has selected a serverless/pay-per-use architecture and identifies DynamoDB as the default transactional store, but the existing documents do not decide physical ownership, trusted tenant keying, access-pattern discipline, or first-frontier transaction boundaries.

A platform-wide single table would allow unrelated modules and one broad IAM/data model to couple before access patterns are stable. A table per aggregate would fragment small transactions and multiply capacity/configuration. A relational database would add an always-on or different serverless cost/operational profile without a business requirement and would conflict with the accepted DynamoDB direction.

TASK-0087 requires atomic onboarding, current Membership authority, last-owner and invitation invariants, Product revision protection, same-tenant references, and tenant-wide SKU uniqueness. These cannot be protected through unguarded read-then-write logic or an eventually consistent index.

## Decision

### Physical ownership

- Use one DynamoDB table per implementation module initially, created only with that module.
- Tenancy owns its table; Catalog owns a different table; Audit and later modules own their tables when introduced.
- Multiple aggregates owned by one implementation module may share its table and a bounded transaction.
- Physical consolidation or shared-table access across modules is not approved.
- Another module never receives or uses a foreign table name, key builder, item schema, index, stream, DynamoDB client, or repository.

### Tenant-first access

- Every tenant-owned base-table partition/key/query includes immutable TenantId.
- Tenant-owned repository contracts require trusted tenant scope and have no unscoped overload.
- Aggregate/reference/input identifiers never determine Tenant scope.
- Application paths use documented `GetItem`, `BatchGetItem`, `Query`, conditions, or bounded transactions; `Scan` is prohibited.
- Pagination is stable, bounded, opaque, versioned, and bound to trusted Tenant + query shape. Raw `LastEvaluatedKey` is not a public contract.

### Access patterns and indexes

- Every persistence task maintains an access-pattern ledger covering use case, trusted scope, key/index, cardinality, consistency, condition/transaction, ordering/pagination, isolation proof, and cost.
- Add a GSI only for an approved query. A `FilterExpression` is not a substitute for an index/keyed pattern.
- GSIs may support eventually consistent lists/projections but never the sole authorization, uniqueness, revision, last-owner, invitation-acceptance, SKU-claim, payment, inventory, or accounting source check.
- No platform search/reporting index is created speculatively.

### Consistency mechanisms

- Use a conditional write for a one-item invariant or optimistic revision.
- Use `TransactWriteItems` for a small same-module all-or-nothing invariant.
- Use the authoritative base table and a transactionally/strongly consistent read when current authorization/business correctness requires it.
- Do not introduce cross-domain distributed ACID or foreign-table writes simply because DynamoDB can transact across tables.

### First-frontier boundaries

Tenancy:

- onboarding atomically writes the durable accepted-intent/admission claim, Active Tenant, Active initial Owner Membership, current authority lookup, and owner guard;
- authority resolution transactionally reads current Tenant + subject authority from the base table;
- Membership/role/status changes conditionally update the Membership, authority lookup, and owner guard;
- invitation acceptance uses one transaction for terminal/single-use/recipient/expiry conditions and the approved Membership effect.

Catalog:

- Product create/update/lifecycle writes use expected revision conditions;
- normalized SKU uniqueness is an authoritative tenant-scoped claim item changed atomically with Product;
- Category/Brand references are condition-checked in the same Tenant partition when assigned;
- a public projection, if later persisted, remains Catalog-owned and is atomically consistent/recoverable with Product state.

The exact admission, role, invitation, SKU, Category/Brand, publication, and public-field semantics remain governed by their `PD-*` decisions. The persistence mechanism does not choose them.

### Technical records and capacity

- Command/idempotency, integration-outbox, and consumer-inbox/source records live in the owning module's table and transaction boundary.
- A TTL may clean a replay cache only when it cannot remove a permanent uniqueness/source invariant.
- Persistent learning/dev may use a small provisioned-capacity profile within the aggregate Free Tier target; on-demand is an intentional preview/burst/production-like profile.
- Every table/GSI capacity, retention/removal policy, encryption, stream, and backup/PITR choice is explicit in CDK and task cost analysis.
- Use AWS-managed/default encryption unless an approved threat/compliance requirement justifies customer-managed keys.
- Single-region tables in `ap-southeast-1` are the initial architecture; no global table is approved.

## Alternatives considered

### Option A — One platform-wide DynamoDB single table

- Benefits: cross-entity transactions and single-table patterns; fewer table resources; potentially flexible access modeling.
- Costs/risks: premature shared persistence, broader IAM, unrelated key/index coupling, difficult module extraction, and greater chance that one module reads another's representation.

### Option B — One table per aggregate/entity

- Benefits: obvious data separation and narrow grants.
- Costs/risks: more tables/capacity configuration, awkward module-local transactions/read models, and table count following modeling detail rather than operational ownership.

### Option C — One table per implementation module

- Benefits: aligns physical/IAM ownership with module boundaries, supports module-local transactions/access patterns, avoids platform-wide single-table coupling, and preserves future extraction.
- Costs/risks: shared module table still needs disciplined item/key ownership; some future cross-module workflows become eventual; several low-volume provisioned tables/GSIs need aggregate Free Tier planning.

### Option D — Always-on relational database

- Benefits: familiar transactions/queries and relational constraints.
- Costs/risks: unapproved service/standing cost, scale-to-zero mismatch, changed architecture/operations, and no current access need that justifies it.

Chosen: Option C with explicit conditional/transactional access rules.

## Consequences

### Positive

- Tenant scope is present in authorization and persistence contracts.
- Module ownership is visible in CDK, IAM, code, tests, and access-pattern documents.
- Critical first-frontier races have explicit atomic mechanisms.
- No Builder must invent a single-table strategy, query via Scan, or rely on eventual uniqueness.
- Module extraction does not first require separating unrelated-domain rows from one platform table.

### Negative / trade-offs

- Cross-module ACID is deliberately unavailable; later business processes need explicit synchronous contracts or durable eventual integration.
- The initial shared API function can still have IAM access to several tables, so code/tests enforce more isolation than process IAM.
- Table-per-module provisioned capacity and each GSI must be planned together to remain inside learning allowances.
- DynamoDB access patterns must be known/refined before feature implementation; arbitrary ad hoc search/report queries are not available.
- Transactional/strong reads cost more than eventual reads and must be measured.

## Security and tenant impact

- Tenant isolation: every tenant repository/key/query requires trusted TenantId; cross-tenant known-ID tests are mandatory at repository and API layers.
- Authentication/authorization: authority resolution uses current base-table records, not an eventual GSI; ADR-004 remains the trust authority.
- Sensitive data/secrets: items contain only domain-required data; no tokens/secrets/raw payment data. Invitation/onboarding proof is hashed/redacted and separately protected when defined.
- IAM: CDK grants functions only explicit table actions/resources. Module Infrastructure hides clients/models; later split workers/functions get narrower table grants.

## Reliability and operability impact

- Failure modes: condition/transaction cancellation produces deterministic conflict/stale/validation outcomes; throttling/unavailability remains a retryable dependency failure and never weakens an invariant.
- Retry/recovery: command records make unsafe retries idempotent; transactions are retried only under bounded, classified technical policy and do not re-evaluate stale business intent silently.
- Consistency: authority/current invariants use base-table transactional/strong paths; lists/projections may be eventually consistent by contract.
- Observability: table/index throttle/latency/system-error metrics, transaction conflict counts, and safe context codes are observable; no high-cardinality custom dimensions.
- Recovery: stream is not the only event recovery store; durable outbox/inbox records support reconciliation under ADR-006.

## Cost impact

- Learning profile: TASK-0088 creates no table/cost. Existing model estimates about $0.29/month for its on-demand assumptions; a deliberately small aggregate provisioned profile may approach $0 within current allowances.
- Beta profile: existing model estimates about $2.88/month. GSIs, transactional reads/writes, outbox/inbox records, and item size add request/storage amplification and must be measured per task.
- Larger-scale implication: hot Tenant partitions, high-GSI write amplification, and authorization reads may require repartitioning/caching/extraction. Those changes require evidence and a migration ADR.
- Cost-model update required? No now; this uses the existing DynamoDB category. A future task adds measured table/GSI/stream/backup assumptions when material.

## Reversibility / migration

- Changing a module's key/index layout requires versioned item migration/backfill and dual-read/write or cutover strategy.
- Consolidating module tables requires a new ADR because it weakens physical ownership/IAM boundaries.
- Splitting one module table for scale/security requires data migration and contract-preserving repository changes; foreign modules remain unaffected.
- Moving to relational persistence requires schema/data migration, new infrastructure/cost/reliability ADR, and unchanged module application contracts.
- Enabling global tables requires multi-region consistency/conflict/recovery analysis; current transaction guarantees are single-region.

## Validation

- Module-local access-pattern ledgers exist and match repository/integration tests.
- Static/architecture checks prohibit Scans in application repositories where practical and reject foreign Infrastructure references.
- Tenant repository signatures have no unscoped overload; Tenant A/B tests prove known-ID, cursor, SKU, Category/Brand, and request override isolation.
- Real DynamoDB tests exercise transaction failure, condition races, strong/current authority reads, and approved GSI eventual behavior.
- Onboarding failure injection cannot create a completed partial Tenant/Owner result.
- Last-owner, invitation single acceptance, Product revision, and SKU uniqueness races preserve their invariants.
- CDK assertions verify table ownership, explicit capacity/encryption/removal/stream settings, least-privilege grants, and no unapproved global table/PITR/index.

## References

- relevant task: [TASK-0088](../../tasks/completed/TASK-0088-technical-architecture-baseline-reconciliation.md)
- architecture docs: [Persistence ownership and access patterns](../architecture/persistence-access-patterns.md), [Technical baseline](../architecture/technical-baseline.md)
- AWS: [DynamoDB transactions](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/transaction-apis.html), [read consistency](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.ReadConsistency.html), [global secondary indexes](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/GSI.html)
