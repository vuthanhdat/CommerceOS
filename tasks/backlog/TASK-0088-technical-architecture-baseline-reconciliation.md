# TASK-0088 — Reconcile technical architecture baseline

Status: Backlog
Specification maturity: Ready
Owner: Technical Architect
Created: 2026-08-09
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
- Material ADRs accepted? Existing ADRs are inputs; additional ADRs may be outputs
- Remaining planning blockers: human architecture decisions discovered during reconciliation

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

- Authentication: define trusted identity boundary and Cognito integration responsibilities without implementing them
- Authorization: define where role/permission checks occur
- Tenant scoping: define trusted TenantContext propagation and persistence/API constraints
- Sensitive data/secrets: define configuration/secret boundary where needed
- Abuse/rate-limit considerations: define API edge/resource guardrails where near-term relevant

## Reliability and idempotency impact

- Retry behavior: define conventions for relevant sync/async boundaries
- Timeout semantics: define external-call ambiguity rules where relevant
- Duplicate-delivery behavior: preserve at-least-once assumptions
- Idempotency key/strategy: define common pattern/ownership for near-term commands
- DLQ/recovery/reconciliation: define when these mechanisms are required, not implementation

## Observability impact

- Logs: define structured logging conventions
- Metrics: define baseline service/business metric policy
- Traces/correlation: define correlation/causation propagation
- Operational states/errors: define stable error/operation-state conventions

## Cost impact

- Request/compute impact: none from design task
- Storage impact: repository documentation only
- Network impact: none
- New AWS resources/services: none deployed
- Free Tier allowance relevant to this task: all proposed architecture must honor existing guardrails
- Expected monthly cost change or `negligible` with rationale: negligible for this task
- Estimated one-off cloud-test/load-test cost, if any: none

## Test plan

- Unit: N/A
- Integration: N/A
- Architecture: consistency review against domain baseline and architecture rules
- Contract: review API/event/application contract ownership
- IaC: verify proposed boundaries align with CDK source-of-truth policy
- E2E/manual: human review of high-consequence architecture decisions
- **Cloud verification required?** No — design-only task
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A
