# ADR-005 — DynamoDB Module Ownership and Access-Pattern Strategy

Status: Accepted
Date: 2026-08-09
Last reconciled: 2026-08-10
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS uses a serverless/pay-per-use architecture with DynamoDB as the default transactional store. The system needs explicit physical ownership, tenant scoping, access-pattern discipline, concurrency protection, idempotency, and reliable integration records without turning DynamoDB into shared cross-domain persistence.

The original ADR established one table per implementation module and described Tenancy/Catalog first-frontier transactions while several product decisions were open. The product/domain reconciliation now resolves multi-Tenant Membership, role model, Catalog lifecycle/uniqueness, cross-domain Trial onboarding, order/payment semantics, and SubscriptionBilling lifecycle. The persistence strategy remains valid but the detailed access consequences must be updated:

- Merchant Access now needs safe cross-Tenant subject Membership discovery;
- completed onboarding now spans Tenancy + SubscriptionBilling and cannot be one Tenancy-only business completion transaction;
- Catalog SKU/slug/name/source claim semantics are now concrete;
- later Sales/Inventory/Payments/SubscriptionBilling/Accounting modules need the same ownership rules.

## Decision

### 1. Physical ownership

Use one DynamoDB table per implementation module initially, created only when a Ready persistence task needs it.

Examples:

```text
Tenancy             -> Tenancy table
Catalog             -> Catalog table
SubscriptionBilling -> SubscriptionBilling table
Sales               -> Sales table
Inventory           -> Inventory table
Payments            -> Payments table
Procurement          -> Procurement table
Accounting           -> Accounting table
```

Multiple aggregates owned by one module may share its table and bounded module-local transaction.

Another module never receives/uses a foreign table name, key builder, item schema, index, stream, DynamoDB client, or repository.

No platform-wide shared business single-table design is approved.

### 2. Tenant-first access

For tenant-owned business data:

- immutable TenantId is part of the base-table key/query contract;
- repository methods require trusted Tenant scope and have no unscoped tenant overload;
- aggregate/reference/input identifiers never determine Tenant scope;
- tenant-facing indexes are Tenant-scoped/prefixed and cannot override trusted scope;
- missing/cross-Tenant known IDs remain non-disclosing.

### 3. Approved technical cross-Tenant lookup exception

Merchant Access may maintain a **module-private subject-to-Membership discovery representation** in the Tenancy table because `PD-001` permits one Subject to belong to multiple Tenants and selection must not rely on an eventual GSI/JWT claim.

Conceptual shape:

```text
PK = SUBJECT#<SubjectId>
SK = MEMBERSHIP#<MembershipId>#TENANT#<TenantId>
```

This record:

- is a technical authorization lookup, not tenant business data/second Membership aggregate;
- contains only minimum Membership/Tenant references/current Membership status/revision needed for discovery;
- is updated atomically with the owning Membership change;
- is read with strongly consistent base-table `Query`;
- never grants command authority by itself;
- always leads to final current Tenant + Membership validation before `TrustedTenantContext` is produced.

This exception does not authorize generic cross-Tenant indexes/search repositories.

### 4. Access-pattern discipline

Application paths use documented:

- `GetItem`;
- `BatchGetItem`;
- `Query`;
- conditional writes;
- bounded `TransactGetItems`/`TransactWriteItems`.

Application `Scan` is prohibited.

A `FilterExpression` is not an access pattern when it replaces a required key/index or reads an unbounded partition.

Every persistence task maintains an access-pattern ledger containing use case, trusted scope, key/index, cardinality, consistency, protection, pagination/order, isolation proof, recovery, and cost.

### 5. Indexes

Add a GSI only for an approved query.

A GSI may support eventually consistent lists/projections but is never sole authority for:

- Tenant/Membership authorization;
- uniqueness claims;
- aggregate revision;
- last-owner invariant;
- invitation single acceptance;
- Inventory quantity invariant;
- Payment outcome/attempt rule;
- Subscription entitlement/current effectivity;
- Accounting source-posting idempotency.

No speculative platform search/report index.

### 6. Consistency mechanisms

- one-item invariant/revision -> conditional write;
- small same-module all-or-nothing invariant -> DynamoDB transaction;
- current authorization/business correctness -> strong/transactional base-table reads where needed;
- display/projection/list -> eventual only when contract permits;
- no cross-domain distributed ACID or foreign-table transaction, even though DynamoDB technically can transact across tables.

### 7. Tenancy local invariants

Tenancy may transactionally protect:

- Tenant + initial Owner local registration outcome;
- current authority lookup;
- subject discovery representation;
- active-owner guard;
- Membership/role/status changes;
- Invitation issue/acceptance invariants;
- onboarding technical operation/idempotency/work-outbox records.

#### Onboarding reconciliation

The old ADR wording that treated completed onboarding as one Tenancy-only transaction is superseded by ADR-009.

Tenancy transaction commits its **local** outcome and durable Trial-bootstrap work intent:

```text
onboarding operation/idempotency claim
+ Active Tenant
+ Active initial Owner Membership
+ authority lookup
+ subject discovery record
+ owner guard
+ Trial work-outbox
+ accepted-state Audit intent where applicable
```

SubscriptionBilling creates Trial Subscription/EntitlementSet in its own table/transaction. Completed onboarding is reported only after that second owner result is proven.

No cross-module transaction is introduced.

### 8. Catalog current invariants

Catalog uses:

- Product expected revision conditions;
- normalized Tenant SKU claim;
- normalized Tenant public slug claim;
- Category/Brand normalized-name claims;
- source-product mapping claim;
- same-Tenant Catalog reference conditions.

Approved lifecycle consequences:

- Draft may omit/change SKU;
- first publication requires SKU and makes it immutable;
- Unpublish/Archive never releases the historical SKU claim;
- Archived is terminal;
- public slug may change and needs no redirect history;
- Category/Brand retirement is non-destructive;
- external source-product identity maps to at most one Product per Tenant.

If a rename/retirement task needs a historical Category/Brand name-reuse rule not present in the domain baseline, it stops for a domain decision rather than defining claim deletion by persistence convenience.

### 9. Later module ownership

The same strategy applies to later modules:

- Sales owns Order/idempotency/process/outbox records;
- Inventory owns Stock/Reservation/Movement/source records;
- Payments owns Payment/Attempt/provider evidence/reconciliation/outbox;
- SubscriptionBilling owns Subscription/Entitlement/UsageMeter/PlatformCharge/provider evidence;
- Procurement owns PO/receipt/invoice/payment evidence;
- Accounting owns chart/valuation/journals/source-posting claims;
- Notification/Audit/FilesMedia own their persistence when introduced.

No module reconstructs another module's business state by reading its table.

### 10. Technical records

Command/idempotency, technical process, work-outbox, integration outbox, and inbox/source records live in the owning module's table.

Rules:

- command record claimed with first durable effect where possible;
- semantic request fingerprint, not raw JSON hash;
- incompatible reuse conflicts;
- work outbox committed with required one-worker recovery source;
- integration outbox committed with producer business fact;
- inbox/source identity committed with consumer-owned effect where possible;
- TTL only where it cannot remove permanent uniqueness/source history.

### 11. Cross-domain process consistency

Cross-domain correctness is achieved with application contracts and durable processes/events, not DynamoDB transactions across owners.

Examples:

- onboarding: ADR-009 synchronous Trial fast path + SQS recovery;
- order payment/allocation: ADR-010 Step Functions calls module application contracts;
- Accounting/Reporting/SubscriptionBilling consumers: ADR-006 outbox/EventBridge/SQS;
- Procurement GoodsReceipt application to Inventory/Accounting: reliable source facts and idempotent consumer effects.

### 12. Capacity and resource policy

- initial tables single-region `ap-southeast-1`;
- no Global Tables without later recovery/data-residency/conflict ADR;
- learning/dev may use small provisioned capacity within aggregate Free Tier target when appropriate;
- on-demand may be used intentionally for preview/burst/production-like behavior;
- each table/GSI capacity, retention/removal, encryption, stream, backup/PITR choice explicit in CDK/task cost analysis;
- AWS-managed/default encryption unless a threat/compliance requirement justifies customer-managed keys;
- no speculative stream/GSI/PITR resource.

## Alternatives considered

### Platform-wide single table

Rejected because it couples unrelated domains, IAM, keys/indexes, migration, and encourages foreign representation access.

### Table per aggregate/entity

Rejected as the default because table count follows modeling detail rather than implementation ownership and fragments module-local transactions.

### One table per implementation module

Chosen because it aligns persistence/IAM/migration with module ownership while preserving bounded local transactions and future extraction.

### Relational always-on database

Rejected as the current default because no approved query/transaction requirement justifies the changed standing-cost/operational model.

### Cross-domain DynamoDB transaction for onboarding or plan limits

Rejected because technical ACID would collapse bounded-context persistence ownership and make future extraction/migration harder. Use explicit contracts/processes instead.

## Consequences

### Positive

- module ownership visible in code/CDK/IAM/tests;
- tenant isolation is present in persistence contract, not only authorization;
- strong subject discovery supports safe multi-Tenant selection without JWT/GSI authority;
- critical races have explicit condition/transaction mechanisms;
- onboarding preserves separate SubscriptionBilling ownership;
- later domain extraction does not begin with disentangling one shared platform table.

### Negative / trade-offs

- cross-module ACID is deliberately unavailable;
- subject discovery adds a second Merchant Access representation to maintain transactionally;
- several low-volume module tables/GSIs require aggregate Free Tier capacity planning;
- strong/transactional reads cost more than eventual reads;
- access patterns must be refined before ad-hoc query feature implementation.

## Security and tenant impact

- tenant-owned repositories require trusted Tenant scope;
- subject discovery is minimal and accessible only through Merchant Access current authenticated-subject path;
- provider/source/aggregate IDs cannot cross Tenant scope by known value alone;
- shared initial Lambda may physically hold IAM to several module tables, so module-private Infrastructure and architecture tests prohibit foreign access;
- no tokens/secrets/raw card/provider payloads in items/keys.

## Reliability and operability impact

- condition/transaction cancellation produces deterministic stale/conflict/validation outcomes;
- technical throttling/unavailability never weakens an invariant;
- command/source records make retry duplicate-safe;
- stream is never the only recovery source; durable outbox/inbox/process records support repair;
- pending onboarding/workflow/event gaps are reconcilable without foreign table reads.

## Cost impact

This ADR update deploys nothing and changes runtime cost by zero.

Future cost comes from module table/transaction/index/stream request amplification and must be measured per Ready task. No standing-cost service is introduced.

## Reversibility / migration

- key/index changes require versioned migration/backfill/cutover;
- consolidating module tables requires a new ADR because it weakens ownership/IAM boundaries;
- splitting a module table for scale/security preserves foreign application contracts;
- moving to relational persistence requires migration/architecture/cost ADR while preserving module application boundaries;
- changing subject discovery representation must preserve intentional selection/current validation semantics during cutover.

## Validation

Dependent implementation must verify:

- module-local access-pattern ledgers match repositories/tests;
- no application Scan/foreign Infrastructure reference;
- tenant repositories have no unscoped overload;
- Tenant A/B known-ID/cursor/SKU/slug/provider/source isolation;
- subject discovery is strongly/currently maintained and never sole final authority;
- onboarding failure cannot falsely report completion and uses no cross-module transaction;
- last-owner/invitation/SKU/slug/name/source races preserve invariants;
- Inventory/Payment/Subscription/Accounting later invariants use owner-local conditions/source claims;
- CDK asserts table ownership/capacity/encryption/removal/stream/index/least-privilege grants;
- no key/index choice fills an explicit domain/product gate by convenience.

## References

- [Persistence access patterns](../architecture/persistence-access-patterns.md)
- [Technical baseline](../architecture/technical-baseline.md)
- [ADR-004](ADR-004-trusted-tenant-authority-and-authorization-boundary.md)
- [ADR-006](ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md)
- [ADR-009](ADR-009-cross-domain-onboarding-completion-and-trial-bootstrap-recovery.md)
- [ADR-010](ADR-010-order-payment-allocation-durable-orchestration.md)