# TASK-0175 — Switch the coding execution profile from Luna to Terra

Status: Backlog
Specification maturity: Ready
Execution permission: YES
Owner: Builder — Engineering / Harness
Recommended implementation model: gpt-5.6-terra, medium reasoning, standard service tier
Created: 2026-08-12
Depends on: TASK-0174
Cloud verification: No

## Goal

Make all future Builder, routine Reviewer, Verification, and Conflict Resolver executions use
`gpt-5.6-terra` while preserving Sol for planning roles, medium reasoning, and Standard service.

## Business context

The human explicitly selected Terra for coding work. Official OpenAI documentation identifies
Terra as the GPT-5.6 tier balancing intelligence and cost and confirms support for medium
reasoning and the required Codex tools.

## Planning readiness

- Owning domain: Engineering / Harness.
- Runtime contract: change only the coding execution profile and directly related policy,
  guardrails, tests, and current documentation.
- Planning profile remains `gpt-5.6-sol`; reasoning remains medium; service remains Standard.
- Historical completed-task evidence remains unchanged.
- Product, tenant, persistence, infrastructure, LocalStack, security, and ADR impact: N/A.
- Remaining planning blockers: None.

## In scope

- Pin the coding Codex execution profile to `gpt-5.6-terra`.
- Update current role/operating-model documentation and comments from Luna-first to Terra-first.
- Update machine-checkable harness and model-profile tests.

## Out of scope

- Changing the Sol planning profile, reasoning effort, service tier, prompts, APIs, or historical
  completed-task records.

## Acceptance criteria

### AC01 — Terra coding execution

Builder, routine Reviewer, Verification, and Conflict Resolver commands select
`gpt-5.6-terra`, medium reasoning, and Standard/non-Fast service in 100% of profile tests.

### AC02 — Planning isolation

Planning commands remain `gpt-5.6-sol` and do not select Terra in 100% of planning-profile tests.

### AC03 — Policy consistency

Current repository instructions, role contracts, Orchestrator documentation, comments, and
harness checks consistently describe and enforce Terra-first coding with zero active Luna-first
policy references.

## Architecture impact

Harness configuration only; no product module, persistence, integration, infrastructure, or ADR
change.

## Security and tenant impact

No authentication, authorization, tenant data, sensitive data, or security-boundary change.

## Reliability and idempotency impact

Execution model selection changes only; retry, timeout, state, evidence, and idempotency contracts
remain unchanged.

## Observability impact

Existing live-agent events will report `gpt-5.6-terra` for future coding-role executions.

## Local runtime/resource impact

No LocalStack, port, persistent state, or external runtime change. Model quota/availability follows
the existing fail-closed capacity handling.

## Cost impact

Terra replaces the lower-cost Luna coding tier by explicit human choice; no cloud infrastructure
cost is introduced.

## Test plan

- Unit: coding and planning command/profile assertions.
- Contract: harness enforces Terra-first policy and rejects active Luna-first policy drift.
- Repository: `python scripts/harness_check.py`.
- LocalStack/infrastructure verification: N/A.
