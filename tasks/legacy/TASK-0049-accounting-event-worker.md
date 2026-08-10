# TASK-0049 — Consume accounting events idempotently

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 12
Milestone: Milestone D
Depends on: TASK-0045, TASK-0048

## Goal

Accounting consumes versioned business events from an SQS queue at least once and creates at most one posting attempt per tenant/source event while failures remain recoverable.

## Business context

Event-driven Accounting must preserve operational truth, idempotency, traceability, and failure isolation before actual posting rules are added.

## In scope

- add accounting-events SQS/DLQ, EventBridge routing, bounded worker Lambda, message validation, and least-privilege IAM;
- implement consumer inbox/idempotency/source-event uniqueness and a posting-rule dispatch contract;
- handle unsupported versions, transient failures, permanent rule/validation rejection, retries, DLQ, and correlation/causation;

## Out of scope

- specific sale/inventory/procurement posting rules;
- querying producer persistence, modifying source transactions, or claiming exactly-once delivery;

## Acceptance criteria

### AC01 — Valid consumption

Given a supported tenant event reaches accounting-events
when worker processes it
then one traceable posting request/inbox record is accepted with the original event/source/correlation data.

### AC02 — Duplicate delivery

Given the same event is delivered concurrently/repeatedly
when workers process copies
then at most one logical posting request/effect can proceed.

### AC03 — Failure isolation

Given transient and permanent/unsupported events are processed
when retry policy exhausts or validation rejects
then transient work retries boundedly and actionable failures reach DLQ/rejection state without affecting producer truth.

### AC04 — Tenant validation

Given event tenant/context conflicts with known source/accounting context
when worker validates it
then processing is rejected and no cross-tenant posting occurs.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected AWS evidence/teardown is recorded.

## Architecture impact

- Owning domain: Accounting / Platform Events
- Domains touched: Accounting, EventBridge, SQS, producer event contracts, Operations
- Persistence impact: Add Accounting inbox/idempotency/source-event state; journal persistence remains Accounting-owned.
- Events/contracts impact: Consume versioned initial posting events; emit JournalRejected/processing operational facts as defined.
- AWS/IaC impact: EventBridge rules, accounting-events SQS/DLQ, Lambda worker, DynamoDB, CloudWatch alarms.
- ADR required? No — accepted architecture covers this scope.

## Security and tenant impact

- Authentication: Use established merchant/internal worker identity.
- Authorization: Worker role accesses only event queue and Accounting state; payload tenant/source is validated before effects.
- Tenant scoping: Every record, command, event, and projection is scoped by trusted tenant context; cross-tenant access is denied/tested.
- Sensitive data/secrets: Event/error storage excludes secrets and unnecessary PII.
- Abuse/rate-limit considerations: Bound message size, batch, concurrency, retries, and unsupported-version handling.

## Reliability and idempotency impact

- Retry behavior: Transient infrastructure errors retry with backoff; deterministic posting/schema rejects do not loop.
- Timeout semantics: Visibility timeout exceeds bounded worker duration; partial/unknown processing is safe through inbox identity.
- Duplicate-delivery behavior: Inbox conditional write and unique event/source key suppress duplicate work.
- Idempotency key/strategy: Tenant + eventId plus posting-rule/source uniqueness.
- DLQ/recovery/reconciliation: DLQ inspection/redrive after cause/version/rule correction; reconciliation later detects missing expected postings.

## Observability impact

- Logs: Structured logs carry safe tenant, source, entity, event/command, and correlation/causation identifiers.
- Metrics: Measure validation rejects, duplicates, failures, latency, backlog/stuck state, and recovery results.
- Traces/correlation: Preserve correlation/causation end-to-end.
- Operational states/errors: Queue age/depth, DLQ, accepted/duplicate/rejected/error counts and event correlation are visible.

## Cost impact

- Request/compute impact: Scales with bounded transactional/event/report activity.
- Storage impact: Add Accounting inbox/idempotency/source-event state; journal persistence remains Accounting-owned.
- Network impact: Bounded internal API/event traffic only.
- New AWS resources/services: EventBridge rules, accounting-events SQS/DLQ, Lambda worker, DynamoDB, CloudWatch alarms.
- Free Tier allowance relevant to this task: Use documented serverless allowances, short logs, small batches, and disabled/low schedules in non-prod.
- Expected monthly cost change or `negligible` with rationale: negligible with low event volume, small batches, bounded concurrency, short logs.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused preview/dev verification.

## Test plan

- Unit: Envelope/version validation, inbox state, duplicate and retry classification, rule dispatch.
- Integration: Real EventBridge→SQS routing, redrive/DLQ, Lambda duplicate/concurrency, IAM.
- Architecture: Enforce domain ownership, inward dependencies, event conventions, and tenant rules.
- Contract: Accounting consumer message/envelope and posting-rule dispatch contracts.
- IaC: CDK assertions, synth, diff, and affected resource policy checks.
- E2E/manual: Route one supported event twice and one poison event; prove one acceptance and DLQ behavior.
- **Cloud verification required?** Yes — EventBridge/SQS/Lambda redrive/IAM semantics require AWS verification.
- AWS environment/stack(s) required: AsyncStack accounting queue/worker plus Accounting state
- Preview/staging teardown plan: Destroy preview resources and clear synthetic queue/DLQ/inbox data.

