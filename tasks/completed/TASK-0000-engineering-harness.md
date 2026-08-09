# TASK-0000 — Establish Phase H0 Engineering Harness

Status: Completed
Owner: ChatGPT / human-directed
Created: 2026-08-09

## Goal

Establish a repository-level Harness Engineering foundation before CommerceOS begins application/AWS implementation.

## Business context

CommerceOS is intentionally developed with AI agents. The project needs a durable mechanism for preserving product intent, architecture constraints, task scope, verification, and learning from repeated failures so quality does not depend on conversational memory or ad-hoc prompting.

## In scope

- root agent constitution;
- task specification workflow/template;
- Definition of Done;
- architecture rules;
- testing strategy;
- ADR process/template;
- builder/reviewer workflow;
- harness-improvement loop;
- executable repository-level harness check;
- GitHub Actions harness CI;
- pull-request checklist;
- H0 exit checklist;
- README integration.

## Out of scope

- application solution structure;
- .NET/React implementation;
- AWS CDK application stack;
- application build/lint/unit/integration commands;
- executable architecture tests against domain assemblies;
- business feature implementation.

These are intentionally deferred to Phase 0 and later phases because the application toolchain/code does not yet exist.

## Acceptance criteria

### AC01 — Agent-readable repository

Given an AI agent enters the repository
when it reads `AGENTS.md` and linked documentation
then it can discover product context, task discipline, architecture rules, Definition of Done, ADR policy, and verification expectations.

### AC02 — Standard task contract

Given a non-trivial development task
when it is created from `tasks/TASK-TEMPLATE.md`
then goal, business context, scope, acceptance criteria, architecture/security/reliability/observability/cost impact, and test plan are explicitly represented.

### AC03 — Repository verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then required harness files, task/ADR structure, local README/AGENTS links, and H0 definition are mechanically checked.

### AC04 — CI enforcement

Given a push or pull request targeting `main`
when GitHub Actions runs
then the repository harness check is executed.

### AC05 — Review discipline

Given a pull request
when the author/reviewer uses the repository template
then acceptance criteria, tests, architecture/domain, tenant/security, distributed-system, observability, cost, and harness impact are explicitly considered.

### AC06 — Harness learning loop

Given a meaningful repeated defect or review failure
when the team performs root-cause analysis
then the repository documents how to convert the failure into reusable instruction/test/guardrail/tooling/observability improvements when justified.

## Architecture impact

- Owning domain: Engineering/Repository Harness
- Domains touched: all future domains indirectly
- Persistence impact: none
- Events/contracts impact: development-process contracts only
- AWS/IaC impact: GitHub Actions only; no AWS runtime resources
- ADR required? No — H0 defines the ADR mechanism and does not change an existing runtime architecture decision.

## Security and tenant impact

- Authentication: no runtime change
- Authorization: no runtime change
- Tenant scoping: establishes mandatory trusted-tenant-context rules for future code
- Sensitive data/secrets: explicitly prohibits committing secrets/real payment data
- Abuse/rate-limit considerations: documented for future task review

## Reliability and idempotency impact

- Retry behavior: future task template/checklist requires explicit consideration
- Timeout semantics: future distributed tasks must define them
- Duplicate-delivery behavior: async consumers are required to assume at-least-once delivery
- Idempotency key/strategy: explicitly required where side effects can repeat
- DLQ/recovery/reconciliation: required to be considered for relevant future tasks

## Observability impact

- Logs: future tasks must identify important failure-path logging
- Metrics: future tasks must consider operational-risk metrics
- Traces/correlation: event/async rules require correlation/causation context where applicable
- Operational states/errors: future distributed workflows must make ambiguous/failure states diagnosable

## Cost impact

- Request/compute impact: no AWS runtime workload
- Storage impact: repository documentation only
- Network impact: negligible GitHub Actions checkout/runtime
- New AWS resources/services: none
- Expected monthly cost change or `negligible` with rationale: negligible; GitHub CI usage only

## Test plan

- Unit: N/A — no application code
- Integration: N/A — no application code
- Architecture: repository structure rules captured for later executable tests
- Contract: task and ADR structural contracts checked by script
- IaC: N/A — deferred to Phase 0
- E2E/manual: verify repository links, task/ADR checks, CI workflow configuration

## Implementation notes

H0 deliberately stops short of pretending application-specific guardrails exist before there is application code. The single verification entry point is established now and will be expanded during Phase 0 instead of replaced by stack-specific ad-hoc commands.

## Completion summary

### What changed

- Added `AGENTS.md`.
- Added the H0 development-document suite.
- Added task and ADR templates.
- Added `scripts/harness_check.py`.
- Added GitHub Actions harness workflow.
- Added harness-aware PR template.
- Added H0 exit checklist and README navigation.

### Verification

- `python3 scripts/harness_check.py`: expected PASS from repository structure; CI is the authoritative clean-checkout execution environment.
- implementation checks: not applicable until Phase 0 application toolchain exists.

### Acceptance criteria status

- AC01: PASS
- AC02: PASS
- AC03: PASS by implementation; CI execution validates clean-checkout behavior
- AC04: PASS by workflow configuration
- AC05: PASS
- AC06: PASS

### Architecture/security/cost notes

No AWS runtime resource or product behavior changed. H0 establishes guardrails that future phases must convert into executable architecture/security tests as code appears.

### Harness improvement

This task is itself the initial harness implementation. The first business vertical slice will be used as the first real harness evaluation and should feed improvements back into H0 artifacts.

### Follow-up tasks

- Phase 0: create concrete application/repository toolchain and wire build/lint/test/CDK checks into `scripts/harness_check.py`.
- Phase 0/1: create first executable architecture and tenant-isolation tests once relevant modules exist.
