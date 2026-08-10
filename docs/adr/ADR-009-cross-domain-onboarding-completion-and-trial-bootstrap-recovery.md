# ADR-009 — Cross-Domain Onboarding Completion and Trial-Bootstrap Recovery

Status: Accepted
Date: 2026-08-10
Decision owners: CommerceOS Technical Architecture
Supersedes: the Tenancy-only onboarding-completion portion of ADR-005/TASK-0088
Superseded by: N/A

## Context

The reconciled domain baseline now defines successful merchant onboarding as three accepted outcomes with separate business owners:

```text
Tenant Management      -> Active Tenant
Merchant Access        -> Active initial Owner Membership
Subscription & Billing -> 30-day Trial Subscription + Trial EntitlementSet
```

The earlier technical baseline could commit the first two outcomes atomically because Tenant Management and Merchant Access are hosted by the same `Tenancy` implementation module and DynamoDB table. It therefore described completed onboarding as one Tenancy transaction.

That description is no longer sufficient. `SubscriptionBilling` is a separate implementation module and persistence owner under ADR-008/ADR-005. Using a cross-module DynamoDB transaction would weaken module ownership and let a technical convenience couple two bounded contexts' persistence. Conversely, committing Tenancy and then best-effort calling SubscriptionBilling can strand a Tenant/Owner without the required Trial while the API incorrectly reports onboarding complete.

The architecture needs a durable, idempotent completion/recovery process without moving Subscription truth into Tenancy or inventing rollback/delete business semantics.

## Decision

### 1. Preserve separate business ownership

`Tenancy` remains owner of Tenant and Membership state. `SubscriptionBilling` remains owner of Trial Subscription and Trial EntitlementSet state.

No module reads/writes the other's table, and no cross-module DynamoDB transaction is used.

### 2. Tenancy transaction commits the durable registration intent and local outcome

For one logical onboarding request, Tenancy atomically writes its own state:

```text
durable onboarding operation / idempotency claim
+ Active Tenant
+ Active initial Owner Membership
+ current authority lookup
+ subject-membership discovery record
+ active-owner guard
+ required source-owned Audit intent when applicable
+ Trial-bootstrap recovery work-outbox record
```

All records are Tenancy-owned technical/business records. The recovery work item contains only the stable contract data required to invoke SubscriptionBilling; it is not a copied Subscription aggregate.

### 3. Start Trial through an explicit SubscriptionBilling application contract

The application coordinator invokes an idempotent producer-owned command conceptually equivalent to:

```text
StartTrialSubscription(
  tenantId,
  onboardingOperationId,
  sourceIdentity,
  correlationMetadata)
    -> TrialAccepted | AlreadyApplied | Failure
```

SubscriptionBilling commits exactly one Trial Subscription and corresponding Trial EntitlementSet for the same logical onboarding source. Equivalent replay returns the accepted result; incompatible source reuse conflicts.

### 4. Use synchronous completion as the fast path, durable work as recovery

Immediately after the Tenancy transaction, the coordinator calls `StartTrialSubscription` synchronously because the user cannot truthfully receive completed onboarding until the Trial exists.

If Trial acceptance is proven, the coordinator conditionally marks the Tenancy onboarding operation `Completed` and returns the completed onboarding result.

If the synchronous call is unavailable/interrupted or completion cannot be proven:

- the API does not claim completed onboarding;
- the already-written work-outbox is relayed idempotently to a single-purpose SQS recovery queue;
- a worker retries the same SubscriptionBilling command using the same logical source identity;
- after Trial acceptance, the worker invokes the Tenancy operation-completion application contract;
- the operation remains queryable and may enter a technical `NeedsAttention` state if bounded automatic recovery cannot proceed.

Queue/retry/NeedsAttention are technical process states only. They do not become Tenant, Membership, or Subscription business states.

### 5. No destructive compensation

A committed Active Tenant/Owner is not deleted merely because Trial creation was delayed or a caller lost the response. The domain baseline does not authorize destructive onboarding rollback.

Until a Trial EntitlementSet exists, subscription-governed ordinary mutations fail closed because no current entitlement authority can be established. This is not Tenant suspension and not Membership disablement.

### 6. HTTP/idempotency behavior

- Completed onboarding may return the normal synchronous creation result only when Active Tenant, Active initial Owner, and Trial acceptance are all proven.
- When the local Tenancy outcome committed but Trial completion is still pending/recovering, return `202 Accepted` with a durable operation identity/status resource under ADR-007 conventions.
- Equivalent client retry uses the same logical onboarding identity and returns the same operation/result.
- Incompatible reuse of the same onboarding idempotency identity returns conflict and creates no second Tenant/Trial.
- A lost response never authorizes a second logical registration.

### 7. Transactional work-outbox to one worker

ADR-006 already distinguishes a direct SQS work item from an EventBridge business fact. This ADR adds the required crash-safety rule for onboarding recovery:

- the work-outbox record is written in the same Tenancy transaction as the local registration outcome;
- a filtered DynamoDB Stream relay sends the stable work item to the named SQS queue;
- relay/delivery can duplicate and therefore the worker command remains idempotent;
- EventBridge is not required because this is one targeted recovery command, not business-fact fan-out;
- the outbox remains queryable for repair after stream retention expires.

## Alternatives considered

### Option A — Put Trial Subscription state in Tenancy

Benefits:
- one transaction and one table.

Costs/risks:
- violates SubscriptionBilling ownership;
- creates duplicate commercial authority;
- couples Merchant Access to commercial lifecycle/provider rules.

Rejected.

### Option B — Cross-module DynamoDB transaction across Tenancy and SubscriptionBilling tables

Benefits:
- one ACID commit for all onboarding outcomes.

Costs/risks:
- persistence coupling across bounded contexts;
- broad IAM and migration coupling;
- contradicts ADR-005's explicit no-cross-domain-ACID strategy;
- makes future module extraction harder.

Rejected.

### Option C — Tenancy commit followed by best-effort synchronous Trial call

Benefits:
- minimal infrastructure.

Costs/risks:
- dual-write gap can permanently strand incomplete onboarding;
- response loss/timeout creates ambiguous completion;
- no durable repair source.

Rejected.

### Option D — Tenancy transaction + idempotent synchronous Trial fast path + durable SQS recovery

Benefits:
- preserves business/persistence ownership;
- user gets synchronous completion in the common case;
- interruption is recoverable without destructive rollback;
- uses existing serverless capabilities and pay-per-use resources;
- explicit durable operation gives honest `202` behavior.

Costs/risks:
- onboarding can temporarily be in a technical pending state;
- adds an outbox relay, queue, worker, operation status, alarms, and recovery procedure.

Chosen.

### Option E — Step Functions for onboarding

Benefits:
- visible durable orchestration and retries.

Costs/risks:
- the process has no approved external wait/branch complexity beyond one recoverable module call;
- adds state-machine/runtime ceremony where a durable operation + one work queue is sufficient;
- transition cost/operational surface is unnecessary for the current process.

Rejected for the initial implementation. A later material expansion may revisit it.

## Consequences

### Positive

- Completed onboarding cannot silently omit the required Trial.
- No domain owns another domain's business state.
- No cross-module persistence transaction is introduced.
- Caller timeout and worker retry are duplicate-safe.
- Recovery remains possible after the synchronous path or stream delivery fails.
- Subscription end/suspension/membership states remain independent after onboarding.

### Negative / trade-offs

- A merchant may briefly see an onboarding operation in `PendingTrial`/recovery rather than immediate completion.
- The first onboarding implementation introduces one small SQS queue/DLQ/worker and an outbox relay path.
- A Tenancy commit can exist while the Trial is temporarily absent; ordinary entitlement-governed mutations therefore fail closed until recovery completes.
- Operations/support needs a pending/NeedsAttention view and redrive/reconciliation procedure.

## Security and tenant impact

- `TrustedOnboardingContext` is built from authenticated verified identity and contains no caller-selected Tenant authority.
- Server assigns Tenant/Membership/Subscription identities; client TenantId never scopes registration.
- The recovery work item carries the minimum stable Tenant/onboarding references and no token, invitation secret, provider secret, or raw personal data.
- Worker IAM receives only the named queue and the application/runtime resources required for its coordinator; it does not obtain permission to bypass module repository boundaries.
- Tenant-visible status remains non-disclosing about any other Tenant.

## Reliability and idempotency impact

- Tenancy registration, Trial start, work relay, worker consumption, and operation completion each have explicit stable logical identities.
- Duplicate stream/SQS delivery and duplicate Trial invocation are safe.
- Queue age/DLQ/retry count never means Trial failed as a business fact.
- Outbox records remain the durable recovery source after DynamoDB Stream retention.
- A reconciliation operation can find Tenancy onboarding operations lacking completed Trial evidence and safely retry the SubscriptionBilling command.

## AWS and cost impact

This ADR selects only already-approved serverless capabilities when onboarding becomes Ready:

- Tenancy DynamoDB table/Stream;
- relay Lambda;
- one small standard SQS queue + DLQ;
- worker Lambda;
- CloudWatch alarms/logs.

No EventBridge bus is required solely for this one targeted recovery command. No Step Functions, NAT Gateway, ALB, EC2, RDS/Aurora, Redis, or always-on compute is introduced.

The ADR itself deploys nothing and changes runtime cost by zero. The implementing task must add queue/relay/worker request assumptions to the cost note; at the learning profile they should remain tiny and pay-per-use.

## Validation

Dependent implementation must prove:

- Tenant + initial Owner local outcome is atomic inside Tenancy;
- SubscriptionBilling state is not in the Tenancy transaction/table;
- a failure after Tenancy commit but before Trial response returns an honest durable pending operation, not completed success;
- queue replay and API retry create one logical Trial;
- incompatible onboarding-idempotency reuse cannot create another Tenant/Trial;
- outbox/relay failure is recoverable from durable pending records;
- Tenant A cannot resolve/update Tenant B onboarding operation by known IDs;
- absent Trial does not fall back to client/JWT entitlement claims;
- CDK contains no EventBridge/Step Functions resource solely for this onboarding path unless a later ADR changes the decision.

## References

- domain reconciliation: `docs/domains/product-decision-reconciliation.md`
- Tenant/Merchant Access baseline: `docs/domains/tenant-identity.md`
- Subscription/Billing baseline: `docs/domains/subscription-billing.md`
- technical reconciliation: `docs/architecture/product-decision-technical-reconciliation.md`
- ADR-005: DynamoDB module ownership and access patterns
- ADR-006: reliable cross-domain integration
- ADR-007: HTTP/version/idempotency conventions
- ADR-008: SubscriptionBilling module/entitlement/provider boundary