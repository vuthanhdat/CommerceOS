# TASK-0083 — Add shopper accounts, customer notes, and data lifecycle controls

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Later product capability
Milestone: Unscheduled
Depends on: TASK-0026, TASK-0069

## Goal

Shoppers can optionally own tenant-specific accounts and merchant staff can keep controlled customer notes while privacy deletion/anonymization respects order, accounting, and audit retention boundaries.

## Business context

Guest checkout remains default, but later customer identity and CRM notes require explicit tenant/account linking and designed—not blind cascade—data lifecycle.

## In scope

- decide and implement shopper authentication/account linking per tenant without conflating merchant Cognito identity;
- add authorized CustomerNote lifecycle, minimal account/profile/contact controls, access/export/delete/anonymize requests, retention classifications, and audit;
- implement deletion/anonymization workflow that removes/editable PII while preserving legally/operationally required order/accounting/audit evidence through safe pseudonymization;

## Out of scope

- marketing automation, loyalty/social graph, statutory privacy compliance certification, or erasing immutable financial facts;
- requiring shopper account for checkout;

## Acceptance criteria

### AC01 — Optional account

Given guest and registered shopper use a tenant storefront
when checkout/order access runs
then guest checkout remains available and registered shopper sees only explicitly authorized own tenant orders.

### AC02 — Customer notes

Given authorized merchant staff create/update/archive a bounded note
when commands run
then note is tenant/customer scoped, audited as appropriate, and unauthorized/shopper/cross-tenant access is denied.

### AC03 — Lifecycle request

Given verified customer requests export/delete/anonymize
when workflow runs
then eligible PII is exported/removed/anonymized while immutable order/accounting/audit evidence retains only required safe reference.

### AC04 — Identity isolation

Given same email/account identity interacts with multiple tenants
when access is resolved
then tenant customer profiles/orders/notes never become mutually visible without explicit policy.

### AC05 — Verification

Given a clean checkout
when `python3 scripts/harness_check.py` runs
then repository verification passes and selected cloud evidence, cost, and cleanup are recorded.

## Architecture impact

- Owning domain: Customer / CRM / Tenant Identity / Privacy
- Domains touched: Customer, Sales order access, Accounting/Audit retention references, Storefront
- Persistence impact: Add shopper identity links, CustomerNote, lifecycle request/checkpoint/audit and pseudonymization mappings with retention policy.
- Events/contracts impact: CustomerLinked/NoteChanged/DataLifecycleRequested/Completed versioned with minimal PII.
- AWS/IaC impact: Cognito or accepted shopper identity config, DynamoDB, workflow/queue only if justified; no SMS by default.
- ADR required? Yes — shopper identity and cross-domain privacy/retention strategy materially affect security/data semantics.

## Security and tenant impact

- Authentication: Verified shopper identity is distinct from merchant staff; account recovery/token lifecycle and guest lookup are explicitly designed.
- Authorization: Own-order access, tenant separation, anti-enumeration, note permissions, lifecycle proof and platform support are audited.
- Tenant scoping: Trusted tenant context scopes all data/actions; public/shopper identities can access only explicitly authorized tenant resources.
- Sensitive data/secrets: Primary scope: minimize/encrypt/redact/export/delete/anonymize PII while preserving required immutable evidence.
- Abuse/rate-limit considerations: Rate-limit signup/login/recovery/order lookup/export/delete and protect against account linking/takeover.

## Reliability and idempotency impact

- Retry behavior: Lifecycle workflow retries idempotently per domain and records partial completion.
- Timeout semantics: Partial deletion/export remains visible/reconcilable; no repeated destructive blind cascade.
- Duplicate-delivery behavior: Repeated identity link/note/lifecycle requests do not duplicate or over-delete.
- Idempotency key/strategy: Tenant + shopper/customer + lifecycle request id and per-domain step keys.
- DLQ/recovery/reconciliation: Failed lifecycle steps are visible, safely resumable, and audited; retention conflicts require explicit disposition.

## Observability impact

- Logs: Structured/redacted logs include safe tenant/entity/operation/event and correlation data.
- Metrics: Measure success/failure, duplicates, latency, backlog/stuck state, and relevant usage/cost.
- Traces/correlation: Preserve correlation/causation through all changed boundaries.
- Operational states/errors: Identity linking, lifecycle progress/failure, redaction and retained-evidence reasons are diagnosable safely.

## Cost impact

- Request/compute impact: Low-volume Cognito/API/workflow operations.
- Storage impact: Additional identity/note/lifecycle metadata offset by defined retention/deletion.
- Network impact: Bounded API/CDN/provider traffic only.
- New AWS resources/services: Cognito or accepted shopper identity config, DynamoDB, workflow/queue only if justified; no SMS by default.
- Free Tier allowance relevant to this task: Validate current pricing/allowances at scheduling time and keep non-production usage bounded.
- Expected monthly cost change or `negligible` with rationale: Cognito likely within allowance; estimate workflow/export storage and any email costs separately.
- Estimated one-off cloud-test/load-test cost, if any: Estimate before execution and record actual bounded test usage.

## Test plan

- Unit: Identity linking, own-order authorization, notes, retention/anonymization decisions.
- Integration: Cognito/API, Sales/Customer/Accounting/Audit lifecycle contracts, retries, tenant isolation.
- Architecture: Enforce domain ownership, tenant isolation, inward dependencies, and event/idempotency rules.
- Contract: Shopper identity/order access, notes, lifecycle request/result schemas.
- IaC: CDK assertions/synth/diff and affected real-AWS policy/resource tests.
- E2E/manual: Guest/registered checkout, own-order access, notes authorization, export/delete/anonymize journey.
- **Cloud verification required?** Yes — Cognito/token lifecycle, cross-domain privacy workflow, IAM and persistence require AWS.
- AWS environment/stack(s) required: Identity/Customer/Sales/Accounting/Audit resources in isolated staging
- Preview/staging teardown plan: Delete synthetic identities/exports/data per tested lifecycle and destroy staging.

