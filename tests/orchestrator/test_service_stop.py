from __future__ import annotations

import tempfile
import threading
import time
import unittest
from pathlib import Path

from helpers import row, write_backlog
from commerceos_orchestrator.agents import FakeAgentRunner
from commerceos_orchestrator.models import OrchestratorState, TaskExecutionState
from commerceos_orchestrator.service import OrchestratorConfig, TaskOrchestrator
from commerceos_orchestrator.state import RunStateStore
from commerceos_orchestrator.verification import FakeVerificationRunner


class DrainingOrchestrator(TaskOrchestrator):
    def __init__(self, *args, release: threading.Event, started: threading.Event, **kwargs):
        super().__init__(*args, **kwargs)
        self.release = release
        self.started = started
        self.started_ids: list[str] = []
        self._started_lock = threading.Lock()

    def _execute_task(self, task, resume: bool) -> None:
        with self._started_lock:
            self.started_ids.append(task.id)
            if len(self.started_ids) >= 2:
                self.started.set()
        self.state.update_task(task.id, TaskExecutionState.INITIAL_BUILD)
        self.release.wait(timeout=5)
        self.state.update_task(task.id, TaskExecutionState.PRE_REVIEW_VERIFICATION)
        self.state.update_task(task.id, TaskExecutionState.FIRST_REVIEW)
        self.state.update_task(task.id, TaskExecutionState.MERGE_QUEUED)
        self.state.update_task(task.id, TaskExecutionState.INTEGRATING)
        self.state.update_task(task.id, TaskExecutionState.FINALIZING)
        self.state.update_task(task.id, TaskExecutionState.COMPLETED)


class GracefulStopServiceTests(unittest.TestCase):
    def test_stop_drains_two_active_tasks_and_does_not_start_third(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(
                root,
                [row("TASK-0100"), row("TASK-0101"), row("TASK-0102")],
                ready=["TASK-0100", "TASK-0101", "TASK-0102"],
                metadata={"TASK-0100": "", "TASK-0101": "", "TASK-0102": ""},
            )
            state = RunStateStore(root / "state.db")
            release = threading.Event()
            started = threading.Event()
            orchestrator = DrainingOrchestrator(
                root,
                state,
                FakeAgentRunner(),
                FakeVerificationRunner(),
                config=OrchestratorConfig(max_builders=2, poll_seconds=0.02),
                release=release,
                started=started,
            )
            thread = threading.Thread(target=orchestrator.run)
            thread.start()
            self.assertTrue(started.wait(timeout=3))
            draining = orchestrator.request_stop()
            self.assertCountEqual(draining, ["TASK-0100", "TASK-0101"])
            release.set()
            thread.join(timeout=5)
            self.assertFalse(thread.is_alive())
            self.assertCountEqual(orchestrator.started_ids, ["TASK-0100", "TASK-0101"])
            self.assertIsNone(state.task_run("TASK-0102"))
            self.assertEqual(state.control_state(), OrchestratorState.STOPPED)

    def test_persisted_stop_survives_restart_without_fresh_dispatch(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100"), row("TASK-0101")], ready=["TASK-0100", "TASK-0101"], metadata={"TASK-0100":"", "TASK-0101":""})
            state = RunStateStore(root / "state.db")
            state.clear_stop_and_run()
            state.claim_task("TASK-0100")
            state.update_task("TASK-0100", TaskExecutionState.INITIAL_BUILD)
            state.request_stop()
            release = threading.Event(); release.set()
            started = threading.Event()
            orchestrator = DrainingOrchestrator(root, state, FakeAgentRunner(), FakeVerificationRunner(), config=OrchestratorConfig(max_builders=2,poll_seconds=0.02), release=release, started=started)
            orchestrator.run()
            self.assertEqual(orchestrator.started_ids, ["TASK-0100"])
            self.assertIsNone(state.task_run("TASK-0101"))
            self.assertEqual(state.control_state(), OrchestratorState.STOPPED)


if __name__ == "__main__":
    unittest.main()
