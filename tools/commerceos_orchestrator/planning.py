from __future__ import annotations

import json
from dataclasses import dataclass
from enum import StrEnum
from pathlib import Path
from typing import Protocol

from .agents import CodexRunner, PLANNING_CODEX_PROFILE
from .backlog import BacklogReader, BacklogValidationError
from .models import (
    AgentResult,
    BacklogSnapshot,
    CanonicalTask,
    OrchestratorState,
    TaskExecutionState,
    Workspace,
)
from .state import RunStateStore
from .verification import VerificationRunner
from .workspace import GitIntegrationManager, GitWorkspaceManager, IntegrationError, WorkspaceError


class PlanningOutcome(StrEnum):
    NO_CANDIDATE = "NO_CANDIDATE"
    READY = "READY"
    HUMAN_REQUIRED = "HUMAN_REQUIRED"
    FAILED = "FAILED"


class PlanningAgentRunner(Protocol):
    def run_backlog_planner(
        self, task: CanonicalTask, worktree: Path, *, attempt: int
    ) -> AgentResult: ...

    def run_domain_architect(
        self, task: CanonicalTask, worktree: Path, *, attempt: int
    ) -> AgentResult: ...

    def run_technical_architect(
        self, task: CanonicalTask, worktree: Path, *, attempt: int
    ) -> AgentResult: ...


class CodexPlanningAgentRunner:
    """Planning-role prompt adapter over a configured local agent runner."""

    def __init__(self, root: Path, logs_root: Path, runner: CodexRunner | None = None):
        self.runner = runner or CodexRunner(
            root, logs_root, profile=PLANNING_CODEX_PROFILE, cloud_authorized=False
        )

    def run_backlog_planner(
        self, task: CanonicalTask, worktree: Path, *, attempt: int
    ) -> AgentResult:
        spec = task.spec_path or "(no detailed task spec exists yet)"
        prompt = f"""Act as the CommerceOS Backlog Planner for {task.id}.

Read, in repository order:
- AGENTS.md
- docs/development/15-planning-factory-and-task-maturity.md
- docs/agents/backlog-planner.md
- tasks/{task.catalog}/BACKLOG.md
- tasks/BACKLOG.v2.yaml and the canonical shard containing {task.id}
- the current task spec at {spec} when it exists
- relevant current domain/architecture/ADR artifacts required to judge the Ready gate

Work only on planning artifacts under docs/ and tasks/ in this task worktree. Do not implement
application/harness code and do not execute real cloud operations. Inspect the current repository
state rather than assuming the candidate is ready because of numeric order.

Your job is to make the next planning decision for {task.id}. If existing accepted domain and
technical artifacts are already sufficient, refine the task all the way to a detailed Ready
implementation contract: create/update its task spec, canonical maturity/spec_path,
execution_metadata and ready_frontier consistently. Never clear a human/product/domain/
architecture/security/cost/cloud gate unless repository evidence proves it satisfied.

If a Builder would still need a business/domain decision, do not guess and end exactly with:
PLANNING_RESULT: DOMAIN_REFINEMENT_REQUIRED

If a Builder would still need a technical architecture/ADR/contract decision, end exactly with:
PLANNING_RESULT: TECHNICAL_REFINEMENT_REQUIRED

If both kinds of work are required, end exactly with:
PLANNING_RESULT: DOMAIN_AND_TECHNICAL_REFINEMENT_REQUIRED

If a human decision/authorization/input is the remaining blocker, preserve that gate, record only
planning evidence that is safe to persist, and end exactly with:
PLANNING_RESULT: HUMAN_REQUIRED

Only when the canonical Ready gate is fully satisfied, end exactly with:
PLANNING_RESULT: READY
"""
        return self.runner._run(
            task,
            role="backlog-planner",
            worktree=worktree,
            prompt=prompt,
            writable=True,
            attempt=attempt,
        )

    def run_domain_architect(
        self, task: CanonicalTask, worktree: Path, *, attempt: int
    ) -> AgentResult:
        spec = task.spec_path or "(no detailed task spec exists yet)"
        prompt = f"""Act as the CommerceOS Domain Architect for planning candidate {task.id}.

Read AGENTS.md, docs/development/15-planning-factory-and-task-maturity.md,
docs/agents/domain-architect.md, current product/domain baselines, product decisions, and the
candidate task/spec ({spec}) where present.

Resolve only business/domain gaps that can be resolved from already accepted product intent and
repository evidence. Update only planning/domain artifacts under docs/ and tasks/ in this
worktree. Do not implement code, choose AWS/persistence architecture, mark the task Ready, clear
human gates, or invent missing business semantics. Do not execute real cloud operations.

If safe domain reconciliation is complete, end exactly with:
DOMAIN_RESULT: UPDATED

If a human product/business decision is required, record the unresolved decision in the approved
repository location and end exactly with:
DOMAIN_RESULT: HUMAN_REQUIRED
"""
        return self.runner._run(
            task,
            role="domain-architect",
            worktree=worktree,
            prompt=prompt,
            writable=True,
            attempt=attempt,
        )

    def run_technical_architect(
        self, task: CanonicalTask, worktree: Path, *, attempt: int
    ) -> AgentResult:
        spec = task.spec_path or "(no detailed task spec exists yet)"
        prompt = f"""Act as the CommerceOS Technical Architect for planning candidate {task.id}.

Read AGENTS.md, docs/development/15-planning-factory-and-task-maturity.md,
docs/agents/technical-architect.md, current domain baselines, architecture rules, accepted ADRs,
and the candidate task/spec ({spec}) where present.

Resolve only technical architecture gaps supported by accepted domain/product semantics. Update
only architecture/planning artifacts under docs/ and tasks/ in this worktree. Do not implement
feature/harness code, mark the task Ready, clear human gates, invent business semantics, or
execute real cloud operations.

If technical reconciliation is complete, end exactly with:
TECHNICAL_RESULT: UPDATED

If technical design exposes a missing domain/business decision, end exactly with:
TECHNICAL_RESULT: DOMAIN_REQUIRED

If a high-consequence architecture decision requires human approval, record the decision need and
end exactly with:
TECHNICAL_RESULT: HUMAN_REQUIRED
"""
        return self.runner._run(
            task,
            role="technical-architect",
            worktree=worktree,
            prompt=prompt,
            writable=True,
            attempt=attempt,
        )


class FakePlanningAgentRunner:
    """Deterministic planning runner for tests; consumes no Codex quota."""

    def __init__(
        self,
        *,
        planner_results: list[AgentResult] | None = None,
        domain_results: list[AgentResult] | None = None,
        technical_results: list[AgentResult] | None = None,
    ):
        self.planner_results = list(planner_results or [])
        self.domain_results = list(domain_results or [])
        self.technical_results = list(technical_results or [])
        self.calls: list[str] = []

    @staticmethod
    def _result(text: str) -> AgentResult:
        return AgentResult(True, 0, text, "", "")

    def run_backlog_planner(
        self, task: CanonicalTask, worktree: Path, *, attempt: int
    ) -> AgentResult:
        self.calls.append("backlog-planner")
        if self.planner_results:
            return self.planner_results.pop(0)
        return self._result("PLANNING_RESULT: HUMAN_REQUIRED")

    def run_domain_architect(
        self, task: CanonicalTask, worktree: Path, *, attempt: int
    ) -> AgentResult:
        self.calls.append("domain-architect")
        if self.domain_results:
            return self.domain_results.pop(0)
        return self._result("DOMAIN_RESULT: UPDATED")

    def run_technical_architect(
        self, task: CanonicalTask, worktree: Path, *, attempt: int
    ) -> AgentResult:
        self.calls.append("technical-architect")
        if self.technical_results:
            return self.technical_results.pop(0)
        return self._result("TECHNICAL_RESULT: UPDATED")


@dataclass(frozen=True)
class PlanningDecision:
    task_id: str | None
    maturity: str | None
    gates: tuple[str, ...] = ()


@dataclass(frozen=True)
class PlanningPersistenceResult:
    persisted: bool
    error_code: str | None = None
    error_detail: str = ""


class PlanningCoordinator:
    """Serial planning factory for the nearest dependency-satisfied non-Ready task."""

    ALLOWED_CHANGE_PREFIXES = ("docs/", "tasks/")

    def __init__(
        self,
        root: Path,
        state: RunStateStore,
        runner: PlanningAgentRunner,
        verification: VerificationRunner,
        *,
        workspace_manager: GitWorkspaceManager | None = None,
        integration_manager: GitIntegrationManager | None = None,
        max_rounds: int = 4,
        catalog: str = "commerceos",
    ):
        self.root = root.resolve()
        self.state = state
        self.runner = runner
        self.verification = verification
        self.workspace = workspace_manager or GitWorkspaceManager(self.root)
        self.integration = integration_manager or GitIntegrationManager(self.root)
        self.max_rounds = max_rounds
        self.catalog = catalog

    def next_candidate(self, snapshot: BacklogSnapshot) -> CanonicalTask | None:
        locally_blocked = {run.task_id for run in self.state.blocked_task_runs()}
        candidates = [
            task
            for task in snapshot.tasks.values()
            if task.id not in locally_blocked
            and task.maturity in {"Outline", "Refined"}
            and task.lifecycle_state == "Backlog"
            and all(
                BacklogReader.dependency_satisfied(snapshot, dependency)
                for dependency in task.depends_on
            )
        ]
        # Refined work is closer to an implementation contract than Outline work. Task id is
        # only a deterministic tie-breaker within the same maturity, never evidence of readiness.
        return min(
            candidates,
            key=lambda task: (0 if task.maturity == "Refined" else 1, task.id),
            default=None,
        )

    def preview(self, snapshot: BacklogSnapshot) -> PlanningDecision:
        task = self.next_candidate(snapshot)
        if task is None:
            return PlanningDecision(None, None, ())
        return PlanningDecision(task.id, task.maturity, task.gates)

    def refine_next(self, snapshot: BacklogSnapshot) -> PlanningOutcome:
        task = self.next_candidate(snapshot)
        if task is None:
            return PlanningOutcome.NO_CANDIDATE
        if not self.state.claim_task(task.id):
            return PlanningOutcome.NO_CANDIDATE

        try:
            workspace = self.workspace.workspace_for(task)
            self.state.update_task(
                task.id,
                TaskExecutionState.PLANNING,
                branch=workspace.branch,
                worktree=str(workspace.path),
            )
            self.state.add_event(
                task.id, "PLANNING_STARTED", f"candidate maturity={task.maturity}"
            )
        except WorkspaceError as exc:
            self._block(task, "PLANNING_WORKTREE_ERROR", str(exc))
            return PlanningOutcome.FAILED

        terminal = PlanningOutcome.FAILED
        blocker_code = "PLANNING_FAILED"
        blocker_detail = "planning did not reach a terminal result"
        role_attempt = 0

        for _ in range(self.max_rounds):
            role_attempt += 1
            self.state.update_task(task.id, TaskExecutionState.PLANNING, attempt_delta=1)
            planner = self.runner.run_backlog_planner(
                task, workspace.path, attempt=role_attempt
            )
            if not planner.success:
                blocker_code = (
                    "EXTERNAL_ENVIRONMENT"
                    if planner.marker == "ENVIRONMENT_UNAVAILABLE"
                    else "BACKLOG_PLANNER_FAILED"
                )
                blocker_detail = planner.stderr or planner.stdout
                break

            marker = self._planner_marker(planner)
            if marker is None and self._canonical_task_is_dispatchable(
                workspace.path, task.id
            ):
                marker = "READY"
                self.state.add_event(
                    task.id,
                    "PLANNING_DECISION_INFERRED",
                    "missing text marker; canonical planning artifacts satisfy the Ready gate",
                )
            self.state.add_event(
                task.id, "PLANNING_DECISION", marker or "MISSING_MARKER"
            )
            if marker == "READY":
                try:
                    planned_snapshot = BacklogReader(workspace.path, self.catalog).load()
                    planned = planned_snapshot.tasks.get(task.id)
                    if planned is None or not BacklogReader.is_dispatchable(
                        planned_snapshot, planned, active_resources=set()
                    ):
                        raise BacklogValidationError(
                            f"{task.id}: Planner returned READY but canonical Ready gate is not dispatchable"
                        )
                except BacklogValidationError as exc:
                    blocker_code = "PLANNER_READY_GATE_INVALID"
                    blocker_detail = str(exc)
                    break
                terminal = PlanningOutcome.READY
                blocker_code = ""
                blocker_detail = ""
                break

            if marker == "HUMAN_REQUIRED":
                terminal = PlanningOutcome.HUMAN_REQUIRED
                blocker_code = "PLANNING_HUMAN_DECISION_REQUIRED"
                blocker_detail = (
                    "Backlog Planner identified an unresolved human gate/decision/input"
                )
                break

            if marker == "DOMAIN_REFINEMENT_REQUIRED":
                outcome = self._run_domain(task, workspace.path, role_attempt)
                if outcome is not None:
                    terminal, blocker_code, blocker_detail = outcome
                    break
                continue

            if marker == "TECHNICAL_REFINEMENT_REQUIRED":
                outcome = self._run_technical(task, workspace.path, role_attempt)
                if outcome is not None:
                    terminal, blocker_code, blocker_detail = outcome
                    break
                continue

            if marker == "DOMAIN_AND_TECHNICAL_REFINEMENT_REQUIRED":
                outcome = self._run_domain(task, workspace.path, role_attempt)
                if outcome is not None:
                    terminal, blocker_code, blocker_detail = outcome
                    break
                outcome = self._run_technical(task, workspace.path, role_attempt)
                if outcome is not None:
                    terminal, blocker_code, blocker_detail = outcome
                    break
                continue

            blocker_code = "BACKLOG_PLANNER_PROTOCOL_ERROR"
            blocker_detail = (
                "Backlog Planner returned no recognized PLANNING_RESULT marker"
            )
            break
        else:
            terminal = PlanningOutcome.FAILED
            blocker_code = "PLANNING_REFINEMENT_LIMIT"
            blocker_detail = (
                f"planning did not converge within {self.max_rounds} Planner rounds"
            )

        # Protocol/agent/non-convergence failure is never auto-integrated. Preserve the
        # worktree for human inspection instead of publishing potentially unsafe planning edits.
        if terminal == PlanningOutcome.FAILED:
            self._block(task, blocker_code, blocker_detail)
            return PlanningOutcome.FAILED

        persisted = self._persist_planning_artifacts(task, workspace)
        if persisted.error_code:
            self._block(task, persisted.error_code, persisted.error_detail)
            return PlanningOutcome.FAILED

        if terminal == PlanningOutcome.READY:
            if not persisted.persisted:
                self._block(
                    task,
                    "PLANNING_PRODUCED_NO_DIFF",
                    "Planner claimed READY without inspectable repository planning changes",
                )
                return PlanningOutcome.FAILED
            self.state.update_task(task.id, TaskExecutionState.PLANNING_COMPLETED)
            self.state.add_event(
                task.id,
                "PLANNING_READY",
                "planning artifacts merged; Builder dispatch may proceed",
            )
            return PlanningOutcome.READY

        # HUMAN_REQUIRED may have useful, structurally valid decision evidence. Persist it when
        # there is a verified diff, otherwise clean the unchanged worktree and retain the blocker.
        if not persisted.persisted:
            self._safe_cleanup(task)
        self._block(task, blocker_code, blocker_detail)
        return PlanningOutcome.HUMAN_REQUIRED

    def _run_domain(
        self, task: CanonicalTask, worktree: Path, attempt: int
    ) -> tuple[PlanningOutcome, str, str] | None:
        self.state.add_event(task.id, "PLANNING_ROLE", "Domain Architect")
        result = self.runner.run_domain_architect(task, worktree, attempt=attempt)
        if not result.success:
            return (
                PlanningOutcome.FAILED,
                "DOMAIN_ARCHITECT_FAILED",
                result.stderr or result.stdout,
            )
        marker = self._marker(result, "DOMAIN_RESULT")
        if marker == "UPDATED":
            return None
        if marker == "HUMAN_REQUIRED":
            return (
                PlanningOutcome.HUMAN_REQUIRED,
                "DOMAIN_HUMAN_DECISION_REQUIRED",
                "Domain Architect requires a human product/business decision",
            )
        return (
            PlanningOutcome.FAILED,
            "DOMAIN_ARCHITECT_PROTOCOL_ERROR",
            "Domain Architect returned no recognized DOMAIN_RESULT marker",
        )

    def _run_technical(
        self, task: CanonicalTask, worktree: Path, attempt: int
    ) -> tuple[PlanningOutcome, str, str] | None:
        self.state.add_event(task.id, "PLANNING_ROLE", "Technical Architect")
        result = self.runner.run_technical_architect(task, worktree, attempt=attempt)
        if not result.success:
            return (
                PlanningOutcome.FAILED,
                "TECHNICAL_ARCHITECT_FAILED",
                result.stderr or result.stdout,
            )
        marker = self._marker(result, "TECHNICAL_RESULT")
        if marker == "UPDATED":
            return None
        if marker == "DOMAIN_REQUIRED":
            return self._run_domain(task, worktree, attempt)
        if marker == "HUMAN_REQUIRED":
            return (
                PlanningOutcome.HUMAN_REQUIRED,
                "ARCHITECTURE_HUMAN_DECISION_REQUIRED",
                "Technical Architect requires a human architecture decision",
            )
        return (
            PlanningOutcome.FAILED,
            "TECHNICAL_ARCHITECT_PROTOCOL_ERROR",
            "Technical Architect returned no recognized TECHNICAL_RESULT marker",
        )

    def _persist_planning_artifacts(
        self, task: CanonicalTask, workspace: Workspace
    ) -> PlanningPersistenceResult:
        integration_prepared = False
        try:
            self.workspace.ensure_committed(task, workspace)
            changed_files = self.workspace.changed_files(workspace)
            unsafe = [
                path
                for path in changed_files
                if not path.startswith(self.ALLOWED_CHANGE_PREFIXES)
            ]
            if unsafe:
                return PlanningPersistenceResult(
                    False,
                    "PLANNING_SCOPE_VIOLATION",
                    "planning agents modified non-planning paths: " + ", ".join(unsafe),
                )

            diff = self.workspace.diff_text(workspace)
            if not diff.strip():
                return PlanningPersistenceResult(False)

            verification = self.verification.run(
                task, workspace.path, phase="planning"
            )
            if not verification.success:
                return PlanningPersistenceResult(
                    False,
                    "PLANNING_VERIFICATION_FAILED",
                    f"planning repository verification failed; log={verification.log_path}",
                )

            self.integration.prepare_main()
            integration_prepared = True
            if not self.integration.branch_is_on_remote_main(workspace.branch):
                if not self.integration.merge_branch(task, workspace.branch):
                    conflicts = self.integration.conflicted_files()
                    self.integration.abort_merge()
                    return PlanningPersistenceResult(
                        False,
                        "PLANNING_MERGE_CONFLICT_REQUIRES_HUMAN",
                        f"planning artifacts conflicted with latest main: {conflicts}",
                    )
                post = self.verification.run(
                    task, self.root, phase="planning-post-integration"
                )
                if not post.success:
                    self.integration.rollback_unpushed_main()
                    return PlanningPersistenceResult(
                        False,
                        "PLANNING_POST_INTEGRATION_VERIFICATION_FAILED",
                        f"log={post.log_path}",
                    )
                self.integration.push_main()

            self._safe_cleanup(task)
            return PlanningPersistenceResult(True)
        except (WorkspaceError, IntegrationError, BacklogValidationError) as exc:
            try:
                if integration_prepared:
                    self.integration.rollback_unpushed_main()
                else:
                    self.integration.abort_merge()
            except Exception as rollback_error:
                self.state.add_event(
                    task.id, "PLANNING_ROLLBACK_WARNING", repr(rollback_error)
                )
            return PlanningPersistenceResult(
                False, "PLANNING_INTEGRATION_ERROR", str(exc)
            )

    @staticmethod
    def _agent_output(result: AgentResult) -> str:
        """Return assistant-generated text without trusting echoed prompt/input JSON events."""
        texts: list[str] = []
        parsed_json = False
        for line in result.stdout.splitlines():
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                continue
            parsed_json = True
            if not isinstance(event, dict) or event.get("type") != "item.completed":
                continue
            item = event.get("item")
            if not isinstance(item, dict) or item.get("type") != "agent_message":
                continue
            text = item.get("text")
            if isinstance(text, str):
                texts.append(text)
        if texts:
            return "\n".join(texts)
        if parsed_json:
            # Fail closed when structured Codex output contains no final agent message rather
            # than scanning arbitrary JSON that may include echoed input/prompt content.
            return result.stderr
        # Fake/test runners return plain text rather than Codex JSONL.
        return f"{result.stdout}\n{result.stderr}"

    @classmethod
    def _marker(cls, result: AgentResult, prefix: str) -> str | None:
        output = cls._agent_output(result)
        for value in ("UPDATED", "DOMAIN_REQUIRED", "HUMAN_REQUIRED"):
            if f"{prefix}: {value}" in output:
                return value
        return None

    @classmethod
    def _planner_marker(cls, result: AgentResult) -> str | None:
        output = cls._agent_output(result)
        for value in (
            "DOMAIN_AND_TECHNICAL_REFINEMENT_REQUIRED",
            "DOMAIN_REFINEMENT_REQUIRED",
            "TECHNICAL_REFINEMENT_REQUIRED",
            "HUMAN_REQUIRED",
            "READY",
        ):
            if f"PLANNING_RESULT: {value}" in output:
                return value
        return None

    def _canonical_task_is_dispatchable(self, root: Path, task_id: str) -> bool:
        try:
            snapshot = BacklogReader(root, self.catalog).load()
            task = snapshot.tasks.get(task_id)
            return task is not None and BacklogReader.is_dispatchable(
                snapshot, task, active_resources=set()
            )
        except (BacklogValidationError, ValueError, OSError):
            return False

    def _block(self, task: CanonicalTask, code: str, detail: str) -> None:
        self.state.update_task(
            task.id,
            TaskExecutionState.HUMAN_REQUIRED,
            blocker_code=code,
            blocker_detail=(detail or code)[-20000:],
        )
        self.state.add_event(task.id, "PLANNING_BLOCKED", code)

    def _safe_cleanup(self, task: CanonicalTask) -> None:
        try:
            self.workspace.cleanup(task)
        except Exception as exc:
            self.state.add_event(
                task.id, "PLANNING_CLEANUP_WARNING", repr(exc)
            )


class PlanningAwareTaskOrchestrator:
    """Runs normal Ready work first, then invokes the planning factory when idle."""

    def __init__(self, delegate, planning: PlanningCoordinator):
        self.delegate = delegate
        self.planning = planning

    def __getattr__(self, name):
        return getattr(self.delegate, name)

    def _planning_candidate_payload(self) -> dict[str, object]:
        decision = self.planning.preview(self.delegate.validate())
        agent_runner = getattr(self.planning.runner, "runner", None)
        profile = getattr(agent_runner, "profile", PLANNING_CODEX_PROFILE)
        return {
            "task": decision.task_id,
            "maturity": decision.maturity,
            "gates": list(decision.gates),
            "provider": (
                getattr(agent_runner, "PROVIDER", "codex") if decision.task_id else None
            ),
            "model": profile.model if decision.task_id else None,
            "reasoning_effort": profile.reasoning_effort if decision.task_id else None,
            "service_tier": profile.service_tier if decision.task_id else None,
        }

    def plan_report(self) -> dict[str, object]:
        """Read-only operator preview for Builder dispatch and planning work."""
        dispatchable = [task.id for task in self.delegate.plan()]
        value: dict[str, object] = {"dispatchable": dispatchable}
        if not dispatchable:
            candidate = self._planning_candidate_payload()
            value["planning_candidate"] = candidate
            value["next_action"] = (
                "Run or Start launches Backlog Planner for this candidate."
                if candidate["task"]
                else "No dependency-satisfied planning candidate is available."
            )
        return value

    def dry_run(self) -> dict[str, object]:
        value = dict(self.delegate.dry_run())
        if not value.get("dispatchable"):
            value["planning_candidate"] = self._planning_candidate_payload()
        return value

    def run(self, *, resume: bool = False) -> None:
        first = True
        while True:
            self.delegate.run(resume=resume if first else False)
            first = False
            control = self.delegate.state.control_state()
            if control != OrchestratorState.IDLE:
                return

            snapshot = self.delegate.validate()
            if self.delegate.scheduler.plan(snapshot).dispatchable:
                self.delegate.state.clear_stop_and_run()
                continue

            outcome = self.planning.refine_next(snapshot)
            control = self.delegate.state.control_state()
            if control in {
                OrchestratorState.STOP_REQUESTED,
                OrchestratorState.STOPPING,
                OrchestratorState.STOPPED,
            }:
                self.delegate.state.set_control_state(OrchestratorState.STOPPED)
                return
            if outcome == PlanningOutcome.READY:
                self.delegate.state.clear_stop_and_run()
                continue
            if outcome in {PlanningOutcome.HUMAN_REQUIRED, PlanningOutcome.FAILED}:
                self.delegate.state.set_control_state(OrchestratorState.HUMAN_REQUIRED)
            return
