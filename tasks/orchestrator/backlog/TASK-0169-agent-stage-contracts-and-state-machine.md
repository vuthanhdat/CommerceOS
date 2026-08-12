# TASK-0169 — Define machine-checkable agent stage contracts and workflow states

Status: Backlog
Specification maturity: Ready
Execution permission: YES
Owner: Builder — Engineering / Harness
Recommended implementation model: gpt-5.6-luna, medium reasoning, standard service tier
Created: 2026-08-12
Depends on: completed TASK-0090, completed TASK-0168, completed TASK-0167
Cloud verification: No

## Goal

Replace implicit prompt-based handoffs with one versioned, machine-checkable contract for every
Orchestrator stage so each actor has an explicit input, output, allowed action set, success
condition, and failure route.

## Business context

The current pipeline names Builder, verification, Reviewer, and Orchestrator responsibilities,
but their handoffs are partly encoded in prose and free-form output. This makes stage ownership
blurred and allows states such as `REVIEWING -> BUILDING` without an inspectable repair packet.

## Planning readiness

- Owning domain: Engineering / Harness.
- Product/domain/tenant decisions: N/A.
- Technical boundary: repository-local Python contracts and persisted SQLite task state.
- Remaining blockers: None. TASK-0167 is completed and its runner/state surface is authoritative.

## In scope

- Define versioned input/output records for Builder, deterministic Verification Runner,
  Reviewer, Repair Builder, merge/integration, and completion finalization.
- Define the authoritative transition table, including actor, required input artifact, required
  output artifact, success predicate, retry route, and terminal failure route.
- Separate first-build, repair-build, verification, review, integration, and finalization states.
- Validate role output before the next stage is entered; malformed/missing output fails closed.
- Persist contract version and artifact identifiers in the event timeline.
- Update role/workflow documentation to match the executable contract.

## Out of scope

- Implementing Builder evidence details, Reviewer ledger semantics, repair path allow-lists, or
  completion file moves beyond the interfaces needed by later tasks.
- Changing CommerceOS product task semantics.

## Acceptance criteria

### AC01 — Complete stage inventory

One canonical transition table represents 100% of production task-state transitions. A test
fails if code enters a state or invokes an actor not represented by that table.

### AC02 — Explicit handoff records

Each agent/runner stage has exactly one versioned input type and one versioned output type. Every
required field is validated; fixtures with one required field missing are rejected in 100% of
covered stage types.

### AC03 — Unambiguous states

Initial build, repair build, pre-review verification, review/re-review, integration, and
finalization are distinguishable in persisted state. `REVIEWING -> BUILDING` is not an allowed
direct transition.

### AC04 — Fail-closed routing

100% of invalid-transition and invalid-output test cases enter a named blocked/human-required
route; none advance to merge or completion.

### AC05 — Documentation parity

The role matrix and workflow document list the same actors, inputs, outputs, and terminal
conditions as the executable contract; a structural test checks the referenced contract version.

## Architecture/security/runtime impact

Harness-only. No tenant data, application modules, AWS-style capability, or LocalStack runtime is
changed. Persisted local state may require an additive schema migration; migration must preserve
all existing task records.

## Quantified Definition of Done

- 100% production transitions are declared and unit-tested.
- 100% stage contract types have valid and missing-field fixtures.
- 0 direct `REVIEWING -> BUILDING` transitions remain.
- 0 existing task records are lost in migration tests.
- All Orchestrator tests and `python scripts/harness_check.py` pass.

## Test plan

- Unit tests for contract serialization/validation and transition allow-listing.
- State-store migration and restart tests.
- Pipeline tests for every success, retry, routed, and invalid-output edge.
- LocalStack verification: N/A — repository harness only.
