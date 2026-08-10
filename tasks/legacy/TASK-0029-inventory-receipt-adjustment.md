# TASK-0029 — Receive and adjust stock with auditability

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 6
Milestone: Milestone A
Depends on: TASK-0028

## Goal

Authorized warehouse staff can receive and adjust stock atomically, with every quantity change represented by a traceable movement and sensitive adjustments audited.

## Business context

Stock becomes useful only through controlled business movements; unexplained direct balance edits would destroy inventory integrity.

## In scope

- implement ReceiveStock and AdjustStock commands with reason, source reference, actor, and optimistic/conditional concurrency;
- atomically update OnHand and append Receive/AdjustmentIncrease/AdjustmentDecrease movements;
- deliver back-office receipt/adjustment form and movement history with authorization and audit records;

## Out of scope

- purchase-order goods receipt integration, reservation/release, issue/fulfillment, returns, or costing policy;
- negative stock unless an explicit approved policy allows it;

## Acceptance criteria

### AC01 — Stock receipt

Given authorized warehouse staff receive a valid positive quantity
when the command commits
then OnHand/Available increase once and one traceable Receive movement is appended.

### AC02 — Audited adjustment

Given authorized staff submit a valid increase/decrease with reason
when the adjustment commits
then balance and one movement update atomically and a safe audit record identifies the actor/source.

### AC03 — Unsafe or duplicate mutation

Given an adjustment would violate negative-stock policy or repeats the same source key
when the command runs
then it is rejected or returns the original logical result without duplicate movement.

### AC04 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and selected cloud verification plus teardown evidence is recorded.

## Architecture impact

- Owning domain: Inventory / Audit
- Domains touched: Inventory, Back Office, Authorization, Audit
- Persistence impact: Atomic StockItem version/balance update plus append-only movement and idempotency/source record.
- Events/contracts impact: StockReceived/StockAdjusted domain facts; external publication waits for TASK-0048.
- AWS/IaC impact: Existing Inventory DynamoDB/API resources; transactional/conditional writes.
- ADR required? No — accepted domain/serverless rules cover this task.

## Security and tenant impact

- Authentication: Use established merchant/Storefront identity boundaries.
- Authorization: Receive and adjust are separate permissions; adjustments require stronger authorization and audit.
- Tenant scoping: All tenant-owned commands/queries use trusted tenant context; client tenant ids are rejected/ignored and cross-tenant access is tested.
- Sensitive data/secrets: No secrets or real payment/card data are stored or logged.
- Abuse/rate-limit considerations: Bound quantities/reasons and rate-limit mutation endpoints; reject overflow/invalid decimals.

## Reliability and idempotency impact

- Retry behavior: Same source/command key is safe to retry; concurrency conflicts are explicit.
- Timeout semantics: No external ambiguous timeout unless stated.
- Duplicate-delivery behavior: N/A unless a retryable command is stated.
- Idempotency key/strategy: Tenant + warehouse + source type/id or command id uniquely identifies a movement.
- DLQ/recovery/reconciliation: N/A — no queue/event consumer.

## Observability impact

- Logs: Structured logs include safe tenant, entity, command/event, and correlation identifiers.
- Metrics: Bounded metrics cover conflicts, failures, and latency for the changed capability.
- Traces/correlation: Preserve correlation across all application/external boundaries.
- Operational states/errors: Adjustment denial, concurrency conflict, duplicate source, and invariant failure are diagnosable.

## Cost impact

- Request/compute impact: Usage scales with bounded business requests.
- Storage impact: Atomic StockItem version/balance update plus append-only movement and idempotency/source record.
- Network impact: Normal API traffic only.
- New AWS resources/services: Existing Inventory DynamoDB/API resources; transactional/conditional writes.
- Free Tier allowance relevant to this task: Use documented Lambda, DynamoDB, API Gateway, SQS, and CloudWatch limits where applicable.
- Expected monthly cost change or `negligible` with rationale: negligible at the learning profile.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded dev/preview tests.

## Test plan

- Unit: Receive/adjust invariants, negative-stock policy, movement creation, and authorization decisions.
- Integration: Concurrent DynamoDB updates, atomic balance/movement write, duplicate key, and cross-tenant tests.
- Architecture: Enforce domain ownership, inward dependencies, and no cross-domain persistence access.
- Contract: ReceiveStock/AdjustStock commands and movement query API.
- IaC: CDK assertions, synth, and reviewed diff.
- E2E/manual: Receive, adjust, audit, and view movement history in Back Office.
- **Cloud verification required?** Yes — DynamoDB conditional/transaction behavior and protected API wiring need AWS verification.
- AWS environment/stack(s) required: Inventory/Audit resources in CommerceStack
- Preview/staging teardown plan: Destroy ephemeral resources and synthetic data.

