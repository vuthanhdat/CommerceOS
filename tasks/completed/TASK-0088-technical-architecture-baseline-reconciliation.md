# TASK-0088 — Reconcile technical architecture baseline

Status: Completed
Specification maturity: Completed
Owner: Technical Architect
Created: 2026-08-09
Completed: 2026-08-10
Depends on: TASK-0087, current serverless architecture docs, accepted ADRs

## Goal

Produce an implementation-useful technical architecture baseline so near-term tasks can be implemented without Builders inventing module boundaries, contracts, persistence strategy, or AWS integration choices.

## Business context

The current serverless architecture is intentionally high-level and the generated backlog sometimes assumes concrete technical decisions. This task reconciles those assumptions against the refined business-domain model and Free Tier constraints.

## Planning readiness

- Owning domain/bounded context: Engineering/Architecture
- Domain invariants required by this task: supplied by TASK-0087
- Aggregate/entity/value-object decisions resolved? Yes for near-term scope — TASK-0087
- State/error semantics resolved? Yes for near-term scope — TASK-0087
- Cross-domain ownership/contracts resolved? Business ownership from TASK-0087; technical contracts are an output
- Module/layer ownership resolved? N/A — output of this task
- Sync/async interaction decision resolved? N/A — output of this task
- Transaction/consistency boundary resolved? N/A — output of this task
- Persistence ownership/access patterns resolved? N/A — output of this task
- Material ADRs accepted? Existing ADRs are inputs; additional ADRs are outputs
- Remaining planning blockers: product/domain decisions explicitly recorded by TASK-0087 or exposed by this reconciliation; they do not authorize Technical Architecture to invent semantics

## In scope

- reconcile modular-monolith/serverless module boundaries with the domain baseline;
- define near-term module/layer dependency rules;
- define trusted tenant-context flow and authorization boundary;
- define API/error/idempotency/correlation conventions needed by first implementation slices;
- define cross-domain sync/async interaction matrix for near-term capabilities;
- define persistence ownership and required DynamoDB access-pattern strategy without speculative over-optimization;
- define transaction/consistency boundaries for near-term Tenant/Identity and Catalog work;
- confirm Lambda/API/CDK deployment boundaries only where justified;
- reconcile EventBridge/SQS/Step Functions usage rules with actual domain needs;
- identify and create/update ADRs for material decisions;
- verify all choices respect Free Tier/credit guardrails.

## Out of scope

- feature implementation;
- full detailed persistence design for distant unscheduled capabilities;
- extracting microservices;
- adding AWS resources;
- rewriting all candidate backlog tasks.

## Acceptance criteria

### AC01 — Near-term implementation architecture

Given Tenant/Identity and Catalog are the first business frontiers
when their technical architecture is read
then module ownership, dependency direction, API/application boundaries, persistence ownership/access needs, and tenant context are explicit enough for task refinement.

### AC02 — Integration matrix

Given later domains interact
when the architecture baseline is reviewed
then the intended sync/async mechanism is explicit at least for near-term dependencies and high-risk flows, with unresolved/distant choices left intentionally deferred rather than guessed.

### AC03 — AWS rationale

Given an AWS service appears in the architecture
when the baseline is reviewed
then the business/technical problem it solves, Free Tier implications, and decision status are explicit.

### AC04 — ADR completeness

Given reconciliation exposes a material architectural choice
when the task completes
then it is captured by an ADR or explicit deferred-decision record before dependent tasks become Ready.

## Architecture impact

- Owning domain: Engineering/Architecture
- Domains touched: all; detailed focus near-term Tenant/Identity and Catalog
- Persistence impact: defines ownership/access-pattern strategy, no runtime data change
- Events/contracts impact: defines near-term technical contracts and event policy
- AWS/IaC impact: reconciles service/deployment decisions, no deployment
- ADR required? Yes where reconciliation introduces or changes material decisions

## Security and tenant impact

- Authentication: Cognito/API Gateway prove external identity only; current Tenant authority remains Merchant Access-owned
- Authorization: protected application use cases consume current trusted authority/capabilities rather than role/Tenant claims
- Tenant scoping: trusted TenantContext is resolved per protected request and repository scope is derived only from that trusted context
- Sensitive data/secrets: no runtime secrets introduced; secret/configuration service selection remains deferred until a real credential is required
- Abuse/rate-limit considerations: request/page/idempotency/correlation/retry/concurrency bounds are required in introducing tasks

## Reliability and idempotency impact

- Retry behavior: stable command-level retry/idempotency conventions are defined; stale business mutations are not blindly retried
- Timeout semantics: timeout/transport failure never proves an independent business failure
- Duplicate-delivery behavior: at-least-once assumptions and producer/relay/consumer idempotency remain mandatory
- Idempotency key/strategy: externally retryable commands use actor/Tenant-scoped semantic idempotency identities rather than correlation IDs
- DLQ/recovery/reconciliation: conditional async consumers require durable outbox/inbox, queue/DLQ, identity-preserving redrive, and reconciliation

## Observability impact

- Logs: structured safe module/operation/outcome/correlation fields; no secrets/tokens/raw sensitive payloads
- Metrics: built-in AWS metrics first; custom metrics remain low-cardinality
- Traces/correlation: request, command/idempotency, correlation, event, causation, and aggregate identities remain distinct
- Operational states/errors: stable HTTP/problem and technical retry classes are separated from business facts

## Cost impact

- Request/compute impact: none from the design task
- Storage impact: repository documentation only
- Network impact: none
- New AWS resources/services: none deployed
- Free Tier allowance relevant to this task: all accepted/conditional target services were checked against existing guardrails
- Expected monthly cost change or `negligible` with rationale: zero runtime cost for TASK-0088; later resources are conditional and use the existing cost model
- Estimated one-off cloud-test/load-test cost, if any: none

## Test plan

- Unit: N/A
- Integration: N/A
- Architecture: consistency review against TASK-0087, architecture rules, accepted ADRs, and no-cross-module-persistence rules
- Contract: review API/event/application contract ownership, versioning, error, idempotency, concurrency, and tenant-context rules
- IaC: verify proposed boundaries align with CDK source-of-truth and conditional-resource policy
- E2E/manual: review high-consequence architecture decisions and explicit domain/product deferrals
- **Cloud verification required?** No — design-only task
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

## Completion summary

### What changed

- Established the canonical technical baseline in `docs/architecture/technical-baseline.md`, preserving the modular-monolith direction while mapping the TASK-0087 bounded contexts to implementation modules, layers, contracts, runtime, and CDK boundaries.
- Chose a first-frontier `Tenancy` implementation module that hosts Tenant Management and Merchant Access as distinct model areas so the approved Active Tenant + initial Active Owner onboarding outcome can commit atomically without merging their business ownership.
- Kept `Catalog` as an independent implementation module and persistence owner; future contexts are created only when a Ready task introduces a real need.
- Defined trusted execution contexts and current Membership/Tenant authority resolution so Cognito authentication, client selectors, JWT custom claims, and cached role/Tenant values cannot become tenant authority.
- Defined versioned external HTTP/problem, optimistic-concurrency, idempotency, pagination, correlation, and causation conventions in `docs/architecture/first-frontier-contracts.md` and ADR-007.
- Defined one DynamoDB table per implementation module, tenant-first repository contracts, mandatory access-pattern ledgers, conditional/transactional invariant protection, and first-frontier Tenancy/Catalog access patterns in `docs/architecture/persistence-access-patterns.md` and ADR-005.
- Defined the sync/async interaction matrix and reliable cross-domain outbox → DynamoDB Stream → EventBridge → consumer SQS/DLQ pattern, while deferring Step Functions until an approved business sequence demonstrates durable orchestration pressure, in `docs/architecture/integration-and-aws.md` and ADR-006.
- Reconciled AWS service purpose, introduction trigger, single-region/stack mapping, IAM boundaries, observability, and Free Tier/cost posture without deploying infrastructure.
- Preserved every TASK-0087 product decision gate and exposed one additional domain decision required for public storefront Tenant addressing instead of inventing its owner/slug/domain semantics.

### Material ADRs

- ADR-003 — first-frontier modular runtime and deployment boundaries.
- ADR-004 — trusted Tenant authority and authorization boundary.
- ADR-005 — DynamoDB module ownership and access-pattern strategy.
- ADR-006 — reliable cross-domain integration and deferred workflow orchestration.
- ADR-007 — versioned HTTP contract and command-safety conventions.

### Acceptance criteria status

- AC01: PASS — Tenancy/Catalog module ownership, dependency direction, trusted context, application/contract boundaries, persistence ownership/access patterns, and consistency boundaries are explicit enough for Backlog Planner refinement.
- AC02: PASS — near-term interactions and high-risk later flows have explicit synchronous/conditional asynchronous/product-gated/deferred mechanisms; no unresolved business sequence was guessed.
- AC03: PASS — every target AWS capability in the integration/service matrix has a named problem, introduction status/trigger, and Free Tier/cost guardrail; TASK-0088 deploys none.
- AC04: PASS — all material architecture decisions produced by this task are recorded in ADR-003 through ADR-007; remaining choices are tied to explicit product gates or technical trigger/deferred records.

### Verification

- Architecture consistency review against TASK-0087: PASS — no technical artifact changes bounded-context fact ownership or resolves a pending `PD-*` business decision.
- Tenant/security review: PASS — authentication and current Membership authority are separated; protected repository scope comes only from trusted Tenant context; cross-tenant not-visible behavior remains non-disclosing.
- Persistence/integration review: PASS — no cross-module table access, application Scan, best-effort dual write, or speculative EventBridge/SQS/Step Functions resource is approved.
- ADR/link reconciliation: PASS for the previously missing contract ADR — `ADR-007-versioned-http-contract-and-command-safety-conventions.md` now exists and matches the contract baseline.
- Cloud verification: N/A — documentation/ADR-only task; no AWS resource was created and no teardown is required.
- `python3 scripts/harness_check.py`: not executable from this connector-only session because no runnable repository checkout is available; the container also cannot resolve GitHub to clone the repository. No GitHub commit-status checks are attached to the connector-created commits. This limitation is recorded rather than represented as a passing harness run.

### Architecture, security, and cost notes

- Architecture: module, deployment, persistence, contract, sync/async, and workflow selection rules are now explicit; the first runtime remains one shared `commerce-api` Lambda, not microservices.
- Security/tenant: Cognito proves identity only; Merchant Access resolves current tenant authority on each protected request initially; client TenantId/role claims cannot authorize data access.
- Reliability: first-frontier work stays synchronous/local where immediate truth is required; future independent effects use durable publication, idempotent consumers, and honest recovery states.
- Cost: this task has zero runtime/monthly AWS cost; accepted target services are pay-per-use/Free-Tier-aligned and created only when a Ready task needs them.

### Follow-up items

- TASK-0089/Backlog Planner must reconcile candidate task dependencies/maturity against this baseline and keep all `PD-*`-gated work non-Ready.
- Domain Architect/Product Owner must define public Storefront Tenant addressing before public route/cache/index contracts become Ready.
- Later payment, accounting, Step Functions, multi-region, public-search/cache, and platform-admin decisions remain deferred until their documented business/technical triggers exist.
- A runnable checkout should execute `python3 scripts/harness_check.py` before the next code-bearing task treats repository-level verification as green.