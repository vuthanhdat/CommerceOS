# F03 — Subscription & Billing

## Feature goal
Make Trial/paid commercial terms and effective entitlements an explicit, immutable authority separate from merchant-order Payments.

## Source requirements
REQ-SUB-001..008.

## Scope
Plan/PlanVersion/Trial bootstrap; Subscription/EntitlementSet; hard-limit evaluation; dedicated simulated SaaS billing provider and PlatformCharge; monthly paid lifecycle; upgrades/downgrades/renewal/cancel/grace/reactivation; usage warning meter and support reads.

## Out of scope
Real billing provider, tax/statutory invoice, annual billing, proration, Enterprise/custom pricing, merchant accounting of CommerceOS SaaS fees.

## Architecture
SubscriptionBilling owns one table and producer-owned contracts. EntitlementSet is runtime authority. SaaS provider is external-like and separate from merchant-order Mock Payment Provider.

## Task sequence
TASK-0120 -> TASK-0121 -> TASK-0122; TASK-0120 -> TASK-0123 -> TASK-0124; TASK-0121 + Sales -> TASK-0125.

## Definition of Done
Trial/Starter/Growth/Business terms are immutable/versioned; Unknown charge state is preserved; limits never auto-delete owner-domain resources; checkout is never blocked by warning threshold.
