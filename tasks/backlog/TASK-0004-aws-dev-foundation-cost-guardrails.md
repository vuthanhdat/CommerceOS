# TASK-0004 — Deploy the AWS dev foundation and cost guardrails

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 0
Milestone: Foundation
Depends on: TASK-0003

## Goal

A clean checkout can bootstrap the documented AWS prerequisites, deploy the cost-safe dev foundation, verify it, destroy it, and deploy it again without hidden Console-created application resources.

## Business context

The Phase 0 skeleton synthesizes locally but the roadmap exit criterion requires a reproducible real-AWS environment. Cost alerts and bounded defaults must exist before unattended business workloads are introduced.

## In scope

- document and validate account, region, CDK bootstrap, and local SSO/temporary-credential prerequisites;
- deploy the FoundationStack to the dev environment with standard tags, bounded log retention, and environment configuration;
- create AWS Budget/Free Tier monitoring at the documented thresholds and record deploy, smoke, destroy, and redeploy evidence;

## Out of scope

- GitHub OIDC and automated delivery workflows, which belong to TASK-0005;
- Cognito, API, DynamoDB business tables, crawler schedules, or production infrastructure;

## Acceptance criteria

### AC01 — Reproducible foundation

Given the documented AWS account prerequisites are satisfied
when the dev foundation is deployed from a clean checkout
then the deployed resource set maps to CDK and requires no hidden application resource.

### AC02 — Cost guardrails

Given the learning account is used
when the foundation deployment is inspected
then cost tags, bounded log retention, Budget notifications, and the absence of prohibited fixed-cost services are verified.

### AC03 — Reversible deployment

Given the dev foundation has been deployed
when it is destroyed and deployed again from repository-owned configuration
then both operations succeed and any retained bootstrap resource is explicitly documented.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and the selected real-AWS verification evidence and teardown result are recorded.

## Architecture impact

- Owning domain: Engineering / Platform
- Domains touched: IaC, CI prerequisites, observability, and cost governance
- Persistence impact: No business persistence; only CDK/bootstrap state and AWS billing configuration.
- Events/contracts impact: No product event or public API contract.
- AWS/IaC impact: CDK bootstrap prerequisites, FoundationStack, CloudWatch, and AWS Budgets configuration; no always-on compute.
- ADR required? No — ADR-001 and ADR-002 already govern CDK and the toolchain.

## Security and tenant impact

- Authentication: Deployment uses an authenticated local AWS SSO/profile or other temporary credentials; no long-lived key is committed.
- Authorization: Bootstrap and deployment permissions are scoped and documented; administrator shortcuts are not the normal deployment path.
- Tenant scoping: N/A — no tenant-owned runtime data exists.
- Sensitive data/secrets: No secrets, tokens, real card data, or unnecessary personal data are stored or logged.
- Abuse/rate-limit considerations: Validate inputs and bound externally reachable or potentially expensive operations.

## Reliability and idempotency impact

- Retry behavior: CDK operations may be retried only after the CloudFormation state is inspected; no blind concurrent deployment.
- Timeout semantics: A deployment timeout is treated as an unknown stack state until CloudFormation is queried.
- Duplicate-delivery behavior: N/A — no at-least-once consumer is introduced.
- Idempotency key/strategy: Repeated synth/deploy converges on the same declared infrastructure.
- DLQ/recovery/reconciliation: N/A — no asynchronous work is introduced.

## Observability impact

- Logs: Structured logs include operation, safe tenant/entity identifiers, and correlation context.
- Metrics: Use built-in metrics; add a bounded custom metric only for a meaningful operational risk.
- Traces/correlation: Preserve request/correlation identifiers across every boundary changed by this task.
- Operational states/errors: Deployment, rollback, budget-alarm, and cleanup states are diagnosable from CloudFormation and CloudWatch.

## Cost impact

- Request/compute impact: Deployment-only API calls plus negligible bounded log usage.
- Storage impact: No business persistence; only CDK/bootstrap state and AWS billing configuration.
- Network impact: No material transfer beyond normal API/cloud verification traffic.
- New AWS resources/services: CDK bootstrap prerequisites, FoundationStack, CloudWatch, and AWS Budgets configuration; no always-on compute.
- Free Tier allowance relevant to this task: Use the documented free/pay-per-use profile and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible; Budget resources are free and the skeleton has no meaningful idle workload.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for a bounded dev/preview verification; record actual resources and destroy ephemeral stacks.

## Test plan

- Unit: CDK configuration and construct assertions for tags, retention, and prohibited resources.
- Integration: Real-AWS deploy/destroy/redeploy smoke verification.
- Architecture: Enforce dependency direction, domain ownership, and trusted tenant-context rules where relevant.
- Contract: Environment configuration schema and documented bootstrap inputs.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Deploy, inspect, destroy, and redeploy the dev foundation.
- **Cloud verification required?** Yes — the acceptance criteria concern CloudFormation, account bootstrap, Budgets, IAM, and real deployed resource behavior.
- AWS environment/stack(s) required: dev FoundationStack and account-level Budget configuration
- Preview/staging teardown plan: Destroy the test deployment, then retain only the intentionally persistent dev foundation and documented account bootstrap/Budget resources.

