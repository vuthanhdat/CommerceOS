from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
from urllib.request import Request, urlopen

from helpers import row, write_backlog
from commerceos_orchestrator.dashboard import LocalDashboardServer
from commerceos_orchestrator.models import OrchestratorState
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
                with urlopen(server.url + "api/status", timeout=2) as response:
                    status = json.load(response)
                self.assertEqual(status["ready_frontier"], ["TASK-0100"])
                request = Request(server.url + "api/stop", method="POST", data=b"")
                with urlopen(request, timeout=2) as response:
                    stopped = json.load(response)
                self.assertTrue(stopped["accepted"])
                self.assertEqual(state.control_state(), OrchestratorState.STOPPED)
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
