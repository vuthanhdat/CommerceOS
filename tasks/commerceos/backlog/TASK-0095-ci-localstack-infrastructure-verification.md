# TASK-0095 — Establish CI LocalStack infrastructure verification

Status: Backlog
Specification maturity: Ready
Execution permission: YES
Owner: Builder — Platform Engineering
Recommended model: Default implementation model
Created: 2026-08-10
Reconciled: 2026-08-12
Ready: 2026-08-12
Roadmap phase: Phase 0
Depends on: completed TASK-0094
Infrastructure verification: Required — LocalStack only

## Goal

Extend the proven TASK-0094 LocalStack lifecycle into GitHub Actions so infrastructure-sensitive changes can be validated reproducibly without AWS OIDC, a real AWS account, or hidden developer-machine state, while changes that do not need infrastructure retain a fast mechanical path.

## Business context

CommerceOS needs trustworthy infrastructure feedback before business modules begin using AWS-style persistence, messaging, identity, workflow, and delivery capabilities. This task adds CI verification capacity only; it introduces no business behavior, Tenant data, domain authority, or cross-domain contract.

CI must exercise the same repository-owned LocalStack lifecycle used by developers. A second CI-only bootstrap path would allow local and CI behavior to drift and would weaken the foundation established by TASK-0094.

## Planning readiness

- Owning area: Platform Engineering / CI and infrastructure harness.
- Domain/business semantics: N/A; no aggregate, entity, value object, state transition, or cross-domain fact is changed.
- Module/layer ownership: GitHub Actions and repository infrastructure tooling; Domain/Application boundaries remain unchanged.
- Sync/async and transaction/consistency decisions: N/A; this task verifies infrastructure lifecycle behavior only.
- Persistence ownership/access patterns: N/A; no business persistence is added.
- Infrastructure capability/mapping: GitHub Actions executes the TASK-0094 repository lifecycle against LocalStack using CDK/CloudFormation-compatible deployment and CloudWatch Logs smoke inspection.
- Runtime authority: ADR-012; LocalStack is the only target and AWS CDK remains the repository IaC source of truth under ADR-001 as amended by ADR-012.
- Supported lifecycle: `config`, `lifecycle`, `inspect`, `reset`, `redeploy`, `smoke`, and `destroy` through `python tools/commerceos.py --instance <id>` as documented by TASK-0094.
- Version/edition assumption: pinned `localstack/localstack:4.8.1` Community image; no LocalStack Pro token or feature is required.
- Known limitation: verification is bounded to LocalStack Community 4.8.1 CloudFormation/CDK and CloudWatch Logs behavior and does not establish exact AWS IAM/control-plane fidelity.
- Dependency gate: TASK-0094 is Completed on authoritative `main` with lifecycle, reset/redeploy, isolation, and harness evidence.
- Human/cloud/account/cost gate: none; real-AWS execution remains prohibited.
- Remaining planning blocker: none.

## In scope

- preserve the existing repository harness as the stable CI verification entry point;
- add an isolated GitHub Actions LocalStack verification path for infrastructure-sensitive changes, using the repository-owned TASK-0094 launcher rather than duplicating lifecycle commands;
- install or make available the declared Docker, Python, Node/CDK, `cdklocal`, and AWS CLI prerequisites needed by that launcher;
- explicitly obtain the pinned TASK-0094 LocalStack image before invoking the launcher, because the launcher intentionally refuses an implicit image/version substitution;
- derive or inject a bounded CI job instance in the launcher-supported `0000`–`0099` range and keep mutable container, port, stack, and resource identities isolated from other jobs on the runner;
- execute `lifecycle -> inspect -> reset -> redeploy -> smoke` and always attempt task-owned `destroy` cleanup, including after failure where GitHub Actions permits it;
- capture launcher output plus LocalStack container status/logs and inspection evidence on failure without exposing credentials or unrelated runner state;
- add mechanical tests/static checks for workflow triggers, lifecycle reuse, diagnostics, cleanup, and the prohibition on real-AWS/OIDC configuration;
- document any CI-specific LocalStack limitation discovered during implementation;
- use conservative path/impact selection or an equivalent repository-owned classifier so pure documentation, frontend-only, and domain/unit-only changes do not start LocalStack unless their task explicitly requires it;
- keep an explicit manual-dispatch path for diagnosing or exercising the LocalStack job independently of change selection.

## Out of scope

- GitHub Actions OIDC federation to AWS;
- AWS IAM roles, accounts, Budgets, Cost Explorer, real credentials, or deployment authorization;
- real-cloud preview/dev/staging/production environments or release automation;
- GitHub repository/branch-protection administration outside version-controlled workflow files;
- adding business resources or services beyond the FoundationStack proven by TASK-0094;
- replacing or redesigning the TASK-0094 launcher;
- claiming AWS behavioral equivalence from LocalStack CI results;
- business-feature implementation.

## Acceptance criteria

### AC01 — Same lifecycle, no CI fork

Given the repository-owned TASK-0094 lifecycle,
when CI performs LocalStack infrastructure verification,
then it invokes `tools/commerceos.py` for lifecycle, inspection, reset/redeploy, smoke, and cleanup instead of reproducing those operations as a separate hidden setup implementation.

### AC02 — Deterministic infrastructure verification

Given an infrastructure-sensitive change on a clean CI runner,
when the LocalStack verification job runs,
then the pinned image is available, LocalStack becomes ready, the FoundationStack is synthesized/bootstrapped/deployed, inspection and smoke checks pass, reset/redeploy succeeds, and task-owned state is destroyed.

### AC03 — No AWS dependency

Given the version-controlled CI configuration,
when it is inspected and executed,
then it requires no AWS account, OIDC federation, IAM role, real AWS credential, Budget, cloud authorization, real-cloud endpoint, or real-cloud teardown.

### AC04 — Isolation, cleanup, and diagnostics

Given repeated or parallel CI executions,
when each job derives its launcher-supported CI instance and executes or fails,
then it does not silently share mutable container/port/stack/resource identity on the same runner, cleanup is attempted unconditionally, and retained output can distinguish workflow/tooling, LocalStack readiness, CDK deployment, smoke, and emulator-limitation failures.

### AC05 — Proportional verification

Given the version-controlled change-selection rules,
when a pure documentation, frontend-only, or domain/unit-only change has no declared infrastructure-verification requirement,
then it retains the ordinary harness path without starting LocalStack; an infrastructure-sensitive change or explicit manual dispatch runs the LocalStack verification path.

### AC06 — Emulator limitations stay visible

Given LocalStack verification output and documentation,
when CI results are reported,
then the evidence is explicitly bounded to the pinned LocalStack setup, any unsupported/partial/different behavior is recorded at the nearest reliable verification layer, and no result is represented as proof of exact AWS semantics.

### AC07 — Workflow contract is mechanically guarded

Given future edits to CI or lifecycle tooling,
when repository tests and `python3 scripts/harness_check.py` run,
then mechanical checks detect removal of the LocalStack job's repository-lifecycle invocation, unconditional cleanup/diagnostics, supported version assumption, or no-real-AWS boundary.

## Architecture impact

- Owning domain: N/A; Platform Engineering owns the CI/runtime harness.
- Domains touched: none.
- Persistence impact: none beyond disposable LocalStack control-plane state owned by the CI job.
- Events/contracts impact: none.
- Infrastructure capability / LocalStack mapping impact: automates the existing CDK/CloudFormation-compatible FoundationStack and CloudWatch Logs verification against LocalStack Community 4.8.1.
- IaC authority: CDK remains the source of truth; CI creates no hidden manual application resource.
- Configuration boundary: endpoints, synthetic credentials, region/account placeholder, CI instance, ports, resource prefixes, image, and feature switches stay in workflow/runtime configuration.
- ADR required: No; ADR-001 as amended by accepted ADR-012 already resolves the IaC and LocalStack-only runtime decisions.

## Security and tenant impact

- Authentication/authorization: N/A; the task adds no application access path.
- Tenant scoping: N/A; no Tenant-owned business data is introduced.
- Sensitive data/secrets: only synthetic LocalStack values may be injected; no real AWS credentials, OIDC token exchange, LocalStack auth token, or production secret is required or committed.
- Workflow permissions remain least privilege (`contents: read` unless a mechanically justified repository operation requires otherwise).
- Diagnostics must not dump the complete environment or unrelated runner/container state.

## Reliability and idempotency impact

- Retry behavior: the launcher owns deterministic readiness and lifecycle behavior; CI must not hide failures behind open-ended retries.
- Timeout semantics: workflow/job and launcher readiness timeouts are bounded and failures remain visible.
- Duplicate-delivery behavior: N/A; no message/event consumer is added.
- Idempotency strategy: each run starts from or resets to task-owned state and may be repeated without manual resource preparation.
- Recovery/cleanup: inspection and container logs are collected on failure where possible, followed by unconditional exact-container destruction.

## Observability impact

- Record the executed lifecycle phase and retain normal command output.
- On failure, capture LocalStack readiness/container status and logs plus `inspect` output when the emulator is reachable.
- Diagnostics must make tool installation, image availability, readiness, CDK bootstrap/deploy, inspection/smoke, reset/redeploy, and cleanup failures distinguishable.
- Workflow results and any uploaded artifacts are operational evidence only, not business authority.

## Cost impact

- No AWS monetary cost, Budget, Free Tier, credit, or Cost Explorer gate applies.
- Relevant cost is bounded GitHub Actions time plus runner CPU, memory, disk, Docker image transfer, and artifact retention.
- LocalStack runs only for selected infrastructure-sensitive changes or explicit manual dispatch, and cleanup prevents avoidable retained runner state.
- No paid LocalStack edition or auth token may become a prerequisite.

## Test plan

- workflow YAML/syntax and repository-owned contract tests;
- static assertions that the LocalStack path calls `tools/commerceos.py`, uses the pinned supported image, declares bounded timeout/least privilege, attempts diagnostics and cleanup on failure, and contains no AWS OIDC/role/account deployment configuration;
- existing launcher unit tests;
- `python3 scripts/harness_check.py`;
- CI proof of successful startup/readiness and `lifecycle -> inspect -> reset -> redeploy -> smoke -> destroy` against LocalStack Community 4.8.1;
- selection proof for one infrastructure-sensitive path, one non-infrastructure path, and manual dispatch;
- intentional failing smoke/lifecycle test or safe workflow-contract simulation proving diagnostic and cleanup steps are failure-conditioned/unconditional as designed, without merging a permanently failing workflow;
- repeated-run evidence using fresh/reset task-owned state;
- LocalStack profile/stack/services: `localstack-test`, FoundationStack, CloudFormation-compatible CDK deployment, CloudWatch Logs, plus TASK-0094 bootstrap dependencies;
- bootstrap/reset/cleanup: use the TASK-0094 launcher; always finish by destroying the exact CI-instance container;
- limitation fallback: test the project-owned workflow/launcher contract statically or in unit tests if a GitHub-hosted runner condition cannot be reproduced locally, and document the exact gap.

## Implementation notes

The Builder may choose the smallest workflow-file organization consistent with the accepted CI documentation and existing `.github/workflows/ci.yml` / `harness.yml`; workflow naming is not an architectural decision. Do not duplicate the lifecycle implementation in YAML. If implementing proportional selection through path rules, keep infrastructure definitions, lifecycle tooling/tests, dependency manifests, and the LocalStack workflow itself in the conservative infrastructure-sensitive set.

If the pinned image or TASK-0094 lifecycle cannot run on the selected GitHub-hosted runner without changing runtime capability, paid-edition assumptions, or architecture contracts, stop with `BLOCKED — PLANNING DECISION REQUIRED` and route the runtime decision to the Technical Architect.

**Current gate: READY — TASK-0094 completion evidence satisfies the dependency and no blocking LocalStack limitation remains.**
