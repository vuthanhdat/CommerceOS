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
- [Task template](tasks/TASK-TEMPLATE.md)
- [ADR template](docs/adr/ADR-000-template.md)

Run the repository-level verification with:

```bash
python3 scripts/harness_check.py
```

This command is intentionally lightweight during H0 and will become the single entry point for build, lint, unit, integration, architecture, IaC, and security checks as the implementation grows.

## Product & architecture documentation

- [Product definition & functional scope](docs/00-product-definition.md)
- [Non-functional requirements](docs/01-non-functional-requirements.md)
- [Business domains](docs/02-business-domains.md)
- [Serverless architecture](docs/03-serverless-architecture.md)
- [Monthly cost model](docs/04-cost-model.md)
- [Product-data ingestion & crawling](docs/05-product-data-ingestion.md)
- [Mock payment provider](docs/06-mock-payment-provider.md)
- [Delivery roadmap](docs/07-delivery-roadmap.md)

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
