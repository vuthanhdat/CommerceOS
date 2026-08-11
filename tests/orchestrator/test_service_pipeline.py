from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from helpers import row, write_backlog
from commerceos_orchestrator.agents import FakeAgentRunner
from commerceos_orchestrator.backlog import BacklogReader
from commerceos_orchestrator.models import AgentResult, OrchestratorState, ReviewResult, TaskExecutionState, Workspace
from commerceos_orchestrator.service import OrchestratorConfig, TaskOrchestrator
from commerceos_orchestrator.state import RunStateStore
from commerceos_orchestrator.verification import FakeVerificationRunner


class FakeWorkspaceManager:
    def __init__(self, root: Path, *, diff: str = "diff --git a/x b/x\n+change\n"):
        self.root = root
        self.diff = diff
        self.cleanup_calls = 0
    def workspace_for(self, task):
        return Workspace(f"agent/{task.id}-{task.slug}", self.root, False)
    def ensure_committed(self, task, workspace):
        return "abc"
    def diff_text(self, workspace):
        return self.diff
    def cleanup(self, task, force: bool = False):
        self.cleanup_calls += 1
        return True


class FakeIntegrationManager:
    def __init__(self):
        self.calls: list[str] = []
        self.remote = False
    def prepare_main(self): self.calls.append("prepare")
    def branch_is_on_remote_main(self, branch): self.calls.append("ancestor"); return self.remote
    def merge_branch(self, task, branch): self.calls.append("merge"); return True
    def conflicted_files(self): return []
    def abort_merge(self): self.calls.append("abort")
    def rollback_unpushed_main(self): self.calls.append("rollback")
    def commit_current_merge(self, task): self.calls.append("commit-merge")
    def commit_bookkeeping(self, task): self.calls.append("bookkeeping")
    def push_main(self): self.calls.append("push"); self.remote = True


def review(passed: bool, text: str) -> ReviewResult:
    raw = AgentResult(True, 0, text, "", "")
    return ReviewResult(passed, text, raw)


class PipelineTests(unittest.TestCase):
    def test_builder_verify_review_merge_and_bookkeeping(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(review_results=[review(True, "REVIEW_RESULT: PASS")])
            verify = FakeVerificationRunner([True, True, True])
            workspace = FakeWorkspaceManager(root)
            integration = FakeIntegrationManager()
            orch = TaskOrchestrator(root, state, agents, verify, workspace_manager=workspace, integration_manager=integration, config=OrchestratorConfig(max_builders=1, poll_seconds=0.01))
            orch.run()
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.COMPLETED)
            self.assertEqual(agents.builder_calls, 1)
            self.assertEqual(agents.reviewer_calls, 1)
            self.assertEqual([phase for _, phase in verify.calls], ["builder-0", "post-integration", "post-bookkeeping"])
            self.assertIn("push", integration.calls)
            self.assertTrue((root / "tasks/completed/TASK-0100-spec.md").exists())
            snap = BacklogReader(root).load()
            self.assertEqual(snap.tasks["TASK-0100"].lifecycle_state, "Completed")
            self.assertEqual(snap.ready_frontier_declared, ())
            self.assertEqual(state.control_state(), OrchestratorState.IDLE)

    def test_verification_failure_enters_bounded_fix_loop(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(review_results=[review(True, "REVIEW_RESULT: PASS")])
            verify = FakeVerificationRunner([False, True, True, True])
            orch = TaskOrchestrator(root, state, agents, verify, workspace_manager=FakeWorkspaceManager(root), integration_manager=FakeIntegrationManager(), config=OrchestratorConfig(max_builders=1,max_fix_attempts=2,poll_seconds=0.01))
            orch.run()
            self.assertEqual(agents.builder_calls, 2)
            self.assertEqual(state.task_run("TASK-0100").fix_attempt, 1)
            self.assertEqual(state.task_run("TASK-0100").execution_state, TaskExecutionState.COMPLETED)

    def test_reviewer_finding_returns_to_builder(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(review_results=[review(False, "finding\nREVIEW_RESULT: FIX_REQUIRED"), review(True, "REVIEW_RESULT: PASS")])
            verify = FakeVerificationRunner([True, True, True, True])
            orch = TaskOrchestrator(root, state, agents, verify, workspace_manager=FakeWorkspaceManager(root), integration_manager=FakeIntegrationManager(), config=OrchestratorConfig(max_builders=1,max_fix_attempts=2,poll_seconds=0.01))
            orch.run()
            self.assertEqual(agents.builder_calls, 2)
            self.assertEqual(agents.reviewer_calls, 2)
            self.assertEqual(state.task_run("TASK-0100").execution_state, TaskExecutionState.COMPLETED)

    def test_no_diff_fails_closed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            orch = TaskOrchestrator(root, state, FakeAgentRunner(), FakeVerificationRunner([True]), workspace_manager=FakeWorkspaceManager(root,diff=""), integration_manager=FakeIntegrationManager(), config=OrchestratorConfig(max_builders=1,poll_seconds=0.01))
            orch.run()
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.HUMAN_REQUIRED)
            self.assertEqual(run.blocker_code, "BUILDER_PRODUCED_NO_DIFF")
            self.assertEqual(state.control_state(), OrchestratorState.HUMAN_REQUIRED)

    def test_required_cloud_task_needs_explicit_runtime_consent(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100", cloud='"required"')], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            orch = TaskOrchestrator(root, state, FakeAgentRunner(), FakeVerificationRunner(), workspace_manager=FakeWorkspaceManager(root), integration_manager=FakeIntegrationManager(), config=OrchestratorConfig(max_builders=1,poll_seconds=0.01,allow_cloud=False))
            orch.run()
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.HUMAN_REQUIRED)
            self.assertEqual(run.blocker_code, "CLOUD_EXECUTION_NOT_AUTHORIZED")


if __name__ == "__main__":
    unittest.main()
