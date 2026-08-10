# TASK-0065 — Compensate inventory and accounting for returns

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 15
Milestone: Milestone D
Depends on: TASK-0031, TASK-0051, TASK-0064

## Goal

A verified refund can return eligible inventory and create policy-correct accounting reversal/contra postings exactly once while preserving original records.

## Business context

Refund is not complete when payment moves; physical and financial consequences must be explicit compensating actions across owned domains.

## In scope

- define/implement Inventory ReturnStock eligibility and movement source linked to fulfilled return lines;
- implement Accounting PaymentRefunded/StockReturned posting rules using reversal or contra policy from TASK-0044;
- coordinate idempotent effects and Return progress without direct cross-domain persistence access;

## Out of scope

- workflow UI/final orchestration, exchanges, partial receipt return-to-supplier, or editing original movements/journals;
- returning stock when business condition says damaged/non-restockable unless a later policy defines it;

## Acceptance criteria

### AC01 — Inventory compensation

Given verified return is eligible for restock
when ReturnStock executes
then OnHand/Available increase once with one traceable Return movement.

### AC02 — Accounting compensation

Given valid PaymentRefunded/return facts arrive
when posting rules run
then one balanced traceable reversal/contra journal is posted and original journals remain immutable.

### AC03 — Non-restock policy

Given returned item is not eligible for stock return
when compensation runs
then inventory is unchanged and disposition is explicit/audited without blocking required financial posting.

### AC04 — Duplicate/race safety

Given refund event/workflow retry/handler duplicates occur
when effects run
then no duplicate stock movement, journal, or Return progress transition appears.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and required cloud evidence/cleanup is recorded.

## Architecture impact

- Owning domain: Inventory / Accounting / Sales Return coordination
- Domains touched: Return, Payment, Inventory, Accounting, Events
- Persistence impact: Inventory owns Return movements; Accounting owns compensation journals; Sales owns Return progress.
- Events/contracts impact: Consume PaymentRefunded/ReturnApproved/StockReturned; emit compensation completion/failure facts as needed.
- AWS/IaC impact: Existing event queues/workers/DynamoDB; no new service.
- ADR required? No — implements accepted return/accounting policies; update ADR if compensation semantics materially change.

## Security and tenant impact

- Authentication: Use established merchant/platform/internal identities.
- Authorization: Only verified internal refund/return sources trigger effects; operator dispositions require permission/audit.
- Tenant scoping: Trusted tenant context scopes all records, messages, and operations; cross-tenant access is denied/tested.
- Sensitive data/secrets: No secrets/real card data; source/customer/business fields are minimized and redacted.
- Abuse/rate-limit considerations: Bound rates, quantities, retries, schedules, batches, and operator actions.

## Reliability and idempotency impact

- Retry behavior: Each compensation step retries independently with stable source keys.
- Timeout semantics: Unknown stock/journal result is queried by source key before retry/progress.
- Duplicate-delivery behavior: Event/workflow duplicates cannot repeat movements/postings.
- Idempotency key/strategy: ReturnRequestId + line/operation + policy/rule version/source event.
- DLQ/recovery/reconciliation: Failed compensation is visible/replayable; original source truth is not edited.

## Observability impact

- Logs: Structured logs include safe tenant/source/entity/operation/event and correlation data.
- Metrics: Track outcomes, failures, retries, duplicates, lag/age, recovery, and latency.
- Traces/correlation: Preserve correlation/causation across changed domains and providers.
- Operational states/errors: Correlation links return, refund, stock movement, original/reversal journals, policy/disposition.

## Cost impact

- Request/compute impact: Bounded business/scheduled/event workload.
- Storage impact: Inventory owns Return movements; Accounting owns compensation journals; Sales owns Return progress.
- Network impact: Only approved bounded external/internal traffic.
- New AWS resources/services: Existing event queues/workers/DynamoDB; no new service.
- Free Tier allowance relevant to this task: Use accepted serverless allowances, disabled/low non-prod schedules, and bounded concurrency.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; measure scheduled/retry usage.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for focused dev/preview tests.

## Test plan

- Unit: Restock eligibility, Return movement, refund/contra posting rules, duplicate decisions.
- Integration: Events/workflow retries across Return–Inventory–Accounting with partial failure.
- Architecture: Enforce domain ownership, tenant isolation, event/idempotency, and no persistence shortcuts.
- Contract: ReturnStock and refund accounting event/completion contracts.
- IaC: CDK assertions, synth, diff, and affected policy/routing checks.
- E2E/manual: Refund returnable/non-returnable item and inspect stock/accounting exactly once.
- **Cloud verification required?** Yes — event workers, DynamoDB idempotency, and cross-domain compensation require AWS.
- AWS environment/stack(s) required: Return, Inventory, Accounting/Async resources
- Preview/staging teardown plan: Destroy ephemeral resources and remove synthetic data/schedules.

