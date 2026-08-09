# TASK-0005 — Establish OIDC CI/CD and ephemeral preview delivery

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 0
Milestone: Foundation
Depends on: TASK-0004

## Goal

Pull requests and main-branch changes can build once and use GitHub OIDC to perform only the required, cost-bounded preview or dev deployment with reliable cleanup.

## Business context

CommerceOS needs real-AWS verification for cloud-sensitive changes without long-lived keys or a full preview for every pull request.

## In scope

- create least-privilege GitHub OIDC roles and trust policies for preview/dev delivery;
- implement CI, conditional preview, dev deployment, and leaked-preview cleanup workflows as their targets become real;
- publish immutable build artifacts, run post-deploy checks, and guarantee preview cleanup on success or failure;

## Out of scope

- staging and production promotion workflows;
- automatic AWS deployment for docs-only or purely local changes;

## Acceptance criteria

### AC01 — Keyless delivery

Given a GitHub workflow requires AWS access
when it authenticates to the preview/dev account
then OIDC temporary credentials are used and no long-lived AWS access key is stored.

### AC02 — Conditional preview

Given a pull request is classified as cloud-sensitive
when preview verification runs
then only affected bounded resources are deployed, tested, and destroyed even when tests fail.

### AC03 — Build once and deploy dev

Given a commit reaches main after verification
when the dev delivery workflow runs
then the same immutable artifacts are deployed and post-deploy smoke checks gate success.

### AC04 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then repository verification passes and the selected real-AWS verification evidence and teardown result are recorded.

## Architecture impact

- Owning domain: Engineering / Platform
- Domains touched: CI/CD, IAM, all future deployable stacks
- Persistence impact: Workflow artifacts and CloudFormation state only; no business data.
- Events/contracts impact: Deployment workflow contract and artifact metadata; no domain event.
- AWS/IaC impact: IAM OIDC provider/roles plus conditional preview/dev CDK deployments.
- ADR required? No — ADR-001 and the accepted CI/CD policy already define this mechanism.

## Security and tenant impact

- Authentication: GitHub Actions authenticates through OIDC federation with repository/branch/environment trust conditions.
- Authorization: Deployment roles are environment-specific and least-privileged; production authority is absent.
- Tenant scoping: N/A for pipeline identity; deployed tests use synthetic tenant data only.
- Sensitive data/secrets: No secrets, tokens, real card data, or unnecessary personal data are stored or logged.
- Abuse/rate-limit considerations: Concurrency, environment naming, TTL/cleanup, and workflow triggers prevent leaked or duplicate previews.

## Reliability and idempotency impact

- Retry behavior: Failed deployment jobs may resume only after stack state is known; cleanup always executes.
- Timeout semantics: Timed-out workflows still run or trigger a bounded cleanup path.
- Duplicate-delivery behavior: Repeated workflow runs converge on the same task/PR environment and do not create untracked stacks.
- Idempotency key/strategy: Artifact identity is the source commit; environment names are deterministic.
- DLQ/recovery/reconciliation: A scheduled/manual cleanup workflow identifies and removes leaked ephemeral stacks by tag.

## Observability impact

- Logs: Structured logs include operation, safe tenant/entity identifiers, and correlation context.
- Metrics: Use built-in metrics; add a bounded custom metric only for a meaningful operational risk.
- Traces/correlation: Preserve request/correlation identifiers across every boundary changed by this task.
- Operational states/errors: Workflow summaries expose artifact identity, selected stacks, deployment result, verification result, and teardown result.

## Cost impact

- Request/compute impact: CI plus small conditional AWS deployments; docs/local-only PRs stay local.
- Storage impact: Workflow artifacts and CloudFormation state only; no business data.
- Network impact: No material transfer beyond normal API/cloud verification traffic.
- New AWS resources/services: IAM OIDC provider/roles plus conditional preview/dev CDK deployments.
- Free Tier allowance relevant to this task: Use the documented free/pay-per-use profile and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible for normal PRs; preview resources live for hours and use low-cost profiles.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for a bounded dev/preview verification; record actual resources and destroy ephemeral stacks.

## Test plan

- Unit: Workflow/path-classification and CDK policy checks where practical.
- Integration: OIDC assume-role denial/allow tests and one preview/dev deployment path.
- Architecture: Enforce dependency direction, domain ownership, and trusted tenant-context rules where relevant.
- Contract: Immutable artifact manifest and environment naming/tag contract.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Open a cloud-sensitive test change, deploy preview, verify, clean up, and validate main-to-dev delivery.
- **Cloud verification required?** Yes — OIDC trust, IAM permissions, deployment, and teardown cannot be proven locally.
- AWS environment/stack(s) required: GitHub OIDC roles, one ephemeral preview, and dev
- Preview/staging teardown plan: Always destroy preview stacks; retain only documented dev resources and OIDC roles.

