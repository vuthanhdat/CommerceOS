# ADR-003 — First-Frontier Modular Runtime and Deployment Boundaries

Status: Accepted
Date: 2026-08-09
Decision owners: CommerceOS Technical Architecture
Supersedes: N/A
Superseded by: N/A

## Context

TASK-0087 separates Tenant Management, Merchant Access, Catalog, Audit, and later commerce contexts by business ownership. It also requires merchant onboarding to produce one usable Active Tenant with an Active initial Owner Membership; completed success cannot expose an ownerless Tenant.

ADR-002 accepts a modular-monolith repository with Domain/Application/Infrastructure layers but does not map the reconciled business contexts to implementation modules, define cross-module contract placement, or choose the first Lambda/CDK deployment boundaries.

Treating every context as an independent module/service would make the onboarding invariant a premature cross-module/distributed consistency problem. Combining all contexts into `Platform` would erase ownership. Creating one Lambda/stack/table per context would also confuse logical and operational boundaries and add packaging, IAM, observability, and delivery overhead before workload evidence exists.

## Decision

### Implementation modules

- Create one `Tenancy` implementation module containing two explicitly separated model areas: Tenant Management and Merchant Access.
- Preserve separate aggregates, namespaces, use cases, repository contracts, and business-fact ownership inside Tenancy. `Tenant` and `Membership` do not become one aggregate.
- Create `Catalog` as a separate implementation module and persistence owner.
- Keep `Platform` technical/foundation-only; it is not a shared business domain or common-model project.
- Add Audit and every later context as its own implementation module only when a Ready task introduces it. Do not pre-create empty modules.
- Keep the Mock Payment Provider a separate external-like application/deployment boundary when it is introduced.

Tenancy is the transaction boundary for the approved completed onboarding outcome. Its application coordinator can atomically persist Tenant Management and Merchant Access records without a cross-service workflow while preserving their business ownership internally.

### Layer and contract graph

- Domain uses only the .NET base class library and references no project/package.
- Application references its own Domain and owns use cases/ports.
- Infrastructure references its own Application and implements persistence/AWS/external adapters.
- Delivery/composition references module Application/Infrastructure projects and maps transport input; it contains no business rules.
- Add a producer-owned `CommerceOS.<Module>.Contracts` project only when delivery or another module has a real contract consumer.
- Contracts are immutable, transport-neutral, implementation-free, and contain no Domain entity, repository, AWS, HTTP framework, or DynamoDB type.
- An Application project may reference an explicitly approved foreign Contracts project, but never a foreign Domain, Application implementation, or Infrastructure project.

Current architecture tests must be evolved before the first Contracts or new Domain project is implemented so the accepted graph is machine-enforced across all modules.

### Initial runtime and stacks

- Package the protected/back-office application initially as one .NET `commerce-api` Lambda behind one API Gateway HTTP API.
- Host Tenancy and Catalog in that runtime while retaining code and table ownership.
- `IdentityStack` owns Cognito authentication resources.
- `CommerceStack` owns API Gateway, the `commerce-api` Lambda, and module-owned table constructs as introduced.
- `FoundationStack` remains bounded technical foundation and does not create an event bus by default.
- `WebStack`, integration resources, `CrawlerStack`, and `MockPaymentStack` are created only with a Ready task and their named workload.
- Run application writes in one initial Region, `ap-southeast-1`; CloudFront remains global when web delivery is introduced.

Split a function, stack, or deployable service only when measured scale, runtime, security/IAM isolation, reliability/blast radius, team ownership, or deployment-cadence pressure justifies it. A bounded context alone is not sufficient evidence.

## Alternatives considered

### Option A — One implementation module and deployable service per bounded context

- Benefits: maximal compile-time/IAM/deployment separation; independent scaling from the start.
- Costs/risks: premature distributed onboarding, more packages/stacks/contracts, extra failure/observability burden, and greater risk of a distributed monolith before domain learning.

### Option B — One Platform/business module and one shared data model

- Benefits: smallest initial project count and simple in-process calls.
- Costs/risks: erases source-of-truth ownership, encourages cross-domain entity/persistence shortcuts, makes later extraction difficult, and contradicts TASK-0087.

### Option C — Tenancy + Catalog modules in one initial commerce runtime

- Benefits: preserves ownership and compile-time boundaries, keeps onboarding atomic/local, avoids early network contracts, and retains later extraction paths through module-owned tables/contracts.
- Costs/risks: one runtime role can access more than one table; deployment failures affect several modules; architecture tests and code review must enforce boundaries that IAM cannot yet enforce.

Chosen: Option C.

## Consequences

### Positive

- The first-frontier business boundaries have explicit technical homes.
- Onboarding can satisfy the accepted all-or-nothing outcome without inventing a saga.
- Catalog remains independently owned and cannot use Tenancy persistence.
- Contracts appear only when a consumer makes them necessary.
- The repository can remain a modular monolith while preserving credible future extraction seams.
- Lambda/stack count follows runtime pressure rather than task or domain count.

### Negative / trade-offs

- Tenant Management and Merchant Access require strong internal naming/ownership discipline inside one implementation module.
- The shared commerce Lambda initially has a wider IAM/resource blast radius than one function per module.
- Current architecture tests do not yet support Contracts or scan every future Domain assembly; a foundation follow-up is required before those shapes are implemented.
- Single-region operation does not meet a future multi-region recovery/data-residency requirement without another ADR and migration.

## Security and tenant impact

- Tenant isolation: module repositories still require trusted tenant scope and use tenant-partitioned keys; a shared runtime does not permit shared/unscoped data access.
- Authentication/authorization: Cognito remains external authentication; Merchant Access owns current Tenant authority. Details are ADR-004.
- Sensitive data/secrets: no credential is shared through Contracts. Stack/function environment settings do not contain secrets by convenience.
- IAM: the initial shared function receives explicit table/actions only; workers and later split functions receive narrower grants. Architecture tests and cross-tenant integration tests compensate for the initial coarser function boundary.

## Reliability and operability impact

- Failure modes: a `commerce-api` deployment/runtime incident affects Tenancy and Catalog together; module bugs remain isolated by contracts/tests rather than process isolation.
- Retry/recovery: first-frontier local commands use module transactions/idempotency; asynchronous consumers are not required for CRUD/onboarding.
- Observability: logs/metrics identify module and operation inside the shared runtime; cost/latency can later justify extraction.
- Operational burden: fewer functions/stacks/packages initially; explicit extraction criteria prevent indefinite accidental coupling.

## Cost impact

- Learning profile: TASK-0088 deploys nothing. The later single commerce function/API and module tables use the already-modeled Lambda/API Gateway/DynamoDB cost categories; fewer deployables do not create a new service charge.
- Beta profile: shared runtime minimizes duplicate low-volume operational resources; requests/compute/storage still scale with use.
- Larger-scale implication: a hot module may require function/table/deployment extraction. Module-owned persistence/contracts reduce migration scope, but a measured ADR and CDK migration plan are required.
- Cost-model update required? No for this ADR. It changes boundaries, not the modeled service set or current runtime resources.

## Reversibility / migration

The code boundary is intentionally reversible.

- Splitting Tenancy model areas into separate modules requires replacing the onboarding local transaction with an explicitly reliable coordination model while preserving the no-ownerless-success invariant.
- Splitting a module into a Lambda/service requires contract hardening, IAM/resource separation, independent deployment/observability, timeout/retry semantics, and migration testing.
- Moving tables requires dual-write/backfill/cutover or another explicit state migration; module ownership avoids unrelated-domain data in that migration.
- Changing Region requires stateful data/object migration and recovery/data-residency review.

## Validation

- Architecture tests enumerate every Domain assembly and reject framework/AWS/foreign-module dependencies.
- Project-reference tests enforce Domain/Application/Contracts/Infrastructure/delivery rules.
- Tenancy tests prove its two model areas retain separate aggregates/facts while onboarding commits one all-or-nothing result.
- Catalog tests prove it cannot reference Tenancy Domain/Infrastructure or use Tenancy persistence.
- CDK assertions show IdentityStack/CommerceStack responsibility and no speculative bus/queue/workflow resources.
- Runtime logs/metrics can be grouped by module; extraction decisions cite measured evidence.

## References

- relevant task: [TASK-0088](../../tasks/completed/TASK-0088-technical-architecture-baseline-reconciliation.md)
- domain baseline: [Tenant Management & Merchant Access](../domains/tenant-identity.md), [Catalog](../domains/catalog.md)
- architecture docs: [Technical baseline](../architecture/technical-baseline.md), [ADR-002](ADR-002-phase-0-toolchain-and-repository-structure.md)
