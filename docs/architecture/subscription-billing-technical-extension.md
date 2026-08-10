# CommerceOS — Subscription & Billing Technical Architecture Extension

_Reconciled by TASK-0092 on 2026-08-10. This document extends the TASK-0088 technical baseline; it does not replace the domain baseline or resolve `PD-043`–`PD-053`._

## 1. Authority and scope

This document maps the approved Subscription & Billing business boundary from `docs/domains/subscription-billing.md` into implementation architecture.

Authority order remains:

1. accepted ADRs for their stated decisions;
2. `docs/architecture/technical-baseline.md` plus this focused extension;
3. the Subscription & Billing domain baseline for business meaning;
4. `docs/domains/product-decisions.md` for unresolved policy.

A technical mechanism below is not approval of a product policy. Where a behavior depends on `PD-043`–`PD-053`, the mechanism is conditional and implementation work stays non-Ready until the relevant decision is approved.

This task introduces no application code, AWS resource, provider, commercial price, tax rule, billing cycle, entitlement catalog, or lifecycle policy.

## 2. Module and dependency mapping

### 2.1 Implementation module

The accepted bounded context maps initially to one implementation module:

```text
CommerceOS.SubscriptionBilling.Domain
CommerceOS.SubscriptionBilling.Application
CommerceOS.SubscriptionBilling.Contracts   # only when a real delivery/cross-module consumer exists
CommerceOS.SubscriptionBilling.Infrastructure
```

The module hosts the approved business concepts without merging their consistency boundaries:

```text
SubscriptionBilling module
  ├── Plan / accepted commercial terms
  ├── Subscription
  ├── EntitlementSet
  ├── UsageMeter           # only for an approved accumulated meter
  └── PlatformCharge
```

Rules:

- `SubscriptionBilling` is separate from `Tenancy`, `Payments`, `Sales`, `Inventory`, `Accounting`, and `Reporting`.
- It is not a generic licensing/shared-policy module. It owns only CommerceOS merchant subscription/commercial-term/entitlement/approved usage-meter/platform-charge truth.
- Merchant-order `Payments` remains the shopper-to-merchant payment boundary. SaaS PlatformCharge execution never uses `Payments` persistence or its Mock Payment Provider by convenience.
- Domain code has no AWS, HTTP, provider SDK, persistence, queue, clock-host, or framework dependency.
- Other modules may reference only producer-owned `SubscriptionBilling.Contracts` contracts approved in the interaction matrix. They never reference SubscriptionBilling Domain/Application implementation/Infrastructure.
- No empty project is created merely because this document names the module. A Backlog Planner task introduces project structure only when a concrete Ready frontier needs it.

### 2.2 Initial deployment boundary

When the first Ready Subscription & Billing application task exists, its synchronous application surface runs in the existing shared `commerce-api` Lambda by default.

A separate Subscription/Billing Lambda or service is **not** approved merely because the bounded context is separate. Split deployment requires measured or explicit pressure such as provider-secret/IAM isolation, externally facing callback isolation, runtime/failure-profile separation, scaling, or ownership. Such a split updates the deployment ADR if material.

Background metering, provider reconciliation, or event consumers may use worker Lambdas only when those named use cases become Ready.

## 3. Trusted entitlement decision boundary

### 3.1 Trust chain

A subscription entitlement does not replace authentication, Membership authority, or TenantStatus.

For a protected merchant command the trust chain is:

```text
AuthenticatedPrincipal
      ↓
Tenancy.ResolveTenantAuthority
      ↓
TrustedTenantContext
      ↓
owning application capability check
      ↓
SubscriptionBilling.EvaluateEntitlement (when the use case is subscription-governed)
      ↓
owner-local invariant / authoritative usage check
      ↓
owner-local persistence commit
```

`TrustedTenantContext` remains owned by Tenancy/Merchant Access and is not expanded into a cached subscription snapshot. SubscriptionBilling receives Tenant scope from that trusted context and resolves current commercial truth from its own authority.

Never use as entitlement authority:

- a client-supplied TenantId, plan name, entitlement list, limit, price, or billing state;
- a JWT custom claim carrying plan/entitlement data;
- a browser/session cache;
- Reporting or platform-admin projections;
- another module's copied display label;
- external provider-private state.

### 3.2 Producer-owned decision contract

When a named consumer exists, SubscriptionBilling owns a transport-neutral synchronous contract conceptually equivalent to:

```text
EvaluateEntitlement(
  TrustedTenantScope,
  EntitlementKey,
  DecisionMetadata)
    -> EffectiveEntitlementDecision | EntitlementDecisionFailure
```

An effective decision exposes only business meaning needed by the consumer, for example:

```text
EffectiveEntitlementDecision
  tenantId
  entitlementKey
  value                 # capability / bounded limit / explicit Unlimited after policy approval
  entitlementSetId
  subscriptionId
  sourceTermsId
  effectiveFrom
  effectiveUntil?       # only when known
  decisionRevision      # opaque freshness/concurrency provenance
```

It does not expose Plan persistence records, provider objects, DynamoDB keys, or a marketing-plan conditional.

`EntitlementKey` names and the exact allowed/limit/soft/overage semantics are product/domain inputs under `PD-044` and `PD-050`; Architecture does not invent them.

### 3.3 Immediate checks and caching

When a command must know whether a capability is currently permitted before it can truthfully complete, entitlement evaluation is a synchronous in-process application contract in the shared runtime.

Initial rule:

- no cross-request entitlement cache is authoritative;
- no eventually consistent projection is authoritative for a hard-limit write;
- stale projections may support display/reporting only and must carry freshness/provenance;
- failure to establish current entitlement authority fails closed for the subscription-governed mutation with a dependency/state-unavailable result; it never becomes `Unlimited`.

A later cache requires measured pressure plus an explicit staleness/revocation contract and cannot weaken stricter-plan-change correctness.

### 3.4 Hard limits and owner-local truth

For an approved hard limit, enforcement has two independent inputs:

1. current trusted entitlement meaning from SubscriptionBilling;
2. current authoritative usage/state from the owning domain.

The owning domain performs its own command invariant and persistence condition. SubscriptionBilling never counts active Memberships by reading Tenancy storage, never counts Warehouses by reading Inventory storage, and never mutates those resources to make a Tenant compliant.

Examples of owner-local responsibilities, only after the relevant product gate approves the limit:

- Merchant Access owns active-Membership count and last-owner invariants;
- Inventory owns Warehouse/Location state and stock invariants;
- Product Data Ingestion owns source/run eligibility in addition to any subscription capability;
- Sales owns accepted Order truth; SubscriptionBilling may own a derived accumulated UsageMeter only after `PD-051` defines the meter.

### 3.5 Restrictive-plan-change fencing

A synchronous entitlement read by itself cannot make a more restrictive plan change atomic with writes in another module. Cross-module DynamoDB transactions are not allowed as a shortcut.

Therefore, **only when `PD-048`/`PD-050` approve a stricter hard-limit transition**, the implementation must use an explicit owner-coordinated transition protocol rather than “update subscription then hope consumers converge”. The protocol must satisfy these technical invariants:

1. the new stricter EntitlementSet is not reported effective merely because a plan-change request exists;
2. each affected owning domain exposes a producer-owned application contract that can assess current authoritative usage against the proposed constraint;
3. affected commands are fenced by an owner-local transition/constraint revision so a concurrent write cannot race through an older higher limit while the lower limit is being made effective;
4. only the owning domain writes its fence/usage/resource state;
5. the Subscription change keeps a durable transition identity and per-owner acknowledgement/recovery state;
6. duplicate assessment/acknowledgement/finalization is idempotent;
7. an interrupted transition is visible and reconcilable; it is never inferred complete from elapsed time;
8. no remediation request is proof that remediation happened;
9. if the approved product policy cannot tolerate the bounded transition behavior/fencing required for safe convergence, the affected implementation remains blocked for a later architecture decision rather than weakening correctness.

This defines a consistency mechanism, not downgrade timing, grandfathering, automatic remediation, or what constitutes a hard limit. Those remain product decisions.

Step Functions is not selected for this protocol now. If later policy creates durable waits, branching, callbacks, or operator compensation that justify workflow orchestration, a workflow task/ADR may select it under ADR-006.

## 4. Application outcome and failure semantics

Subscription-specific application outcomes remain distinct from authentication/authorization and from provider billing observations.

Minimum technical result classes:

| Outcome | Meaning | Safe handling |
|---|---|---|
| `NotEntitled` | current authoritative EntitlementSet does not allow the governed capability | owner command has no business effect; do not reinterpret as Membership denial |
| `LimitReached` | approved hard limit is reached according to current entitlement plus owner-authoritative usage | owner command has no effect; return current safe limit context only when contract permits |
| `EntitlementAuthorityUnavailable` | current trusted subscription/entitlement truth cannot be established | fail closed for the governed mutation; retry only under bounded dependency policy |
| `CommercialTransitionInProgress` | a safe restrictive transition/fence is unresolved and the operation cannot prove which rule is authoritative | no effect; expose durable/retryable state without claiming plan change completed |
| `PlatformChargeOutcomeUnknown` | CommerceOS cannot prove whether an external SaaS charge committed | persist/reconcile as PlatformCharge truth; **does not automatically change entitlement, Subscription condition, TenantStatus, or Membership** |
| `AlreadyApplied` | equivalent idempotent command/provider evidence was already accepted | return/reference prior logical result; do not duplicate effect |
| `IncompatibleReplay` | the same logical idempotency/source identity is reused with different semantics | deterministic conflict; no effect |

External HTTP routes map these application outcomes through ADR-007. Route-specific problem codes/statuses are defined by the introducing Ready task; Architecture does not use an HTTP code to invent a business lifecycle rule.

## 5. Persistence ownership and access patterns

### 5.1 Physical owner

When persistent Subscription & Billing work becomes Ready, `SubscriptionBilling` owns one DynamoDB table under ADR-005.

```text
SubscriptionBilling module ──owns──► SubscriptionBilling table
```

No other module receives its table name, item model, key codec, GSI, stream, repository, or provider-reference record. The shared `commerce-api` role may have physical IAM grants to multiple module tables, but code/module boundaries still prohibit cross-module persistence access.

Tenant-owned records use tenant-prefixed base partitions. Platform-global Plan catalog records, if `PD-044` approves them, use an explicitly platform-scoped partition and are accessible only through SubscriptionBilling application contracts; their global scope is not permission for a generic unscoped tenant repository.

### 5.2 Symbolic record intent

The exact item layout waits for Ready tasks, but the following ownership/access needs are approved:

| Logical record | Scope | Authority / consistency intent |
|---|---|---|
| `Plan` / accepted `PlanVersion` or terms record | platform-global within SubscriptionBilling | historical accepted meaning must not be rewritten; catalog management waits for `PD-044` |
| `Subscription` | Tenant | strong/current read for entitlement decisions and lifecycle mutation; expected revision |
| immutable `EntitlementSet` | Tenant | effective history + provenance; current pointer/reference changes atomically with accepted Subscription transition within the module |
| `UsageMeter` | Tenant + approved meter/window | conditional/idempotent source application; only when an accumulated meter is approved |
| `PlatformCharge` | Tenant | separate aggregate/revision from Subscription; preserves attempts, provider references, known/unknown observations |
| command/idempotency record | accepting scope | claimed in owning transaction where possible; semantic fingerprint + stable result |
| provider evidence/inbox record | Tenant/PlatformCharge | deduplicates verified callback/query evidence; out-of-order evidence cannot regress known truth |
| integration outbox | source aggregate scope | atomic with committed source fact only when a named consumer exists |

### 5.3 Access-pattern baseline

| ID | Use case | Key/access intent | Consistency / protection | Gate |
|---|---|---|---|---|
| `SUB-AP-01` | get current Tenant subscription | tenant partition + current Subscription identity | strong/current; no GSI authority | acquisition/lifecycle details `PD-043`, `PD-049` |
| `SUB-AP-02` | evaluate current entitlement | current Subscription + current EntitlementSet reference/history in SubscriptionBilling table | strong/transactional as needed so one decision has coherent provenance | keys/semantics `PD-044`, `PD-050` |
| `SUB-AP-03` | list subscription/entitlement history | tenant partition ordered immutable records | bounded page; eventual allowed for display if contract exposes freshness | display refinement |
| `SUB-AP-04` | request/finalize plan change | Subscription + command identity + new EntitlementSet/history records | expected revision + idempotency; requested intent never equals effective change | `PD-047`, `PD-048` |
| `SUB-AP-05` | read/write accumulated UsageMeter | tenant + meter/window key | source-id conditional application; duplicate source cannot increment twice | meter definition `PD-050`, `PD-051` |
| `SUB-AP-06` | create/get/update PlatformCharge | Tenant + PlatformCharge identity | expected revision + logical charge idempotency; timeout may leave Unknown | amount/provider `PD-046`, `PD-052` |
| `SUB-AP-07` | ingest provider callback/evidence | provider-evidence identity mapped to PlatformCharge through module-owned claim/index | verify before definitive interpretation; dedupe; out-of-order non-regression | provider execution `PD-052` |
| `SUB-AP-08` | find charges needing reconciliation | no index until provider execution is approved; then a sparse bounded/sharded operational due-work index may be added | operational IAM only; never tenant list authority; no Scan | `PD-052` + concrete provider semantics |
| `SUB-AP-09` | platform-admin subscription/billing visibility | application query only; no direct table access | separate admin context/IAM/audit; mutations absent by default | `PD-053`, Audit policy |

Every implementation task expands these into the ADR-005 access-pattern ledger with exact query cardinality, consistency, pagination, isolation tests, and cost notes. No speculative single-table trick or GSI is approved by this document.

## 6. Cross-domain integration matrix

| Caller / producer | Owner / consumer | Mechanism/status | Required technical truth | Product gate |
|---|---|---|---|---|
| protected merchant use case | SubscriptionBilling | synchronous `EvaluateEntitlement`; **Conditional** when the use case is subscription-governed | Tenant scope comes only from `TrustedTenantContext`; current EntitlementSet is authoritative | entitlement definition/enforcement `PD-044`, `PD-050` |
| Tenant onboarding / Tenancy | SubscriptionBilling | separate synchronous command or later fact; **Product-gated** | Tenant creation and Subscription activation stay separate facts unless policy explicitly couples them | `PD-043` |
| Merchant Access | SubscriptionBilling | synchronous entitlement decision for staff capability/limit; optional synchronous downgrade assessment; **Product-gated** | Merchant Access owns active-member truth and last-owner invariant | `PD-048`, `PD-050` |
| Inventory | SubscriptionBilling | synchronous entitlement decision for Warehouse/Location capability/limit; optional downgrade assessment; **Product-gated** | Inventory owns Warehouse/Location/stock truth | `PD-048`, `PD-050` |
| Product Data Ingestion | SubscriptionBilling | synchronous capability decision before governed starts; **Product-gated** | source/run policy remains Ingestion-owned; entitlement does not authorize an otherwise forbidden source | `PD-050` |
| Sales accepted order facts | SubscriptionBilling UsageMeter | outbox → EventBridge → SubscriptionBilling SQS/DLQ; **Conditional only if an approved order meter has a named consumer** | idempotent source counting; meter lag never rewrites Sales Order truth | `PD-051` |
| SubscriptionBilling committed facts | Reporting | EventBridge projection; **Conditional** | projection is rebuildable/display-only and never entitlement authority | reporting scope/refinement |
| designated SubscriptionBilling privileged mutation/evidence | Audit | durable audit intent/outbox → Audit; **Product-gated then Conditional** | business success cannot silently omit required Audit evidence when coverage is approved | `PD-033`, `PD-053` |
| platform-admin delivery | SubscriptionBilling | separate synchronous admin query/command contracts; visibility first, mutation absent until approved | never a Tenant bypass flag; no direct DynamoDB access | `PD-053` |
| SubscriptionBilling | external SaaS billing provider | provider port/adapter; **Product-gated** | request idempotency, verified evidence, unknown outcome, query/reconciliation | `PD-046`, `PD-052` |
| external SaaS billing provider | SubscriptionBilling | verified callback/webhook ingress; **Product-gated** | callback is evidence only; duplicate/out-of-order safe; no implicit Subscription effect | `PD-049`, `PD-052` |

No Subscription event is published merely because an aggregate changed. A producer-owned versioned integration fact exists only when a named consumer requires it and uses ADR-006 reliable publication.

## 7. Plan-change consistency and recovery

### 7.1 General rule

A plan-change request, billing attempt, provider callback, remediation request, and entitlement effectivity are separate durable facts/operations.

Never implement plan change as a distributed transaction that directly edits Tenancy/Inventory/Ingestion/Sales data.

### 7.2 Upgrade

Until `PD-047` is approved, Architecture does not choose immediate versus scheduled effectivity or charge preconditions.

Once policy is approved:

- the plan-change command has one stable logical identity;
- any PlatformCharge has a distinct identity and idempotency boundary;
- higher EntitlementSet becomes effective only when the approved condition is proven;
- retries/callback duplicates cannot create another EntitlementSet or charge effect;
- an external charge timeout remains Unknown and cannot be translated into successful/failed upgrade by transport behavior.

If policy allows immediate effectivity with no cross-domain hard-limit restriction, the SubscriptionBilling transition may commit atomically inside its own table and consumers see the new value on the next authoritative decision.

### 7.3 Downgrade / stricter constraints

Until `PD-048` and the affected `PD-050` rules are approved, downgrade execution tasks remain non-Ready.

When approved, target-limit assessment uses synchronous owner contracts or an approved UsageMeter. SubscriptionBilling records the transition and evidence but never substitutes a Reporting count. A target plan that fails the approved precondition remains blocked/remediation-required; architecture does not auto-delete/deactivate foreign resources.

For a stricter hard limit with concurrent writes, use the fencing protocol in section 3.5. Every stage is durable and idempotent, and reconciliation can distinguish:

```text
Requested
AssessmentPending
BlockedByUsage / RemediationRequired   # only when approved domain policy says so
OwnerFencePending
ReadyToFinalize
Finalized
NeedsReconciliation
```

Only names already approved by the domain baseline are business states; other labels above are technical operation-state examples and must not leak as new Subscription domain conditions. A Ready task chooses transport-visible operation codes without changing business semantics.

## 8. External billing-provider seam

`PD-052` is unresolved, so no real or simulated provider is selected and no provider AWS resources are provisioned.

The stable architecture seam is:

```text
SubscriptionBilling.Application
      │ IPlatformBillingPort
      ▼
SubscriptionBilling.Infrastructure
      │ provider adapter / protocol mapping
      ▼
external SaaS billing provider (future)
```

Rules when provider execution is approved:

- Domain types do not contain provider SDK objects or HTTP response models.
- A PlatformCharge/attempt is durably identified before an unsafe external retry.
- The adapter sends a provider-supported idempotency identity derived from one logical PlatformCharge operation, not a correlation ID.
- Timeout/network cancellation/5xx without conclusive provider semantics leaves an explicit Unknown observation.
- Unsafe retry waits for provider query/reconciliation semantics; elapsed time alone proves nothing.
- Callback/webhook ingress authenticates/verifies provider evidence before it can produce a definitive observation.
- Duplicate callback delivery returns the prior logical interpretation; out-of-order evidence cannot regress a later verified state.
- Provider customer/subscription/invoice/payment identifiers are references/evidence inside SubscriptionBilling only.
- No raw card data, CVV, merchant secret, signing secret, or prohibited provider credential is stored in domain state/logs/fixtures.
- A provider secret/configuration service is selected only in the concrete provider implementation task with IAM, rotation, local-development, and cost analysis.
- Reconciliation has a durable query/status path and bounded operator recovery procedure before production use.

A provider callback may use a distinct delivery endpoint/function if the introducing task demonstrates external-ingress, secret/IAM, rate-limit, or failure-isolation pressure. That deployment choice is not pre-created now.

## 9. AWS and CDK mapping

TASK-0092 adds no AWS service. It reuses accepted capabilities conditionally:

| Need | AWS mapping | Status |
|---|---|---|
| SubscriptionBilling transactional state | module-owned DynamoDB table in `CommerceStack` | **Conditional** with first Ready persistence task; governed by ADR-005 |
| shared synchronous entitlement evaluation | existing `commerce-api` Lambda | **Conditional** with first governed use case; no network hop required |
| reliable subscription facts | DynamoDB outbox/Stream → EventBridge → consumer SQS/DLQ | **Conditional** with a named consumer; ADR-006 |
| accumulated order-usage consumer | SubscriptionBilling SQS/DLQ + worker Lambda | **Product-gated/Conditional** by `PD-051` |
| provider webhook ingress | API Gateway/Lambda delivery boundary | **Product-gated** by `PD-052`; exact isolation deferred |
| provider reconciliation polling | bounded worker plus EventBridge Scheduler only if provider semantics require periodic reconciliation | **Product-gated/Conditional** by `PD-052`; no schedule now |
| durable multi-step orchestration | Step Functions Standard | **Deferred**; requires approved sequence and demonstrated orchestration pressure |
| provider credentials/signing material | AWS-managed secret/config facility | **Technically deferred** until a provider is selected and requirements are known |
| logs/metrics/alarms | CloudWatch built-ins first | **Accepted** when runtime exists; bounded retention/low cardinality |

No NAT Gateway, ALB, EC2, RDS/Aurora, Redis, OpenSearch, Kafka/MSK, EKS, always-on service, or provisioned Lambda concurrency is introduced for Subscription & Billing.

### Cost impact

TASK-0092 itself changes documentation only: runtime monthly cost change is zero.

Later SubscriptionBilling cost follows the existing serverless cost model:

- one additional low-volume module DynamoDB table when business persistence is Ready;
- no stream/event bus/queue/worker until a named asynchronous consumer exists;
- no periodic reconciliation schedule until provider execution exists;
- no paid billing platform/provider is selected by Architecture;
- each introduced GSI, stream, queue, callback worker, schedule, secret facility, and external provider cost must be included in the Ready task cost note.

## 10. Security and tenant isolation

- SubscriptionBilling tenant-facing APIs require `TrustedTenantContext`; request TenantId never scopes persistence.
- Entitlement evaluation accepts trusted Tenant scope and returns the TenantId from authoritative state, not from the requested target.
- Plan/global catalog administration is a separate platform path and never creates cross-tenant mutation authority.
- Platform-admin visibility/mutation uses a dedicated context and explicit SubscriptionBilling application contract, not `isAdmin`, `bypassTenant`, direct table access, or Owner Membership in every Tenant.
- Provider callbacks use provider-authenticated evidence, not merchant authentication; they still resolve to a known module-owned PlatformCharge/Tenant reference before state mutation.
- Secrets/tokens/raw provider payloads are minimized, redacted, and never copied into integration events for convenience.
- Any tenant-visible failure is non-disclosing about another Tenant's subscription/charge existence.

## 11. Reliability, observability, and reconciliation

Every retryable external command and provider evidence path has separate identities for request attempt, command/idempotency, correlation, PlatformCharge, event, and causation.

Required operational visibility when the relevant runtime is introduced:

- entitlement decision unavailable/error counts without high-cardinality Tenant dimensions;
- plan-change technical operation age/state when a multi-step transition exists;
- provider attempt Unknown age/count;
- callback verification/deduplication failures;
- reconciliation due/failed/succeeded counts;
- queue oldest-message/DLQ depth where async consumers exist;
- outbox relay lag/error where integration facts exist.

Logs never claim a business transition that the owning aggregate did not commit. Queue age, retry count, callback receipt, or reconciliation execution time are telemetry, not Subscription/PlatformCharge facts.

Recovery rules:

- retry equivalent commands using the same logical identity;
- do not retry stale business revisions blindly;
- preserve original provider/source/event identity through redrive;
- use module-owned durable operation/outbox/inbox records for repair, never another module's table;
- Unknown external outcome remains Unknown until verified evidence resolves it;
- operator support actions, when `PD-053` permits them, go through explicit application commands and required Audit evidence.

## 12. Product gates preserved

| Gate | Architecture deliberately does not choose |
|---|---|
| `PD-043` | acquisition flow, automatic trial, default plan, Tenant-without-subscription eligibility |
| `PD-044` | offered plans/prices/terms/version lifecycle/Enterprise policy and concrete entitlement keys |
| `PD-045` | monthly/annual cycle, anchors, renewal/timezone/period semantics |
| `PD-046` | SaaS currency, tax, invoice legal meaning, proration |
| `PD-047` | upgrade effective time and charge precondition |
| `PD-048` | downgrade timing, grandfathering/overage/remediation policy |
| `PD-049` | cancellation, grace, delinquency, reactivation, access restriction, retention |
| `PD-050` | which entitlements are hard/soft/overage/unlimited and exact enforcement/recovery behavior |
| `PD-051` | order-volume meter/window and shopper-checkout impact |
| `PD-052` | domain-only vs simulated vs real SaaS billing provider and provider selection |
| `PD-053` | platform-admin mutation/override authority and consent/Audit obligations |

Architecture must be revisited after a decision only when the approved policy creates a materially new consistency, security, provider, or deployment need. Product approval alone does not authorize a Builder to bypass the Ready gate.

## 13. Handoff to TASK-0089

### Technically resolved for backlog generation

TASK-0089 may treat these as accepted architecture constraints when generating/refining Subscription & Billing tasks:

- one `SubscriptionBilling` implementation module, initially inside the shared commerce runtime;
- separate ownership from merchant-order `Payments` and Tenancy;
- producer-owned synchronous entitlement decision contract for immediate governed commands;
- no entitlement authority in JWT/client/cache/Reporting projections;
- owner-local authoritative usage combined with trusted entitlement for hard limits;
- explicit fencing/reconciliation requirement for a stricter cross-module hard-limit transition once product policy approves such a transition;
- one module-owned DynamoDB table and the `SUB-AP-*` access needs under ADR-005;
- no cross-module persistence reads/writes or cross-domain DynamoDB transaction;
- ADR-006 reliable async facts only for named consumers;
- provider-agnostic application port/infrastructure adapter with idempotency, verified callback evidence, Unknown outcome, and reconciliation;
- no new always-on AWS service; existing serverless capabilities are conditional only when a Ready consumer exists.

### Work that must remain product-blocked/non-Ready

Do not mark implementation tasks Ready when they require unresolved semantics from:

- subscription acquisition/trial/ordinary-commerce gating (`PD-043`);
- concrete plan catalog/terms/entitlement definitions (`PD-044`);
- recurring period/renewal logic (`PD-045`);
- priced charge/invoice/tax/proration (`PD-046`);
- upgrade execution/effectivity (`PD-047`);
- downgrade/remediation execution (`PD-048`);
- cancellation/expiry/delinquency/reactivation/retention (`PD-049`);
- hard/soft/overage/unlimited enforcement policy (`PD-050`);
- order-volume meter affecting checkout/billing (`PD-051`);
- simulated/real provider execution/webhook/reconciliation (`PD-052`);
- platform-admin subscription mutation/override (`PD-053`).

TASK-0089 may create Outline/Refined placeholders for these areas and may create architecture/foundation tasks that do not encode a gated policy, but it must not manufacture defaults to increase the Ready frontier.

## 14. Verification checklist for dependent implementation

When the corresponding code-bearing task later exists, verification must cover the applicable subset:

- SubscriptionBilling Domain has no AWS/framework/provider dependency;
- another module cannot reference SubscriptionBilling Domain/Infrastructure or table/key types;
- Tenant A cannot evaluate/read/mutate Tenant B subscription state with a known ID, cursor, provider reference, or request TenantId;
- client/JWT plan or entitlement data cannot authorize a command;
- authoritative entitlement failure does not fall back to cache/Reporting/UI values;
- duplicate plan-change/UsageMeter/PlatformCharge/provider-callback identities create one logical effect;
- concurrent hard-limit writes and restrictive-plan-change fencing cannot end in a falsely finalized lower limit;
- timeout/provider 5xx/missing callback cannot become definitive charge failure or subscription end without verified evidence and approved policy;
- out-of-order provider/event evidence cannot regress a later known state;
- async event replay/DLQ redrive preserves source/event identity;
- no implementation path reads another module's persistence for usage/remediation/reconciliation;
- CDK contains no speculative stream/event bus/queue/schedule/secret resource and no unapproved standing-cost service;
- cost notes cover every newly introduced table/index/worker/schedule/provider dependency.

## 15. Conclusion

`TECHNICAL BASELINE RECONCILED`

The technical boundary for Subscription & Billing is sufficiently explicit for TASK-0089 to generate/refine a V2 task graph without asking Builders to invent module ownership, entitlement trust, persistence ownership, cross-domain consistency, provider uncertainty, AWS mapping, or recovery strategy.

The remaining inability to implement commercial behavior is intentional and traceable to `PD-043`–`PD-053`, not an architecture gap.
