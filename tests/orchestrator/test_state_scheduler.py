from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from helpers import row, write_backlog
from commerceos_orchestrator.backlog import BacklogReader
from commerceos_orchestrator.models import OrchestratorState, TaskExecutionState
from commerceos_orchestrator.scheduler import Scheduler
from commerceos_orchestrator.state import RunStateStore


class StateAndSchedulerTests(unittest.TestCase):
    def test_graceful_stop_marks_only_active_runs_and_rejects_new_claim(self):
        with tempfile.TemporaryDirectory() as td:
            state = RunStateStore(Path(td) / "state.db")
            state.clear_stop_and_run()
            self.assertTrue(state.claim_task("TASK-0100"))
            state.update_task("TASK-0100", TaskExecutionState.INITIAL_BUILD)
            draining = state.request_stop()
            self.assertEqual(draining, ["TASK-0100"])
            self.assertEqual(state.control_state(), OrchestratorState.STOP_REQUESTED)
            self.assertTrue(state.task_run("TASK-0100").drain_at_stop)
            self.assertFalse(state.claim_task("TASK-0101"))

    def test_stop_with_no_active_work_is_immediately_stopped(self):
        with tempfile.TemporaryDirectory() as td:
            state = RunStateStore(Path(td) / "state.db")
            self.assertEqual(state.request_stop(), [])
            self.assertEqual(state.control_state(), OrchestratorState.STOPPED)

    def test_local_human_required_is_not_auto_redispatched(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(
                root,
                [row("TASK-0100"), row("TASK-0101")],
                ready=["TASK-0100", "TASK-0101"],
                metadata={"TASK-0100": "", "TASK-0101": ""},
            )
            snap = BacklogReader(root).load()
            state = RunStateStore(root / "state.db")
            state.clear_stop_and_run()
            state.claim_task("TASK-0100")
            state.update_task("TASK-0100", TaskExecutionState.HUMAN_REQUIRED, blocker_code="TEST")
            decision = Scheduler(state, max_builders=2).plan(snap)
            self.assertEqual([t.id for t in decision.dispatchable], ["TASK-0101"])
            state.reset_retryable_terminal_runs()
            decision = Scheduler(state, max_builders=2).plan(snap)
            self.assertEqual([t.id for t in decision.dispatchable], ["TASK-0100", "TASK-0101"])

    def test_exclusive_resources_prevent_parallel_selection(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(
                root,
                [row("TASK-0100"), row("TASK-0101")],
                ready=["TASK-0100", "TASK-0101"],
                metadata={"TASK-0100": "shared", "TASK-0101": "shared"},
            )
            snap = BacklogReader(root).load()
            state = RunStateStore(root / "state.db")
            state.clear_stop_and_run()
            decision = Scheduler(state, max_builders=2).plan(snap)
            self.assertEqual(len(decision.dispatchable), 1)


if __name__ == "__main__":
    unittest.main()
