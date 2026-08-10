# TASK-0048 — Establish reliable domain-event publication

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 12
Milestone: Milestone D
Depends on: TASK-0024, TASK-0031, TASK-0034, TASK-0043, TASK-0045
Execution gate: Requires an ADR for the publication/atomicity mechanism and public event contracts.

## Goal

Committed domain facts are published reliably with the required versioned envelope and routed through EventBridge without losing events or coupling consumers to producer persistence.

## Business context

Automatic accounting/reporting requires at-least-once events, but publication must address atomicity between domain commit and event delivery rather than using best-effort fire-and-forget.

## In scope

- create an ADR selecting the domain commit-to-publication mechanism, including atomicity, ordering assumptions, replay, retention, failure, and cost;
- implement required event envelope fields, schema/version governance, correlation/causation, tenant validation, and producer-side publication/idempotency mechanism;
- add EventBridge bus/rules baseline, contract registry/fixtures, observability, recovery, and executable architecture/contract guardrails;

## Out of scope

- accounting posting rules and reporting consumers;
- event sourcing, vague database-change events, or forcing all in-process calls through EventBridge;

## Acceptance criteria

### AC01 — Publication decision

Given credible atomic publication mechanisms are compared
when the ADR is accepted
then failure windows, delivery guarantee, replay/recovery, IAM, cost, migration, and validation are explicit.

### AC02 — Required envelope

Given a public domain event is produced
when contract checks run
then eventId/type/version/tenantId/aggregateId/occurredAt/correlationId/causationId are present as applicable and schema is versioned.

### AC03 — No lost committed fact

Given a producer commit succeeds while publication is interrupted/retried
when the selected mechanism recovers
then the event is eventually published at least once or becomes an actionable failure without rolling back the committed business fact.

### AC04 — Duplicate-safe routing

Given the same event reaches EventBridge/targets more than once
when delivery occurs
then event identity remains stable and consumers can deduplicate without producer-table access.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected AWS evidence/teardown is recorded.

## Architecture impact

- Owning domain: Platform Events / all producer domains
- Domains touched: Sales, Payment, Inventory, Procurement, Catalog, Accounting, Reporting, Operations
- Persistence impact: Add only the selected outbox/publication/idempotency state; domain persistence ownership remains intact.
- Events/contracts impact: Establish public event envelope v1 and initial versioned business-event schemas.
- AWS/IaC impact: EventBridge custom bus/rules plus selected publisher/outbox resources, IAM, CloudWatch; exact mechanism follows ADR.
- ADR required? Yes — cross-domain integration and commit/publication atomicity are material architecture decisions.

## Security and tenant impact

- Authentication: Use established merchant/internal worker identity.
- Authorization: Producer/consumer IAM is least-privilege; event source/detail validation prevents tenant/context spoofing.
- Tenant scoping: Tenant context is captured from committed trusted domain state and validated by consumers, never accepted from untrusted event payload alone.
- Sensitive data/secrets: Events carry minimum stable facts/ids, not database rows, secrets, or unnecessary PII.
- Abuse/rate-limit considerations: Bound event sizes/rates/replay batches and schema cardinality; prevent event-bus misuse as generic logging.

## Reliability and idempotency impact

- Retry behavior: Publisher retries transient delivery per ADR; poison/schema failures become visible without infinite loops.
- Timeout semantics: Unknown publication attempt is resolved by stable event/outbox identity and status lookup.
- Duplicate-delivery behavior: At-least-once is explicit; stable eventId and consumer idempotency are mandatory.
- Idempotency key/strategy: One stable event id per logical committed fact; retry never generates a new logical id.
- DLQ/recovery/reconciliation: Provide documented stuck-publication inspection/replay and audit; no source-domain table mutation by consumers.

## Observability impact

- Logs: Structured logs carry safe tenant, source, entity, event/command, and correlation/causation identifiers.
- Metrics: Measure validation rejects, duplicates, failures, latency, backlog/stuck state, and recovery results.
- Traces/correlation: Preserve correlation/causation end-to-end.
- Operational states/errors: Publish pending/failed/age, EventBridge failed invocations, rule matches, and correlation are observable.

## Cost impact

- Request/compute impact: Event volume follows committed facts; retries/replay are bounded.
- Storage impact: Add only the selected outbox/publication/idempotency state; domain persistence ownership remains intact.
- Network impact: Bounded internal API/event traffic only.
- New AWS resources/services: EventBridge custom bus/rules plus selected publisher/outbox resources, IAM, CloudWatch; exact mechanism follows ADR.
- Free Tier allowance relevant to this task: Use documented serverless allowances, short logs, small batches, and disabled/low schedules in non-prod.
- Expected monthly cost change or `negligible` with rationale: low-volume EventBridge is credit-funded but negligible; model measured event counts later.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused preview/dev verification.

## Test plan

- Unit: Envelope validation, event identity/versioning, publication state machine.
- Integration: Commit/publication failure windows, duplicate/replay, EventBridge routing/IAM, and schema compatibility.
- Architecture: Enforce domain ownership, inward dependencies, event conventions, and tenant rules.
- Contract: Event envelope and each initial public business event v1.
- IaC: CDK assertions, synth, diff, and affected resource policy checks.
- E2E/manual: Commit representative Sales/Inventory/Payment/Procurement facts, interrupt publication, recover, and observe routed duplicate-safe events.
- **Cloud verification required?** Yes — EventBridge routing/IAM and selected publication atomicity mechanism require real AWS tests.
- AWS environment/stack(s) required: Foundation/Async plus affected producer resources
- Preview/staging teardown plan: Destroy preview bus/rules/workers and clear synthetic publication state.

