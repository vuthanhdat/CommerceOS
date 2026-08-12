# TASK-0176 - Add dashboard command controls and agent settings

Status: Backlog
Specification maturity: Ready
Execution permission: YES
Owner: Builder - Engineering / Harness
Recommended implementation model: gpt-5.6-terra, medium reasoning, standard service tier
Created: 2026-08-12
Depends on: TASK-0175, ADR-013
Cloud verification: No

## Goal

Let a local operator invoke every relevant Orchestrator command from the dashboard and configure
the provider/model used by each logical agent role, including supported Antigravity execution.

## Business context

The CLI exposes validate, plan, dry-run, run, stop, resume, cleanup, status, UI, and start flows,
but the browser currently exposes only Stop and Resume. Operators should not need a second terminal
for routine control, and local agent selection should reflect runtimes available on their machine.

## Planning readiness

- Owning domain/bounded context: Engineering / Harness.
- Domain invariants: browser remains a thin local control client; canonical readiness, planning,
  scheduling, verification, review, merge, and completion remain server-owned.
- State/error semantics: typed actions return accepted/result/error envelopes; only one mutable
  scheduler action runs at a time; unsupported provider/profile combinations fail closed.
- Module/layer ownership: dashboard UI and HTTP adapter under `commerceos_orchestrator`; provider
  settings and runner adapters remain server-side.
- Persistence: ignored atomic JSON settings beside the selected catalog's SQLite state.
- Infrastructure/LocalStack: N/A.
- Material ADR: ADR-013 accepted.
- Remaining planning blockers: None.

## In scope

- Add dashboard controls for refresh/status, validate, plan, dry-run, run/start, stop, resume, and
  cleanup, with inputs for supported global runtime parameters where they affect a new run.
- Add typed HTTP action endpoints and structured results without shell interpolation.
- Add a Settings page for per-role provider/model configuration and runtime parameters.
- Preserve Codex Sol/Terra defaults and add capability-gated Antigravity support through `agy`.
- Detect Antigravity in PATH and its documented Windows install location.
- Add loading, success, empty, unavailable, validation-error, and busy states.
- Add API, runner/settings, UI contract, and responsive browser tests.
- Update current Orchestrator and model-policy documentation.

## Out of scope

- Arbitrary executable paths, arbitrary CLI flags, credentials, account login, or provider install.
- Canonical backlog editing from the browser.
- Hard process termination, force cleanup of active work, or bypassing graceful stop.
- Silent provider/model fallback.
- Antigravity Reviewer execution when the installed CLI cannot expose command evidence required by
  the strict Reviewer contract.
- Changes to CommerceOS product/runtime or LocalStack infrastructure.

## Acceptance criteria

### AC01 - Complete typed command surface

Given the dashboard is running on loopback, when the operator uses each relevant CLI-equivalent
control, then status/refresh, validate, plan, dry-run, run/start, stop, resume, and cleanup execute
through typed server actions and return visible structured results without browser-built commands.

### AC02 - Parameters and action safety

Given a command needs configurable runtime values, when the operator edits catalog, Builder limit,
fix-attempt limit, or cloud-consent input, then valid values are persisted for the next process and
invalid, busy, cross-origin, or unsupported actions fail visibly without changing canonical tasks.

### AC03 - Per-role agent settings

Given the Settings page, when the operator configures planning, Builder, Reviewer, or Conflict
Resolver, then provider/model and provider-supported profile fields are validated, saved atomically
outside Git, secrets are never requested or returned, and repository defaults are restorable.

### AC04 - Antigravity capability gating

Given Antigravity is installed locally, when provider capabilities are loaded, then the UI reports
the discovered executable/version and enables only role combinations supported by that CLI;
unsupported strict Reviewer selection is rejected rather than silently falling back.

### AC05 - Usable and verified UI

Given desktop and narrow viewports, when the dashboard and Settings page are used by keyboard or
pointer, then controls have accessible labels/focus, do not overflow, expose loading/error/empty
states, contain no unsafe dynamic HTML insertion, and pass automated API/UI tests plus browser QA.

## Architecture impact

- Owning domain: Engineering / Harness.
- Persistence impact: ignored local JSON settings only.
- Contracts impact: local loopback dashboard action/settings API and typed provider adapter.
- Infrastructure impact: none.
- ADR required: Yes, ADR-013.

## Security and tenant impact

- Authentication/authorization: local loopback only; mutating requests require same-origin checks.
- Tenant scoping: N/A.
- Sensitive data/secrets: no credential fields; provider CLIs retain their own authentication.
- Abuse considerations: fixed action/provider enums, bounded request bodies and numeric values, no
  arbitrary argv or shell execution.

## Reliability and idempotency impact

- Read-only actions are repeatable.
- Run/resume reject or report busy state rather than starting duplicate scheduler threads.
- Stop remains graceful and cleanup remains restricted to terminal task states.
- Settings writes are atomic; malformed files fail closed with actionable diagnostics.

## Observability impact

- Action responses and the dashboard report action state/result.
- Live agent records identify provider, model, role, and completion status.
- Settings report capability and restart-required state without credentials.

## Local runtime/resource impact

- No LocalStack services.
- Small ignored JSON settings file under `.commerceos/orchestrator/<catalog>/`.
- Optional local `agy` process uses the operator's existing Antigravity installation/account.

## Cost impact

- Repository defaults preserve the existing Sol/Terra usage profile.
- Provider/model overrides use the operator's existing local account and plan; the UI displays the
  selected provider/model but does not estimate, purchase, or authorize quota.
- Capability discovery uses zero-token CLI metadata commands. Browser QA does not start agents.

## Test plan

- Unit: settings schema/defaults/validation/persistence, provider discovery and command building.
- Integration: loopback action/settings APIs, same-origin and malformed-body rejection, busy states.
- UI contract: controls, forms, safe text rendering, accessible labels, mobile CSS.
- E2E/manual: start local dashboard, exercise read-only actions, Settings save/reset and responsive
  browser screenshots; do not start a quota-consuming agent run.
- LocalStack/infrastructure verification required? No; local tooling only.

## Implementation notes

- Preserve the existing standard-library dashboard architecture; do not add a frontend framework
  solely for these controls.
- The design is a targeted evolution of the current developer dashboard. Use one existing accent,
  consistent radii, explicit button/input states, and restrained feedback motion.
- Settings apply to newly constructed runners. Show a restart-required message after save/reset.
- Installed baseline observed during implementation: Antigravity CLI `agy.exe` v1.1.12 at the
  documented Windows path, with `--print`, `--model`, `--effort`, `--sandbox`, and machine-readable
  `stream-json` output. Capability probing still supports older text-only installations with
  Reviewer disabled. Reviewer eligibility additionally requires the enriched-tool-event release,
  and each review fails closed when no auditable command telemetry is emitted.

## Completion summary

### What changed

- Pending implementation.

### Verification

- `python3 scripts/harness_check.py`: pending.
- LocalStack/infrastructure verification: N/A.

### Acceptance criteria status

- AC01-AC05: pending.

### Architecture/security/runtime notes

- Pending implementation.

### Harness improvement

- Pending implementation.

### Follow-up tasks

- None identified.
