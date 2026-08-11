# ADR-003 — First-Frontier Modular Runtime and Deployment Boundaries

Status: Accepted, amended by ADR-012
Date: 2026-08-09
Amended: 2026-08-11
Decision owners: CommerceOS Technical Architecture

## Context

TASK-0087 separates Tenant Management, Merchant Access, Catalog, Audit, and later commerce contexts by business ownership. ADR-002 accepts a modular-monolith repository with Domain/Application/Infrastructure layers but does not map contexts to implementation modules or initial runtime boundaries.

Treating every context as an independent service would make onboarding a premature distributed-consistency problem. Combining all contexts into `Platform` would erase ownership. Creating one deployable per bounded context would confuse logical and operational boundaries.

ADR-012 later changed the physical infrastructure target from real AWS to LocalStack only. This amendment preserves the module/runtime boundary decision while removing AWS account/region/IAM deployment assumptions.

## Decision

### Implementation modules

- Create one `Tenancy` implementation module containing Tenant Management and Merchant Access as explicitly separated model areas.
- Preserve separate aggregates, namespaces, use cases, repository contracts, and business-fact ownership inside Tenancy.
- Create `Catalog` as a separate implementation module and persistence owner.
- Keep `Platform` technical/foundation-only.
- Add Audit and later contexts only when a Ready task introduces them.
- Keep Mock Payment Provider a separate external-like application/runtime boundary when introduced.

Tenancy remains the local transaction boundary for the approved completed onboarding outcome where that transaction is within Tenancy ownership. Cross-module Trial completion follows ADR-009.

### Layer and contract graph

- Domain uses only approved base/cross-cutting types and has no AWS SDK, LocalStack, framework, persistence, or provider dependency.
- Application references its Domain and owns use cases/ports.
- Infrastructure references its Application and implements persistence/messaging/provider/runtime adapters.
- Delivery/composition references module Application/Infrastructure and maps transport input; it contains no business rules.
- Add producer-owned `CommerceOS.<Module>.Contracts` only when a real consumer exists.
- Contracts are transport-neutral, implementation-free, and contain no Domain entity, repository, AWS/LocalStack, HTTP-framework, or persistence type.
- Application may reference explicitly approved foreign Contracts, never foreign Domain/Application implementation/Infrastructure.

### Initial runtime boundaries

The initial application runtime remains one shared .NET `commerce-api` serverless delivery composition hosting Tenancy and Catalog while preserving code/persistence ownership.

Preferred LocalStack mappings when Ready and sufficiently supported:

- HTTP/serverless delivery capability -> API Gateway + Lambda;
- identity-edge capability -> Cognito;
- module persistence -> DynamoDB;
- web/integration/crawler/provider resources -> only when a named Ready task introduces them.

`FoundationStack` remains bounded technical foundation and does not pre-create generic event infrastructure.

There is **no fixed AWS Region/account target** under ADR-012. Region/account-id values used by SDK/CDK/LocalStack are configuration placeholders and are not business/runtime authority.

Split a function, stack, or deployable service only when measured runtime, security isolation, reliability/blast-radius, team ownership, or deployment-cadence pressure justifies it. A bounded context alone is not sufficient evidence.

## Consequences

### Positive

- first-frontier business boundaries have explicit technical homes;
- Catalog ownership remains independent;
- Contracts appear only when a consumer requires them;
- modular-monolith learning remains intact;
- LocalStack runtime can exercise serverless mappings without forcing real cloud deployment;
- later extraction seams remain explicit.

### Trade-offs

- shared runtime requires architecture tests/code review to prevent boundary erosion;
- process-level isolation is coarser than module ownership;
- LocalStack may not reproduce every Lambda/API Gateway/Cognito control-plane behavior exactly;
- extraction still requires measured evidence and a focused ADR.

## Security and tenant impact

- a shared runtime never permits unscoped/foreign persistence access;
- identity-edge mapping proves identity only; Merchant Access remains current Tenant authority under ADR-004;
- no LocalStack synthetic credential/endpoints belong in Domain/Application contracts;
- authorization is tested at application boundaries rather than inferred from emulator infrastructure.

## Reliability and operability impact

- a shared runtime incident can affect multiple modules, while logical failures remain isolated by contracts/tests;
- module-local commands use owned transactions/idempotency;
- logs/metrics should identify module/operation where supported by the selected local runtime;
- LocalStack runtime state is operational/testing state only.

## Reversibility / migration

- splitting Tenancy model areas into separate modules requires reliable coordination while preserving the no-ownerless-success invariant;
- splitting a module into a separate runtime requires contract hardening, independent failure semantics, deployment isolation, and migration testing;
- moving persistence requires explicit migration/cutover planning;
- changing from LocalStack to real AWS requires a human architecture decision superseding ADR-012.

## Validation

- architecture tests enumerate Domain assemblies and reject framework/AWS/LocalStack/foreign-module dependencies;
- project-reference tests enforce Domain/Application/Contracts/Infrastructure/delivery direction;
- Tenancy and Catalog tests enforce ownership separation;
- CDK assertions show no speculative bus/queue/workflow resources;
- LocalStack tests validate selected runtime mappings where supported and document limitations where not.
