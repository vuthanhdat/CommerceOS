# TASK-0174 — Expose and verify the strict Orchestrator workflow end to end

Status: Completed
Specification maturity: Completed
Execution permission: NO — completed
Owner: Builder — Engineering / Harness
Recommended implementation model: gpt-5.6-luna, medium reasoning, standard service tier
Created: 2026-08-12
Completed: 2026-08-12
Depends on: TASK-0172, TASK-0173
Cloud verification: No

## Planning readiness

- Owning domain: Engineering / Harness; product and tenant domains are unaffected.
- Dependencies TASK-0172 and TASK-0173 are canonically Completed.
- Status semantics, actors, transition table, stage/evidence contracts, review ledger, repair
  packet, and completion transaction are all versioned repository contracts.
- Dashboard work extends the existing loopback-only read model and DOM-safe UI; no visual
  redesign, persistence technology, remote monitoring, cloud service, or ADR decision is needed.
- Evidence counters are derived from persisted JSON contract artifacts under the task evidence
  directory and fail closed when artifacts are missing or malformed.
- E2E scenarios use deterministic fake agents/runners and assert actor-call absence as well as
  terminal state; no LocalStack or external account is involved.
- Remaining planning blockers: None.

## Goal

Make the strict role boundaries and workflow status visible to operators and prove the complete
pipeline with end-to-end contract tests.

## Business context

Even correct internal routing is hard to trust when dashboard states collapse first build,
verification, review, and repair into ambiguous columns. Operators need to know which actor owns
the task, what input it received, and what measurable condition advances it.

## In scope

- Expose explicit states for queued, initial build, pre-review verification, first review,
  repair required, repair build, repair verification, re-review, merge queued, integration,
  finalization, completed, planning required, Orchestrator action required, human required, and
  blocked.
- Display current actor, attempt/round, input artifact IDs, open finding counts by owner, required
  test pass totals, and the next transition criterion.
- Add audit events for every state transition and rejected transition.
- Add end-to-end fake-agent tests for happy path, verification repair, Reviewer repair,
  non-Builder finding routing, malformed output, retry exhaustion, completion failure/recovery,
  and graceful stop/restart.
- Publish the final workflow/status table in Orchestrator documentation.

## Out of scope

- Redesigning dashboard visual styling.
- Adding remote monitoring or cloud services.

## Acceptance criteria

### AC01 — Operator-visible ownership

For 100% of non-terminal states, dashboard/status output shows exactly one current owner and one
next measurable transition condition.

### AC02 — Status fidelity

Initial build, repair build, verification, review, re-review, integration, and finalization never
share the same persisted status. No test observes `REVIEWING -> BUILDING` without an intervening
accepted repair state and repair packet ID.

### AC03 — Evidence counters

Displayed AC coverage, changed-file coverage, test totals, and open-finding counts equal persisted
contract artifacts in 100% of dashboard fixtures.

### AC04 — Complete audit trail

Every accepted or rejected state transition produces exactly one audit event containing task ID,
from/to state, actor, contract version, and relevant artifact IDs.

### AC05 — End-to-end scenarios

All named pipeline scenarios have deterministic automated coverage; scenario pass rate is 100%,
and each asserts both final state and absence of unauthorized actor actions.

## Architecture impact

Harness/dashboard read-model and tests only. Existing versioned workflow contracts and SQLite
run state remain authoritative; no module, persistence, integration, or ADR change.

## Security and tenant impact

Agent output remains untrusted text and the dashboard remains loopback-only with DOM-safe text
rendering. No authentication, authorization, tenant data, or secrets impact.

## Reliability and idempotency impact

Status is derived from persisted state and validated evidence artifacts. Missing/malformed
artifacts fail closed; read-model refresh and restart scenarios are deterministic and idempotent.

## Observability impact

Every non-terminal state exposes one actor and next condition, evidence-derived counters, and a
complete accepted/rejected transition audit record.

## Cost impact

Repository-local tests and dashboard reads only; no external or cloud cost.

## Local runtime/resource impact

Existing loopback dashboard, SQLite state, and repository-local evidence only. LocalStack: N/A.

## Quantified Definition of Done

- Non-terminal states with owner/exit criterion: 100%.
- Ambiguous build/review/finalization state aliases: 0.
- Dashboard counter fidelity: 100%.
- Transition audit event coverage: 100%.
- Required E2E scenario pass rate: 100%.
- All Orchestrator tests and repository harness pass.

## Test plan

- State/read-model/dashboard contract tests.
- Transition audit event cardinality tests.
- Full fake-agent pipeline matrix, including stop/restart and recovery.
- DOM safety regression checks.
- LocalStack verification: N/A.

## Completion summary

### Orchestrator evidence

- Dashboard exposes distinct workflow state, current owner, next measurable condition, and evidence-derived counters.
- Accepted/rejected transitions use complete versioned audit payloads; the deterministic E2E matrix covers all named scenarios.
- Independent Reviewer result: PASS; all findings resolved.
- 119 Orchestrator tests passed after integration.
- Full repository harness passed before integration; post-bookkeeping harness follows.
- Architecture/security: loopback harness only, DOM-safe output, no tenant, cloud, or LocalStack impact.
