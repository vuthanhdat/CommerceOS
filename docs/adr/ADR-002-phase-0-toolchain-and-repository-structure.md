# ADR-002 — Phase 0 toolchain and repository structure

Status: Accepted
Date: 2026-08-09
Decision owners: CommerceOS maintainers
Supersedes: N/A
Superseded by: N/A

## Context

CommerceOS documentation recommends C#/.NET for Lambda application code, React/TypeScript for the two web applications, and AWS CDK for infrastructure, but Phase H0 intentionally did not select concrete versions, the CDK language, or the repository layout. Phase 0 needs one reproducible toolchain that preserves modular-domain boundaries, supports AWS Lambda, remains maintainable for several development phases, and can be verified by the repository harness.

At the decision date, .NET 10 is the current LTS release and the AWS Lambda managed `dotnet10` runtime is supported through November 2028. .NET 8 reaches end of support in November 2026. AWS CDK supports C# as a stable first-class language and requires .NET 8 or later.

## Decision

- Target .NET 10 LTS for backend application, tests, and AWS CDK projects.
- Use C# for AWS CDK v2 so application and infrastructure share the .NET toolchain.
- Use React 19 with TypeScript and Vite for two independent npm-workspace applications: Storefront and Back Office.
- Use a modular monolith source layout. Each business module owns separate Domain, Application, and Infrastructure projects as it is introduced. Delivery/composition projects sit outside domain modules.
- Start with a small Platform module solely to prove dependency direction and composition. Do not pre-create speculative business entities or persistence abstractions.
- Keep one repository-level verification entry point that orchestrates .NET, frontend, architecture, and CDK checks.
- Pin major toolchain intent in repository-owned configuration and commit package lock files for reproducibility.

## Alternatives considered

### Option A — .NET 10 and C# CDK

- Benefits: longest current LTS runway; managed Lambda runtime; one backend/IaC language; strong compile-time boundaries; aligns with documented architecture.
- Costs/risks: developers currently on .NET 8 must install .NET 10; CDK CLI still requires Node.js; C# CDK examples are less numerous than TypeScript examples.

### Option B — .NET 8 and C# CDK

- Benefits: already installed in the initial developer environment; mature Lambda ecosystem.
- Costs/risks: support ends in November 2026, creating an immediate migration task and avoidable foundation churn.

### Option C — .NET backend and TypeScript CDK

- Benefits: largest CDK example ecosystem; shared TypeScript tooling with frontend.
- Costs/risks: two infrastructure/application languages for backend developers; duplicated package conventions; less cohesive Phase 0 verification.

### Option D — TypeScript for the whole stack

- Benefits: one language across web, API, and CDK.
- Costs/risks: contradicts the documented initial backend recommendation and changes the intended C#/.NET learning path without a product-driven reason.

## Consequences

### Positive

- The foundation has support runway through November 2028.
- Backend and infrastructure conventions can share analyzers, tests, and build tooling.
- Domain projects remain free from ASP.NET Core, Lambda, AWS SDK, and persistence dependencies.
- Storefront and Back Office can evolve independently while sharing only intentional packages later.
- Future feature tasks gain a repeatable module template and mechanical architecture checks.

### Negative / trade-offs

- .NET 10 becomes a required local/CI prerequisite.
- Node.js remains required for both Vite and the CDK CLI.
- A modular monolith still requires discipline and executable dependency checks to avoid boundary erosion.
- Lambda packaging/deployment is intentionally deferred, so CDK synthesis does not yet prove runtime integration.

## Security and tenant impact

- Tenant isolation: no tenant-owned behavior is added; future repositories must accept trusted tenant context and cross-tenant tests remain mandatory.
- Authentication/authorization: Cognito and membership authorization remain Phase 1.
- Sensitive data/secrets: repository configuration contains no credentials; local secrets/settings stay ignored.

## Reliability and operability impact

- Failure modes: local toolchain/version mismatch and dependency drift are surfaced by pinned configuration and the harness.
- Retry/recovery: N/A for the skeleton; no asynchronous effects exist.
- Observability: a bounded non-production log group proves IaC policy without introducing a workload.
- Operational burden: developers maintain both .NET and Node.js toolchains, already required by the documented product architecture.

## Cost impact

- Learning profile: no AWS cost until deployment; the optional skeleton log group is negligible at learning volume and has seven-day retention.
- Beta profile: no material effect; workload resources will be introduced by later tasks with their own cost analysis.
- Larger-scale implication: neutral; this ADR chooses toolchains and boundaries, not capacity or paid services.
- Cost-model update required? No — no new service category or standing-cost architecture is introduced.

## Reversibility / migration

Frontend framework or CDK language changes would require rebuilding their respective workspaces but would not alter business-domain contracts. Changing the backend language/runtime after domain code exists would be expensive. .NET major upgrades should normally be incremental because project boundaries and tests remain stable.

## Validation

- A clean checkout restores, formats, builds, and tests all .NET projects.
- Both web applications lint, type-check/build, and test independently.
- Architecture tests reject forbidden Domain dependencies and invalid project references.
- CDK assertion tests and `cdk synth` pass without AWS credentials.
- `python3 scripts/harness_check.py` orchestrates all checks locally and in CI.

## References

- relevant task: `../../tasks/active/TASK-0003-codebase-skeleton.md`
- architecture docs: `../03-serverless-architecture.md`, `../development/03-architecture-rules.md`, `../development/12-infrastructure-as-code.md`
- external references: AWS Lambda .NET runtimes, Microsoft .NET support policy, AWS CDK C# guide
