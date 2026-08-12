from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from helpers import row, write_backlog
from commerceos_orchestrator.agents import FakeAgentRunner
from commerceos_orchestrator.backlog import BacklogReader, BacklogWriter
from commerceos_orchestrator.models import AgentResult, OrchestratorState, ReviewResult, TaskExecutionState, Workspace
from commerceos_orchestrator.service import OrchestratorConfig, TaskOrchestrator
from commerceos_orchestrator.state import RunStateStore
from commerceos_orchestrator.verification import FakeVerificationRunner


class FakeWorkspaceManager:
    def __init__(self, root: Path, *, diff: str = "diff --git a/x b/x\n+change\n"):
        self.root = root
        self.diff = diff
        self.cleanup_calls = 0
        self.lifecycle_restore_calls = 0
    def workspace_for(self, task):
        return Workspace(f"agent/{task.id}-{task.slug}", self.root, False)
    def ensure_committed(self, task, workspace):
        return "abc"
    def diff_text(self, workspace):
        return self.diff
    def changed_files(self, workspace):
        return ["x"] if self.diff.strip() else []
    def cleanup(self, task, force: bool = False):
        self.cleanup_calls += 1
        return True
    def restore_task_lifecycle(self, task, directory):
        self.lifecycle_restore_calls += 1


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


class MalformedStageOutputOrchestrator(TaskOrchestrator):
    @staticmethod
    def _stage_output_payload(*args, **kwargs):
        payload = TaskOrchestrator._stage_output_payload(*args, **kwargs)
        if payload["stage"] == "builder":
            payload.pop("contract_version")
        return payload


def review(passed: bool, text: str) -> ReviewResult:
    raw = AgentResult(True, 0, text, "", "")
    return ReviewResult(passed, text, raw)


def capacity_failure() -> AgentResult:
    return AgentResult(
        False,
        1,
        '{"type":"error","message":"Selected model is at capacity. Please try a different model."}\n',
        "",
        "",
    )


def reviewer_environment_failure() -> ReviewResult:
    raw = AgentResult(
        False,
        0,
        "",
        "CreateProcessAsUserW failed: 5 (Access is denied.)",
        "review.log",
        "ENVIRONMENT_UNAVAILABLE",
    )
    return ReviewResult(False, raw.stderr, raw)


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
            validated = {
                json.loads(event["detail"])["stage"]
                for event in state.recent_events(100)
                if event["kind"] == "STAGE_OUTPUT_VALIDATED"
            }
            self.assertEqual(
                validated,
                {"builder", "verification", "reviewer", "integration", "finalization"},
            )
            review_call = agents.review_calls[0]
            self.assertTrue(review_call["builder_manifest_path"])
            self.assertTrue(review_call["verification_report_path"])
            self.assertTrue((root / review_call["builder_manifest_path"]).is_file())
            self.assertTrue((root / review_call["verification_report_path"]).is_file())

    def test_invalid_builder_manifest_prevents_verification_and_review(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(
                builder_results=[AgentResult(True, 0, "", "", "", evidence={})]
            )
            verify = FakeVerificationRunner([True])
            orch = TaskOrchestrator(
                root,
                state,
                agents,
                verify,
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
                config=OrchestratorConfig(max_builders=1, poll_seconds=0.01),
            )
            orch.run()
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.HUMAN_REQUIRED)
            self.assertEqual(run.blocker_code, "BUILDER_EVIDENCE_INVALID")
            self.assertEqual(verify.calls, [])
            self.assertEqual(agents.reviewer_calls, 0)

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
            validated = [
                json.loads(event["detail"])["stage"]
                for event in state.recent_events(100)
                if event["kind"] == "STAGE_OUTPUT_VALIDATED"
            ]
            self.assertIn("repair_builder", validated)

    def test_malformed_production_stage_output_fails_closed_before_verification(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(review_results=[review(True, "REVIEW_RESULT: PASS")])
            verify = FakeVerificationRunner([True])
            orch = MalformedStageOutputOrchestrator(
                root,
                state,
                agents,
                verify,
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
                config=OrchestratorConfig(max_builders=1, poll_seconds=0.01),
            )
            orch.run()
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.HUMAN_REQUIRED)
            self.assertEqual(run.blocker_code, "INVALID_STAGE_OUTPUT")
            self.assertEqual(verify.calls, [])
            self.assertEqual(agents.reviewer_calls, 0)

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

    def test_non_builder_review_finding_routes_to_planning_root(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(
                review_results=[
                    review(
                        False,
                        "FINDING F-001 STATUS: OPEN OWNER: DOMAIN_ARCHITECT ROUTE: PLANNING_REQUIRED "
                        "TITLE: business invariant is unresolved\nREVIEW_RESULT: FIX_REQUIRED",
                    )
                ]
            )
            orch = TaskOrchestrator(
                root,
                state,
                agents,
                FakeVerificationRunner([True]),
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
                config=OrchestratorConfig(max_builders=1, max_fix_attempts=2, poll_seconds=0.01),
            )
            orch.run()
            run = state.task_run("TASK-0100")
            self.assertEqual(run.blocker_code, "PLANNING_REQUIRED")
            self.assertIn("Backlog Planner", run.blocker_detail)
            self.assertEqual(agents.builder_calls, 1)
            events = state.recent_events(limit=10)
            self.assertTrue(any(event["kind"] == "REVIEW_ROUTED" for event in events))

    def test_repair_review_receives_the_previous_review_ledger_and_final_scope(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(
                review_results=[
                    review(False, "FINDING F-001 STATUS: OPEN TITLE: missing test\nREVIEW_RESULT: FIX_REQUIRED"),
                    review(True, "FINDING F-001 STATUS: RESOLVED TITLE: missing test\nREVIEW_RESULT: PASS"),
                ]
            )
            verify = FakeVerificationRunner([True, True, True, True])
            orch = TaskOrchestrator(
                root,
                state,
                agents,
                verify,
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
                config=OrchestratorConfig(max_builders=1, max_fix_attempts=1, poll_seconds=0.01),
            )
            orch.run()
            self.assertEqual(len(agents.review_calls), 2)
            self.assertIsNone(agents.review_calls[0]["context"])
            self.assertFalse(agents.review_calls[0]["final"])
            self.assertIsNotNone(agents.review_calls[1]["context"])
            self.assertTrue(agents.review_calls[1]["final"])
            self.assertTrue(agents.review_calls[0]["builder_manifest_path"])
            self.assertTrue(agents.review_calls[0]["verification_report_path"])
            self.assertIn("FINDING F-001", Path(root / agents.review_calls[1]["context"]).read_text(encoding="utf-8"))

    def test_explicit_open_finding_cannot_be_hidden_behind_pass_marker(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(
                review_results=[
                    review(
                        True,
                        "FINDING F-001 STATUS: OPEN TITLE: missing regression test\nREVIEW_RESULT: PASS",
                    ),
                    review(True, "FINDING F-001 STATUS: RESOLVED TITLE: fixed\nREVIEW_RESULT: PASS"),
                ]
            )
            orch = TaskOrchestrator(
                root,
                state,
                agents,
                FakeVerificationRunner([True, True, True, True]),
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
                config=OrchestratorConfig(max_builders=1, max_fix_attempts=1, poll_seconds=0.01),
            )
            orch.run()
            self.assertEqual(state.task_run("TASK-0100").execution_state, TaskExecutionState.COMPLETED)

    def test_reviewer_environment_failure_stops_without_builder_fix_loop(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(review_results=[reviewer_environment_failure()])
            orch = TaskOrchestrator(
                root,
                state,
                agents,
                FakeVerificationRunner([True]),
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
                config=OrchestratorConfig(max_builders=1, max_fix_attempts=2, poll_seconds=0.01),
            )
            orch.run()
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.HUMAN_REQUIRED)
            self.assertEqual(run.blocker_code, "REVIEWER_ENVIRONMENT_UNAVAILABLE")
            self.assertEqual(agents.builder_calls, 1)
            self.assertEqual(agents.reviewer_calls, 1)

    def test_builder_premature_completion_is_restored_before_verification(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            initial = BacklogReader(root).load()
            task = initial.tasks["TASK-0100"]
            BacklogWriter(root).finalize_task(initial, task, "premature completion fixture")
            state = RunStateStore(root / "state.db")
            self.assertTrue(state.claim_task("TASK-0100"))
            agents = FakeAgentRunner(review_results=[review(True, "REVIEW_RESULT: PASS")])
            workspace = FakeWorkspaceManager(root)
            orch = TaskOrchestrator(
                root,
                state,
                agents,
                FakeVerificationRunner([True]),
                workspace_manager=workspace,
                integration_manager=FakeIntegrationManager(),
                config=OrchestratorConfig(max_builders=1, poll_seconds=0.01),
            )
            self.assertTrue(orch._builder_left_task_open(task, root))
            run = state.task_run("TASK-0100")
            self.assertNotEqual(run.execution_state, TaskExecutionState.HUMAN_REQUIRED)
            self.assertIsNone(run.blocker_code)
            self.assertEqual(workspace.lifecycle_restore_calls, 1)

    def test_model_capacity_is_retried_without_consuming_fix_budget_or_changing_model_policy(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(
                builder_results=[capacity_failure(), AgentResult(True, 0, "", "", "")],
                review_results=[review(True, "REVIEW_RESULT: PASS")],
            )
            orch = TaskOrchestrator(
                root,
                state,
                agents,
                FakeVerificationRunner([True, True, True]),
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
                config=OrchestratorConfig(
                    max_builders=1,
                    poll_seconds=0.01,
                    max_capacity_retries=3,
                    capacity_backoff_seconds=0,
                ),
            )
            orch.run()
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.COMPLETED)
            self.assertEqual(run.attempt, 2)
            self.assertEqual(run.fix_attempt, 0)
            self.assertEqual(agents.builder_calls, 2)
            retry_events = [
                event for event in state.recent_events(100)
                if event["task_id"] == "TASK-0100" and event["kind"] == "MODEL_CAPACITY_RETRY"
            ]
            self.assertEqual(len(retry_events), 1)
            self.assertIn("pinned_model_unchanged=true", retry_events[0]["detail"])

    def test_model_capacity_exhaustion_becomes_retryable_blocked_not_human_task_failure(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            agents = FakeAgentRunner(builder_results=[capacity_failure(), capacity_failure(), capacity_failure()])
            orch = TaskOrchestrator(
                root,
                state,
                agents,
                FakeVerificationRunner(),
                workspace_manager=FakeWorkspaceManager(root),
                integration_manager=FakeIntegrationManager(),
                config=OrchestratorConfig(
                    max_builders=1,
                    poll_seconds=0.01,
                    max_capacity_retries=2,
                    capacity_backoff_seconds=0,
                ),
            )
            orch.run()
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.BLOCKED)
            self.assertEqual(run.blocker_code, "MODEL_CAPACITY_EXHAUSTED")
            self.assertEqual(run.attempt, 3)
            self.assertEqual(agents.builder_calls, 3)
            self.assertIn("model fallback is disabled by policy", run.blocker_detail)
            self.assertEqual(state.control_state(), OrchestratorState.HUMAN_REQUIRED)

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

    def test_required_localstack_task_does_not_need_real_cloud_consent(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100", cloud='"required"')], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            orch = TaskOrchestrator(root, state, FakeAgentRunner(), FakeVerificationRunner(), workspace_manager=FakeWorkspaceManager(root), integration_manager=FakeIntegrationManager(), config=OrchestratorConfig(max_builders=1,poll_seconds=0.01,allow_cloud=False))
            orch.run()
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.COMPLETED)
            self.assertIsNone(run.blocker_code)


if __name__ == "__main__":
    unittest.main()
