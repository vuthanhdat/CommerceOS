# TASK-0046 — Reverse journals and control manual accounting actions

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 11
Milestone: Milestone C
Depends on: TASK-0045

## Goal

Authorized accountants can create controlled manual journals and correct posted journals only through a traceable reversal and optional replacement.

## Business context

Corrections must preserve history; privileged accounting actions require stronger authorization and audit evidence.

## In scope

- implement manual-journal reason/source requirements and separate create/post permissions;
- implement ReverseJournal creating a balanced posted reversal linked to the original, with duplicate and already-reversed protection;
- deliver journal create/post/reverse back-office flow and safe audit records;

## Out of scope

- editing/deleting posted journals, automatic event posting, period locks, approvals workflow, or statutory adjustments;
- partial reversal unless a later policy explicitly defines it;

## Acceptance criteria

### AC01 — Manual control

Given an authorized accountant creates/posts a valid manual journal with reason
when commands run
then the journal is traceable as manual and posting is independently authorized/audited.

### AC02 — Reversal

Given a reversible posted journal is selected
when ReverseJournal runs
then one balanced posted reversal with opposite lines and links/reason is created while original remains unchanged.

### AC03 — Replay and invalid reversal

Given the reversal repeats or targets draft/already-reversed/forbidden entry
when the command runs
then no duplicate reversal appears and a deterministic result is returned.

### AC04 — Unauthorized action

Given a role lacks journal.post or journal.reverse
when the action is attempted
then it is denied and the attempt is safely auditable.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected AWS evidence/teardown is recorded.

## Architecture impact

- Owning domain: Accounting / Audit / Back Office
- Domains touched: Accounting, Authorization, Audit, Back Office
- Persistence impact: Extend Journal links/status metadata and unique reversal source record; Audit owns security records.
- Events/contracts impact: JournalReversed fact with original/reversal ids; publication later uses TASK-0048.
- AWS/IaC impact: Existing Accounting API/DynamoDB resources; no new service.
- ADR required? No — accepted architecture covers this scope.

## Security and tenant impact

- Authentication: Use established merchant/internal worker identity.
- Authorization: Separate journal.create/post/reverse permissions; owner/accountant policy and audit are explicit.
- Tenant scoping: Every record, command, event, and projection is scoped by trusted tenant context; cross-tenant access is denied/tested.
- Sensitive data/secrets: Reasons and references are bounded/redacted; no secrets in journal narrative.
- Abuse/rate-limit considerations: Validate values/payloads and bound queries, retries, batches, and privileged actions.

## Reliability and idempotency impact

- Retry behavior: Synchronous retry behavior is explicit and protected by command/version keys.
- Timeout semantics: N/A unless an external/cloud boundary is invoked.
- Duplicate-delivery behavior: N/A — no async consumer.
- Idempotency key/strategy: Tenant + originalJournalId + reversal operation version uniquely identifies correction.
- DLQ/recovery/reconciliation: N/A unless stated.

## Observability impact

- Logs: Structured logs carry safe tenant, source, entity, event/command, and correlation/causation identifiers.
- Metrics: Measure validation rejects, duplicates, failures, latency, backlog/stuck state, and recovery results.
- Traces/correlation: Preserve correlation/causation end-to-end.
- Operational states/errors: Unauthorized, not-posted, already-reversed, invalid policy, and conflict states are visible.

## Cost impact

- Request/compute impact: Scales with bounded transactional/event/report activity.
- Storage impact: Extend Journal links/status metadata and unique reversal source record; Audit owns security records.
- Network impact: Bounded internal API/event traffic only.
- New AWS resources/services: Existing Accounting API/DynamoDB resources; no new service.
- Free Tier allowance relevant to this task: Use documented serverless allowances, short logs, small batches, and disabled/low schedules in non-prod.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measured event/workflow/storage impact is material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused preview/dev verification.

## Test plan

- Unit: Reversal line generation/balance/link invariants and manual-journal authorization.
- Integration: Atomic reversal uniqueness, concurrent replay, audit, and tenant isolation.
- Architecture: Enforce domain ownership, inward dependencies, event conventions, and tenant rules.
- Contract: Manual journal/reversal APIs and JournalReversed v1.
- IaC: CDK assertions, synth, diff, and affected resource policy checks.
- E2E/manual: Create/post manual journal, reverse it, retry reversal, inspect audit/history.
- **Cloud verification required?** Yes — transactional uniqueness and protected API/audit behavior require AWS evidence.
- AWS environment/stack(s) required: Accounting/Audit resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic records after evidence.

