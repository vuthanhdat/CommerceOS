from __future__ import annotations

import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import helpers  # noqa: F401 - initializes the repository tools import path
from commerceos_orchestrator.models import OrchestratorState, TaskExecutionState
from commerceos_orchestrator.runtime_control import (
    RuntimeControlError,
    WorkerRegistration,
    WorkerRuntimeRegistry,
)
from commerceos_orchestrator.state import RunStateStore
from commerceos_orchestrator.state import InvalidTransitionError


class RuntimeControlTests(unittest.TestCase):
    def test_force_stop_preserves_active_checkpoint_and_clears_drain(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            state = RunStateStore(root / "runtime" / "state.db")
            state.clear_stop_and_run()
            state.claim_task("TASK-0100", branch="agent/task", worktree=str(root / "worktree"))
            state.update_task(
                "TASK-0100",
                TaskExecutionState.INITIAL_BUILD,
                attempt_delta=2,
                output_artifact_id="builder:2",
            )
            state.request_stop()
            before = state.task_run("TASK-0100")

            preserved = state.force_stop(1234)
            after = state.task_run("TASK-0100")

            self.assertEqual(preserved, ["TASK-0100"])
            self.assertEqual(state.control_state(), OrchestratorState.STOPPED)
            self.assertEqual(after.execution_state, before.execution_state)
            self.assertEqual(after.branch, before.branch)
            self.assertEqual(after.worktree, before.worktree)
            self.assertEqual(after.attempt, before.attempt)
            self.assertEqual(after.output_artifact_id, before.output_artifact_id)
            self.assertFalse(after.drain_at_stop)
            event = next(item for item in state.recent_events(10) if item["kind"] == "FORCE_STOPPED")
            self.assertEqual(json.loads(event["detail"])["worker_pid"], 1234)

    def test_force_stop_validates_identity_before_termination_or_state_change(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            state = RunStateStore(root / "runtime" / "state.db")
            state.clear_stop_and_run()
            registry = WorkerRuntimeRegistry(root, state.path, "commerceos")
            registration = WorkerRegistration(
                pid=4321,
                token="token",
                repository=str(root),
                catalog="commerceos",
                state_path=str(state.path),
                command="resume",
                started_at="now",
            )
            registry._write(registration)

            with (
                patch.object(registry, "_pid_alive", return_value=True),
                patch.object(registry, "_identity_matches", return_value=False),
                patch.object(registry, "_terminate_tree") as terminate,
                self.assertRaisesRegex(RuntimeControlError, "no longer identifies"),
            ):
                registry.force_stop(state)

            terminate.assert_not_called()
            self.assertEqual(state.control_state(), OrchestratorState.RUNNING)
            rejection = next(
                item for item in state.recent_events(10) if item["kind"] == "FORCE_STOP_REJECTED"
            )
            self.assertEqual(json.loads(rejection["detail"])["code"], "IDENTITY_MISMATCH")

    def test_force_stop_fence_rejects_late_stage_transition(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            state = RunStateStore(root / "runtime" / "state.db")
            state.clear_stop_and_run()
            state.claim_task("TASK-0100", branch="agent/task", worktree=str(root / "worktree"))
            state.update_task("TASK-0100", TaskExecutionState.INITIAL_BUILD)
            before = state.task_run("TASK-0100")

            fence = state.begin_force_stop(4321)
            self.assertEqual(state.control_state(), OrchestratorState.FORCE_STOPPING)
            with self.assertRaisesRegex(InvalidTransitionError, "force-stop transition fence"):
                state.update_task(
                    "TASK-0100",
                    TaskExecutionState.PRE_REVIEW_VERIFICATION,
                    actor="VERIFICATION_RUNNER",
                )

            after = state.task_run("TASK-0100")
            self.assertEqual(after.execution_state, before.execution_state)
            self.assertEqual(after.output_artifact_id, before.output_artifact_id)
            rejection = next(
                item for item in state.recent_events(10) if item["kind"] == "TRANSITION_REJECTED"
            )
            self.assertIn("force-stop transition fence", rejection["detail"])
            state.abort_force_stop(fence, "test cleanup")
            self.assertEqual(state.control_state(), OrchestratorState.RUNNING)

    def test_termination_failure_rolls_back_fence_without_mutating_task(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            state = RunStateStore(root / "runtime" / "state.db")
            state.clear_stop_and_run()
            state.claim_task("TASK-0100", branch="agent/task", worktree=str(root / "worktree"))
            state.update_task("TASK-0100", TaskExecutionState.INITIAL_BUILD)
            before = state.task_run("TASK-0100")
            registry = WorkerRuntimeRegistry(root, state.path, "commerceos")
            registry._write(
                WorkerRegistration(
                    pid=4321,
                    token="token",
                    repository=str(root),
                    catalog="commerceos",
                    state_path=str(state.path),
                    command="resume",
                    started_at="now",
                )
            )
            with (
                patch.object(registry, "_pid_alive", return_value=True),
                patch.object(registry, "_identity_matches", return_value=True),
                patch.object(
                    registry,
                    "_terminate_tree",
                    side_effect=RuntimeControlError("termination failed"),
                ),
                self.assertRaisesRegex(RuntimeControlError, "termination failed"),
            ):
                registry.force_stop(state)

            after = state.task_run("TASK-0100")
            self.assertEqual(state.control_state(), OrchestratorState.RUNNING)
            self.assertEqual(after, before)
            self.assertTrue(registry.path.exists())

    def test_force_stop_terminates_tree_then_preserves_registration_checkpoint(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            state = RunStateStore(root / "runtime" / "state.db")
            state.clear_stop_and_run()
            state.claim_task("TASK-0100", branch="agent/task", worktree=str(root / "worktree"))
            state.update_task("TASK-0100", TaskExecutionState.INITIAL_BUILD)
            registry = WorkerRuntimeRegistry(root, state.path, "commerceos")
            registry._write(
                WorkerRegistration(
                    pid=4321,
                    token="token",
                    repository=str(root),
                    catalog="commerceos",
                    state_path=str(state.path),
                    command="resume",
                    started_at="now",
                )
            )
            alive = iter((True, False, False))
            with (
                patch.object(registry, "_pid_alive", side_effect=lambda _pid: next(alive)),
                patch.object(registry, "_identity_matches", return_value=True),
                patch.object(registry, "_terminate_tree") as terminate,
            ):
                result = registry.force_stop(state)

            terminate.assert_called_once_with(4321)
            self.assertEqual(result["preserved_tasks"], ["TASK-0100"])
            self.assertFalse(result["worktrees_removed"])
            self.assertFalse(registry.path.exists())

    def test_missing_or_malformed_registration_fails_closed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            state = RunStateStore(root / "runtime" / "state.db")
            registry = WorkerRuntimeRegistry(root, state.path, "commerceos")
            with self.assertRaisesRegex(RuntimeControlError, "no registered"):
                registry.force_stop(state)
            missing = next(
                item for item in state.recent_events(10) if item["kind"] == "FORCE_STOP_REJECTED"
            )
            self.assertEqual(json.loads(missing["detail"])["code"], "REGISTRATION_MISSING")
            registry.path.write_text("not-json", encoding="utf-8")
            with self.assertRaisesRegex(RuntimeControlError, "malformed"):
                registry.force_stop(state)
            malformed = next(
                item for item in state.recent_events(10) if item["kind"] == "FORCE_STOP_REJECTED"
            )
            self.assertEqual(json.loads(malformed["detail"])["code"], "REGISTRATION_INVALID")

    def test_windows_termination_targets_registered_pid_tree(self):
        completed = subprocess.CompletedProcess([], 0, "SUCCESS", "")
        with (
            patch("commerceos_orchestrator.runtime_control.os.name", "nt"),
            patch("commerceos_orchestrator.runtime_control.subprocess.run", return_value=completed) as run,
        ):
            WorkerRuntimeRegistry._terminate_tree(4321)
        self.assertEqual(run.call_args.args[0], ["taskkill.exe", "/PID", "4321", "/T", "/F"])

    def test_identity_requires_catalog_and_unpredictable_worker_token(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            state = RunStateStore(root / "runtime" / "state.db")
            registry = WorkerRuntimeRegistry(root, state.path, "commerceos")
            registration = WorkerRegistration(
                pid=4321,
                token="secret-token",
                repository=str(root),
                catalog="commerceos",
                state_path=str(state.path),
                command="resume",
                started_at="now",
            )
            valid = (
                f"python {root / 'tools/orchestrator.py'} --repo {root} "
                f"--state {state.path} --catalog commerceos "
                "--worker-token secret-token resume"
            )
            with patch.object(registry, "_command_line", return_value=valid):
                self.assertTrue(registry._identity_matches(registration))
            with patch.object(
                registry, "_command_line", return_value=valid.replace("secret-token", "other")
            ):
                self.assertFalse(registry._identity_matches(registration))
            with patch.object(
                registry,
                "_command_line",
                return_value=valid.replace("--repo " + str(root) + " ", ""),
            ):
                self.assertFalse(registry._identity_matches(registration))
            with patch.object(
                registry,
                "_command_line",
                return_value=valid.replace("--catalog commerceos", "--catalog commerceos2"),
            ):
                self.assertFalse(registry._identity_matches(registration))

    def test_worker_status_distinguishes_unregistered_stale_and_running(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            state = RunStateStore(root / "runtime" / "state.db")
            registry = WorkerRuntimeRegistry(root, state.path, "commerceos")
            self.assertEqual(registry.status()["state"], "UNREGISTERED")
            registry._write(
                WorkerRegistration(
                    pid=4321,
                    token="secret-token",
                    repository=str(root),
                    catalog="commerceos",
                    state_path=str(state.path),
                    command="resume",
                    started_at="now",
                )
            )
            with patch.object(registry, "_pid_alive", return_value=False):
                self.assertEqual(registry.status()["state"], "STALE")
            with (
                patch.object(registry, "_pid_alive", return_value=True),
                patch.object(registry, "_identity_matches", return_value=True),
            ):
                self.assertEqual(registry.status()["state"], "RUNNING")

if __name__ == "__main__":
    unittest.main()
