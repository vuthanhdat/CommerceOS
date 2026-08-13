# F02 — Tenant & Merchant Access

## Feature goal
Enable verified identities to onboard and operate only through current trusted Tenant/Membership authority, including safe Suspended read-only behavior.

## Source requirements
REQ-SEC-001..003, REQ-TEN-001..005, REQ-SUB-001/003, REQ-AUD-001.

## Scope
Tenancy module/table; Tenant/Profile; subject discovery; read/mutation contexts; Membership roles/lifecycle/last-owner; invitations; onboarding Trial coordination; platform suspend/reactivate/support read paths.

## Out of scope
Tenant closure/deletion/privacy erasure; custom permission engine; automatic plan authority from token claims.

## Architecture
One Tenancy implementation module/table. Subject discovery is candidate discovery only. Strong current validation creates separate `TrustedTenantReadContext` and `TrustedTenantMutationContext`. Onboarding follows ADR-009 durable Trial recovery.

## Task sequence
TASK-0110 -> TASK-0111; TASK-0110 + TASK-0121 -> TASK-0112; TASK-0110 + TASK-0120 -> TASK-0113; TASK-0111 -> TASK-0114.

## Progress

TASK-0110 and TASK-0111 are complete. Tenancy now owns its
Domain/Application/Infrastructure boundary, task-prefixed DynamoDB table,
tenant-scoped persistence contracts, strong subject discovery, and separate
trusted read/mutation authority contexts. Authority resolution revalidates current
Tenant and Membership state on every request and fails closed. TASK-0114 is now
Ready; TASK-0113 is now Ready after the Subscription catalog bootstrap. TASK-0112
remains blocked by entitlement authority.

## Definition of Done
Cross-Tenant access is non-disclosing; current authority/last-owner/limits are concurrency safe; successful onboarding proves Tenant + Owner + Trial; suspension is non-destructive and audited.
