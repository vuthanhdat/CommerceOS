# TASK-0068 — Deliver platform-admin tenant and operations controls

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 16
Milestone: Milestone E
Depends on: TASK-0019, TASK-0038, TASK-0052, TASK-0056, TASK-0067

## Goal

Explicit platform administrators can suspend/reactivate tenants and inspect tenant/platform health, failed work, usage, and cost indicators through an audited cross-tenant operations surface.

## Business context

Platform operations need visibility and control distinct from merchant permissions; cross-tenant authority must be rare, explicit, and auditable.

## In scope

- introduce separate platform-admin identity/permission boundary and audited support access model;
- implement tenant list/detail, suspend/reactivate, platform health, queue/DLQ/workflow/crawler/accounting/payment summaries, and usage/cost indicators;
- enforce suspended-tenant behavior across protected merchant and public storefront paths while preserving safe recovery/read needs;

## Out of scope

- impersonating merchant users, editing tenant business data, billing/subscriptions, production break-glass automation, or raw secret/payload access;
- claiming precise invoice cost when only estimates are available;

## Acceptance criteria

### AC01 — Explicit platform authority

Given merchant admin and platform admin identities exist
when platform operations are called
then only platform admin can access cross-tenant summaries/actions and every access/action is audited.

### AC02 — Tenant suspension

Given active tenant is suspended/reactivated
when merchant/public requests run
then new protected/public commerce access is denied/enabled according to explicit policy without deleting data.

### AC03 — Operational overview

Given queues/workflows/crawlers/payments/accounting failures and usage exist
when platform dashboard loads
then bounded aggregate indicators and drill-down links show current state/freshness without tenant leakage.

### AC04 — No business mutation

Given platform operator inspects/supports tenant
when available actions are reviewed
then there is no generic direct edit/impersonation path into tenant-owned business records.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and cloud evidence, cost, and teardown/retention decisions are recorded.

## Architecture impact

- Owning domain: Platform Administration / Tenant & Identity / Operations / Audit
- Domains touched: Tenant, Storefront, all operations projections, Authorization, Audit
- Persistence impact: Add tenant status/platform operation/audit projections; do not duplicate domain business records.
- Events/contracts impact: TenantSuspended/Reactivated and operational summary facts as explicit versioned contracts if consumers require them.
- AWS/IaC impact: Protected platform admin APIs/Lambda, read projections/CloudWatch integrations using existing services.
- ADR required? Yes if a distinct admin identity pool/account or new cross-tenant access architecture is chosen.

## Security and tenant impact

- Authentication: Platform admin authentication/authorization is separated from merchant roles and protected with stronger controls.
- Authorization: Every cross-tenant read/action is explicit, least-privileged, logged/audited, and cannot impersonate tenant staff.
- Tenant scoping: Tenant-owned data and async context remain scoped by trusted identity; explicit audited platform access is the only cross-tenant path.
- Sensitive data/secrets: Dashboards default to aggregates/safe metadata and redact customer/payment/source payloads.
- Abuse/rate-limit considerations: Strict rate/page/query windows; suspension requires reason/confirmation and guards against accidental bulk actions.

## Reliability and idempotency impact

- Retry behavior: Suspend/reactivate commands are idempotent; metric/projection fetch failures degrade independently.
- Timeout semantics: Unknown tenant status update is queried by operation id; dashboard partial data shows stale/unavailable.
- Duplicate-delivery behavior: Repeated admin command/event cannot duplicate status/audit effect.
- Idempotency key/strategy: Tenant + platform operation type/version + command id.
- DLQ/recovery/reconciliation: Suspension rollback/reactivation and failed operations are explicit/audited; projection failures do not change tenant state.

## Observability impact

- Logs: Structured, redacted logs retain safe tenant/entity/event/operation/correlation context.
- Metrics: Use built-in metrics first; measure security, saturation, errors, latency, failures, recovery, and cost at bounded cardinality.
- Traces/correlation: Verify end-to-end correlation/causation through affected journeys.
- Operational states/errors: This task centralizes health/usage/failure indicators with freshness and correlation.

## Cost impact

- Request/compute impact: Bounded aggregate/read-model/API queries; avoid high-cardinality CloudWatch polling.
- Storage impact: Add tenant status/platform operation/audit projections; do not duplicate domain business records.
- Network impact: Measured and bounded; no unapproved fixed-cost network component.
- New AWS resources/services: Protected platform admin APIs/Lambda, read projections/CloudWatch integrations using existing services.
- Free Tier allowance relevant to this task: Respect the approximately USD 100 credit envelope and normal $0–$5/month target.
- Expected monthly cost change or `negligible` with rationale: negligible; use built-in metrics and existing projections first.
- Estimated one-off cloud-test/load-test cost, if any: Estimate and approve before execution; record actual spend/request volume afterward.

## Test plan

- Unit: Platform role, tenant status transition, suspension policy, redaction, summary composition.
- Integration: Cross-tenant authorization, audit, suspended behavior, projection/CloudWatch adapters.
- Architecture: Re-run and extend tenant/domain/event/IaC/security guardrails for discovered recurring risks.
- Contract: Platform admin tenant/action/health APIs and tenant-status events.
- IaC: CDK assertions/synth/diff plus real AWS policy/resource verification.
- E2E/manual: Platform admin suspends/reactivates a tenant; merchant/platform authorization and audit are verified.
- **Cloud verification required?** Yes — identity/IAM, API authorization, CloudWatch integrations, and suspended deployed behavior require AWS.
- AWS environment/stack(s) required: Identity/Commerce/operations resources
- Preview/staging teardown plan: Destroy ephemeral resources and document intentionally retained protected data.

