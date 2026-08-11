from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from helpers import row, write_backlog
from commerceos_orchestrator.backlog import BacklogReader
from commerceos_orchestrator.models import AgentResult, TaskExecutionState, Workspace
from commerceos_orchestrator.planning import (
    CodexPlanningAgentRunner,
    FakePlanningAgentRunner,
    PlanningCoordinator,
    PlanningOutcome,
)
from commerceos_orchestrator.state import RunStateStore
from commerceos_orchestrator.verification import FakeVerificationRunner


def result(text: str) -> AgentResult:
    return AgentResult(True, 0, text, "", "")


class FakeWorkspaceManager:
    def __init__(self, root: Path, *, diff: str = ""):
        self.root = root
        self.diff = diff
        self.cleaned: list[str] = []

    def workspace_for(self, task):
        return Workspace(branch=f"agent/{task.id}-planning", path=self.root, created=False)

    def ensure_committed(self, task, workspace):
        return "fake-sha"

    def diff_text(self, workspace, base="origin/main"):
        return self.diff

    def cleanup(self, task, force=False):
        self.cleaned.append(task.id)
        return True


class FakeIntegrationManager:
    def __init__(self):
        self.prepare_calls = 0

    def prepare_main(self):
        self.prepare_calls += 1
        return None

    def branch_is_on_remote_main(self, branch):
        return True

    def merge_branch(self, task, branch):
        return True

    def conflicted_files(self):
        return []

    def abort_merge(self):
        return None

    def rollback_unpushed_main(self):
        return None

    def push_main(self):
        return None


class PromotingPlanningRunner(FakePlanningAgentRunner):
    def __init__(self, root: Path):
        super().__init__()
        self.root = root

    def run_backlog_planner(self, task, worktree, *, attempt):
        self.calls.append("backlog-planner")
        shard = self.root / "tasks/backlog-v2/00.yaml"
        shard.write_text(
            shard.read_text(encoding="utf-8").replace(
                f"[{task.id}, Refined,", f"[{task.id}, Ready,", 1
            ),
            encoding="utf-8",
        )
        master = self.root / "tasks/BACKLOG.v2.yaml"
        text = master.read_text(encoding="utf-8")
        text = text.replace("ready_frontier:\n", f"ready_frontier:\n  - {task.id}\n", 1)
        master.write_text(text, encoding="utf-8")
        return result("PLANNING_RESULT: READY")


class PlanningCoordinatorTests(unittest.TestCase):
    def test_nearest_dependency_satisfied_non_ready_task_is_planning_candidate(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(
                root,
                [
                    row("TASK-0100", maturity="Refined", deps="[TASK-0001]"),
                    row("TASK-0101", maturity="Outline", deps="[TASK-0100]"),
                ],
                ready=[],
            )
            state = RunStateStore(root / "state.db")
            coordinator = PlanningCoordinator(
                root,
                state,
                FakePlanningAgentRunner(),
                FakeVerificationRunner(),
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
            )
            candidate = coordinator.next_candidate(BacklogReader(root).load())
            self.assertIsNotNone(candidate)
            self.assertEqual(candidate.id, "TASK-0100")

    def test_planner_routes_to_domain_architect_then_returns_to_planner(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100", maturity="Refined")], ready=[])
            state = RunStateStore(root / "state.db")
            runner = FakePlanningAgentRunner(
                planner_results=[
                    result("PLANNING_RESULT: DOMAIN_REFINEMENT_REQUIRED"),
                    result("PLANNING_RESULT: HUMAN_REQUIRED"),
                ],
                domain_results=[result("DOMAIN_RESULT: UPDATED")],
            )
            coordinator = PlanningCoordinator(
                root,
                state,
                runner,
                FakeVerificationRunner(),
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
            )
            outcome = coordinator.refine_next(BacklogReader(root).load())
            self.assertEqual(outcome, PlanningOutcome.HUMAN_REQUIRED)
            self.assertEqual(
                runner.calls,
                ["backlog-planner", "domain-architect", "backlog-planner"],
            )

    def test_planner_routes_to_technical_architect_only_when_requested(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100", maturity="Outline")], ready=[])
            state = RunStateStore(root / "state.db")
            runner = FakePlanningAgentRunner(
                planner_results=[
                    result("PLANNING_RESULT: TECHNICAL_REFINEMENT_REQUIRED"),
                    result("PLANNING_RESULT: HUMAN_REQUIRED"),
                ],
                technical_results=[result("TECHNICAL_RESULT: UPDATED")],
            )
            coordinator = PlanningCoordinator(
                root,
                state,
                runner,
                FakeVerificationRunner(),
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
            )
            outcome = coordinator.refine_next(BacklogReader(root).load())
            self.assertEqual(outcome, PlanningOutcome.HUMAN_REQUIRED)
            self.assertEqual(
                runner.calls,
                ["backlog-planner", "technical-architect", "backlog-planner"],
            )

    def test_ready_result_requires_canonical_ready_gate_then_releases_for_builder(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100", maturity="Refined")], ready=[])
            state = RunStateStore(root / "state.db")
            workspace = FakeWorkspaceManager(root, diff="planning diff")
            integration = FakeIntegrationManager()
            verification = FakeVerificationRunner()
            coordinator = PlanningCoordinator(
                root,
                state,
                PromotingPlanningRunner(root),
                verification,
                workspace_manager=workspace,
                integration_manager=integration,
            )
            outcome = coordinator.refine_next(BacklogReader(root).load())
            self.assertEqual(outcome, PlanningOutcome.READY)
            planned = BacklogReader(root).load().tasks["TASK-0100"]
            self.assertEqual(planned.maturity, "Ready")
            self.assertEqual(state.task_run("TASK-0100").execution_state, TaskExecutionState.COMPLETED)
            self.assertEqual(verification.calls, [("TASK-0100", "planning")])
            self.assertEqual(integration.prepare_calls, 1)
            self.assertEqual(workspace.cleaned, ["TASK-0100"])

    def test_protocol_failure_never_integrates_partial_planning_edits(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100", maturity="Refined")], ready=[])
            state = RunStateStore(root / "state.db")
            integration = FakeIntegrationManager()
            coordinator = PlanningCoordinator(
                root,
                state,
                FakePlanningAgentRunner(planner_results=[result("no protocol marker")]),
                FakeVerificationRunner(),
                workspace_manager=FakeWorkspaceManager(root, diff="unsafe partial edit"),
                integration_manager=integration,
            )
            outcome = coordinator.refine_next(BacklogReader(root).load())
            self.assertEqual(outcome, PlanningOutcome.FAILED)
            self.assertEqual(integration.prepare_calls, 0)
            self.assertEqual(
                state.task_run("TASK-0100").blocker_code,
                "BACKLOG_PLANNER_PROTOCOL_ERROR",
            )

    def test_planning_codex_execution_boundary_is_sol_medium_standard(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            runner = CodexPlanningAgentRunner(root, root / "logs").runner
            command = runner._build_command(
                "codex",
                worktree=root,
                writable=True,
                prompt="planning prompt",
            )
            self.assertIn("gpt-5.6-sol", command)
            self.assertIn('model_reasoning_effort="medium"', command)
            self.assertIn('service_tier="default"', command)
            self.assertNotIn("gpt-5.6-luna", command)


if __name__ == "__main__":
    unittest.main()
