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

Improve the local CommerceOS Task Orchestrator so operators can watch Codex activity live in the dashboard, autonomous implementation uses a deterministic Luna profile, and the Orchestrator can invoke a bounded Planning Factory when no Builder-ready work exists. Planning must enter through Backlog Planner and only call Domain Architect and/or Technical Architect when the Planner identifies the corresponding gap.

## Business context

The Orchestrator is local harness tooling. V1 records agent output only after a Codex process exits and stops when no Ready task is available. The human has explicitly chosen two stable execution profiles: planning should prioritize reasoning quality with Sol, while implementation/review should use Luna for cost/throughput efficiency. Both use medium reasoning and normal/standard inference rather than Fast tier. Detailed implementation planning remains a first-class prerequisite: a Builder must receive a repository-backed Ready task rather than silently becoming the architect.

## In scope

- Replace buffered `subprocess.run(..., capture_output=True)` Codex execution with a streaming `subprocess.Popen` implementation that drains stdout/stderr safely on Windows and Linux.
- Keep `codex exec --json` as the automation engine and persist complete inspectable logs.
- Publish stdout JSONL events to a per-task live feed while the process is running.
- Add a loopback-only dashboard endpoint for live task activity using Server-Sent Events (SSE).
- Render live Codex activity in the existing task detail area without using dynamic `innerHTML` or inline event handlers.
- Preserve audit log files after process completion.
- Pin Orchestrator coding/execution agents to `gpt-5.6-luna` with `model_reasoning_effort=medium` and standard/non-Fast service behavior.
- Pin Domain Architect, Technical Architect, and Backlog Planner execution to `gpt-5.6-sol`, medium reasoning, standard/non-Fast service behavior.
- Add a serial Planning Coordinator that runs only when the normal Ready scheduler is idle.
- Select the nearest dependency-satisfied Outline/Refined task deterministically as the planning candidate; numeric order alone never grants readiness.
- Always call Backlog Planner first. Planner may return `READY`, `DOMAIN_REFINEMENT_REQUIRED`, `TECHNICAL_REFINEMENT_REQUIRED`, `DOMAIN_AND_TECHNICAL_REFINEMENT_REQUIRED`, or `HUMAN_REQUIRED`.
- Invoke Domain Architect and/or Technical Architect only after the Planner requests those roles; after architect reconciliation, return to Backlog Planner for the final Ready-gate decision.
- Require planning roles to communicate through repository artifacts in the planning worktree rather than private cross-agent conversation.
- Persist verified planning artifacts through the serialized integration checkout without marking the implementation task Completed.
- When planning reaches Ready, reload canonical state and allow the normal Luna Builder pipeline to claim the task.
- Preserve human/product/domain/architecture/security/cost/cloud gates and fail closed when they remain unresolved.
- Document the distinction between interactive Codex TUI, mechanical DAG planning, AI planning/refinement, and implementation execution.
- Add tests for model command construction, streaming log publication, SSE exposure, Planning Coordinator routing, candidate selection, and DOM safety.

## Out of scope

- Replacing Codex CLI with a direct Responses API client.
- Embedding the interactive Codex TUI through PTY/ConPTY.
- Exposing dashboard/log streams outside loopback.
- Allowing planning agents to implement application/business code.
- Allowing Domain Architect or Technical Architect to mark tasks Ready directly.
- Automatically resolving human product/architecture/security/cost/cloud decisions.
- Running planning roles concurrently against the same canonical backlog.
- Enabling Codex Fast tier or Pro mode.

## Acceptance criteria

1. Codex automation still uses `codex exec --json` and no interactive TUI dependency is introduced.
2. Agent stdout is observable in a log/feed before the Codex process exits.
3. stderr is drained without a pipe deadlock and remains present in final audit evidence.
4. Dashboard task detail can subscribe to a loopback SSE feed and receives new task-agent events as they are appended.
5. Live rendering uses safe DOM APIs (`textContent`/element creation) and does not introduce `innerHTML` or inline `onclick` handlers.
6. Builder, Reviewer, Verification-oriented execution, and Conflict Resolver use `gpt-5.6-luna`, medium reasoning, Standard/non-Fast service behavior.
7. Backlog Planner, Domain Architect, and Technical Architect use `gpt-5.6-sol`, medium reasoning, Standard/non-Fast service behavior at the Codex execution boundary.
8. Interactive Codex TUI model/Fast settings cannot silently change autonomous role profiles.
9. Normal Ready work is always attempted before AI planning; Planning Coordinator runs only when no task is dispatchable.
10. Planning candidate selection requires Outline/Refined + Backlog + satisfied dependencies; unresolved gates are preserved for Planner/human handling rather than treated as Ready.
11. Backlog Planner is always the planning entry/final gate. Domain/Technical roles run only on explicit Planner routing and cannot directly mark the candidate Ready.
12. `PLANNING_RESULT: READY` is accepted only when the candidate is canonically Ready, Backlog, ungated, dependency-satisfied, and repository validation passes.
13. Human/semantic planning ambiguity stops at Human Required rather than being guessed.
14. Verified planning artifacts may be merged to authoritative `main` without completion bookkeeping; implementation completion semantics remain unchanged.
15. Fake agent/planning tests continue consuming no Codex quota.
16. Existing Orchestrator scheduling/Stop/merge/cloud tests remain green.
17. `python scripts/harness_check.py` / repository CI passes before merge.

## Architecture impact

Local harness-only change. Adds a live event/logging adapter, SSE read surface, and serial Planning Coordinator layered above the existing Ready-task execution engine. The coordinator reuses Git worktree/integration/verification primitives but does not change CommerceOS business modules, AWS services, persistence ownership, tenant model, or deployment architecture.

## Security and tenant impact

No tenant data model change. Live logs remain local under `.commerceos/orchestrator/` and are exposed only through the existing loopback-only dashboard. Prompt text remains excluded from command log output. Agent-generated output is treated as untrusted display data and rendered through text-only DOM APIs. Planning agents receive no real-cloud authorization and must preserve unresolved human/security/cost/cloud gates.

## Reliability and idempotency impact

Streaming must not change agent success semantics: Codex exit code remains authoritative. Both stdout and stderr pipes are drained. Log/feed append operations must tolerate browser reconnects without affecting task execution. Planning is serial and bounded; repeated architect/planner routing cannot loop indefinitely. Planning changes are verified before and after integration, and a stop request prevents fresh Builder dispatch after the current bounded planning lifecycle drains.

## Observability impact

Improves local observability from phase-only status to near-real-time Codex activity plus retained audit logs. Planning-role events are visible with role names so an operator can distinguish Backlog Planner, Domain Architect, Technical Architect, Builder, and Reviewer activity.

## Cost impact

No AWS cost. Model policy intentionally uses Sol for planning and Luna for coding/review, both at medium reasoning and standard/non-Fast service. Planning only runs when Ready work is exhausted and invokes architects on demand, limiting unnecessary Sol usage. Tests use fakes and no Codex quota.

## Test plan

- Unit-test role/model profile command construction.
- Unit-test JSONL live-feed publication using a fake subprocess/Popen adapter or isolated helper.
- Dashboard HTTP test for SSE endpoint headers and initial/replayed event delivery.
- Unit-test planning candidate selection and Planner-first routing.
- Unit-test Planner → Domain → Planner and Planner → Technical → Planner routing with fake planning agents.
- Unit-test Human Required and bounded non-convergence outcomes.
- Verify dry-run shows the next planning candidate/model only when no Ready task is dispatchable.
- DOM regression checks for no `innerHTML`/inline `onclick`.
- Run complete Orchestrator tests and repository harness.
