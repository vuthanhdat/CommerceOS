# TASK-0167 — Stream Codex agent activity and pin role-based model profiles

Status: Active
Specification maturity: Ready
Execution permission: YES
Owner: Builder — Engineering / Harness
Recommended implementation model: gpt-5.6-luna, medium reasoning, standard service tier
Created: 2026-08-11
Depends on: completed TASK-0090
Cloud verification: No
Exclusive resources when Active: `repository-task-state`, `orchestrator-ui`, `codex-runner`

## Goal

Improve the local CommerceOS Task Orchestrator so operators can watch Codex Builder/Reviewer activity live in the dashboard using the newline-delimited JSON stream from `codex exec --json`, while making model selection deterministic by role: planning roles use GPT-5.6 Sol with medium reasoning and standard service tier; coding/execution roles use GPT-5.6 Luna with medium reasoning and standard service tier.

## Business context

The Orchestrator is local harness tooling. V1 records agent output only after a Codex process exits, which makes long-running autonomous work difficult to inspect. The human has explicitly chosen a stable model policy: planning should prioritize reasoning quality with Sol, while implementation should use Luna for cost/throughput efficiency. Both use medium reasoning and normal/standard inference rather than Fast tier.

## In scope

- Replace buffered `subprocess.run(..., capture_output=True)` Codex execution with a streaming `subprocess.Popen` implementation that drains stdout/stderr safely on Windows and Linux.
- Keep `codex exec --json` as the automation engine and persist complete inspectable logs.
- Publish stdout JSONL events to a per-task live feed while the process is running.
- Add a loopback-only dashboard endpoint for live task activity using Server-Sent Events (SSE).
- Render live Codex activity in the existing task detail area without using dynamic `innerHTML` or inline event handlers.
- Preserve audit log files after process completion.
- Pin Orchestrator coding/execution agents to `gpt-5.6-luna` with `model_reasoning_effort=medium` and standard/non-Fast service behavior.
- Update repository agent policy so Domain Architect, Technical Architect, and Backlog Planner planning work uses `gpt-5.6-sol`, medium reasoning, standard/non-Fast service behavior.
- Document the distinction between interactive Codex TUI and non-interactive Orchestrator execution.
- Add tests for model command construction, streaming log publication, SSE exposure, and DOM safety.

## Out of scope

- Replacing Codex CLI with a direct Responses API client.
- Embedding the interactive Codex TUI through PTY/ConPTY.
- Exposing dashboard/log streams outside loopback.
- Changing scheduling, dependency, merge, review, Stop, or cloud-gating semantics.
- Adding a Planner execution loop to Orchestrator V1.
- Enabling Codex Fast tier or Pro mode.

## Acceptance criteria

1. Codex automation still uses `codex exec --json` and no interactive TUI dependency is introduced.
2. Agent stdout is observable in a log/feed before the Codex process exits.
3. stderr is drained without a pipe deadlock and remains present in final audit evidence.
4. Dashboard task detail can subscribe to a loopback SSE feed and receives new task-agent events as they are appended.
5. Live rendering uses safe DOM APIs (`textContent`/element creation) and does not introduce `innerHTML` or inline `onclick` handlers.
6. Builder, Reviewer, and Conflict Resolver Codex runs explicitly select `gpt-5.6-luna` and medium reasoning.
7. Orchestrator does not opt into Fast service tier; normal/standard service remains the required policy.
8. Repository planning-role documentation explicitly selects `gpt-5.6-sol`, medium reasoning, standard/non-Fast service.
9. Fake-agent tests continue consuming no Codex quota.
10. Existing Orchestrator scheduling/Stop/merge/cloud tests remain green.
11. `python scripts/harness_check.py` / repository CI passes before merge.

## Architecture impact

Local harness-only change. Adds a live event/logging adapter and SSE read surface; no CommerceOS business module, AWS service, persistence ownership, tenant model, or deployment architecture changes.

## Security and tenant impact

No tenant data model change. Live logs remain local under `.commerceos/orchestrator/` and are exposed only through the existing loopback-only dashboard. Prompt text remains excluded from command log output. Agent-generated output is treated as untrusted display data and rendered through text-only DOM APIs.

## Reliability and idempotency impact

Streaming must not change agent success semantics: Codex exit code remains authoritative. Both stdout and stderr pipes are drained. Log/feed append operations must tolerate browser reconnects without affecting task execution.

## Observability impact

Improves local observability from phase-only status to near-real-time Codex activity plus retained audit logs.

## Cost impact

No AWS cost. Model policy intentionally uses Sol for planning and Luna for coding, both at medium reasoning and standard/non-Fast service. Tests use fakes and no Codex quota.

## Test plan

- Unit-test role/model profile command construction.
- Unit-test JSONL live-feed publication using a fake subprocess/Popen adapter or isolated helper.
- Dashboard HTTP test for SSE endpoint headers and initial/replayed event delivery.
- DOM regression checks for no `innerHTML`/inline `onclick`.
- Run complete Orchestrator tests and repository harness.
