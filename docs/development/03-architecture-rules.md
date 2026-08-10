# CommerceOS — Architecture Rules

These rules define the structural constraints that should eventually be enforced by architecture tests/static checks.

## 1. Dependency direction

Inside each business domain, use a dependency direction similar to:

```text
Domain
  ↑
Application
  ↑
Infrastructure / API / Workers
```

Rules:

- Domain code must not depend on AWS SDKs.
- Domain code must not depend on ASP.NET Core, Lambda runtime types, DynamoDB models, EventBridge models, or HTTP clients.
- Application code orchestrates use cases and depends on domain abstractions.
- Infrastructure implements persistence, messaging, external integrations, and AWS-specific adapters.
- Delivery mechanisms (HTTP/Lambda/queue handlers) translate external input into application commands/queries.
- A producer-owned `*.Contracts` project is added only for a real delivery/cross-module consumer and contains no Domain entity, repository, implementation, AWS, HTTP-framework, or persistence type.
- One module's Application may reference an explicitly approved foreign Contracts project, never a foreign Domain, Application implementation, or Infrastructure project.
- A business module, Lambda/function, DynamoDB table, and CDK stack are different boundaries. Do not create one deployed service per bounded context by default.

## 2. Business-domain boundaries

Initial domains are documented in `docs/02-business-domains.md`.

Rules:

- A domain owns its aggregate/business rules and persistence contract.
- A domain must not directly read/write another domain's persistence representation.
- Cross-domain behavior requires an explicit contract: command/query/API/event/projection.
- Shared code must be genuinely cross-cutting; do not create a generic shared domain model that couples bounded contexts.
- The Platform foundation module must not become a generic shared business model or foreign-domain persistence gateway.
- First-frontier module/deployment rules are authoritative in `docs/architecture/technical-baseline.md` and ADR-003.

## 3. Multi-tenancy

For tenant-owned data:

- trusted tenant context comes from authenticated membership/authorization, never request-body `tenantId`;
- persistence access patterns include tenant scope as part of the key/query contract;
- authorization and data partitioning are both required;
- cross-tenant tests are mandatory for externally reachable tenant data operations.
- Cognito/token validation proves external identity only; current Tenant/Membership/capability authority is resolved through Merchant Access for every protected request under ADR-004.
- route, header, query, body, cursor, token custom claim, and aggregate identifiers may select/identify a target but never replace trusted Tenant scope.
- merchant, onboarding, public, background-worker, and platform-admin execution contexts are distinct; no generic tenant-bypass flag is allowed.

Target executable checks later:

- tenant-owned aggregate types expose TenantId/value-object policy;
- repository interfaces require tenant context;
- integration suite attempts cross-tenant reads/writes.

## 4. Events

Domain events represent meaningful business facts.

Required envelope fields when applicable:

```text
eventId
eventType
eventVersion
tenantId
aggregateId
occurredAt
correlationId
causationId
```

Rules:

- Consumers assume duplicate delivery.
- Side-effecting consumers are idempotent.
- Event contracts are versioned.
- Event handlers must not depend on producer persistence internals.
- Do not use EventBridge merely to avoid a direct in-process call when no decoupling benefit exists.
- Internal domain facts are not published automatically; a named producer-owned integration contract and consumer must exist.
- Critical facts use the ADR-006 transactional-outbox/reliable-delivery pattern. A database write followed by best-effort publication is not sufficient.
- Critical/bursty consumers use their own queue/DLQ; redrive preserves event/logical-source identity and does not create a new business fact.
- Step Functions requires an approved business sequence and a demonstrated durable orchestration need; it must not encode a pending product decision.

## 5. Accounting integrity

- Posted journal entries are immutable.
- Correction uses reversal + corrected posting.
- Every posted entry satisfies total debit == total credit.
- Automatic posting is traceable to source transaction/event.
- A source event cannot create duplicate logical journals.
- Accounting does not query Sales/Inventory persistence directly as a shortcut for missing event data.

## 6. Inventory integrity

Initial invariant:

```text
Available = OnHand - Reserved
```

Rules:

- stock reservation/issue/release operations must be concurrency-safe;
- avoid unprotected read-then-write sequences for stock mutation;
- negative stock behavior must be an explicit business decision, not an accidental outcome;
- stock movements remain auditable.

## 7. Payment boundary

- CommerceOS integrates through a payment-port abstraction.
- Mock Payment Provider behaves like an external system even though we own it.
- Payment calls use idempotency keys.
- timeout may result in unknown state; reconciliation/query is required before unsafe retry.
- webhook handlers deduplicate delivery.

## 8. Product-data ingestion

- source snapshots/raw extracts are not canonical merchant products;
- source adapters isolate source-specific parsing/API behavior;
- crawler work is asynchronous when source latency/failure makes synchronous UX unsafe;
- parser fixtures are versioned and tested;
- source-specific rate limit/concurrency and kill switch are supported before scheduled crawling.

## 9. Serverless resource rules

Preferred default:

- pay-per-use/serverless managed services;
- explicit retry/DLQ for asynchronous boundaries;
- bounded log retention;
- resource tags and cost attribution;
- least-privilege IAM;
- no public data store by default.
- provision EventBridge, SQS/DLQ, DynamoDB Streams, or Step Functions only with a named contract/consumer/workflow; target diagrams are not a reason to pre-create resources.

Adding one of the following requires explicit ADR/cost justification:

- EC2;
- NAT Gateway;
- Application Load Balancer;
- always-on relational database;
- Redis/ElastiCache;
- OpenSearch;
- Kafka/MSK;
- any new managed service with meaningful standing cost.

## 10. Architecture-test backlog

As code appears, convert these rules into tests in roughly this order:

1. domain dependency rules;
2. cross-domain dependency rules;
3. tenant repository/access rules;
4. event-envelope conventions;
5. accounting immutable/balance invariants;
6. idempotent consumer tests;
7. CDK/IaC policy checks;
8. cost-risk/resource-policy checks.

A prose rule that repeatedly fails in implementation should be prioritized for mechanical enforcement.
