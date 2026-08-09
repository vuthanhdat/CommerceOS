# TASK-0085 — Deliver optional email notifications

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Later product capability
Milestone: Unscheduled
Depends on: TASK-0056, TASK-0069
Execution gate: Requires provider/service and cost decision before implementation.

## Goal

If an email provider/service is approved, opted-in transactional notifications are delivered asynchronously with tenant-safe templates, deduplication, retries, bounce/suppression handling, and no effect on source transactions.

## Business context

Email is optional and adds a new external/service boundary, privacy, deliverability, and recurring usage considerations beyond in-app notification.

## In scope

- compare current AWS SES and credible alternatives for region/account setup, identity/domain verification, sandbox/production access, privacy, reliability, cost, and choose/document one;
- implement opt-in transactional templates/audience rules and async delivery adapter/queue/DLQ with provider idempotency/reference, retry, bounce/complaint/suppression handling;
- add merchant configuration/status, operations, audit, redaction, rate/budget limits, and deterministic local/provider contract tests;

## Out of scope

- marketing campaigns, spam/bulk outreach, SMS/push, customer-data enrichment, or sending without consent/configuration;
- blocking order/payment/accounting flows on email failure;

## Acceptance criteria

### AC01 — Provider gate

Given no approved provider/account/domain/cost decision exists
when email enablement is attempted
then no live email resource/send is enabled.

### AC02 — Transactional delivery

Given approved configuration and opted-in recipient/event exist
when notification is delivered repeatedly/retried
then at most one logical email is accepted and source business transaction remains independent.

### AC03 — Failure/suppression

Given provider transient failure, bounce, complaint, invalid address, or retry exhaustion occurs
when delivery handles it
then bounded retry/DLQ/suppression/actionable status prevents repeated harmful sends.

### AC04 — Tenant/privacy safety

Given two tenants/templates/recipients exist
when send and operations run
then branding/config/audience/data remain tenant-scoped and logs/events contain no unnecessary PII/content/secrets.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected cloud evidence, cost, and cleanup are recorded.

## Architecture impact

- Owning domain: Notification / External Email Integration
- Domains touched: Notification, Tenant settings, Customer contacts, Operations, Audit
- Persistence impact: Add tenant email config/template version/delivery/suppression/idempotency records; provider credentials are secret references.
- Events/contracts impact: Consume selected notification facts; provider delivery/bounce events versioned and verified.
- AWS/IaC impact: Likely SES plus SQS/Lambda/DLQ/SNS or accepted equivalent; new service and identity/domain resources via CDK where supported.
- ADR required? Yes — external email provider/service, delivery/bounce topology, privacy and cost are material decisions.

## Security and tenant impact

- Authentication: Merchant owner/admin configures email; workers use least-privilege provider identity; webhook/provider callbacks verified.
- Authorization: Verified sender/domain, secret management, injection-safe templates, consent/recipient rules and restricted operations.
- Tenant scoping: Trusted tenant context scopes all data/actions; public/shopper identities can access only explicitly authorized tenant resources.
- Sensitive data/secrets: Minimize email content/recipient storage, redact logs, define retention/export/deletion interaction.
- Abuse/rate-limit considerations: Per-tenant/global send rate/budget, suppression, template limits, no arbitrary recipients/body, kill switch.

## Reliability and idempotency impact

- Retry behavior: Retry only transient provider failures with bounded backoff; bounce/complaint/invalid is suppressed.
- Timeout semantics: Provider timeout is unknown until provider id/query/key or safe retry resolves it.
- Duplicate-delivery behavior: Source notification + template/audience key suppresses duplicate sends.
- Idempotency key/strategy: Tenant + sourceEvent/notification + templateVersion + recipient identity.
- DLQ/recovery/reconciliation: Delivery DLQ/replay after cause, suppression review, provider sandbox/limit status and kill switch.

## Observability impact

- Logs: Structured/redacted logs include safe tenant/entity/operation/event and correlation data.
- Metrics: Measure success/failure, duplicates, latency, backlog/stuck state, and relevant usage/cost.
- Traces/correlation: Preserve correlation/causation through all changed boundaries.
- Operational states/errors: Queued/sent/accepted/failed/bounced/complained/suppressed/DLQ plus provider reference/correlation visible.

## Cost impact

- Request/compute impact: Low-volume transactional sends and queue worker.
- Storage impact: Add tenant email config/template version/delivery/suppression/idempotency records; provider credentials are secret references.
- Network impact: Outbound provider API plus verified provider feedback.
- New AWS resources/services: Likely SES plus SQS/Lambda/DLQ/SNS or accepted equivalent; new service and identity/domain resources via CDK where supported.
- Free Tier allowance relevant to this task: Validate current pricing/allowances at scheduling time and keep non-production usage bounded.
- Expected monthly cost change or `negligible` with rationale: estimate current provider/request/data cost and enforce per-tenant/project budget before enablement.
- Estimated one-off cloud-test/load-test cost, if any: Estimate before execution and record actual bounded test usage.

## Test plan

- Unit: Audience/consent/template rendering/redaction/retry/suppression/dedup.
- Integration: Provider sandbox/contract, queue/DLQ, callback verification, tenant isolation.
- Architecture: Enforce domain ownership, tenant isolation, inward dependencies, and event/idempotency rules.
- Contract: Email provider adapter, delivery/feedback schemas and template variables.
- IaC: CDK assertions/synth/diff and affected real-AWS policy/resource tests.
- E2E/manual: Send to controlled verified test recipient, simulate transient/bounce/duplicate, recover and inspect operations.
- **Cloud verification required?** Yes — email identity/sandbox/provider, queue/callback/IAM and deliverability semantics require real service verification.
- AWS environment/stack(s) required: isolated dev email/Notification resources with verified test identities
- Preview/staging teardown plan: Disable/delete test rules/queues/templates/identities when safe; remove synthetic recipient/delivery data.

