# TASK-0045 — Post balanced, immutable, traceable journals

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 11
Milestone: Milestone C
Depends on: TASK-0044

## Goal

Authorized accounting commands can create drafts and atomically post only balanced journals; posted journals and lines are immutable and uniquely traceable to their source.

## Business context

Ledger correctness requires mutation rules unlike CRUD: balance, atomic commit, source uniqueness, and immutable posted history.

## In scope

- introduce JournalEntry, JournalLine, SourceDocumentReference, Draft/Posted/Rejected state, money/debit/credit invariants;
- implement draft creation/validation and atomic PostJournal with balanced totals, immutable snapshot, posting metadata, and optimistic concurrency;
- persist/query journals by tenant, date, account/source/status with unique logical source/idempotency protection;

## Out of scope

- manual-journal authorization UI, reversal, automatic event posting, general ledger/trial balance views, or period close;
- editing/deleting a posted journal;

## Acceptance criteria

### AC01 — Balanced posting

Given a valid draft has equal debit and credit totals
when PostJournal runs
then entry and all lines become Posted atomically with actor/time/source traceability.

### AC02 — Unbalanced rejection

Given draft totals differ or lines/accounts are invalid
when posting is attempted
then no posted journal/partial lines exist and a deterministic rejection is returned.

### AC03 — Immutability

Given a posted journal is targeted by edit/delete/line mutation
when the command runs
then the mutation is rejected and original data remains unchanged.

### AC04 — Source uniqueness

Given the same source/idempotency key is submitted repeatedly/concurrently
when posting runs
then at most one logical posted journal exists and replay returns the established outcome.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected AWS evidence/teardown is recorded.

## Architecture impact

- Owning domain: Accounting
- Domains touched: Accounting, Authorization, Audit hooks
- Persistence impact: Add JournalEntry/Line/posting/source uniqueness records with transactional/conditional write strategy and read indexes.
- Events/contracts impact: JournalCreated/Posted/Rejected domain facts; reliable publication waits for TASK-0048.
- AWS/IaC impact: DynamoDB transactional Accounting persistence and protected API/Lambda routes.
- ADR required? No — implements TASK-0044 policy; create an ADR only if storage/atomicity changes materially.

## Security and tenant impact

- Authentication: Use established merchant/internal worker identity.
- Authorization: Journal view/create/post permissions are distinct; posting is privileged and audited.
- Tenant scoping: Every record, command, event, and projection is scoped by trusted tenant context; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Bound line count, amount precision/range, query date windows, and pagination.

## Reliability and idempotency impact

- Retry behavior: Post retry uses the same journal/source/idempotency key; validation rejection is permanent.
- Timeout semantics: Unknown commit outcome is queried by journal/source key before retry.
- Duplicate-delivery behavior: Concurrent repeated post cannot produce a second journal or duplicate lines.
- Idempotency key/strategy: Tenant + journal command id and optional unique source type/id/event id.
- DLQ/recovery/reconciliation: N/A unless stated.

## Observability impact

- Logs: Structured logs carry safe tenant, source, entity, event/command, and correlation/causation identifiers.
- Metrics: Measure validation rejects, duplicates, failures, latency, backlog/stuck state, and recovery results.
- Traces/correlation: Preserve correlation/causation end-to-end.
- Operational states/errors: Balance reject, invalid account, stale version, duplicate source, and unknown commit are distinct.

## Cost impact

- Request/compute impact: Small bounded DynamoDB transaction per journal; line count capped.
- Storage impact: Add JournalEntry/Line/posting/source uniqueness records with transactional/conditional write strategy and read indexes.
- Network impact: Bounded internal API/event traffic only.
- New AWS resources/services: DynamoDB transactional Accounting persistence and protected API/Lambda routes.
- Free Tier allowance relevant to this task: Use documented serverless allowances, short logs, small batches, and disabled/low schedules in non-prod.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measured event/workflow/storage impact is material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused preview/dev verification.

## Test plan

- Unit: Double-entry balance, line/account validation, status/immutability, source uniqueness.
- Integration: Atomic DynamoDB post, concurrent duplicates, tenant isolation, and query access patterns.
- Architecture: Enforce domain ownership, inward dependencies, event conventions, and tenant rules.
- Contract: Journal create/post/query application and HTTP contracts.
- IaC: CDK assertions, synth, diff, and affected resource policy checks.
- E2E/manual: Create balanced/unbalanced drafts, post once, and prove immutability/replay.
- **Cloud verification required?** Yes — DynamoDB transactional/conditional atomicity and IAM/API need real AWS verification.
- AWS environment/stack(s) required: Accounting resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic records after evidence.

