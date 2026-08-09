# TASK-0026 — Support guest checkout and tenant customer profiles

Status: Backlog
Owner: Unassigned
Created: 2026-08-09
Roadmap phase: Phase 5
Milestone: Milestone A
Depends on: TASK-0024, TASK-0025

## Goal

Merchants can manage minimal tenant-owned customer profiles while guest checkout stores only the customer snapshot required for the order and exposes an order-history projection.

## Business context

CommerceOS needs customer context without forcing shopper authentication or collecting unnecessary personal data.

## In scope

- introduce Customer/CRM profile and contact/address value objects with tenant-scoped create/update/query APIs;
- capture a minimal immutable guest customer/contact snapshot on SalesOrder without making CRM a checkout dependency;
- build merchant customer list/detail and Sales-owned order-history/total-spend projections through explicit contracts;

## Out of scope

- shopper accounts, customer notes, marketing consent/segmentation, receivable collection, or broad PII storage;
- direct Customer reads of Sales persistence;

## Acceptance criteria

### AC01 — Minimal guest checkout

Given a guest submits required contact/delivery fields
when checkout creates an order
then only validated necessary data is captured in an immutable order snapshot and no Cognito account is required.

### AC02 — Tenant-owned CRM

Given authorized staff create or update a customer profile
when the command succeeds
then the profile is scoped to the tenant and another tenant cannot infer it.

### AC03 — Explicit history projection

Given orders reference a customer/profile identity
when merchant views customer history
then results come from a Sales projection/contract and CRM never reads Sales tables directly.

### AC04 — Privacy validation

Given unnecessary or oversized sensitive fields are submitted
when the request is processed
then they are rejected/not stored and logs remain redacted.

### AC05 — Verification

Given a clean checkout with documented prerequisites
when `python3 scripts/harness_check.py` runs
then all repository checks pass and real-AWS evidence and cleanup are recorded.

## Architecture impact

- Owning domain: Customer / CRM and Sales snapshot
- Domains touched: Customer, Sales, Storefront, Back Office, Reporting projection
- Persistence impact: Add tenant-scoped Customer records; order customer snapshots remain Sales-owned; history is a read projection.
- Events/contracts impact: CustomerCreated/Updated and order facts are versioned when published through TASK-0048.
- AWS/IaC impact: DynamoDB and Customer/Sales API routes; no new managed service.
- ADR required? No — follows accepted architecture; create one if a significant new decision emerges.

## Security and tenant impact

- Authentication: Use the established merchant identity or explicit anonymous storefront boundary.
- Authorization: Customer management requires permissions; guest order lookup is not exposed without a separate safe access design.
- Tenant scoping: Tenant-owned data is scoped from trusted context; public lookup resolves an approved tenant slug and exposes only public projections.
- Sensitive data/secrets: Minimize, validate, encrypt at rest, and redact contact/address data; define retention/deletion follow-up in TASK-0069/0083.
- Abuse/rate-limit considerations: Bound text/address fields, search/pagination, and prevent PII enumeration.

## Reliability and idempotency impact

- Retry behavior: Synchronous failures are deterministic; retryable writes use explicit concurrency/idempotency controls.
- Timeout semantics: No external ambiguity is introduced unless stated.
- Duplicate-delivery behavior: N/A — no at-least-once consumer introduced.
- Idempotency key/strategy: Customer create/update uses command id/version; guest snapshot is part of idempotent checkout result.
- DLQ/recovery/reconciliation: N/A — no asynchronous boundary introduced.

## Observability impact

- Logs: Structured logs contain operation, safe tenant/entity identifiers, and correlation id.
- Metrics: Use built-in metrics and bounded business metrics for meaningful risk.
- Traces/correlation: Preserve correlation across every API/application boundary changed here.
- Operational states/errors: Duplicate contact, invalid data, privacy redaction, and projection lag are diagnosable safely.

## Cost impact

- Request/compute impact: Scales with bounded user traffic.
- Storage impact: Add tenant-scoped Customer records; order customer snapshots remain Sales-owned; history is a read projection.
- Network impact: Normal web/API payloads; avoid unbounded responses.
- New AWS resources/services: DynamoDB and Customer/Sales API routes; no new managed service.
- Free Tier allowance relevant to this task: Use existing pay-per-use services and documented learning-profile limits.
- Expected monthly cost change or `negligible` with rationale: negligible at learning volume.
- Estimated one-off cloud-test/load-test cost, if any: Cents or less for bounded preview/dev checks.

## Test plan

- Unit: Customer/contact/address validation, minimization, and snapshot rules.
- Integration: Tenant-scoped CRM persistence plus Sales projection contract and PII logging checks.
- Architecture: Verify domain ownership, inward dependencies, and no cross-domain persistence shortcuts.
- Contract: Customer APIs and CustomerOrderHistory query projection.
- IaC: CDK assertions, synth, and reviewed diff for affected resources.
- E2E/manual: Guest checkout creates an order snapshot; merchant links/views customer history without cross-domain table access.
- **Cloud verification required?** Yes — tenant-scoped DynamoDB queries, API authorization, and cross-module integration require AWS evidence.
- AWS environment/stack(s) required: Customer and Sales resources in CommerceStack
- Preview/staging teardown plan: Destroy preview resources; document retained dev state.

