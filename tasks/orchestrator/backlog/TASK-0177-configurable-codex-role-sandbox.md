# TASK-0177 - Configure Codex sandbox by agent role

Status: Backlog
Specification maturity: Ready
Execution permission: YES
Owner: Builder - Engineering / Harness
Recommended implementation model: gpt-5.6-terra, medium reasoning, standard service tier
Created: 2026-08-12
Depends on: TASK-0176, ADR-013
Cloud verification: No

## Goal

Let a local operator select the Codex process sandbox for each supported agent role from the
dashboard Settings page, including `danger-full-access` for Windows Builder execution.

## Business context

Codex `workspace-write` can block Docker, Git subprocesses, Node/JSII, and other infrastructure
verification inside Windows task worktrees. The operator needs an explicit, persisted control
instead of a hidden platform-specific hard-code or a provider switch.

## Planning readiness

- Owning domain/bounded context: Engineering / Harness.
- Domain invariants: role contracts remain authoritative; Reviewer is always read-only.
- State/error semantics: unsupported provider/role/sandbox combinations fail validation.
- Module/layer ownership: local settings, runner argv construction, and dashboard Settings UI.
- Persistence: existing ignored atomic orchestrator settings JSON.
- Infrastructure/LocalStack: no resource change; this enables task-declared local verification.
- Material ADRs: ADR-013 remains authoritative; no new ADR required.
- Remaining planning blockers: None.

## In scope

- Add a typed Codex sandbox field to each role profile.
- Add a Settings dropdown with role-appropriate sandbox options.
- Support `danger-full-access` for writable Codex roles, including Builder and Conflict Resolver.
- Keep Reviewer fixed to `read-only` and reject unsafe Reviewer settings server-side.
- Apply saved sandbox selection to generated Codex CLI argv and live observability.
- Add settings, runner, API/UI contract, and responsive UI regression tests.

## Out of scope

- Changing Antigravity permission behavior.
- Disabling Reviewer read-only enforcement.
- Arbitrary CLI arguments or shell templates.
- Automatically granting full access without an explicit saved operator choice.

## Acceptance criteria

### AC01 - Typed sandbox settings

Given a Codex role profile, when an operator saves an allowed sandbox mode, then the setting is
validated, persisted atomically, restored on reload, and included in the effective profile.

### AC02 - Role safety

Given Reviewer or an unsupported provider combination, when an unsafe sandbox is submitted, then
the server rejects it and Reviewer remains `read-only`.

### AC03 - Runner application

Given Builder or Conflict Resolver is configured as `danger-full-access`, when Codex starts that
role, then its fixed argv contains that sandbox while read-only roles retain their boundary.

### AC04 - Usable Settings control

Given desktop or narrow viewport Settings, when the operator edits a Codex role, then a labeled
sandbox dropdown exposes only valid options, explains the risk, and preserves existing UI states.

## Architecture impact

- Owning domain: Engineering / Harness.
- Persistence impact: one validated field in existing ignored settings JSON.
- Contracts impact: local Settings API response/request schema and Codex adapter profile.
- Infrastructure impact: none.
- ADR required: No; this refines ADR-013 local provider/profile selection.

## Security and tenant impact

- Authentication/authorization: loopback Settings API and same-origin mutation remain unchanged.
- Tenant scoping: N/A.
- Sensitive data/secrets: none.
- Security boundary: full access is explicit and never valid for Reviewer.

## Reliability and idempotency impact

- Settings writes remain atomic and repeatable.
- Invalid values fail before agent construction.
- Existing task recovery/resume behavior is unchanged.

## Observability impact

- Live agent start records expose the selected sandbox.
- Settings API returns the effective sandbox without secrets.

## Local runtime/resource impact

- No LocalStack resource changes.
- `danger-full-access` intentionally permits task-owned Docker/Git/Node processes when selected.
- No real AWS use is authorized.

## Cost impact

- No hosted infrastructure or real-cloud cost is introduced.
- Provider usage continues to follow the operator's local account and selected model.
- Cost-model update required? No.

## Test plan

- Unit: defaults, validation, persistence, role constraints, argv construction.
- Integration: Settings API save/load and malformed/unsafe input rejection.
- UI contract: labeled dropdown, option gating, risk copy, responsive layout.
- E2E/manual: save Builder full access, restart dashboard, verify effective setting.
- LocalStack/infrastructure verification required? No; this is orchestrator tooling.

## Completion summary

### What changed

- Pending implementation.

### Verification

- `python3 scripts/harness_check.py`: pending.

### Acceptance criteria status

- AC01-AC04: pending.

### Architecture/security/runtime notes

- Pending implementation.

### Harness improvement

- Pending implementation.

### Follow-up tasks

- None identified.
