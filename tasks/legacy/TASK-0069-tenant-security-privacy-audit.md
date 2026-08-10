# TASK-0069 — Audit tenant isolation, privacy, and sensitive operations

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 16
Milestone: Milestone E
Depends on: TASK-0026, TASK-0067, TASK-0068

## Goal

A systematic audit proves tenant isolation, sensitive-operation auditability, and privacy/retention behavior across all implemented domains, with defects fixed and recurring gaps converted into harness guardrails.

## Business context

Tenant/security/privacy rules accumulated across many slices; production-minded readiness requires adversarial evidence, not self-assertion.

## In scope

- enumerate every tenant-owned/public/platform API, repository, event, queue, projection, export/drill-through, and privileged operation;
- run cross-tenant/IDOR/client-tenant spoofing/auth downgrade/PII-log/audit completeness/retention tests and threat review;
- fix discovered defects in scope and add reusable fixtures/static checks/documentation so the failure class is harder to reintroduce;

## Out of scope

- external certification, legal privacy compliance claim, penetration test by third party, or new product features;
- blind cascade deletion of accounting/audit records with different retention duties;

## Acceptance criteria

### AC01 — Complete surface inventory

Given all implemented domain and async/public/admin surfaces are inspected
when audit matrix is completed
then owner, trust source, tenant key, authorization, sensitive fields, audit, and tests are recorded.

### AC02 — Adversarial isolation

Given Tenant A/B, anonymous, merchant, and platform identities attack known ids/filters/events
when tests run
then no unauthorized read/write/inference/cross-tenant async effect succeeds.

### AC03 — Privacy and audit evidence

Given logs/events/audit/customer/payment/crawler data and retention paths are reviewed
when checks run
then sensitive fields are minimized/redacted, privileged actions traceable, and retention conflicts documented.

### AC04 — Harness improvement

Given a defect or repeatable gap is found
when it is fixed
then a regression test plus at least one reusable guardrail/fixture/instruction is added when practical.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then all checks pass and cloud evidence, cost, and teardown/retention decisions are recorded.

## Architecture impact

- Owning domain: Security / Tenant & Identity / Audit / Engineering Harness
- Domains touched: All implemented domains and infrastructure boundaries
- Persistence impact: No new business store by default; security fixes may adjust key/access/retention/audit metadata with migration plan.
- Events/contracts impact: Validate envelope tenant context, consumer trust, sensitive payload minimization, and audit/event distinction.
- AWS/IaC impact: IAM/API/Cognito/DynamoDB/SQS/EventBridge/S3/CloudWatch policy review and selected hardening fixes.
- ADR required? Yes if tenant isolation, admin access, identity, or retention architecture changes materially.

## Security and tenant impact

- Authentication: Test authentication lifecycle, stale/disabled membership, platform separation, anonymous routes, and token handling.
- Authorization: Primary task purpose; all findings are prioritized and resolved or recorded as blocking follow-up with owner/risk.
- Tenant scoping: Primary task purpose; cross-tenant tests cover direct and indirect inference/effects.
- Sensitive data/secrets: Review PII, invitation/webhook secrets, source raw data, audit/accounting retention, fixtures, and logs.
- Abuse/rate-limit considerations: Include enumeration, pagination/filter escape, payload size, replay, and rate-control cases.

## Reliability and idempotency impact

- Retry behavior: Audit duplicate/retry paths for cross-tenant or repeated sensitive effects.
- Timeout semantics: Audit unknown-state recovery for payments/workflows and authorization after timeouts.
- Duplicate-delivery behavior: Audit consumer and command idempotency across all sensitive effects.
- Idempotency key/strategy: Inventory the stable key/retention/tenant binding for every high-risk operation.
- DLQ/recovery/reconciliation: Verify DLQ/reconciliation actions preserve authorization/audit and cannot replay across tenants.

## Observability impact

- Logs: Structured, redacted logs retain safe tenant/entity/event/operation/correlation context.
- Metrics: Use built-in metrics first; measure security, saturation, errors, latency, failures, recovery, and cost at bounded cardinality.
- Traces/correlation: Verify end-to-end correlation/causation through affected journeys.
- Operational states/errors: Security denials/audit gaps/PII redaction failures are observable without sensitive leakage.

## Cost impact

- Request/compute impact: Bounded test traffic; no large load campaign.
- Storage impact: No new business store by default; security fixes may adjust key/access/retention/audit metadata with migration plan.
- Network impact: Measured and bounded; no unapproved fixed-cost network component.
- New AWS resources/services: IAM/API/Cognito/DynamoDB/SQS/EventBridge/S3/CloudWatch policy review and selected hardening fixes.
- Free Tier allowance relevant to this task: Respect the approximately USD 100 credit envelope and normal $0–$5/month target.
- Expected monthly cost change or `negligible` with rationale: mostly test effort; cloud verification remains within approved small envelope.
- Estimated one-off cloud-test/load-test cost, if any: Estimate and approve before execution; record actual spend/request volume afterward.

## Test plan

- Unit: Security policies, redaction, retention/deletion decision logic, audit record rules.
- Integration: Full reusable cross-tenant/security matrix across APIs/repositories/events/queues/projections.
- Architecture: Re-run and extend tenant/domain/event/IaC/security guardrails for discovered recurring risks.
- Contract: Security expectations for public/event/admin contracts.
- IaC: CDK assertions/synth/diff plus real AWS policy/resource verification.
- E2E/manual: Representative merchant/shopper/platform attack journeys and privileged audit trails.
- **Cloud verification required?** Yes — Cognito/API/IAM/DynamoDB/SQS/EventBridge/S3 policies and deployed isolation need AWS.
- AWS environment/stack(s) required: ephemeral staging or carefully isolated dev across affected stacks
- Preview/staging teardown plan: Destroy staging/preview and all synthetic identities/data; retain only redacted audit evidence.

