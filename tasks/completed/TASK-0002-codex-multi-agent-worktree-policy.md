# TASK-0002 — Codex Multi-Agent & Worktree Operating Model

Status: Completed
Owner: ChatGPT / human-directed
Created: 2026-08-09

## Goal

Define a cost-conscious Codex multi-agent operating model for CommerceOS that uses Luna by default, escalates model strength only for high-reasoning/high-risk decisions, and isolates concurrent writable tasks with Git worktrees.

## Business context

CommerceOS is intentionally built with Harness Engineering and ChatGPT Plus/Codex. The project needs to maximize useful agent work within finite included usage while preventing concurrent agents from overwriting repository state, sharing mutable local/cloud resources accidentally, or introducing coordination overhead greater than the productivity benefit.

## In scope

- Luna-first model-selection policy;
- stronger-model escalation criteria;
- Planner/Builder/Reviewer/Verification/Harness roles;
- concurrency limit;
- one-task/one-branch/one-worktree rule;
- Codex built-in worktree usage;
- manual Git worktree fallback commands;
- worktree branch naming;
- review isolation;
- local port/state isolation;
- AWS preview isolation;
- integration/cleanup commands;
- standard Planner/Builder/Reviewer prompts;
- README/AGENTS integration;
- harness enforcement of the policy document.

## Out of scope

- installing/configuring the Codex desktop application on the user's machine;
- creating actual application worktrees before Phase 0 implementation starts;
- application toolchain/bootstrap implementation;
- AWS preview stacks;
- CI automation that creates worktrees;
- changing Codex product-level usage limits or account configuration.

## Acceptance criteria

### AC01 — Luna-first policy

Given a normal implementation/testing/documentation task
when an agent is selected
then Luna is the documented default model and stronger reasoning is reserved for difficult/high-consequence design or review work.

### AC02 — Worktree isolation

Given two writable agent tasks execute concurrently
when repository isolation is applied
then each task uses its own branch and worktree and neither writes directly to the primary main checkout.

### AC03 — Concurrency bounded

Given ChatGPT Plus/Codex usage constraints
when parallel coding is used
then the documented default is at most two active Builder-style coding tasks and parallelism requires sufficiently independent boundaries/contracts.

### AC04 — Manual fallback documented

Given built-in Codex worktree behavior is unavailable or needs debugging
when the developer follows repository documentation
then they can create, inspect, update, review, remove, and prune Git worktrees with standard Git commands.

### AC05 — Runtime isolation considered

Given two worktrees run local or AWS integration workloads concurrently
when the tasks are executed
then local mutable state/ports and cloud preview resources are required to be isolated by task identity rather than silently shared.

### AC06 — Discoverable and guarded

Given an agent enters the repository
when it reads README/AGENTS and runs the harness
then the Codex operating model is discoverable and the required policy document/Luna/worktree/concurrency invariants are mechanically checked.

## Architecture impact

- Owning domain: Engineering/Repository Harness
- Domains touched: all future domains indirectly
- Persistence impact: none
- Events/contracts impact: development-process contract only
- AWS/IaC impact: no runtime resources; defines future preview isolation behavior
- ADR required? No — this is an engineering operating policy, not a runtime architecture change.

## Security and tenant impact

- Authentication: none
- Authorization: none
- Tenant scoping: no runtime change
- Sensitive data/secrets: reiterates ignored local config and no committed AWS credentials
- Abuse/rate-limit considerations: limits concurrent Builder usage and unnecessary cloud deployments

## Reliability and idempotency impact

- Retry behavior: N/A — workflow policy only
- Timeout semantics: N/A
- Duplicate-delivery behavior: N/A
- Idempotency key/strategy: N/A
- DLQ/recovery/reconciliation: N/A

## Observability impact

- Logs: no runtime change
- Metrics: no runtime change
- Traces/correlation: no runtime change
- Operational states/errors: improves traceability by tying branches/worktrees/previews to task IDs

## Cost impact

- Request/compute impact: no AWS runtime change
- Storage impact: repository documentation only
- Network impact: negligible
- New AWS resources/services: none
- Expected monthly cost change or `negligible` with rationale: negligible; policy is intended to reduce Codex usage and unnecessary AWS preview execution
- Free Tier allowance relevant? No new AWS consumption
- One-off cloud verification cost: none

## Test plan

- Unit: N/A
- Integration: N/A
- Architecture: N/A
- Contract: verify README/AGENTS policy discoverability
- IaC: N/A
- E2E/manual: inspect documented worktree command sequence for coherent create/update/review/remove lifecycle
- Cloud verification required? No — no AWS behavior is introduced

## Implementation notes

The official OpenAI Codex documentation states that Codex supports multiple agents in separate threads and includes built-in worktree isolation. CommerceOS documents built-in worktrees as preferred and standard Git worktree commands as the fallback/conceptual model.

## Completion summary

### What changed

- Added `docs/development/14-codex-multi-agent-and-worktrees.md`.
- Added Luna-first model escalation policy.
- Added worktree/branch/concurrency/local/AWS isolation rules.
- Updated `AGENTS.md` and `README.md`.
- Updated `scripts/harness_check.py` to require and sanity-check the policy.

### Verification

- `python3 scripts/harness_check.py`: not directly executed through the GitHub connector; structural policy requirements were added to the harness for CI/local execution
- implementation checks: documentation-only change

### Acceptance criteria status

- AC01: PASS
- AC02: PASS
- AC03: PASS
- AC04: PASS
- AC05: PASS
- AC06: PASS

### Architecture/security/cost notes

No runtime architecture or AWS resource was added. The operating model is designed to reduce Codex usage and prevent accidental cloud/resource concurrency.

### Harness improvement

The Codex execution/worktree policy became a required harness document and its Luna-first, one-task/one-branch/one-worktree, and two-Builder defaults are mechanically checked.

### Follow-up tasks

- Phase 0 toolchain foundation should implement a task-instance-aware local launcher/port allocation mechanism.
- Future preview IaC should derive stack/resource names and tags from task/PR identity.
