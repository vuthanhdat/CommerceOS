# ADR-009 — Cross-Domain Onboarding Completion and Trial-Bootstrap Recovery

Status: Accepted
Date: 2026-08-10
Decision owners: CommerceOS Technical Architecture
Supersedes: the Tenancy-only onboarding-completion portion of ADR-005/TASK-0088
Superseded by: N/A

## Context

The reconciled domain baseline defines successful merchant onboarding as three accepted outcomes with separate business owners:

```text
Tenant Management      -> Active Tenant
Merchant Access        -> Active initial Owner Membership
Subscription & Billing -> 30-day Trial Subscription + Trial EntitlementSet
```

The earlier technical baseline could commit the first two outcomes atomically because Tenant Management and Merchant Access share the `Tenancy` implementation module/table. It therefore described completed onboarding as one Tenancy transaction.

That is no longer sufficient. `SubscriptionBilling` is a separate module/persistence owner under ADR-008/ADR-005. A cross-module DynamoDB transaction would weaken bounded-context persistence ownership, while a Tenancy commit followed by a best-effort Trial call can strand a local Tenant/Owner and falsely report onboarding complete.

The architecture needs durable, idempotent completion/recovery without moving Subscription truth into Tenancy or inventing destructive rollback semantics.

## Decision

### 1. Preserve separate business ownership

`Tenancy` owns Tenant/Membership state. `SubscriptionBilling` owns Trial Subscription/EntitlementSet state.

No module reads/writes the other's table and no cross-module DynamoDB transaction is used.

### 2. Tenancy commits its local outcome and durable operation

For one logical registration, Tenancy atomically writes:

```text
onboarding operation / idempotency claim
+ Active Tenant
+ Active initial Owner Membership
+ current authority lookup
+ subject-membership discovery record
+ active-owner guard
+ required source-owned Audit intent when applicable
+ Trial-bootstrap WORKOUTBOX record
```

All are Tenancy-owned business/technical records. The work item contains only stable contract data required to invoke SubscriptionBilling; it is not copied Subscription state.

### 3. Start Trial through a SubscriptionBilling application contract

Conceptual producer-owned command:

```text
StartTrialSubscription(
  tenantId,
  onboardingOperationId,
  sourceIdentity,
  correlationMetadata)
    -> TrialAccepted | AlreadyApplied | Failure
```

SubscriptionBilling atomically creates exactly one Trial Subscription + Trial EntitlementSet for the same logical onboarding source. Equivalent replay returns the accepted result; incompatible source reuse conflicts.

### 4. Synchronous fast path, durable recovery path

Immediately after the Tenancy transaction, the coordinator calls `StartTrialSubscription` synchronously because the caller cannot truthfully receive completed onboarding until Trial acceptance exists.

If Trial acceptance is proven, the coordinator conditionally marks the Tenancy onboarding operation `Completed` and returns the completed result.

If the synchronous call is unavailable/interrupted or completion cannot be proven:

- API does not claim completed onboarding;
- the already-written work-outbox is relayed idempotently through DynamoDB Streams to a single-purpose SQS recovery queue;
- the worker retries the same SubscriptionBilling command with the same logical source identity;
- after Trial acceptance the worker invokes the Tenancy operation-completion application contract;
- bounded automatic recovery may stop in a technical `NeedsAttention` operation state without manufacturing a Tenant/Subscription business transition.

Queue age, retry count, DLQ, and `NeedsAttention` are technical states only.

### 5. No destructive compensation

A committed Active Tenant/Owner is not deleted merely because Trial creation is delayed or a caller loses the response. The domain baseline does not authorize destructive onboarding rollback.

Until Trial entitlement authority exists, subscription-governed ordinary mutations fail closed. This is not Tenant suspension and not Membership disablement.

### 6. HTTP and idempotency behavior

- return normal synchronous creation success only after Active Tenant + Active initial Owner + Trial acceptance are all proven;
- when Tenancy committed but Trial completion is still recovering, return `202 Accepted` with durable operation identity/status under ADR-007;
- equivalent client retry returns the same logical operation/result;
- incompatible reuse of onboarding idempotency identity returns conflict and creates no second Tenant/Trial;
- lost response never authorizes a second logical registration.

### 7. Transactional work-outbox to one worker

ADR-006 distinguishes one-worker work from business-fact fan-out. Onboarding recovery uses:

```text
Tenancy transaction
  local state + WORKOUTBOX
      ↓ DynamoDB Stream
idempotent relay
      ↓
SQS recovery queue
      ↓
worker -> SubscriptionBilling.StartTrialSubscription
```

Rules:

- work item is a command/request, not a business fact;
- duplicate relay/queue delivery is expected;
- EventBridge is unnecessary because there is one known target and no fan-out;
- outbox remains queryable for repair after stream retention expires.

## Alternatives considered

### Option A — Put Trial state in Tenancy

Rejected because it creates duplicate commercial authority and violates SubscriptionBilling ownership.

### Option B — Cross-module DynamoDB transaction

Rejected because it couples bounded-context persistence/IAM/migration and contradicts ADR-005's no-cross-domain-ACID rule.

### Option C — Tenancy commit followed by best-effort Trial call

Rejected because it creates an unrecoverable dual-write gap and ambiguous completion.

### Option D — Tenancy local transaction + synchronous Trial fast path + transactional SQS recovery

Chosen because it preserves ownership, provides common-case synchronous completion, survives interruption, and uses existing pay-per-use services.

### Option E — Step Functions for onboarding

Rejected initially because one recoverable cross-module call does not justify a state machine. Durable operation + one targeted queue is sufficient. A later materially more complex onboarding process may revisit this.

## Consequences

### Positive

- completed onboarding cannot silently omit the required Trial;
- no domain owns another domain's business state;
- no cross-module persistence transaction;
- caller timeout/worker retry duplicate-safe;
- recovery remains possible after synchronous/stream/queue interruption;
- Subscription/Tenant/Membership lifecycles remain independent after onboarding.

### Negative / trade-offs

- onboarding may briefly be a durable pending operation;
- implementation adds outbox relay, SQS/DLQ, worker, operation status, alarms, and recovery procedure;
- Tenancy may exist while Trial is temporarily absent, so governed mutations fail closed during recovery;
- operations/support needs visibility for old Pending/NeedsAttention operations.

## Security and tenant impact

- `TrustedOnboardingContext` is built from authenticated verified identity and contains no caller-selected Tenant authority;
- server assigns Tenant/Membership/Subscription identities;
- work item carries minimum Tenant/onboarding references and no token/invitation secret/provider secret/raw personal payload;
- worker IAM is limited to named queue/runtime capabilities and does not permit module-boundary bypass;
- operation status is tenant/non-tenant scoped by trusted accepted identities and remains non-disclosing.

## Reliability and operability impact

- Tenancy registration, Trial start, relay, worker consumption, and operation completion each use stable logical identities;
- duplicate Stream/SQS delivery and duplicate Trial invocation are safe;
- queue age/DLQ/retry count never means Trial business failure;
- outbox remains durable recovery source after Stream retention;
- reconciliation can find incomplete onboarding operations and retry the same SubscriptionBilling command without foreign-table access;
- alarms cover oldest pending operation/work item, relay errors, queue age/DLQ, worker failures, and reconciliation gaps.

## Cost impact

This ADR itself deploys nothing and changes runtime cost by zero.

When onboarding becomes Ready, conditional resources are limited to:

- Tenancy DynamoDB Stream;
- relay Lambda;
- one small Standard SQS queue + DLQ;
- worker Lambda;
- CloudWatch alarms/logs.

No EventBridge bus or Step Functions resource is required solely for onboarding. The implementing task must include queue/relay/worker request/log assumptions in its cost note.

## Reversibility / migration

- A later orchestration mechanism may replace the work-queue coordinator if it preserves stable onboarding/Trial source identities and honest pending/completed semantics.
- Moving SubscriptionBilling to a separate service changes transport/deployment but not ownership or idempotency contracts.
- Changing outbox dispatch from Stream relay requires a cutover that preserves pending work records and logical work IDs.
- Existing completed operations need no migration if the coordinator implementation changes; pending operations require explicit resume/replay handling.
- No migration may collapse Subscription business state into Tenancy merely to simplify the process.

## Validation

Dependent implementation must prove:

- Tenant + initial Owner local outcome is atomic inside Tenancy;
- SubscriptionBilling state is not in the Tenancy transaction/table;
- failure after Tenancy commit but before Trial response returns durable pending status, not completed success;
- queue/API replay creates one logical Trial;
- incompatible idempotency reuse cannot create another Tenant/Trial;
- outbox/relay failure is recoverable from durable pending records;
- Tenant A cannot resolve/update Tenant B operation by known IDs;
- absent Trial does not fall back to client/JWT entitlement claims;
- CDK contains no EventBridge/Step Functions resource solely for onboarding unless a later ADR changes the decision.

## References

- `docs/domains/product-decision-reconciliation.md`
- `docs/domains/tenant-identity.md`
- `docs/domains/subscription-billing.md`
- `docs/architecture/product-decision-technical-reconciliation.md`
- ADR-005 — DynamoDB module ownership/access patterns
- ADR-006 — reliable integration/work delivery
- ADR-007 — HTTP/version/idempotency conventions
- ADR-008 — SubscriptionBilling boundary