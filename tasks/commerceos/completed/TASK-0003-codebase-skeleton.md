# TASK-0003 — Establish the Phase 0 codebase skeleton

Status: Completed
Owner: Codex / human-directed
Created: 2026-08-09

## Goal

Create a buildable, testable, and architecture-guarded codebase foundation from which CommerceOS business features can be developed without first restructuring the repository.

## Business context

Phase H0 established documentation and repository guardrails but intentionally deferred the application toolchain. Phase 0 now needs concrete backend, frontend, infrastructure, testing, and local-development entry points that preserve the documented modular-serverless architecture, domain boundaries, tenant-security rules, and Free Tier constraints.

## In scope

- choose and document the concrete Phase 0 toolchain;
- create a .NET solution with a modular backend and explicit Domain/Application/Infrastructure/API dependency direction;
- create independently buildable Storefront and Back Office React/TypeScript applications;
- create an AWS CDK C# application with a minimal cost-safe foundation stack;
- provide a local API health endpoint and task-instance-aware local port convention;
- add unit, architecture, frontend, and CDK assertion tests;
- extend the repository harness to run restore/install, format/lint, build/type-check, tests, and CDK synth;
- add CI for the concrete application toolchain;
- document setup, structure, verification, and environment prerequisites.

## Out of scope

- Tenant/Cognito business implementation;
- product/catalog business behavior;
- DynamoDB tables or access patterns;
- API Gateway, Lambda packaging, EventBridge, SQS, Step Functions, or storefront hosting resources;
- AWS account bootstrap, Budget creation, OIDC roles, deployment workflows, or any AWS deployment;
- real merchant/customer data and real payment processing.

## Acceptance criteria

### AC01 — Reproducible local toolchain

Given a clean checkout with the documented .NET, Node.js, npm, and Python prerequisites
when the repository verification command runs
then dependency restore/install, formatting/linting, builds, tests, and CDK synthesis complete from repository-owned configuration.

### AC02 — Enforced backend boundaries

Given a business module
when its projects are inspected and architecture tests run
then Domain has no framework/AWS dependencies, Application depends only on Domain, Infrastructure implements outward concerns, and delivery/composition depends inward without cross-domain persistence coupling.

### AC03 — Feature-ready web applications

Given future Storefront or Back Office work
when a developer opens the matching workspace application
then it has an independent React/TypeScript build, lint, and test entry point without coupling the two applications.

### AC04 — Cost-safe IaC skeleton

Given the CDK application is synthesized for a non-production environment
when its template is inspected
then it contains only the agreed minimal foundation resource(s), applies standard cost-attribution tags, uses bounded log retention/removal behavior, and introduces no always-on/base-cost service.

### AC05 — Single verification entry point

Given local development or CI
when `python3 scripts/harness_check.py` runs
then repository/document checks and all application-specific checks are orchestrated through that command and failures are reported with actionable command context.

### AC06 — No business behavior invented

Given this is a Phase 0 skeleton
when the delivered code is reviewed
then it includes only health/bootstrap examples and structural seams, not speculative Tenant, Catalog, Inventory, Payment, or Accounting behavior.

## Architecture impact

- Owning domain: Engineering/Platform
- Domains touched: establishes shared structure for future domains; no business-domain behavior
- Persistence impact: none
- Events/contracts impact: none
- AWS/IaC impact: selects C# for AWS CDK and adds a minimal Foundation stack with bounded CloudWatch log retention
- ADR required? Yes — ADR-002 records the concrete toolchain and repository structure.

## Security and tenant impact

- Authentication: N/A — Cognito integration remains Phase 1.
- Authorization: N/A — no protected business operation is added.
- Tenant scoping: architecture seam reserves trusted request context; no tenant-owned data operation exists yet.
- Sensitive data/secrets: no secrets or credentials are stored; local settings are ignored.
- Abuse/rate-limit considerations: health endpoint only; throttling belongs to the future API Gateway task.

## Reliability and idempotency impact

- Retry behavior: N/A — no distributed side effect.
- Timeout semantics: N/A — no external operation.
- Duplicate-delivery behavior: N/A — no consumer.
- Idempotency key/strategy: N/A — no retryable business command.
- DLQ/recovery/reconciliation: N/A — no asynchronous boundary.

## Observability impact

- Logs: local/API logging seam plus a 7-day non-production CDK log group.
- Metrics: built-in AWS metrics only; no custom metrics.
- Traces/correlation: request correlation middleware/seam may be added only if it remains behavior-neutral.
- Operational states/errors: health endpoint provides a deterministic local smoke target.

## Cost impact

- Request/compute impact: local/CI only; no deployed compute.
- Storage impact: source/dependency artifacts only; generated artifacts are ignored.
- Network impact: package restoration only.
- New AWS resources/services: one optional CloudWatch Log Group in the skeleton stack; CloudWatch is already an accepted platform service.
- Free Tier allowance relevant to this task: bounded logs fit the documented CloudWatch learning profile.
- Expected monthly cost change or `negligible` with rationale: zero until deployed; negligible at learning volume if deployed.
- Estimated one-off cloud-test/load-test cost, if any: none; no cloud test or deployment.

## Test plan

- Unit: backend bootstrap behavior and frontend rendering smoke tests.
- Integration: N/A — no persistence/external integration.
- Architecture: project/dependency and forbidden Domain dependency rules.
- Contract: N/A — no public/domain contract beyond health smoke behavior.
- IaC: CDK assertion tests plus `cdk synth`.
- E2E/manual: local health endpoint smoke documented; automated process launch is optional.
- **Cloud verification required?** No — the task does not change IAM, managed-service integration semantics, or deploy resources.
- AWS environment/stack(s) required: none.
- Preview/staging teardown plan: N/A — no environment is created.

## Implementation notes

- Use .NET 10 LTS because it and the managed AWS Lambda runtime are supported through November 2028; .NET 8 reaches end of support in November 2026.
- Use C# for CDK so backend and IaC share one language/toolchain while retaining TypeScript for React applications.
- Keep module scaffolding deliberately small; add business-domain projects when their active task begins.

## Completion summary

### What changed

- added the .NET 10 solution, Platform module boundary, API composition root, and deterministic health endpoint;
- added independent React/TypeScript Storefront and Back Office workspaces;
- added the C# CDK Foundation stack with environment profiles, cost tags, bounded log retention, and removal policies;
- added unit, architecture, frontend, and CDK assertion tests;
- upgraded the repository harness and CI to restore, format/lint, build, test, and synth the concrete toolchain;
- documented setup, repository structure, local worktree port isolation, and ADR-002.

### Verification

- `python3 scripts/harness_check.py`: PASS
- local implementation checks: PASS — .NET build (0 warnings), 7 .NET tests, 2 frontend tests, ESLint, TypeScript/Vite production builds, and CDK synth
- local API smoke: PASS — `GET /health` returned `{"status":"ok","service":"commerceos-api"}`
- cloud verification: N/A — no IAM or managed-service integration semantics changed and no deployment occurred
- ephemeral resource teardown: N/A — no AWS resources were created

### Acceptance criteria status

- AC01: PASS — pinned toolchains and locked packages restore and verify through one command.
- AC02: PASS — executable rules enforce Domain and Application dependency constraints.
- AC03: PASS — both web applications lint, build, and test independently.
- AC04: PASS — CDK assertions verify bounded logs and cost tags; synth contains no always-on service.
- AC05: PASS — local and CI use `scripts/harness_check.py`.
- AC06: PASS — only platform readiness/bootstrap behavior was added.

### Architecture/security/cost notes

- Architecture: modular monolith boundaries are executable; C# is selected for CDK in ADR-002.
- Security/tenant: no tenant-owned data or client-supplied tenant context exists; Phase 1 must introduce trusted tenant context and cross-tenant tests.
- Cost: zero until deployment; the only skeleton resource is a bounded CloudWatch Log Group with negligible learning-profile cost.

### Harness improvement

- The H0 document-only check now orchestrates application restore, formatting, builds, tests, architecture rules, frontend verification, and CDK synthesis.
- Domain/Application project-reference rules are mechanically enforced for future modules.

### Follow-up tasks

- Phase 1 Tenant & merchant identity, Cognito, trusted tenant context, authorization, and tenant-isolation integration tests.
- AWS account bootstrap, Budget alerts, GitHub OIDC roles, and deploy/destroy verification remain separate cloud-foundation tasks.
