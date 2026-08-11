# CommerceOS — Serverless Architecture

_Technical architecture entry point reconciled to ADR-012 LocalStack-only runtime on 2026-08-11._

## 1. Authority

CommerceOS is a multi-tenant modular serverless SaaS learning project.

Implementation-useful authority:

- [Technical architecture baseline](architecture/technical-baseline.md)
- [LocalStack runtime and lifecycle](architecture/localstack-runtime-and-lifecycle.md)
- [Product-decision technical reconciliation](architecture/product-decision-technical-reconciliation.md)
- [First-frontier contracts and trusted context](architecture/first-frontier-contracts.md)
- [Persistence ownership and access patterns](architecture/persistence-access-patterns.md)
- [Integration and AWS-style service matrix](architecture/integration-and-aws.md)
- [Subscription & Billing technical extension](architecture/subscription-billing-technical-extension.md)

Accepted ADRs include ADR-001 through ADR-012. ADR-012 is authoritative for the runtime target and supersedes prior real-AWS account/deployment/validation assumptions.

Business/domain baseline and product decisions remain authoritative for business meaning.

## 2. Core principles

1. Business ownership/contracts precede infrastructure/service selection.
2. Start as a modular monolith, not a deployed microservice per domain.
3. Module, function, table, queue, workflow, stack, and emulator container are different boundaries.
4. Identity evidence is not Tenant authority; Merchant Access resolves current Tenant context.
5. Tenant authorization and tenant-partitioned persistence are both mandatory.
6. Suspended read-only authority is distinct from Active mutation authority.
7. Subscription EntitlementSet is separate commercial authority from Tenant/Membership.
8. Keep immediate owner decisions synchronous.
9. Use durable work queues for one-worker retry/backpressure and reliable facts for independent consumers.
10. Use durable workflows only for named processes with real orchestration pressure.
11. Commands/producers/relays/workflows/consumers are idempotent at their own boundaries.
12. Domain code has no AWS SDK, LocalStack, framework, persistence, or provider dependency.
13. AWS CDK remains infrastructure source of truth.
14. **LocalStack is the only infrastructure/runtime target** under ADR-012.
15. LocalStack endpoints, synthetic credentials, region/account placeholders, ports, resource prefixes, reset policy, and feature/edition switches are configuration concerns.
16. Unsupported/different emulator behavior is documented explicitly; it never silently changes business/application contracts.

## 3. Runtime model

```text
merchant/public clients
        │
        ▼
HTTP/serverless delivery capability
  API Gateway + Lambda mapping in LocalStack where supported
        ▼
shared application runtime (`commerce-api`)
        │
        ├── Tenancy
        ├── Catalog
        ├── SubscriptionBilling
        ├── Sales
        ├── Inventory
        ├── Payments
        ├── Procurement
        ├── Accounting
        ├── Reporting
        ├── ProductDataIngestion
        └── supporting modules when Ready

module-owned persistence
  DynamoDB mapping in LocalStack when Ready

named async/workflow capabilities
  SQS / EventBridge / Step Functions mappings where supported
```

Merchant-order Mock Payment Provider and simulated SaaS Billing Provider remain separate external-like applications when introduced.

## 4. Trusted Tenant context

Protected merchant trust chain:

```text
identity evidence
  (LocalStack Cognito where supported, otherwise test identity adapter)
      + optional Tenant selector (untrusted)
      ▼
Merchant Access current validation
      ├── merchant read -> TrustedTenantReadContext
      └── merchant mutation -> TrustedTenantMutationContext
```

Identity transport can change without changing Tenant authority rules.

## 5. Persistence

DynamoDB remains the preferred persistence mapping with one table per implementation module when Ready.

Rules remain:

- trusted Tenant scope on tenant-owned keys/queries;
- no foreign-module persistence access;
- conditional writes/transactions protect local invariants;
- no application `Scan`;
- eventual indexes are not sole authority for uniqueness/invariants;
- module-owned idempotency/outbox/inbox/process records support recovery.

LocalStack DynamoDB differences are limitations to document, not reasons to change ownership/business semantics.

## 6. Integration

### Synchronous

Use producer-owned application contracts for immediate owner answers.

### Durable work

Use queue capability for one known retryable worker; preferred LocalStack mapping is SQS/DLQ.

### Reliable facts

```text
owner state + outbox
      ↓ owned change feed
idempotent relay
      ↓ fact routing
consumer-specific queue/DLQ
      ↓
inbox/source identity + owned effect
```

Preferred mapping is DynamoDB Streams -> EventBridge -> SQS/DLQ where supported.

### Durable workflow

ADR-010 keeps the order payment/allocation workflow using Step Functions-style semantics. ADR-011 keeps refund propagation as event choreography.

## 7. Local infrastructure lifecycle

```text
start LocalStack
  ↓
wait readiness
  ↓
CDK synth/deploy/bootstrap
  ↓
seed required technical data
  ↓
smoke/integration/E2E/failure checks
  ↓
collect diagnostics
  ↓
logical reset or clean reset
  ↓
redeploy when repeatability matters
```

No required application/test resource may exist only through manual LocalStack setup.

Parallel tasks/worktrees isolate ports/resource names using task-instance configuration where possible.

## 8. Verification model

Use `local-fast` for unit/architecture/contract feedback and `localstack-test` for infrastructure-sensitive behavior.

When a required LocalStack feature is unsupported, partial, behaviorally different, or edition-dependent:

1. record the exact gap;
2. preserve project-owned capability contracts;
3. test at the nearest reliable layer;
4. do not claim exact AWS equivalence;
5. do not fall back to real AWS unless ADR-012 is explicitly superseded.

## 9. What changed from the previous AWS strategy

Removed/superseded:

- real AWS DEV/preview/staging/prod target assumptions;
- AWS account/region selection as planning gates;
- IAM/OIDC deployment roles;
- AWS Budget/Free Tier/credit controls;
- cloud execution authorization;
- real-cloud validation/teardown as Definition-of-Done evidence.

Unchanged:

- domain model/business semantics;
- module boundaries;
- trusted Tenant context;
- persistence ownership/access patterns;
- synchronous/asynchronous contract rules;
- idempotency/Unknown/reconciliation semantics;
- accounting/inventory integrity;
- ADR-009 onboarding consistency;
- ADR-010 order orchestration;
- ADR-011 refund choreography.
