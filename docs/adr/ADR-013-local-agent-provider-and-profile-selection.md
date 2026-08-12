# ADR-013 - Local agent provider and profile selection

Status: Accepted
Date: 2026-08-12
Decision owners: Human operator, Technical Architect
Supersedes: Fixed-provider portions of TASK-0167 and TASK-0175
Superseded by: N/A

## Context

The local Task Orchestrator currently constructs Codex runners with repository-fixed Sol and
Terra profiles. The operator wants to select the model used by each agent role and also has Google
Antigravity installed locally. The dashboard must expose this choice without accepting arbitrary
commands, storing credentials, weakening the Reviewer contract, or making browser code an
orchestration authority.

## Decision

- Keep Codex with `gpt-5.6-sol` for planning and `gpt-5.6-terra` for coding as repository defaults.
- Add a repository-owned agent-provider abstraction with explicit `codex` and `antigravity`
  adapters. Provider, model, reasoning effort, and service tier are typed settings, never raw
  command fragments.
- Store operator overrides in ignored `.commerceos/orchestrator/settings.json` local state.
  Do not commit settings, credentials, account identifiers, or authentication material.
- Configure logical role profiles independently for planning, Builder, Reviewer, and Conflict
  Resolver. The planning profile applies to Backlog Planner, Domain Architect, and Technical
  Architect.
- Discover Antigravity through `agy` on PATH and its documented Windows install location. Use its
  non-interactive print mode, sandbox, working directory, and model flag through a fixed argv
  builder.
- Expose provider capabilities in the Settings page. A provider/role combination is selectable
  only when the installed adapter can preserve that role's machine-checkable contract.
- Antigravity CLI versions without machine-readable command events are not eligible for the
  strict read-only Reviewer role. Codex remains the Reviewer default and fallback is never silent.
- Settings changes are validated and written atomically. They apply to newly constructed runners;
  the UI clearly reports when a dashboard restart is required.
- Dashboard command buttons call typed server-side actions. The browser never assembles or runs a
  shell command and cannot mutate canonical backlog YAML directly.

## Alternatives considered

### Option A - Keep Codex-only fixed profiles

- Benefits: smallest attack surface and no provider compatibility work.
- Costs/risks: does not satisfy operator model selection or use the installed Antigravity runtime.

### Option B - Accept an arbitrary executable and argument template

- Benefits: supports almost any local agent immediately.
- Costs/risks: creates command-injection, secret-handling, compatibility, and auditability risks.

### Option C - Typed provider adapters with capability gating

- Benefits: supports real local provider choice while preserving role contracts and safe argv.
- Costs/risks: each provider requires a maintained adapter; some provider/role combinations remain
  unavailable until their CLI exposes sufficient machine-readable evidence.

Option C is accepted.

## Consequences

### Positive

- The operator can select a provider and model per logical agent role from the local dashboard.
- Codex defaults remain deterministic when no override exists.
- Antigravity can be used where its installed CLI capabilities satisfy the workflow contract.
- Unsupported combinations fail closed and are visible before a run starts.

### Negative / trade-offs

- Settings take effect after the Orchestrator process is restarted.
- Antigravity live activity can be less detailed than Codex JSONL on older installed versions.
- Antigravity cannot be selected for strict Reviewer execution until structured command evidence
  is available and supported by the adapter.

## Security and tenant impact

- Tenant isolation: N/A; this is local engineering tooling.
- Authentication/authorization: loopback-only dashboard; same-origin control requests only.
- Sensitive data/secrets: provider credentials remain owned by each installed CLI and are never
  read, returned by the API, or persisted by CommerceOS.

## Reliability and operability impact

- Failure modes: missing executable, unsupported profile, malformed local settings, provider
  process failure, and restart-required state are explicit.
- Retry/recovery: existing Orchestrator retry policy remains authoritative; provider fallback is
  not automatic.
- Observability: status and live feed identify provider, model, and role.
- Operational burden: provider capability probes and settings validation are covered by tests.

## Cost impact

- Learning profile: provider/model usage follows the operator's local account and plan.
- Beta profile: unchanged.
- Larger-scale implication: local-only; no hosted execution cost is introduced.
- Cost-model update required? No.

## Reversibility / migration

Delete the ignored local settings file to restore repository defaults. Removing the Antigravity
adapter leaves Codex defaults and canonical task data unchanged.

## Validation

- Unit tests for default settings, validation, atomic persistence, executable discovery, and fixed
  provider argv.
- Dashboard API tests for action dispatch, parameter validation, settings reads/writes, and
  same-origin protection.
- Browser verification of command controls, parameter inputs, Settings navigation, provider
  capability messaging, responsive layout, and error/loading states.
- Full repository harness.

## References

- relevant task: TASK-0176
- architecture docs: `docs/development/16-task-orchestrator.md`
- external references: Google Antigravity CLI getting-started and usage documentation
