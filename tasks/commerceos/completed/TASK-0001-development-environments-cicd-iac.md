# TASK-0001 — Define Development Environments, CI/CD, IaC, and Free Tier Guardrails

Status: Completed
Owner: ChatGPT / human-directed
Created: 2026-08-09

## Goal

Define how CommerceOS is developed, tested, deployed, promoted, and cost-controlled while using multiple AWS serverless services under an AWS Free Tier / approximately USD 100 credit constraint.

## Business context

CommerceOS is a learning-first serverless SaaS. Developers and AI agents need fast local feedback, but important behaviors such as IAM, SQS retry/DLQ, EventBridge routing, Step Functions execution, Cognito, and CDK deployment must be verified on real AWS.

At the same time, the project must not consume limited learning credits through unnecessary always-on infrastructure, permanent staging copies, or full cloud deployment for every code change.

The deployment process must be reproducible and reviewable from the repository, so AWS application infrastructure is managed as Infrastructure as Code rather than manual Console configuration.

## In scope

- hybrid local/AWS development environment strategy;
- local/dev/preview/staging/prod responsibilities;
- local vs cloud test matrix;
- failure-oriented cloud verification;
- PR CI pipeline;
- conditional ephemeral preview deployments;
- main-to-dev deployment policy;
- on-demand staging/release flow;
- future protected production deployment;
- build-once/promote-artifact principle;
- GitHub Actions OIDC authentication to AWS;
- AWS CDK Infrastructure as Code policy;
- environment configuration/tagging/drift rules;
- AWS Free Tier and approximately USD 100 credit guardrails;
- preferred/limited/prohibited service guidance;
- budget/usage monitoring principles;
- harness checks ensuring these documents remain present.

## Out of scope

- creating the .NET/React application solution;
- choosing the final CDK implementation language;
- bootstrapping an AWS account;
- creating GitHub OIDC deployment roles;
- implementing `ci.yml`, preview, dev, staging, or prod workflows beyond the existing H0 harness workflow;
- deploying any AWS application stack;
- production account creation;
- real customer data;
- real payment integration.

Those are Phase 0/later implementation tasks.

## Acceptance criteria

### AC01 — Environment responsibilities

Given an agent starts a CommerceOS task
when it reads the environment documentation
then it can distinguish local, dev, preview, staging, and production responsibilities and knows that not all AWS services are emulated locally.

### AC02 — Cloud source of truth

Given behavior depends on IAM or AWS managed-service semantics
when the task is verified
then selected real-AWS integration testing is required rather than declaring completion from local emulation alone.

### AC03 — Cost-aware previews

Given a pull request does not change cloud-sensitive behavior
when CI runs
then the documented default is not to create a full AWS preview environment.

Given a cloud-sensitive PR needs AWS verification
when a preview is created
then it is bounded, tagged, minimal, and destroyed after use.

### AC04 — Infrastructure as Code

Given an AWS application resource is introduced
when CommerceOS deploys it
then AWS CDK is the version-controlled source of truth and permanent manual Console-only configuration is not accepted.

### AC05 — Free Tier constraint

Given architecture or CI/CD proposes an AWS service
when its cost is reviewed
then the Free Tier/credit document identifies preferred free/pay-per-use defaults, bounded credit-funded usage, and services prohibited by default because of recurring/base cost.

### AC06 — CI/CD promotion

Given a change is merged and later promoted
when it moves through environments
then the documented flow is PR verification → DEV → on-demand STAGING → protected PROD later, with build-once/promote-artifact behavior.

### AC07 — CI AWS credentials

Given GitHub Actions requires AWS access
when deployment identity is implemented
then OIDC federation/temporary credentials are used instead of long-lived AWS access keys in repository secrets.

### AC08 — Harness enforcement

Given repository harness checks run
when the environment/IaC/Free Tier documents or key definitions are missing
then the harness fails.

## Architecture impact

- Owning domain: Engineering/Platform
- Domains touched: all future domains indirectly
- Persistence impact: none
- Events/contracts impact: development/deployment contracts only
- AWS/IaC impact: formalizes AWS CDK as source of truth; no resources deployed by this documentation task
- ADR required? Yes — `ADR-001-aws-cdk-infrastructure-as-code.md`

## Security and tenant impact

- Authentication: no runtime change
- Authorization: documents environment-separated deployment roles and least privilege
- Tenant scoping: no runtime change; cloud tests will later include tenant-isolation paths
- Sensitive data/secrets: prohibits long-lived AWS deployment keys and real production/card data in lower environments
- Abuse/rate-limit considerations: preview/load/crawler activity is bounded by cost guardrails

## Reliability and idempotency impact

- Retry behavior: real AWS verification required for SQS/Step Functions retry semantics where affected
- Timeout semantics: failure-oriented tests explicitly include ambiguous external timeouts
- Duplicate-delivery behavior: cloud tests verify SQS/EventBridge consumer assumptions where changed
- Idempotency key/strategy: test strategy requires replay/duplicate scenarios
- DLQ/recovery/reconciliation: cloud test matrix includes real DLQ/recovery checks for affected changes

## Observability impact

- Logs: short bounded non-prod retention documented
- Metrics: prefer built-in metrics before custom/high-cardinality metrics
- Traces/correlation: verified in cloud when affected
- Operational states/errors: post-deployment verification is required rather than treating `cdk deploy` success as application health

## Cost impact

- Request/compute impact: documentation only; no AWS workload created by this task
- Storage impact: repository text only
- Network impact: none
- New AWS resources/services: none
- Expected monthly cost change or `negligible` with rationale: negligible; task establishes controls intended to keep normal dev near $0–$5/month under Free Tier/credit constraints

The project currently plans against the user's stated approximately USD 100 available credit rather than assuming additional promotional credits will be earned.

## Test plan

- verify all new development documentation exists;
- verify README links to environment/testing/CI-CD/IaC/Free-Tier docs;
- verify ADR-001 contains required ADR headings;
- verify this completed task contains required task headings;
- run `python3 scripts/harness_check.py` from a clean checkout;
- confirm the check validates AWS CDK source-of-truth and Free Tier constraint markers;
- CI should run the updated harness through `.github/workflows/harness.yml`.
