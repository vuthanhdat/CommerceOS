# TASK-0092 — Reconcile Subscription & Billing into technical architecture

Status: Completed
Specification maturity: Completed
Owner: Technical Architect
Recommended model: Strong reasoning model
Created: 2026-08-10
Completed: 2026-08-10
Depends on: completed TASK-0088, completed TASK-0091

## Goal

Reconcile the Subscription & Billing domain extension produced by TASK-0091 into the accepted CommerceOS technical architecture baseline without rewriting unrelated architecture or implementing business features.

Resolve the technical boundaries needed so the Backlog Planner can generate/refine canonical Backlog V2 with Subscription & Billing coverage without forcing Builders to invent module ownership, persistence, integration, entitlement enforcement, security, reliability, or external billing-provider boundaries.

## Execution gate

PASS.

- TASK-0088 is Completed and established the canonical technical baseline plus ADR-003 through ADR-007.
- TASK-0091 is Completed and established the Subscription & Billing bounded context, invariants, `PD-043`–`PD-053`, and the Technical Architecture handoff.
- The prompt phrase “domain baseline produced by TASK-0092” is treated as a numbering slip; the repository task itself and completed TASK-0091 establish TASK-0091 as the authoritative domain input.

## Scope completed

### Module/project ownership

- Mapped the bounded context to one `SubscriptionBilling` implementation module with normal Domain/Application/optional Contracts/Infrastructure layering.
- Kept SubscriptionBilling separate from Tenancy, merchant-order Payments, Sales, Inventory, Accounting, Reporting, and the Mock Payment Provider.
- Preserved Plan/Subscription/EntitlementSet/optional UsageMeter/PlatformCharge as distinct business consistency concepts within the module.
- Kept initial deployment in the shared `commerce-api` Lambda when a Ready task introduces the module; no microservice/Lambda split is implied by the bounded context.

### Trusted entitlement decision boundary

- Preserved Tenancy/Merchant Access as the source of `TrustedTenantContext`.
- Defined producer-owned synchronous SubscriptionBilling entitlement evaluation for subscription-governed immediate commands.
- Prohibited client/JWT/cache/Reporting/provider plan or entitlement values from becoming authority.
- Required hard-limit commands to combine current trusted entitlement meaning with authoritative owner-local usage/state.
- Defined fail-closed current-authority behavior and kept external billing ambiguity separate from subscription/entitlement conclusions.

### Persistence ownership/access patterns

- Applied ADR-005: one module-owned DynamoDB table only when a Ready SubscriptionBilling persistence task exists.
- Defined `SUB-AP-01` through `SUB-AP-09` covering current Subscription, effective EntitlementSet/history, plan change, UsageMeter, PlatformCharge, provider evidence, reconciliation lookup, and platform-admin visibility.
- Kept platform-global Plan data, if approved, inside SubscriptionBilling application ownership rather than a generic unscoped repository.
- Prohibited foreign table access, Scan-based application paths, and speculative GSIs.

### Integration and consistency

- Added explicit sync interactions for entitlement decisions and downgrade/constraint assessment with Merchant Access, Inventory, and Ingestion when product policy makes those limits applicable.
- Kept Tenant onboarding and Subscription acquisition as separate business facts until `PD-043` says otherwise.
- Kept order-volume metering asynchronous and conditional on `PD-051` and a named consumer.
- Kept Reporting/Audit integration conditional on named consumers/approved coverage.
- Defined an owner-coordinated fencing/reconciliation requirement for a future more-restrictive hard-limit transition so concurrent foreign-domain writes cannot be made safe by a false distributed ACID assumption.
- Explicitly deferred Step Functions until an approved business sequence demonstrates durable orchestration pressure.

### External SaaS billing-provider boundary

- Defined a provider-neutral Application port and Infrastructure adapter seam without selecting a provider.
- Required stable PlatformCharge/idempotency identity, verified callback evidence, duplicate/out-of-order tolerance, explicit Unknown outcome, provider query/reconciliation, and no raw card/secret storage.
- Kept merchant-order Payments and its Mock Payment Provider outside this SaaS billing responsibility.
- Deferred provider ingress isolation, secret/configuration service, worker/schedule resources, and actual provider selection until `PD-052` plus a concrete Ready task provide requirements.

### AWS/cost

- Added no new AWS service.
- Mapped SubscriptionBilling state to a conditional module DynamoDB table in `CommerceStack`.
- Reused the existing conditional ADR-006 outbox/Streams/EventBridge/SQS/Lambda pattern only for named asynchronous consumers.
- Kept EventBridge Scheduler conditional for a concrete provider reconciliation need.
- Kept Step Functions deferred.
- Introduced no NAT Gateway, ALB, EC2, RDS/Aurora, Redis, OpenSearch, Kafka/MSK, EKS, always-on compute, or provisioned Lambda concurrency.
- TASK-0092 runtime/monthly AWS cost change: zero.

## Repository outputs

1. `docs/architecture/technical-baseline.md`
   - linked the Subscription & Billing extension and ADR-008;
   - added `SubscriptionBilling` to module/runtime/persistence/trust/integration/CDK/security/product-gate/handoff sections.
2. `docs/architecture/subscription-billing-technical-extension.md`
   - canonical focused architecture for module mapping, entitlement decision boundary, `SUB-AP-*` persistence needs, integration matrix, restrictive-plan-change fencing/reconciliation, provider seam, AWS mapping, security/reliability, and TASK-0089 handoff.
3. `docs/adr/ADR-008-subscription-billing-module-entitlement-and-provider-boundary.md`
   - accepted durable decisions for the module, entitlement authority, hard-limit owner-local truth/fencing, DynamoDB ownership, provider seam, async policy, and no-standing-cost posture.
4. `tasks/completed/TASK-0092-subscription-billing-technical-architecture-reconciliation.md`
   - canonical completed record.
5. `tasks/backlog/TASK-0092-subscription-billing-technical-architecture-reconciliation.md`
   - retained as a completed pointer to this canonical record.

## Product decisions deliberately not resolved

`PD-043`–`PD-053` remain authoritative human gates:

- acquisition/trial/Tenant-without-subscription;
- concrete plans/prices/terms/version policy;
- billing cycle/period semantics;
- SaaS currency/tax/invoice/proration;
- upgrade effectivity/charge precondition;
- downgrade timing/remediation/grandfathering/overage;
- cancellation/expiry/grace/delinquency/reactivation/retention;
- hard/soft/overage/unlimited enforcement behavior;
- order-volume meter/checkout effect;
- simulated/real provider strategy/provider choice;
- platform-admin mutation/override authority.

No common SaaS convention was converted into an architecture default.

## Acceptance criteria

### AC01 — Domain extension is represented technically: PASS

The canonical technical baseline now explicitly contains the `SubscriptionBilling` implementation module and links the focused extension/ADR while preserving the TASK-0091 bounded-context ownership.

### AC02 — Trusted entitlement checks are implementable: PASS

The decision path is explicit:

```text
AuthenticatedPrincipal
  -> Tenancy.ResolveTenantAuthority
  -> TrustedTenantContext
  -> SubscriptionBilling.EvaluateEntitlement when governed
  -> owner-local authoritative usage/invariant
  -> owner-local commit
```

Client/JWT/cache/Reporting/provider plan data cannot satisfy the check.

### AC03 — Persistence ownership is explicit: PASS

SubscriptionBilling owns its DynamoDB table and `SUB-AP-*` access needs. Other modules interact through Contracts/Application boundaries only and cannot read/write its private persistence; SubscriptionBilling likewise cannot read foreign tables for usage or remediation.

### AC04 — Plan change does not require unsafe distributed mutation: PASS

The architecture prohibits cross-domain DynamoDB ACID/destructive mutation. A future stricter hard-limit transition uses explicit owner assessment/fencing, durable transition identity, idempotent acknowledgement/finalization, and reconciliation once the governing product policy is approved.

### AC05 — External billing uncertainty is safe: PASS

The provider seam explicitly preserves request idempotency, verified callback/query evidence, duplicate/out-of-order tolerance, Unknown outcome, reconciliation, and separation between PlatformCharge evidence and Subscription/Tenant/Membership effects.

### AC06 — Cost remains bounded: PASS

No AWS resource or new managed service is deployed. Future resources are conditional and reuse accepted serverless capabilities; no always-on infrastructure is introduced.

### AC07 — Backlog handoff is explicit: PASS

The focused extension lists the technical constraints TASK-0089 can use immediately and enumerates every Subscription/Billing area that must remain non-Ready while its `PD-043`–`PD-053` input is unresolved.

## Verification

- Domain/architecture consistency review against TASK-0091: PASS — no architecture artifact changes Subscription & Billing fact ownership or resolves a `PD-043`–`PD-053` product choice.
- Module boundary review: PASS — SubscriptionBilling is separate from Tenancy/Payments and foreign Domain/Infrastructure/persistence access remains prohibited.
- Tenant/security review: PASS — trusted Tenant authority precedes subscription evaluation; request/JWT/cache/projection/provider data is non-authoritative.
- Hard-limit concurrency review: PASS at architecture level — strict restrictive transitions require owner-local fencing/reconciliation rather than a cross-table transaction or eventual hope.
- Provider reliability review: PASS — timeout/duplicate/out-of-order/missing callback cannot be converted into definitive business outcome without verified evidence.
- AWS/cost review: PASS — no new service/resource and no always-on infrastructure; conditional use follows existing ADR-005/ADR-006 rules.
- Application/business implementation review: PASS — only Markdown architecture/task/ADR artifacts changed.
- Cloud verification: N/A — no AWS deployment occurred and no teardown is required.
- `python3 scripts/harness_check.py`: not executable from this connector-only session. A public `git clone` attempt from the tool container failed because the environment could not resolve `github.com`; repository verification is therefore recorded as not run, not represented as green.

## Architecture/security/cost implications

- Architecture: adds one explicit implementation module and trusted entitlement/provider seams while preserving the modular-monolith deployment strategy.
- Security/tenant: subscription eligibility remains a server-side Tenant-scoped authority separate from Membership authorization; no bypass/cache/claim shortcut is approved.
- Reliability: restrictive plan changes and provider calls now have explicit idempotency/Unknown/reconciliation/fencing constraints.
- Cost: zero runtime cost for TASK-0092; future costs remain conditional, pay-per-use/serverless, and task-scoped.

## TASK-0089 handoff

Technically resolved for V2 generation:

- `SubscriptionBilling` module ownership/layers/shared-runtime default;
- producer-owned entitlement contract/trust flow;
- owner-local usage + entitlement enforcement rule;
- restrictive hard-limit transition fencing/reconciliation requirement;
- one module DynamoDB owner plus `SUB-AP-*` access needs;
- ADR-006-only async integration with named consumers;
- provider-neutral port/adapter and Unknown/idempotency/reconciliation semantics;
- conditional serverless AWS mapping and no-standing-cost constraint.

Must remain Outline/Refined or otherwise non-Ready while product-gated:

- all implementation whose behavior depends on `PD-043` through `PD-053` as listed above.

TASK-0089 may create foundation/architecture-test/module-skeleton work only when it does not encode one of those unresolved policies. It must not manufacture a larger Ready frontier by choosing defaults.

## Stop condition

`TECHNICAL BASELINE RECONCILED`
