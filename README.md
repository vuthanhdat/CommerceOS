# CommerceOS

CommerceOS is a learning-first, production-minded **multi-tenant serverless commerce operating platform** for small and medium merchants.

The project is broader than a single online store. A business can register as a tenant, manage products, inventory, purchasing, customers, orders, and a lightweight accounting ledger, while also exposing a customer-facing storefront. Business activity generates the operational and accounting data used by the system.

## Product direction

CommerceOS combines four views of the same business:

1. **Storefront** — customers browse products, add to cart, and place orders.
2. **Back office** — merchant staff manage catalog, orders, inventory, purchasing, customers, and operations.
3. **Accounting** — business events automatically produce journal entries and financial projections.
4. **Platform** — tenant onboarding, identity, authorization, observability, audit, data ingestion, and runtime/infrastructure concerns.

Payment is deliberately implemented through an internal **Mock Payment Provider** so the project can simulate success, decline, timeout, duplicate callback, delayed confirmation, retry, refund, and idempotency problems without processing real money or card data.

Product catalog seed data comes from configurable external-source adapters where collection is permitted. Crawled source snapshots remain separate from the merchant canonical catalog.

## Architecture goal

CommerceOS learns AWS-style serverless architecture while running infrastructure locally through **LocalStack**.

Current preferred capability mappings include:

- API Gateway + Lambda for HTTP/serverless delivery where supported;
- Cognito for identity-edge experiments where sufficiently supported;
- DynamoDB for module-owned persistence;
- EventBridge for named business-fact routing;
- SQS + DLQ for retryable asynchronous work;
- Step Functions for ADR-approved durable workflows;
- S3 for object storage;
- CloudWatch-style APIs for local observability where useful;
- AWS CDK as Infrastructure as Code.

These service names are learning/runtime mappings, not a requirement to deploy to AWS.

The system starts as a **modular serverless monolith**, not microservices. Business domains have explicit boundaries, contracts, events, ownership, and data-access rules. Deployment boundaries may be split later only when measured scale, team ownership, isolation, or reliability needs justify it.

## Infrastructure strategy — LocalStack only

ADR-012 establishes the current runtime decision:

- CommerceOS does **not** use a real AWS account for development, staging, validation, or deployment;
- LocalStack is the default and only infrastructure target;
- no task requires AWS account provisioning, IAM/OIDC federation, AWS Budget/Free Tier controls, cloud execution authorization, real-cloud preview/staging, or AWS teardown evidence;
- LocalStack endpoints, synthetic credentials, region/account placeholders, ports, task-instance prefixes, reset policy, and feature/edition switches are configuration concerns;
- Domain/Application code must not depend on LocalStack-specific implementation details;
- unsupported or behaviorally different LocalStack features are documented as limitations rather than silently treated as AWS-compatible.

See:

- [ADR-012 — LocalStack-only infrastructure runtime](docs/adr/ADR-012-localstack-only-infrastructure-runtime.md)
- [LocalStack runtime and lifecycle](docs/architecture/localstack-runtime-and-lifecycle.md)
- [Development environment strategy](docs/development/09-development-environment.md)

## Development model — Harness Engineering

Before feature implementation, CommerceOS uses a repository-centered Harness Engineering workflow:

```text
Human intent
    ↓
Task specification
    ↓
Agent implementation
    ↓
Mechanical verification
    ↓
Review
    ↓
Human product validation
    ↓
Failure -> harness improvement
```

Start here:

- [Agent constitution](AGENTS.md)
- [H0 engineering harness](docs/development/00-engineering-harness.md)
- [Task specification process](docs/development/01-task-specification.md)
- [Definition of Done](docs/development/02-definition-of-done.md)
- [Architecture rules](docs/development/03-architecture-rules.md)
- [Testing strategy](docs/development/04-testing-strategy.md)
- [ADR process](docs/development/05-adr-process.md)
- [Agent workflow](docs/development/06-agent-workflow.md)
- [Harness improvement loop](docs/development/07-harness-improvement.md)
- [H0 exit checklist](docs/development/08-h0-exit-checklist.md)
- [Development environment strategy](docs/development/09-development-environment.md)
- [Testing & infrastructure verification](docs/development/10-testing-and-cloud-verification.md)
- [CI pipeline](docs/development/11-ci-cd-pipeline.md)
- [Infrastructure as Code](docs/development/12-infrastructure-as-code.md)
- [Historical AWS Free Tier guardrails — superseded](docs/development/13-free-tier-and-credit-guardrails.md)
- [Codex multi-agent & worktree model](docs/development/14-codex-multi-agent-and-worktrees.md)
- [Task template](tasks/TASK-TEMPLATE.md)
- [ADR template](docs/adr/ADR-000-template.md)

Run repository verification with:

```bash
python3 scripts/harness_check.py
```

## Phase 0 codebase

The foundation uses:

- .NET 10 LTS for API, modules, tests, and CDK application;
- React 19 + TypeScript + Vite for Storefront and Back Office;
- Node.js 24 and npm workspaces for frontend/CDK tooling;
- Python 3.12+ for repository harness/local launcher.

Repository shape:

```text
apps/
  storefront/
  backoffice/
src/
  CommerceOS.Api/
  Modules/<Module>/
    *.Domain/
    *.Application/
    *.Contracts/
    *.Infrastructure/
infra/CommerceOS.Cdk/
tests/
tools/commerceos.py
```

Domain projects remain free from AWS SDK, LocalStack, HTTP-framework, and persistence dependencies. Infrastructure projects implement project-owned ports and may use AWS SDKs configured for LocalStack.

## Runtime / testing path

```text
LOCAL-FAST
unit / architecture / contract / direct-host tests
      ↓
LOCALSTACK-DEV
persistent developer learning/exploration
      ↓
LOCALSTACK-TEST
isolated infrastructure integration/failure verification
      ↓
LOCALSTACK-STAGE
optional production-shaped local validation profile
```

There is no real AWS DEV/STAGING/PROD path under the current architecture.

A normal LocalStack lifecycle is:

```text
start
  ↓
wait ready
  ↓
CDK synth/deploy/bootstrap
  ↓
seed required technical data
  ↓
smoke/integration/E2E/failure tests
  ↓
collect diagnostics
  ↓
reset/stop/remove state as required
```

## Product & architecture documentation

- [Product definition & functional scope](docs/00-product-definition.md)
- [Non-functional requirements](docs/01-non-functional-requirements.md)
- [Business domains](docs/02-business-domains.md)
- [Serverless architecture](docs/03-serverless-architecture.md)
- [Technical architecture baseline](docs/architecture/technical-baseline.md)
- [LocalStack runtime and lifecycle](docs/architecture/localstack-runtime-and-lifecycle.md)
- [First-frontier contracts & trusted context](docs/architecture/first-frontier-contracts.md)
- [Persistence ownership & access patterns](docs/architecture/persistence-access-patterns.md)
- [Integration & AWS-style service matrix](docs/architecture/integration-and-aws.md)
- [Product-data ingestion & crawling](docs/05-product-data-ingestion.md)
- [Mock payment provider](docs/06-mock-payment-provider.md)
- [Delivery roadmap](docs/07-delivery-roadmap.md)
- [Implementation task backlog](tasks/BACKLOG.md)

## Important scope boundaries

CommerceOS is a software architecture learning project, not certified accounting or tax software.

The initial Accounting module implements bookkeeping concepts such as chart of accounts, immutable posted journals, double-entry validation, general ledger, trial balance, basic receivables/payables, and system-generated journal entries. It does not initially implement statutory Vietnamese tax filing, legally compliant e-invoices, payroll, depreciation, or jurisdiction-specific certification.

No real payment processor is used in the initial system.

## Guiding design chain

```text
Business requirement
        ↓
Domain problem
        ↓
Architecture capability
        ↓
Design pattern / contract
        ↓
LocalStack-supported AWS-style service mapping
```

The objective is to learn why an architectural mechanism exists, not merely how to configure a cloud service.
