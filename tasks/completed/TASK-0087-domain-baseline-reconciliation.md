# TASK-0087 — Reconcile business-domain baseline

Status: Completed
Specification maturity: Completed
Owner: Domain Architect
Created: 2026-08-09
Depends on: Product definition, NFRs, current `docs/02-business-domains.md`

## Goal

Produce an implementation-useful business-domain baseline so near-term CommerceOS tasks no longer require Builders to invent domain ownership, invariants, or state semantics.

## Business context

The current domain document is a useful high-level map, but the generated implementation backlog assumes details that have not all been explicitly approved. This task establishes the missing domain runway before business implementation continues.

## Planning readiness

- Owning domain/bounded context: Cross-domain planning task
- Domain invariants required by this task: existing product/NFR/domain principles
- Aggregate/entity/value-object decisions resolved? N/A — this task resolves the required near-term decisions
- State/error semantics resolved? N/A — this task resolves them where needed
- Cross-domain ownership/contracts resolved? N/A — business ownership is an output
- Module/layer ownership resolved? N/A — Technical Architect handles that in TASK-0088
- Sync/async interaction decision resolved? N/A — technical concern for TASK-0088
- Transaction/consistency boundary resolved? N/A — technical concern for TASK-0088 informed by invariants
- Persistence ownership/access patterns resolved? N/A — technical concern for TASK-0088
- Material ADRs accepted? N/A — domain decisions may create ADR candidates but not infrastructure ADRs
- Remaining planning blockers: human product decisions discovered during domain analysis

## In scope

- review and refine the overall bounded-context/domain map;
- define implementation-relevant ownership and invariants for the first delivery frontier, especially Tenant/Identity and Catalog;
- refine Sales, Inventory, Payment, Procurement, Accounting, Reporting, and Product Data Ingestion to sufficient medium-depth runway for dependency planning;
- identify aggregate roots/entities/value objects where the distinction materially affects near-term tasks;
- define state transitions and business error semantics where required by existing candidate tasks;
- identify business facts/events and distinguish them from technical events;
- produce an explicit unresolved product-decision list instead of guessing.

## Out of scope

- application code;
- AWS service selection;
- DynamoDB key/schema design;
- CDK/runtime deployment design;
- rewriting all 83 task specifications;
- detailed design of distant unscheduled features beyond what dependency planning needs.

## Acceptance criteria

### AC01 — Ownership clarity

Given the near-term Tenant/Identity and Catalog candidate tasks
when the domain baseline is read
then the owning bounded context, relevant aggregate ownership, invariants, and business states are explicit enough that Technical Architect does not need to invent business semantics.

### AC02 — Cross-domain facts

Given Sales, Inventory, Payment, Procurement, and Accounting interact later
when their baseline is reviewed
then source-of-truth ownership and business facts shared across boundaries are explicit at a medium level.

### AC03 — Uncertainty is visible

Given a material business question cannot be safely inferred
when the task completes
then it is recorded as a human product decision rather than silently encoded as a domain rule.

### AC04 — No premature technical design

Given the domain baseline is completed
when the diff is reviewed
then AWS services, DynamoDB keys, Lambda boundaries, or implementation project structure are not chosen as substitutes for business modeling.

## Architecture impact

- Owning domain: Engineering/Product architecture across business domains
- Domains touched: all; deep focus Tenant/Identity and Catalog
- Persistence impact: none directly
- Events/contracts impact: identifies business facts/event candidates; technical contracts are deferred
- AWS/IaC impact: none
- ADR required? No by default; unresolved material product decisions may generate separate records/tasks

## Security and tenant impact

- Authentication: business concepts and membership ownership clarified; technical Cognito design deferred
- Authorization: business authorization concepts/roles/permissions clarified where needed
- Tenant scoping: tenant-owned facts and isolation expectations remain mandatory
- Sensitive data/secrets: no runtime data introduced
- Abuse/rate-limit considerations: N/A

## Reliability and idempotency impact

- Retry behavior: N/A — no runtime implementation
- Timeout semantics: N/A
- Duplicate-delivery behavior: N/A
- Idempotency key/strategy: business idempotency requirements may be identified, technical strategy deferred
- DLQ/recovery/reconciliation: N/A

## Observability impact

- Logs: N/A
- Metrics: N/A
- Traces/correlation: N/A
- Operational states/errors: business-visible states/errors are refined where relevant

## Cost impact

- Request/compute impact: none
- Storage impact: repository documentation only
- Network impact: none
- New AWS resources/services: none
- Free Tier allowance relevant to this task: no AWS consumption
- Expected monthly cost change or `negligible` with rationale: negligible
- Estimated one-off cloud-test/load-test cost, if any: none

## Test plan

- Unit: N/A
- Integration: N/A
- Architecture: review ownership/invariant consistency against product scope
- Contract: verify business facts/ownership do not conflict across domain sections
- IaC: N/A
- E2E/manual: human review of material product decisions
- **Cloud verification required?** No — design-only task
- AWS environment/stack(s) required: none
- Preview/staging teardown plan: N/A

## Completion summary

### What changed

- Replaced the high-level business-domain map with a canonical bounded-context, source-of-truth, cross-cutting-invariant, and business-error baseline.
- Added deep first-frontier baselines for Tenant Management, Merchant Access, and Catalog, including aggregate ownership, state semantics, invariants, business facts, and task handoffs.
- Added a medium-depth operations baseline for Sales, Inventory, Payments, the Mock Payment Provider, Procurement, Accounting, Reporting, Product Data Ingestion, and supporting contexts.
- Added an explicit human product-decision register with 42 decision gates. Pending decisions block the affected candidate tasks instead of becoming Builder defaults.
- Recorded contradictions in the generated candidate backlog as inputs to TASK-0088 and TASK-0089 without rewriting those implementation tasks.

### Acceptance criteria status

- AC01: PASS — independent frontier review confirmed Tenant/Identity and Catalog ownership, aggregates, invariants, states, errors, and visible decision gates are sufficient for technical design without invented business semantics.
- AC02: PASS — independent operations/finance reviews confirmed authoritative owners and shared fact meanings are explicit across Sales, Inventory, Payment, Procurement, Accounting, Reporting, and Product Data Ingestion.
- AC03: PASS — every material uncertainty found in scope is recorded in `docs/domains/product-decisions.md` with affected tasks, decision gate, and safe no-guess constraint.
- AC04: PASS — independent review found no AWS service, persistence/schema/key, deployment/project-boundary, transport, or sync/async selection in the domain baseline.

### Verification

- `python3 scripts/harness_check.py`: PASS after normalizing a Windows worktree line-ending mismatch; the normalization produced no application-source diff and was reverted after verification.
- .NET build: PASS with 0 warnings and 0 errors.
- .NET tests: PASS — 7 tests.
- Frontend lint/build/tests: PASS — both applications built and 2 tests passed.
- CDK synthesis and repository structure/link/planning checks: PASS.
- Independent acceptance review: AC01/AC02/AC03/AC04 all PASS.
- Cloud verification: N/A — documentation-only domain design; no AWS resource was created.
- Ephemeral teardown: N/A.

### Architecture, security, and cost notes

- Architecture: business ownership/facts were refined only; technical module, interaction, deployment, and persistence choices remain TASK-0088.
- Security/tenant: authentication identity is separated from active Membership authority; trusted tenant scope, non-disclosure, last-owner, and audit boundaries are explicit.
- Cost: no runtime or infrastructure change; monthly and one-off cloud cost remain zero for this task.

### Follow-up items

- The human product owner must resolve each registered decision before its listed candidate tasks pass the Ready gate.
- TASK-0088 consumes this baseline to reconcile technical architecture and record any architecture decisions.
- TASK-0089 consumes both baselines to repair candidate-task maturity/dependencies and select the first safe Builder frontier.
