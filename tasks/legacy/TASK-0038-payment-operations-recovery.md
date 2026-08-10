# TASK-0038 — Deliver payment operations and recovery tooling

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 8
Milestone: Milestone B
Depends on: TASK-0037

## Goal

Authorized operators can find ambiguous or failed payments, inspect correlated order/inventory/provider history, and invoke only safe query/retry/reconcile actions.

## Business context

Automated reconciliation needs an operational surface for exceptions that remain ambiguous or exhaust retries.

## In scope

- build tenant-authorized operational payment list/detail with status age, attempts, provider observations, webhook and correlation history;
- add safe actions for query now, resume reconciliation, acknowledge/escalate, and audited recovery where policy permits;
- add platform aggregate view without exposing tenant details beyond explicit platform-admin authority;

## Out of scope

- manual state editing, forced success/failure without evidence, real refunds, or generic DLQ tooling;
- displaying signatures, secrets, or raw sensitive payloads;

## Acceptance criteria

### AC01 — Exception visibility

Given a payment is unknown, callback-failed, or exhausted
when authorized staff open operations
then the complete safe correlated timeline and next allowed action are visible.

### AC02 — Safe recovery action

Given staff requests query/reconcile
when the provider returns evidence
then normal idempotent resolution logic runs and direct state override is impossible.

### AC03 — Authorization and audit

Given tenant staff or platform admin views/acts on an exception
when access is evaluated
then tenant boundaries/platform privilege are enforced and every action/outcome is audited.

### AC04 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and cloud verification is explicitly N/A unless AWS semantics changed.

## Architecture impact

- Owning domain: Payment Operations / Back Office / Audit
- Domains touched: Payment, Sales, Inventory, Audit, Platform Operations
- Persistence impact: No direct state mutation; queries existing histories and appends audited operational decisions.
- Events/contracts impact: Operational notification facts only; business state changes occur through normal Payment resolution commands.
- AWS/IaC impact: Existing APIs; optional bounded CloudWatch dashboard widgets, no new service.
- ADR required? No — existing accepted decisions cover the task.

## Security and tenant impact

- Authentication: Use established merchant/internal/provider identities.
- Authorization: Tenant roles see only their payments; platform admin uses explicit audited cross-tenant authorization.
- Tenant scoping: Trusted tenant context scopes every merchant record and message; client tenant ids cannot override it.
- Sensitive data/secrets: Redact webhook signatures, secrets, raw tokens, and unnecessary customer details.
- Abuse/rate-limit considerations: Rate-limit manual query/reconcile, prevent bulk blind retry, and require reason for escalation actions.

## Reliability and idempotency impact

- Retry behavior: Manual action calls the same idempotent query/reconciliation service; no alternate unsafe path.
- Timeout semantics: UI preserves unknown state on action timeout and refreshes by operation id.
- Duplicate-delivery behavior: Repeated button/API action returns existing operation/result.
- Idempotency key/strategy: Operational action id plus payment/reference and requested operation.
- DLQ/recovery/reconciliation: N/A unless explicitly in scope.

## Observability impact

- Logs: Structured logs carry safe tenant/entity/operation/event and correlation identifiers.
- Metrics: Measure outcomes, failures, retries, duplicates, latency, and stuck states at bounded cardinality.
- Traces/correlation: Preserve correlation/causation across all changed boundaries.
- Operational states/errors: This task is the primary diagnostic/recovery surface for payment ambiguity.

## Cost impact

- Request/compute impact: Scales with bounded business activity and retry policy.
- Storage impact: No direct state mutation; queries existing histories and appends audited operational decisions.
- Network impact: Only bounded API/event traffic.
- New AWS resources/services: Existing APIs; optional bounded CloudWatch dashboard widgets, no new service.
- Free Tier allowance relevant to this task: Use accepted serverless allowances and bounded non-production settings.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume; measure workflows/retries where relevant.
- Estimated one-off cloud-test/load-test cost, if any: None expected.

## Test plan

- Unit: Allowed-action policy, redaction, role rules, and timeline projection.
- Integration: Operations APIs against webhook/reconciliation histories, authorization, and audit.
- Architecture: Check domain boundaries, tenant context, and no persistence shortcuts.
- Contract: Payment exception list/detail/action APIs.
- IaC: N/A unless infrastructure changes.
- E2E/manual: Resolve one unknown payment from the operations UI and prove unauthorized action fails.
- **Cloud verification required?** No — UI/application behavior uses existing deployed semantics; optional smoke only.
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

