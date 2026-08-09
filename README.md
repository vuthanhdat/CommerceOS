# CommerceOS

CommerceOS is a learning-first, production-minded **multi-tenant serverless commerce operating platform** for small and medium merchants.

The project is intentionally broader than a single online store. A business can register as a tenant, manage products, inventory, purchasing, customers, orders, and a lightweight accounting ledger, while also exposing a customer-facing storefront. Business activity generates the operational and accounting data used by the system.

## Product direction

CommerceOS combines four views of the same business:

1. **Storefront** — customers browse products, add to cart, and place orders.
2. **Back office** — merchant staff manage catalog, orders, inventory, purchasing, customers, and operations.
3. **Accounting** — business events automatically produce journal entries and financial projections.
4. **Platform** — tenant onboarding, identity, authorization, observability, audit, data ingestion, and cost controls.

Payment is deliberately implemented through an internal **Mock Payment Provider** so that the project can simulate success, decline, timeout, duplicate webhook, delayed confirmation, retry, refund, and idempotency problems without processing real money or card data.

Product catalog seed data will come from configurable external-source adapters such as Amazon, The Gioi Di Dong, Dien May Xanh, CellphoneS, or other public catalog sources where collection is permitted. Crawled source snapshots are kept separate from the merchant's canonical catalog.

## Architecture goal

The system is designed around AWS serverless services and event-driven integration:

- Amazon CloudFront + S3
- Amazon API Gateway HTTP API
- AWS Lambda
- Amazon Cognito
- Amazon DynamoDB
- Amazon EventBridge
- Amazon SQS + DLQ
- AWS Step Functions
- Amazon CloudWatch
- AWS CDK for Infrastructure as Code

We start as a **modular serverless architecture**, not as microservices. Business domains have explicit boundaries, contracts, events, ownership, and data access rules. Deployment boundaries may be split later only when scale, team ownership, isolation, or reliability requirements justify it.

## Cost constraint

CommerceOS begins under an **AWS Free Tier / approximately USD 100 available-credit constraint**.

This is treated as an architecture constraint:

- prefer Always Free/monthly-free/pay-per-use serverless services;
- keep persistent non-production infrastructure tiny;
- use local development for normal fast feedback;
- use real AWS dev/preview only when cloud semantics need verification;
- keep staging on-demand/ephemeral during the learning phase;
- do not introduce recurring/base-cost services without explicit cost analysis and, when architectural, an ADR.

See [AWS Free Tier & credit guardrails](docs/development/13-free-tier-and-credit-guardrails.md).

## Development model — Harness Engineering

Before Phase 0 business/AWS implementation, CommerceOS establishes **Phase H0 — Engineering Harness**.

The goal is to make the repository itself provide enough context, constraints, verification, and feedback that AI agents can work reliably without depending on conversational memory.

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
Failure → harness improvement
```

Start here when developing:

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
- [Testing & cloud verification](docs/development/10-testing-and-cloud-verification.md)
- [CI/CD pipeline](docs/development/11-ci-cd-pipeline.md)
- [Infrastructure as Code](docs/development/12-infrastructure-as-code.md)
- [AWS Free Tier & credit guardrails](docs/development/13-free-tier-and-credit-guardrails.md)
- [Codex multi-agent & Git worktree operating model](docs/development/14-codex-multi-agent-and-worktrees.md)
- [Task template](tasks/TASK-TEMPLATE.md)
- [ADR template](docs/adr/ADR-000-template.md)
- [ADR-001: AWS CDK as IaC](docs/adr/ADR-001-aws-cdk-infrastructure-as-code.md)

Run the repository-level verification with:

```bash
python3 scripts/harness_check.py
```

This command is intentionally lightweight during H0 and will become the single entry point for build, lint, unit, integration, architecture, IaC, and security checks as the implementation grows.

## Phase 0 codebase

The concrete foundation uses:

- .NET 10 LTS for the API, modules, tests, and AWS CDK application;
- React 19 + TypeScript + Vite for Storefront and Back Office;
- Node.js 24 and npm workspaces for frontend/CDK tooling;
- Python 3.12+ for the repository harness and local launcher.

Install those prerequisites, then run the one repository verification command:

```bash
python3 scripts/harness_check.py
```

It restores locked dependencies, verifies formatting and linting, builds both stacks, runs unit/architecture/IaC/frontend tests, and synthesizes the cost-safe CDK skeleton without deploying AWS resources.

Repository shape:

```text
apps/
  storefront/                  public customer application
  backoffice/                  merchant employee application
src/
  CommerceOS.Api/              HTTP delivery and composition root
  Modules/<Module>/
    *.Domain/                  business rules; no framework/AWS dependencies
    *.Application/             use cases and ports
    *.Infrastructure/          persistence, messaging, and external adapters
infra/CommerceOS.Cdk/          AWS infrastructure source of truth
tests/                         unit, architecture, and CDK assertion tests
tools/commerceos.py            task-instance-aware local launcher
```

To inspect isolated local ports or run the API for task/worktree `0003`:

```bash
python3 tools/commerceos.py ports --instance 0003
python3 tools/commerceos.py api --instance 0003
```

The health endpoint is `GET /health` on the allocated API port. The skeleton deliberately contains no Tenant, Catalog, Inventory, Payment, or Accounting behavior; each is introduced by its own task and module boundary.

See [ADR-002: Phase 0 toolchain and repository structure](docs/adr/ADR-002-phase-0-toolchain-and-repository-structure.md).

## Codex operating model

CommerceOS is **Luna-first** for Codex usage.

```text
Business/domain/architecture reasoning
        ↓
stronger reasoning model
        ↓
TASK / ADR / invariants
        ↓
Luna
implementation + tests + docs + routine review
        ↓
Harness / CI
```

A stronger reasoning model is reserved for decisions where reasoning difficulty or the consequence of being wrong is high: domain design, architecture, security/tenant isolation, accounting semantics, payment/idempotency, concurrency, difficult distributed-system reasoning, and high-risk review.

Parallel coding follows:

```text
one writable task
      =
one branch
      =
one isolated Git worktree
```

The primary `main` checkout remains the integration/control checkout. The default limit is two concurrent Builder-style coding tasks, and only when their boundaries/contracts are sufficiently independent.

See [Codex multi-agent & Git worktree operating model](docs/development/14-codex-multi-agent-and-worktrees.md) for commands, naming, review isolation, local port isolation, AWS preview isolation, and cleanup.

## Development/deployment path

CommerceOS uses a hybrid workflow rather than trying to emulate all AWS services locally:

```text
LOCAL
fast logic/tests
   ↓
AWS DEV / conditional PR PREVIEW
real IAM/Lambda/API/SQS/EventBridge/Step Functions semantics
   ↓
ON-DEMAND STAGING
production-like E2E/failure verification
   ↓
PROD later
```

All AWS application infrastructure is deployed from version-controlled **AWS CDK**. Manual AWS Console changes are not the infrastructure source of truth.

## Product & architecture documentation

- [Product definition & functional scope](docs/00-product-definition.md)
- [Non-functional requirements](docs/01-non-functional-requirements.md)
- [Business domains](docs/02-business-domains.md)
- [Serverless architecture](docs/03-serverless-architecture.md)
- [Monthly cost model](docs/04-cost-model.md)
- [Product-data ingestion & crawling](docs/05-product-data-ingestion.md)
- [Mock payment provider](docs/06-mock-payment-provider.md)
- [Delivery roadmap](docs/07-delivery-roadmap.md)
- [Implementation task backlog](tasks/BACKLOG.md)

## Important scope boundaries

CommerceOS is a software architecture learning project, not certified accounting or tax software.

The initial accounting module will implement bookkeeping concepts such as chart of accounts, immutable posted journals, double-entry validation, general ledger, trial balance, basic receivables/payables, and system-generated journal entries. It will **not initially implement statutory Vietnamese tax filing, legally compliant e-invoices, payroll, depreciation, or jurisdiction-specific accounting certification**.

Likewise, no real payment processor is used in the initial system.

## Guiding design chain

```text
Business requirement
        ↓
Domain problem
        ↓
Architecture problem
        ↓
Design pattern
        ↓
AWS service
```

The objective is to learn why an architectural mechanism exists, not merely how to configure an AWS service.
