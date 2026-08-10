# TASK-0050 — Post sales and payment events automatically

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 12
Milestone: Milestone C / D
Depends on: TASK-0034, TASK-0049

## Goal

PaymentCaptured and the chosen sale-recognition facts automatically produce one balanced, traceable journal per logical source under the accepted accounting policy.

## Business context

Sales/payment automation is the first proof that committed operational activity can feed Accounting without re-entry or cross-domain reads.

## In scope

- implement versioned posting rules for cash/receivable versus sales revenue using TASK-0044 recognition policy;
- validate event amounts/currency/accounts/source, create balanced journal lines, post atomically, and record rule version;
- handle duplicate, out-of-order, missing prerequisite, correction policy, and source drill-through;

## Out of scope

- COGS/inventory posting, refunds, tax/VAT, multi-currency, or reading Sales/Payment tables;
- recognizing revenue at a trigger not accepted by TASK-0044;

## Acceptance criteria

### AC01 — Automatic sale posting

Given a supported valid PaymentCaptured/sale-recognition event arrives
when Accounting applies the active rule
then one balanced posted journal debits the policy account and credits Sales Revenue with source/event traceability.

### AC02 — Duplicate source

Given the same logical payment/sale event is delivered repeatedly or with duplicate transport
when worker processes it
then no second logical journal is created.

### AC03 — Invalid or conflicting event

Given amount/currency/account/source data is invalid or another event conflicts
when posting is attempted
then no unbalanced/partial journal is posted and an actionable rejection/reconciliation state is recorded.

### AC04 — Drill-through

Given an accountant opens the generated journal
when source context is requested
then event/order/payment identifiers and rule version are available without Accounting reading producer persistence directly.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected AWS evidence/teardown is recorded.

## Architecture impact

- Owning domain: Accounting
- Domains touched: Accounting, Payment/Sales event contracts, Back Office drill-through
- Persistence impact: Accounting posting rule/version and journal source-event uniqueness records.
- Events/contracts impact: Consume PaymentCaptured/accepted sale-recognition v1; emit JournalPosted/Rejected v1.
- AWS/IaC impact: Existing accounting event worker/DynamoDB; no new service.
- ADR required? No — accepted architecture covers this scope.

## Security and tenant impact

- Authentication: Use established merchant/internal worker identity.
- Authorization: Automatic worker uses least privilege; financial drill-through is permission-protected.
- Tenant scoping: Every record, command, event, and projection is scoped by trusted tenant context; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets, real card data, or unnecessary personal data are stored/logged.
- Abuse/rate-limit considerations: Validate values/payloads and bound queries, retries, batches, and privileged actions.

## Reliability and idempotency impact

- Retry behavior: Transient journal persistence retries safely; deterministic rule/data rejection is recorded, not retried forever.
- Timeout semantics: Unknown post result is queried by source event before worker retry.
- Duplicate-delivery behavior: Event/source uniqueness prevents duplicate journal.
- Idempotency key/strategy: Tenant + posting rule/type + logical source transaction/event.
- DLQ/recovery/reconciliation: Rejected/missing prerequisites enter operations and TASK-0052 reconciliation.

## Observability impact

- Logs: Structured logs carry safe tenant, source, entity, event/command, and correlation/causation identifiers.
- Metrics: Measure validation rejects, duplicates, failures, latency, backlog/stuck state, and recovery results.
- Traces/correlation: Preserve correlation/causation end-to-end.
- Operational states/errors: Journal links event/payment/order/rule version; posting latency/reject/duplicate metrics.

## Cost impact

- Request/compute impact: Scales with bounded transactional/event/report activity.
- Storage impact: Accounting posting rule/version and journal source-event uniqueness records.
- Network impact: Bounded internal API/event traffic only.
- New AWS resources/services: Existing accounting event worker/DynamoDB; no new service.
- Free Tier allowance relevant to this task: Use documented serverless allowances, short logs, small batches, and disabled/low schedules in non-prod.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; update the cost model if measured event/workflow/storage impact is material.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused preview/dev verification.

## Test plan

- Unit: Posting-rule mapping, amounts/accounts, balance, event validation, and source uniqueness.
- Integration: Duplicate/out-of-order events through worker to atomic journal posting.
- Architecture: Enforce domain ownership, inward dependencies, event conventions, and tenant rules.
- Contract: PaymentCaptured/sale-recognition and JournalPosted/Rejected v1.
- IaC: CDK assertions, synth, diff, and affected resource policy checks.
- E2E/manual: Complete a paid order, deliver event twice, and inspect one balanced sale journal.
- **Cloud verification required?** Yes — queue worker, DynamoDB atomicity, duplicate delivery, and EventBridge/SQS integration need AWS.
- AWS environment/stack(s) required: Accounting worker/resources plus CommerceStack/MockPayment test flow
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic records after evidence.

