# TASK-0095 — Establish CI LocalStack infrastructure verification

Status: Backlog
Specification maturity: Refined
Execution permission: NO
Owner: Builder — Platform Engineering
Recommended model: Default implementation model
Created: 2026-08-10
Reconciled: 2026-08-11
Roadmap phase: Phase 0
Depends on: TASK-0094
Infrastructure verification: Required — LocalStack only

## Goal

Extend the proven TASK-0094 LocalStack lifecycle into CI so infrastructure-sensitive changes can be validated reproducibly without AWS OIDC, a real AWS account, or hidden developer-machine state.

## Why this remains Refined

The architecture and intended outcome are resolved, but TASK-0094 must first establish the authoritative local start/readiness/bootstrap/deploy/reset commands and version/edition assumptions. TASK-0095 must reuse those proven commands rather than invent a second CI-only lifecycle.

## In scope

- run the repository harness and existing IaC/static checks in CI;
- start LocalStack using the TASK-0094-supported version/edition and wait for deterministic readiness;
- inject only synthetic credentials, region/account placeholders, endpoint configuration, and isolated resource prefixes required by tooling;
- execute selected infrastructure bootstrap/deploy/smoke verification using the same repository-owned lifecycle as local development;
- ensure CI state is fresh or deterministically reset between runs;
- capture useful diagnostics when LocalStack/bootstrap/deploy/smoke fails;
- document and mechanically encode any CI-only LocalStack limitations that materially affect verification;
- keep docs-only/pure-domain changes on the cheapest mechanical path when infrastructure verification adds no value.

## Out of scope

- GitHub Actions OIDC federation to AWS;
- AWS IAM roles, accounts, Budgets, Cost Explorer or deployment credentials;
- real-cloud preview/dev/staging/prod environments;
- production release automation;
- claiming AWS behavioral equivalence from LocalStack CI results;
- business-feature implementation.

## Ready gates

TASK-0095 may become Ready when:

1. TASK-0094 is Completed on authoritative `main`;
2. its completion record names the supported LocalStack lifecycle commands and assumptions;
3. no unresolved LocalStack limitation prevents the CI scenarios required here.

## Acceptance criteria once Ready

### AC01 — Same lifecycle, no CI fork

CI uses the repository-owned LocalStack lifecycle proven by TASK-0094 rather than a separate hidden setup path.

### AC02 — Deterministic infrastructure verification

For an infrastructure-sensitive change, CI can start LocalStack, verify readiness, deploy/bootstrap the selected stack/resources, run smoke checks, and clean/reset state deterministically.

### AC03 — No AWS dependency

The workflow requires no AWS account, OIDC federation, IAM role, real AWS credential, Budget, cloud authorization, or real-cloud teardown.

### AC04 — Isolation and repeatability

Parallel or repeated CI runs do not silently share mutable resource names/state. Failed runs leave enough diagnostics to classify environment, IaC, application, or emulator-limit failures.

### AC05 — Proportional verification

Pure documentation/domain/unit-only changes are not forced through expensive LocalStack lifecycle work unless their task explicitly requires it. Infrastructure-sensitive tasks can require the LocalStack job as a merge gate.

### AC06 — Emulator limitations stay visible

Known unsupported/different behavior is referenced from the architecture limitation register and is not presented as proof of real AWS semantics.

## Architecture/security/resource constraints

- ADR-012 is authoritative.
- Domain/Application code remains free from LocalStack-specific dependencies.
- Synthetic credentials are test configuration only and must never be confused with real secrets.
- CI must not require internet access to AWS control-plane APIs.
- Resource usage should be bounded so LocalStack jobs do not create avoidable CI instability.

## Test plan once Ready

- workflow syntax/static validation;
- repository harness;
- successful LocalStack startup/readiness;
- deploy/bootstrap/smoke using TASK-0094 lifecycle;
- repeated-run/reset proof;
- intentional failure case confirming diagnostics/cleanup behavior;
- verify no AWS OIDC/account/credential configuration is required.

**Current gate: REFINED — blocked only by completion evidence from TASK-0094.**
