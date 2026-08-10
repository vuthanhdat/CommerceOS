# TASK-0093 — Reconcile architecture-test contract rules

Status: Backlog
Specification maturity: Ready
Execution permission: YES
Owner: Builder — Engineering / Harness
Recommended model: Default implementation model
Created: 2026-08-10
Roadmap phase: Phase 0 remediation
Depends on: completed TASK-0089
Cloud verification: No
Exclusive write surface: `tests/CommerceOS.ArchitectureTests/**` and only directly required harness/test fixtures

## Goal

Align the executable architecture-test guardrails with the accepted producer-owned `*.Contracts` dependency rule before business modules start collaborating across boundaries.

This is a structural remediation task. It must not implement a CommerceOS business feature or create a speculative business contract merely to exercise the rule.

## Why this is Ready

The current Phase 0 architecture test requires every `*.Application` project reference to be the module's own Domain project. The approved architecture allows an Application to consume an explicitly approved foreign producer-owned `*.Contracts` project while still forbidding foreign Domain, Application implementation, Infrastructure, and persistence internals.

The required architecture meaning is already approved in the technical baseline/architecture rules. No product, domain, AWS, persistence, or cost decision is missing.

## In scope

- change architecture tests so a module Application may reference:
  - its own Domain project; and
  - an approved producer-owned `*.Contracts` project;
- continue rejecting direct references to foreign Domain, foreign Application implementation, and foreign Infrastructure projects;
- add mechanical checks appropriate to `*.Contracts` projects so they do not become a shared implementation/domain dumping ground;
- keep Contracts free of Domain entities, repositories/implementations, AWS dependencies, HTTP-framework types, and persistence representations according to accepted architecture rules;
- make tests work correctly while the repository still has zero business `*.Contracts` projects;
- use small test fixtures/helpers if needed to prove both allowed and forbidden dependency shapes without adding a business module.

## Out of scope

- creating Tenancy, Catalog, SubscriptionBilling, Sales, Inventory, Payments, or another business module;
- defining an actual cross-module business contract before its owning Ready task;
- changing the accepted dependency architecture;
- AWS/CDK/resource changes;
- persistence schemas or access patterns;
- API behavior.

## Acceptance criteria

### AC01 — Approved Application dependency shape passes

Given the accepted architecture rules
when architecture tests evaluate an Application dependency shape containing its own Domain and an approved producer-owned Contracts reference
then the shape is accepted.

### AC02 — Foreign implementation dependencies remain forbidden

Given an Application attempts to reference another module's Domain, Application implementation, or Infrastructure project
when architecture tests run
then the dependency is rejected with actionable diagnostics.

### AC03 — Contracts remain narrow

Given a `*.Contracts` project exists
when architecture tests inspect it
then forbidden Domain entity/repository implementation/AWS/HTTP-framework/persistence dependencies are rejected according to the accepted Contracts boundary.

### AC04 — Zero-contract skeleton remains valid

Given the current Phase 0 repository has no business `*.Contracts` project
when architecture tests run
then absence of a Contracts project is not itself a failure.

### AC05 — No business feature is introduced

The diff contains only architecture-test/harness support required to enforce the already-approved boundary. It does not add business module behavior, AWS resources, or a speculative published contract.

### AC06 — Verification

- `dotnet test tests/CommerceOS.ArchitectureTests/CommerceOS.ArchitectureTests.csproj` passes;
- `python3 scripts/harness_check.py` passes;
- no test is weakened into permitting arbitrary foreign project references.

## Architecture impact

- Owning area: Engineering / Harness
- Architecture source: `docs/development/03-architecture-rules.md`, `docs/architecture/technical-baseline.md`, ADR-003
- ADR required: No — this task reconciles tests to an already accepted decision.

## Security and tenant impact

No runtime authentication, authorization, Tenant, or data-isolation behavior changes. The task strengthens mechanical module-boundary enforcement that later protects those concerns.

## Reliability and idempotency impact

N/A for runtime business behavior. Tests must be deterministic and local.

## Cost impact

- New AWS resources/services: none
- Cloud verification: not required
- Expected AWS cost change: `$0`

## Test plan

- Positive architecture fixture/check for legal own-Domain + approved Contracts reference.
- Negative checks for foreign Domain/Application/Infrastructure reference.
- Negative Contracts-boundary checks.
- Current repository architecture suite.
- Full repository harness.

**Ready gate: satisfied.**
