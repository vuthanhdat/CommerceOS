# CommerceOS — Architecture Rules

These rules define structural constraints that should progressively become executable architecture/static checks. ADR-012 is authoritative for runtime/infrastructure targeting.

## 1. Dependency direction

```text
Domain
  ↑
Application
  ↑
Infrastructure / API / Workers
```

Rules:

- Domain code must not depend on AWS SDKs, LocalStack packages, ASP.NET Core, Lambda runtime types, DynamoDB/EventBridge/SQS models, or persistence implementations.
- Application orchestrates use cases and depends on domain abstractions/project-owned ports/contracts, not LocalStack endpoints/configuration.
- Infrastructure implements persistence, messaging, external integrations, and AWS-style adapters configured for LocalStack where appropriate.
- Delivery mechanisms translate external input into application commands/queries.
- Producer-owned `*.Contracts` exists only for real delivery/cross-module consumers and contains no Domain entity, repository, implementation, AWS/LocalStack, HTTP-framework, or persistence type.
- One module Application may reference an explicitly approved foreign Contracts project, never foreign Domain/Application implementation/Infrastructure.
- Business module, Lambda/function, table, queue, state machine, CDK stack, and LocalStack container are distinct boundaries.

## 2. Business-domain boundaries

- A domain owns its aggregate/business rules and persistence contract.
- A domain must not directly read/write another domain persistence representation.
- Cross-domain behavior requires explicit command/query/API/event/projection contracts.
- Shared code must be genuinely cross-cutting; no generic shared business model.
- The Platform foundation must not become a foreign-domain persistence gateway.
- `docs/architecture/technical-baseline.md` and accepted ADRs define current module/runtime boundaries.

## 3. Multi-tenancy

- trusted tenant context comes from authenticated membership/authorization, never request `tenantId`;
- persistence access includes tenant scope as part of the key/query contract;
- authorization and data partitioning are both required;
- cross-tenant tests are mandatory for externally reachable tenant data operations;
- identity evidence from Cognito/test adapters proves subject identity only; current Tenant/Membership/capability authority is resolved through Merchant Access under ADR-004;
- route/header/query/body/cursor/token claims may identify/select a target but never replace trusted Tenant scope;
- merchant, onboarding, public, background-worker, platform-admin/support contexts are distinct; no generic tenant-bypass flag.

## 4. Events and queues

Required integration-event envelope when applicable:

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

- consumers assume duplicate/out-of-order delivery where transport permits it;
- side-effecting consumers are idempotent;
- event contracts are versioned;
- consumers do not depend on producer persistence internals;
- EventBridge-style routing is used only for a named decoupling/fan-out problem;
- critical facts use ADR-006 reliable publication, never `state commit -> best-effort publish`;
- critical/bursty consumers use an explicit queue/DLQ capability where justified;
- redrive preserves logical source/event identity;
- Step Functions-style orchestration requires an approved business sequence and durable orchestration need.

LocalStack SQS/EventBridge/Step Functions are implementation mappings only. Emulator behavior never changes business semantics.

## 5. Accounting integrity

- Posted journals are immutable.
- Correction uses reversal + corrected posting.
- Every posted journal balances.
- Automatic posting is traceable to source transaction/event.
- A source fact cannot create duplicate logical journals.
- Accounting does not read Sales/Inventory persistence as an integration shortcut.

## 6. Inventory integrity

```text
Available = OnHand - Reserved
```

- reservation/issue/release operations are concurrency-safe;
- avoid unprotected read-then-write stock mutation;
- negative-stock behavior requires explicit business policy;
- stock movements remain auditable.

## 7. Payment/provider boundary

- CommerceOS integrates through project-owned payment/provider ports.
- Mock providers behave like external systems.
- unsafe calls use idempotency keys.
- timeout may be OutcomeUnknown; query/reconciliation precedes unsafe retry.
- callbacks/webhooks deduplicate delivery.

## 8. Product-data ingestion

- source snapshots are not canonical merchant products;
- source adapters isolate source-specific behavior;
- crawler work is asynchronous where source latency/failure makes synchronous UX unsafe;
- parser fixtures are versioned/tested;
- source-specific limits/kill switches exist before scheduled crawling.

## 9. Capability-first infrastructure rules

Define the required capability before selecting an AWS-style service mapping.

Examples:

- conditional/transactional module persistence -> DynamoDB mapping;
- retryable one-worker work -> SQS/DLQ mapping;
- business-fact fan-out -> EventBridge mapping;
- durable wait/branch/retry workflow -> Step Functions mapping;
- object storage -> S3 mapping;
- identity evidence -> Cognito mapping where supported.

Rules:

- LocalStack is the only infrastructure target under ADR-012;
- no architecture decision/task requires a real AWS account, AWS IAM/OIDC, AWS Budget, Free Tier/credit checks, cloud preview/staging, or real-cloud validation;
- endpoints, synthetic credentials, region/account placeholders, ports, resource prefixes, reset policy, and feature/edition switches are configuration concerns;
- no manually-created LocalStack resource may be a hidden application/test prerequisite;
- provision queues/events/workflows/tables only for named Ready producers/consumers/processes;
- unsupported/partial/different/edition-dependent LocalStack behavior must be documented and must not be hidden by weakening contracts;
- a real AWS account is not the fallback unless ADR-012 is explicitly superseded.

## 10. Architecture-test backlog

Prioritize mechanical checks roughly in this order:

1. Domain/Application dependency rules, including no LocalStack leakage;
2. cross-module dependency rules;
3. tenant repository/access rules;
4. event-envelope/idempotent-consumer conventions;
5. accounting invariants;
6. CDK/IaC source-of-truth checks;
7. LocalStack configuration-boundary checks;
8. bootstrap/reset reproducibility and forbidden real-AWS assumptions where mechanically detectable.

A prose rule that repeatedly fails should be promoted to executable enforcement.
