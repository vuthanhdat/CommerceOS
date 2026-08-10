# CommerceOS — Technical Architecture Baseline

_Reconciled by TASK-0088 on 2026-08-09._

## 1. Purpose and authority

This document is the canonical implementation-architecture map for CommerceOS. It translates the approved business ownership in [the domain baseline](../02-business-domains.md) into module, layer, contract, deployment, and consistency boundaries.

When technical documents or candidate tasks disagree:

1. accepted ADRs take precedence for their stated decision;
2. this baseline and its linked architecture documents take precedence over older directional diagrams and candidate-task assumptions;
3. the domain baseline and its [product-decision register](../domains/product-decisions.md) remain authoritative for business meaning;
4. a technical choice must preserve a pending product decision rather than select one indirectly;
5. a Builder stops when a required decision is still marked product-gated or technically deferred.

Detailed architecture documents:

- [First-frontier contracts and trusted context](first-frontier-contracts.md)
- [Persistence ownership and access patterns](persistence-access-patterns.md)
- [Integration and AWS service matrix](integration-and-aws.md)

Material decisions are recorded in:

- [ADR-003 — First-frontier modular runtime and deployment boundaries](../adr/ADR-003-first-frontier-modular-runtime-and-deployment-boundaries.md)
- [ADR-004 — Trusted tenant authority and authorization boundary](../adr/ADR-004-trusted-tenant-authority-and-authorization-boundary.md)
- [ADR-005 — DynamoDB module ownership and access-pattern strategy](../adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md)
- [ADR-006 — Reliable cross-domain integration and deferred workflow orchestration](../adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md)

## 2. Scope and decision vocabulary

This baseline is detailed for Tenant Management, Merchant Access, and Catalog. It gives medium-depth integration runway for later contexts without finalizing their product-gated workflows.

Decision status means:

- **Accepted** — a dependent task must implement this constraint and may not choose an alternative silently.
- **Conditional** — the mechanism is accepted only when the named trigger or consumer exists; no resource/project is pre-created.
- **Product-gated** — an entry in the product-decision register, or another missing business decision, must be resolved first.
- **Technically deferred** — the decision has no justified near-term consumer or evidence; its trigger and safe interim constraint are explicit.

This task changes documentation and ADRs only. It does not create application behavior or AWS resources.

## 3. Implemented baseline versus approved target

### Implemented now

The repository currently contains only Phase 0 foundation scaffolding:

- one technical `Platform` Domain/Application/Infrastructure project set;
- an anonymous `GET /health` endpoint in the ASP.NET Core host;
- two frontend foundation applications;
- one CDK `FoundationStack` containing a bounded CloudWatch log group and cost/environment tags;
- dependency tests for the current Platform module;
- no Lambda packaging, business module, Cognito, API Gateway, DynamoDB, EventBridge, SQS, Step Functions, S3, or CloudFront application resource.

The scaffold is not evidence that a business capability, persistence model, or deployment resource already exists.

### Approved near-term target

```text
API Gateway HTTP API
       │ Cognito validates token only
       ▼
one commerce-api Lambda deployment
       │
       ├── Tenancy module
       │     ├── Tenant Management model area
       │     └── Merchant Access model area
       │
       ├── Catalog module
       │
       └── delivery/composition concerns

module-owned DynamoDB tables
CDK operational stacks
CloudWatch bounded observability
```

The deployment is a modular monolith. A business boundary is not automatically a Lambda, table, queue, or CDK stack boundary.

## 4. Technical module map

| Implementation module | Business context(s) hosted | Boundary decision | Initial deployment |
|---|---|---|---|
| `Platform` | no merchant business context | Technical readiness/configuration only; never a generic shared business model | `commerce-api` composition |
| `Tenancy` | Tenant Management; Merchant Access | One implementation module with distinct model areas, aggregates, namespaces, use cases, and fact ownership | `commerce-api` |
| `Catalog` | Catalog | Separate module and persistence owner | `commerce-api` |
| `Audit` | Audit | Separate supporting module when its product coverage is approved | worker/API in the shared deployment unless later justified |
| `Sales` | Sales & Order Management | Separate module when introduced | shared commerce runtime initially |
| `Inventory` | Inventory | Separate module; conditional-write/transaction boundary remains local | shared commerce runtime initially |
| `Payments` | CommerceOS Payments | Separate module; provider-private state never enters it directly | shared runtime initially; provider integration adapter isolated |
| `Procurement` | Procurement | Separate module | shared commerce runtime initially |
| `Accounting` | Accounting | Separate module and persistence owner | asynchronous worker when approved posting consumers exist |
| `Reporting` | Reporting | Separate owner of rebuildable projections | asynchronous worker when a projection exists |
| `ProductDataIngestion` | Product Data Ingestion | Separate failure/concurrency profile | crawler dispatcher/worker deployment when introduced |
| `Customer`, `Pricing`, `Notification`, `FilesMedia` | matching domain contexts | Separate modules only when a Ready task introduces them | shared or worker runtime according to actual behavior |
| Mock Payment Provider | external-like provider context | Separate application, endpoint, persistence, credentials, and failure profile | independently deployed stack when introduced |

`Tenancy` groups two business contexts for an implementation reason: the approved onboarding outcome must atomically establish an Active Tenant and its initial Active Owner Membership. Grouping them does not merge `Tenant` and `Membership` into one aggregate and does not make authentication a Tenancy-owned credential system.

No empty future module/project is created in advance.

## 5. Project and dependency rules

The dependency direction remains:

```text
Domain
  ↑
Application  ← producer-owned Contracts
  ↑
Infrastructure
  ↑
API / queue handler / worker composition
```

### Domain

- Contains aggregates, value objects, domain policies, and internal domain facts.
- Uses only the .NET base class library.
- Has no package/project reference and no AWS, ASP.NET Core, HTTP, DynamoDB, queue, or serialization type.
- Never references another module's Domain or Contracts project.

### Application

- Owns use cases, command/query handlers, authorization requirements, transaction intent, and ports.
- References its own Domain and, when a real external consumer exists, its own Contracts project.
- May reference another module's Contracts project only for an explicit synchronous query/command or integration contract recorded in the interaction matrix.
- Never references another module's Domain, Application implementation, or Infrastructure project.
- Receives trusted execution context; it does not parse JWTs, HTTP headers, queue records, or DynamoDB items.

### Contracts

- A `CommerceOS.<Module>.Contracts` project is added only when delivery code or another module actually consumes the contract.
- Contains transport-neutral immutable request/result/event shapes and stable identifiers needed at the boundary.
- Has no Domain entity, repository, AWS SDK, HTTP framework, DynamoDB attribute, or implementation code.
- Producer ownership and versioning are explicit. A shared `Common` contract dumping ground is prohibited.

### Infrastructure

- Implements only its module's application ports.
- Owns persistence mapping, AWS SDK usage, external adapters, and message serialization.
- Does not expose a table model as a contract and does not read another module's table.

### Delivery and composition

- `CommerceOS.Api` maps HTTP/authentication input to application contracts, resolves trusted execution context, and composes modules.
- Worker handlers map queue/stream/provider input to the owning application use case.
- Delivery code contains no business transition, SKU rule, role mapping, stock rule, payment conclusion, or accounting rule.
- CDK composes operational resources and IAM; it is not referenced by application code.

The current architecture tests allow only the Phase 0 three-project shape. TASK-0089 must plan the test evolution for Contracts and all future Domain assemblies before a Builder adds those projects.

## 6. First-frontier application boundaries

### Tenancy

Tenancy owns application contracts for:

- the complete merchant-onboarding result;
- business-profile reads and updates;
- current tenant authority resolution;
- membership, invitation, and role-assignment operations after their product gates are resolved.

Onboarding is one synchronous application coordinator inside Tenancy. Its persistence transaction includes the Tenant, initial Owner Membership, current authority lookup record, owner guard, and the durable accepted-intent claim required by the approved admission contract. It never returns completed success for a partial owner bootstrap.

The exact admission mechanism, initial Owner binding, mandatory profile fields, functional currency, and business uniqueness claim remain `PD-002` and `PD-034`. The technical transaction does not choose them.

### Catalog

Catalog owns application contracts for:

- tenant-scoped Product create/get/list and revision-sensitive change;
- SKU claim/check after `PD-005` defines its normalization and lifecycle;
- Category and Brand management after `PD-009`;
- publication and Catalog-owned public projection after `PD-006`–`PD-010` and `PD-037`;
- explicit application of merchant-approved ingestion candidates after `PD-040`.

Catalog accepts a trusted tenant context at its application boundary. It never resolves membership by reading Tenancy storage and never accepts a request TenantId as repository scope.

Ordinary first-frontier commands and queries are synchronous in-process calls. `ProductCreated` or `ProductPublished` is not published externally until a named consumer and an accepted integration contract exist.

## 7. Trusted execution contexts

Merchant, public storefront, onboarding, background consumer, and platform-administration execution are separate paths and types. No `isAdmin` or `bypassTenant` flag turns one into another.

For protected merchant requests:

```text
Cognito access token
      │ API Gateway verifies issuer/audience/signature/time/scopes
      ▼
AuthenticatedPrincipal (identity evidence only)
      + requested tenant selector (untrusted target)
      ▼
Tenancy.ResolveTenantAuthority
      │ current Tenant + Membership resolution
      ▼
TrustedTenantContext
      │ application capability check
      ▼
tenant-scoped repository contract/key
```

The exact tenant-selection experience and transport remain product-gated by `PD-001`. Whatever selection is approved, a selected identifier is only a candidate until Merchant Access proves an Active Membership in an Active Tenant.

Details and transport/error conventions are in [First-frontier contracts and trusted context](first-frontier-contracts.md) and [ADR-004](../adr/ADR-004-trusted-tenant-authority-and-authorization-boundary.md).

## 8. Persistence and consistency

The initial persistence strategy is one DynamoDB table per implementation module, introduced only with that module. Tenancy and Catalog therefore own different physical tables; Audit owns its own table when introduced.

Every repository access pattern:

- accepts trusted tenant scope for tenant-owned data;
- includes tenant scope in the base-table key/query;
- uses `GetItem`, `Query`, a condition, or a bounded transaction for a known access pattern;
- never uses `Scan` for an application path;
- does not use an eventually consistent GSI as the sole authorization, uniqueness, or invariant check;
- records consistency, pagination, index, isolation, and cost expectations before implementation.

Critical first-frontier boundaries are:

- atomic Tenant + initial Owner onboarding and idempotent result;
- transactionally consistent current Tenant + Membership authority resolution;
- conditional Membership revision and last-owner guard;
- single-use invitation acceptance after `PD-036` defines recipient binding;
- conditional Product revision/lifecycle changes;
- atomic tenant-wide normalized-SKU claim changes after `PD-005`;
- same-tenant Category/Brand reference checks after `PD-009`.

The detailed access-pattern ledger and symbolic key grammar are in [Persistence ownership and access patterns](persistence-access-patterns.md) and [ADR-005](../adr/ADR-005-dynamodb-module-ownership-and-access-pattern-strategy.md).

## 9. Synchronous, asynchronous, and workflow rules

Use synchronous application contracts when the caller must know the owning module's accepted/rejected result before it can truthfully complete and both modules share the runtime. Use asynchronous integration when a committed source fact must survive independent consumer failure, fan out, or absorb burst/backpressure.

Rules:

- First-frontier Tenancy and Catalog work is synchronous and does not require EventBridge, SQS, or Step Functions.
- A cross-domain fact is published only through a durable outbox after the business commit and only when a named consumer exists.
- EventBridge routes versioned business facts; it does not carry commands merely to avoid an in-process call.
- A critical or bursty consumer receives its own SQS queue and DLQ and remains idempotent.
- Direct EventBridge-to-Lambda is limited to low-risk, bounded, rebuildable projections with an explicit recovery source.
- Step Functions is not the default application layer. A workflow is selected only after its business sequence is approved and durable waiting/branching/retry/compensation is demonstrated.
- A timeout, retry exhaustion, stream lag, queue age, or DLQ placement never creates a commerce business fact.

The authoritative interaction and service matrix is [Integration and AWS service matrix](integration-and-aws.md); reliable publication is governed by [ADR-006](../adr/ADR-006-reliable-cross-domain-integration-and-deferred-workflow-orchestration.md).

## 10. Deployment and CDK boundaries

Accepted initial operational shape:

| Stack/deployment | Owns when introduced | Does not imply |
|---|---|---|
| `FoundationStack` | bounded shared technical configuration/observability | a domain, event bus by default, or business persistence |
| `IdentityStack` | Cognito merchant authentication resources | Membership, Tenant, role, or capability authority |
| `CommerceStack` | API Gateway HTTP API, one `commerce-api` Lambda, module-owned Tenancy/Catalog tables and least-privilege wiring | one domain or a permanent single-function architecture |
| `WebStack` | private S3 origins and CloudFront for static frontends when deployed | public Catalog authority or permission to copy media |
| integration/async resources | outbox relays, event bus, consumer queues/DLQs/workers when a real consumer exists | mandatory infrastructure for CRUD |
| `CrawlerStack` | bounded dispatcher/queue/workers/raw-object lifecycle when ingestion is Ready | Catalog ownership |
| `MockPaymentStack` | independently callable provider API/state/callback delivery when payment is Ready | CommerceOS Payments or Sales state |

The initial application region is single-region `ap-southeast-1`; CloudFront remains global. No global tables, cross-region business writes, NAT Gateway, ALB, EC2, always-on database, provisioned Lambda concurrency, or standing-cost service is approved.

One `commerce-api` Lambda initially minimizes cold packages and operational duplication while preserving code/table boundaries. Its IAM role may need access to more than one module table, so compile-time architecture tests, module-private infrastructure, tenant tests, and code review remain required. Split functions only for measured scale, security/IAM isolation, reliability, runtime, or ownership pressure.

All resources are CDK-owned under ADR-001. TASK-0088 deploys none.

## 11. Security, secrets, and abuse boundary

- Cognito authenticates merchant staff; guest/public traffic does not become merchant authority.
- Protected application use cases check a capability from current trusted context. A UI role label is never authorization.
- Repository scope is derived only from trusted context and cross-tenant targets use non-disclosing results.
- Public S3 origins are private behind CloudFront when introduced; no DynamoDB table is public.
- The initial SPA Cognito client has no client secret. SMS and paid advanced Cognito features are not enabled by default.
- Credentials, webhook secrets, source credentials, and signing material are never committed or logged. A task that first needs one must select an AWS-managed secret/configuration facility with IAM and cost analysis; no service is selected speculatively here.
- Request sizes, page sizes, idempotency-key length, correlation-id length, API throttling, worker concurrency, and retry count are bounded in the introducing task.
- CloudWatch custom metrics do not use TenantId, SubjectId, ProductId, or another unbounded value as a dimension.

## 12. Observability and operational conventions

Every ingress creates or validates a correlation identifier and returns it to the caller where safe. Request ID, idempotency/command ID, correlation ID, event ID, and causation ID remain distinct.

Structured logs use safe fields such as:

```text
timestamp, level, module, operation, outcomeCode,
correlationId, requestId, eventId, tenantId where permitted,
aggregateId where permitted, duration
```

Tokens, secrets, invitation credentials, raw personal data, external raw payloads, and stack traces in public responses are prohibited. Logs are telemetry, not Audit evidence.

Use built-in AWS metrics first. Add low-cardinality business/operational metrics and alarms only for a named risk. Non-production log retention follows the Free Tier guardrails: preview 1–3 days, dev 7 days, staging 7–14 days unless a task justifies otherwise.

## 13. Product-gated and deferred decisions

### First frontier product gates

| Gate | What architecture deliberately does not choose |
|---|---|
| `PD-001` | Membership cardinality, tenant-selection UX, and tenant-selector HTTP contract |
| `PD-002`, `PD-034` | currency/profile values, registration admission, initial Owner proof/binding, business uniqueness |
| `PD-003` | role cardinality and role-to-capability matrix |
| `PD-004`, `PD-033` | suspended support/read behavior and exact Audit coverage/readers |
| `PD-005` | SKU requiredness, normalization, mutation, and Archived claim reuse |
| `PD-006`–`PD-010`, `PD-037` | publication fields, lifecycle branches, Product addressing, Category/Brand policy, media policy, and public fields |
| `PD-036` | invitation recipient proof and duplicate/resend/disabled-member behavior |
| `PD-040` | external mapping and ImportCandidate application lifecycle |

### Later high-risk product gates

Sales/Inventory/Payment/Accounting/Procurement workflow and integration routes remain gated by `PD-011`–`PD-031`, `PD-035`, `PD-038`, `PD-039`, `PD-041`, and `PD-042` as recorded in the product-decision register. In particular, no technical diagram selects the reserve/pay/confirm sequence, payment terminal outcome, stock floor, accounting trigger, or procurement recognition fact.

### Domain decision exposed by reconciliation

**DOMAIN DECISION REQUIRED — public storefront tenant addressing.** TASK-0087 does not define the owner, lifecycle, uniqueness, or trusted resolution of a tenant storefront slug/subdomain/custom-domain mapping. Public API/CDN routes must not infer this from `TenantId` or from `PD-008`, which concerns Product addressing. The Domain Architect/Product Owner must add an explicit decision before a Storefront route task becomes Ready.

This gap does not block protected Tenancy or back-office Catalog refinement.

### Technical deferrals

| Deferred choice | Trigger before selection | Safe interim constraint |
|---|---|---|
| Public Catalog persisted projection/cache/index | approved public contract plus measured/list access pattern | query through Catalog; no second authority |
| Search service | approved search behavior and DynamoDB evidence is insufficient | no Scan and no OpenSearch by convenience |
| Platform-admin path | platform administration task plus security/audit model | no tenant bypass flag or Owner-in-every-tenant model |
| Authorization cache | measured read pressure plus explicit revocation/staleness SLA | resolve current authority on every protected request |
| Function/service/table extraction | measured runtime, IAM, scale, reliability, ownership, or deployment pressure | preserve module/table ownership inside the shared runtime |
| Step Functions state machine | approved business sequence plus demonstrated durable orchestration need and transition-cost estimate | application contracts and honest states; no speculative state machine |
| Multi-region/global tables | approved recovery/data-residency need and migration/cost ADR | single-region writes in `ap-southeast-1` |
| Public CDN invalidation policy | approved route/projection freshness contract | no cache may authorize a transaction |

## 14. Handoff to TASK-0089 and Builders

This baseline resolves technical architecture; it does not mark candidate tasks Ready.

TASK-0089 must:

- reshape TASK-0006/0007/0008 around the atomic onboarding and authority boundaries;
- keep tasks blocked by the listed product decisions at Outline/Refined;
- introduce any needed foundation task for Contracts project rules, Lambda packaging, and expanded architecture tests;
- require an access-pattern ledger in every DynamoDB task;
- require two-tenant, inactive-membership, stale-revision, idempotency-race, and transaction-failure verification where applicable;
- preserve conditional AWS provisioning rather than creating the target diagram all at once.

A Builder must not choose a product-gated value, introduce a different deployment/integration/persistence strategy without an ADR, or access another module's table.

## 15. Baseline conclusion

`TECHNICAL BASELINE READY`

The requested technical scope no longer requires a Builder to invent module, trust, persistence, integration, deployment, or AWS-service rules. Product decisions and later technical triggers remain explicit and therefore do not masquerade as implementation defaults.
