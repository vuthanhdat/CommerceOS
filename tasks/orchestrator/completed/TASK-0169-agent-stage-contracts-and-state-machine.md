# TASK-0169 — Define machine-checkable agent stage contracts and workflow states

Status: Completed
Specification maturity: Completed
Execution permission: NO — completed
Owner: Builder — Engineering / Harness
Recommended implementation model: gpt-5.6-luna, medium reasoning, standard service tier
Created: 2026-08-12
Completed: 2026-08-12
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

## Architecture impact

Harness-only. The authoritative transition table and versioned stage records live in the
repository-local Python Orchestrator boundary. Persisted local state may use an additive SQLite
schema migration; migration must preserve every existing task record.

## Security and tenant impact

Agent and runner outputs remain untrusted until contract validation succeeds. No tenant data,
authentication, authorization, application module, or external network boundary changes.

## Reliability and idempotency impact

Invalid transitions and malformed outputs fail closed to a named route. Restart migration is
additive and repeatable, and existing runs must remain readable without duplicate records.

## Observability impact

Each accepted transition persists the contract version and relevant input/output artifact IDs;
rejected transitions remain inspectable in the event timeline.

## Cost impact

No Codex quota is consumed by tests. No AWS or LocalStack resources or external services are
introduced.

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

## Completion summary

TASK-0169 is complete. The Orchestrator now uses the versioned
`commerceos.orchestrator.stage/v1` contract, distinct persisted workflow states, one canonical
transition table, fail-closed transition/output validation, additive SQLite migration, and
artifact-linked timeline events.

### Acceptance criteria status

- AC01 — Satisfied: every production edge, including failure routes, is materialized in the
  canonical transition table and structurally tested.
- AC02 — Satisfied: each planning/build/verification/review/repair/integration/finalization stage
  has one versioned input and output type; missing-field fixtures fail and production outputs are
  validated before handoff.
- AC03 — Satisfied: initial build, repair build, both verification/review phases, integration,
  and finalization are distinct; direct review-to-initial-build transitions fail closed.
- AC04 — Satisfied: invalid transitions and malformed stage outputs route to named terminal states
  and cannot advance to merge or completion.
- AC05 — Satisfied: workflow and role documentation references the executable contract version
  and stage inventory, with structural parity coverage.

### Verification evidence

- `python -m unittest discover -s tests/orchestrator -p "test_*.py"` — 66 tests passed.
- `python scripts/harness_check.py` — passed before integration; post-integration Orchestrator
  verification also passed.
- Independent review — PASS after two Builder findings were repaired and re-reviewed.

### Scope and impact

- Harness-only Python/SQLite/dashboard/docs changes; additive migration preserves existing runs.
- Agent outputs remain untrusted until validated; no tenant, CommerceOS product, cloud, or
  LocalStack runtime behavior changed.
- LocalStack verification: N/A.
