# TASK-0093 — Reconcile architecture-test contract rules

Status: Completed
Specification maturity: Completed
Execution permission: NO — completed
Owner: Builder — Engineering / Harness
Created: 2026-08-10
Completed: 2026-08-11
Depends on: completed TASK-0089
Cloud verification: No

## Goal

Align the executable architecture-test guardrails with the accepted producer-owned `*.Contracts` dependency rule before business modules collaborate across boundaries.

## Delivered

- Application project-reference checks allow the module's own Domain project and approved producer-owned `*.Contracts` projects.
- Foreign Domain, Application implementation, and Infrastructure references remain rejected with actionable diagnostics.
- Contracts project checks reject project references and framework, AWS, or persistence package dependencies.
- The zero-Contracts Phase 0 skeleton remains valid.
- Regression fixtures cover legal Application + Contracts references, forbidden foreign implementations, and forbidden Contracts dependencies.

## Verification evidence

- Implementation commit `b7ccb40` is present on authoritative `main`.
- `dotnet test tests/CommerceOS.ArchitectureTests/CommerceOS.ArchitectureTests.csproj`: PASS, 9 tests.
- Full .NET build and tests: PASS.
- Orchestrator tests: PASS, 42 tests.
- `python scripts/harness_check.py`: PASS.
- No AWS resources or business modules were introduced; cloud verification is not applicable.

## Completion summary

TASK-0093 is accepted as Completed. The architecture-test harness now matches the accepted producer-owned Contracts boundary while preserving strict module implementation and persistence boundaries. Canonical lifecycle and spec-path bookkeeping are updated for this completion record.

## Out-of-scope follow-up

Future business modules may add producer-owned Contracts projects only when a real consumer exists and the corresponding task is Ready.
