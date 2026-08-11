# CommerceOS — AWS Free Tier & Credit Guardrails

_Status: Superseded on 2026-08-11 by ADR-012._

CommerceOS no longer uses a real AWS account for development, staging, validation, or deployment exercises.

Accordingly, the previous AWS Free Tier / credit-budget architecture constraint is no longer active. The following are **not current requirements**:

- AWS Free Tier eligibility;
- AWS Budget alarms or credit-balance monitoring;
- AWS Cost Explorer evidence;
- cloud-spend approval gates;
- cost-bounded AWS preview/dev/staging environments;
- account-level cost tags/cleanup as an acceptance gate;
- avoiding a service solely because it might incur AWS account charges.

The project now uses LocalStack as the only infrastructure/runtime target under `docs/adr/ADR-012-localstack-only-infrastructure-runtime.md`.

## What remains valid

The architectural preference for simple, serverless, demand-driven capabilities remains useful as a design principle, but it is no longer an AWS billing constraint.

Do not introduce infrastructure complexity without a named problem. In particular, avoid speculative always-on components, unnecessary brokers/databases, or service proliferation merely because LocalStack can emulate them.

Technical Architecture should still ask:

1. What capability is required?
2. Is an existing project capability sufficient?
3. Does the new service materially increase conceptual/operational complexity?
4. Is LocalStack support adequate for the learning scenario?
5. Are there edition/licensing or local-resource constraints that affect reproducibility?

## Current operational-cost concerns

Only local/tooling concerns remain relevant:

- LocalStack edition/licensing requirements;
- developer machine CPU/memory/disk usage;
- CI runtime/resource limits;
- large test fixtures or persistent local volumes;
- unnecessary high-volume loops that make tests slow or unstable.

These concerns may shape harness/tooling choices but must not become business/domain semantics.

## Historical note

Earlier sections of repository history may mention approximately USD 100 AWS credits, Budget thresholds, Free Tier allowances, AWS dev/preview/staging profiles, or real-cloud validation. Those assumptions are obsolete unless a later accepted ADR explicitly supersedes ADR-012.
