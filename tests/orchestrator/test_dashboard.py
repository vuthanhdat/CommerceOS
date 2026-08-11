from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
from urllib.request import Request, urlopen

from helpers import row, write_backlog
from commerceos_orchestrator.dashboard import LocalDashboardServer
from commerceos_orchestrator.models import OrchestratorState, TaskExecutionState
from commerceos_orchestrator.state import RunStateStore


class DashboardTests(unittest.TestCase):
    def test_local_dashboard_status_and_stop_control(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            server = LocalDashboardServer(root, state, port=0)
            thread = server.serve_in_thread()
            try:
                with urlopen(server.url, timeout=2) as response:
                    html = response.read().decode("utf-8")
                self.assertNotIn("innerHTML", html)
                self.assertNotIn("onclick=", html)
                with urlopen(server.url + "api/status", timeout=2) as response:
                    status = json.load(response)
                self.assertEqual(status["ready_frontier"], ["TASK-0100"])
                logs = state.path.parent / "logs"
                logs.mkdir(parents=True, exist_ok=True)
                (logs / "TASK-0100-builder-1.log").write_text("builder log", encoding="utf-8")
                with urlopen(server.url + "api/tasks/TASK-0100", timeout=2) as response:
                    detail = json.load(response)
                self.assertEqual(detail["logs"][0]["name"], "TASK-0100-builder-1.log")
                request = Request(server.url + "api/stop", method="POST", data=b"")
                with urlopen(request, timeout=2) as response:
                    stopped = json.load(response)
                self.assertTrue(stopped["accepted"])
                self.assertEqual(state.control_state(), OrchestratorState.STOPPED)
            finally:
                server.shutdown()
                thread.join(timeout=2)

    def test_runtime_ready_frontier_excludes_locally_active_task(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(
                root,
                [row("TASK-0100"), row("TASK-0101")],
                ready=["TASK-0100", "TASK-0101"],
                metadata={"TASK-0100": "", "TASK-0101": ""},
            )
            state = RunStateStore(root / "state.db")
            state.clear_stop_and_run()
            self.assertTrue(state.claim_task("TASK-0100"))
            state.update_task("TASK-0100", TaskExecutionState.BUILDING)
            server = LocalDashboardServer(root, state, port=0)
            thread = server.serve_in_thread()
            try:
                with urlopen(server.url + "api/status", timeout=2) as response:
                    status = json.load(response)
                self.assertEqual(status["ready_frontier"], ["TASK-0101"])
            finally:
                server.shutdown()
                thread.join(timeout=2)

    def test_non_loopback_binding_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            with self.assertRaisesRegex(ValueError, "loopback"):
                LocalDashboardServer(root, state, host="0.0.0.0", port=0)


if __name__ == "__main__":
    unittest.main()
