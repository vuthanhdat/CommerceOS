from __future__ import annotations

import json
import tempfile
import unittest
from http import HTTPStatus
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from helpers import row, write_backlog
from commerceos_orchestrator.backlog import BacklogReader
from commerceos_orchestrator.dashboard import LocalDashboardServer, RuntimeController
from commerceos_orchestrator.live_feed import LiveAgentFeed
from commerceos_orchestrator.models import OrchestratorState, TaskExecutionState
from commerceos_orchestrator.state import RunStateStore


class DashboardTests(unittest.TestCase):
    @staticmethod
    def control_request(url: str, *, method: str = "POST", payload: object | None = None):
        body = None if payload is None else json.dumps(payload).encode("utf-8")
        headers = {"X-CommerceOS-Dashboard": "1"}
        if payload is not None:
            headers["Content-Type"] = "application/json"
        return Request(url, method=method, data=body, headers=headers)

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
                self.assertIn("Live agent activity", html)
                self.assertIn("EventSource", html)
                self.assertIn("Click any task card to inspect it.", html)
                self.assertIn("Every task node is selectable.", html)
                self.assertIn("function selectTask(id)", html)
                self.assertIn("function dependencyNode(t)", html)
                self.assertIn("activeStates", html)
                self.assertIn("Owner:", html)
                self.assertIn("Next:", html)
                for command in ("Validate", "Plan", "Dry run", "Run", "Resume", "Cleanup"):
                    self.assertIn(f">{command}<", html)
                self.assertIn("Agent settings", html)
                self.assertIn("Sandbox", html)
                self.assertIn("danger-full-access", html)
                self.assertIn("Reviewer is always read-only.", html)
                self.assertIn("X-CommerceOS-Dashboard", html)
                self.assertIn("window.confirm", html)
                with urlopen(server.url + "api/status", timeout=2) as response:
                    status = json.load(response)
                self.assertEqual(status["ready_frontier"], ["TASK-0100"])
                ready_task = next(task for task in status["tasks"] if task["id"] == "TASK-0100")
                self.assertIsNone(ready_task["current_actor"])
                self.assertIn("evidence_counters", ready_task)
                logs = state.path.parent / "logs"
                logs.mkdir(parents=True, exist_ok=True)
                (logs / "TASK-0100-builder-1.log").write_text("builder log", encoding="utf-8")
                with urlopen(server.url + "api/tasks/TASK-0100", timeout=2) as response:
                    detail = json.load(response)
                self.assertEqual(detail["logs"][0]["name"], "TASK-0100-builder-1.log")
                request = self.control_request(server.url + "api/stop")
                with urlopen(request, timeout=2) as response:
                    stopped = json.load(response)
                self.assertTrue(stopped["accepted"])
                self.assertEqual(state.control_state(), OrchestratorState.STOPPED)
            finally:
                server.shutdown()
                thread.join(timeout=2)

    def test_settings_api_saves_resets_and_requires_dashboard_header(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            server = LocalDashboardServer(root, state, port=0)
            thread = server.serve_in_thread()
            try:
                with urlopen(server.url + "api/settings", timeout=12) as response:
                    view = json.load(response)
                self.assertEqual(view["settings"]["profiles"]["builder"]["model"], "gpt-5.6-terra")
                updated = view["settings"]
                updated["catalog"] = "orchestrator"
                updated["max_builders"] = 1
                updated["profiles"]["builder"]["sandbox_mode"] = "danger-full-access"
                request = self.control_request(
                    server.url + "api/settings", method="PUT", payload=updated
                )
                with urlopen(request, timeout=12) as response:
                    saved = json.load(response)
                self.assertTrue(saved["accepted"])
                self.assertTrue(saved["restart_required"])
                self.assertEqual(
                    saved["settings"]["profiles"]["builder"]["sandbox_mode"],
                    "danger-full-access",
                )
                self.assertTrue((root / ".commerceos/orchestrator/settings.json").is_file())

                updated["profiles"]["reviewer"]["sandbox_mode"] = "danger-full-access"
                unsafe = self.control_request(
                    server.url + "api/settings", method="PUT", payload=updated
                )
                with self.assertRaises(HTTPError) as unsafe_error:
                    urlopen(unsafe, timeout=3)
                self.assertEqual(unsafe_error.exception.code, HTTPStatus.BAD_REQUEST)
                updated["profiles"]["reviewer"]["sandbox_mode"] = "read-only"

                forbidden = Request(
                    server.url + "api/settings", method="PUT",
                    data=json.dumps(updated).encode("utf-8"),
                    headers={"Content-Type": "application/json"},
                )
                with self.assertRaises(HTTPError) as caught:
                    urlopen(forbidden, timeout=3)
                self.assertEqual(caught.exception.code, HTTPStatus.FORBIDDEN)

                reset = self.control_request(server.url + "api/settings/reset")
                with urlopen(reset, timeout=3) as response:
                    reset_value = json.load(response)
                self.assertEqual(reset_value["settings"]["catalog"], "commerceos")
                self.assertFalse((root / ".commerceos/orchestrator/settings.json").exists())
            finally:
                server.shutdown()
                thread.join(timeout=2)

    def test_unknown_typed_action_is_rejected_without_shell_execution(self):
        class StubRuntime:
            def execute(self, action):
                return {"error": "unsupported action", "action": action}, HTTPStatus.NOT_FOUND

        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            server = LocalDashboardServer(root, state, port=0, runtime=StubRuntime())
            thread = server.serve_in_thread()
            try:
                request = self.control_request(server.url + "api/actions/not-a-command")
                with self.assertRaises(HTTPError) as caught:
                    urlopen(request, timeout=3)
                self.assertEqual(caught.exception.code, HTTPStatus.NOT_FOUND)
            finally:
                server.shutdown()
                thread.join(timeout=2)

    def test_runtime_controller_exposes_read_only_cli_equivalents(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            snapshot = BacklogReader(root).load()

            class FakeOrchestrator:
                def validate(self):
                    return snapshot

                def plan(self):
                    return [snapshot.tasks["TASK-0100"]]

                def dry_run(self):
                    return {"dispatchable": ["TASK-0100"]}

            runtime = RuntimeController(FakeOrchestrator())
            validated, validate_status = runtime.execute("validate")
            planned, plan_status = runtime.execute("plan")
            dry_run, dry_status = runtime.execute("dry-run")
            self.assertEqual(validate_status, HTTPStatus.OK)
            self.assertTrue(validated["valid"])
            self.assertEqual(planned["dispatchable"], ["TASK-0100"])
            self.assertEqual(plan_status, HTTPStatus.OK)
            self.assertEqual(dry_run["result"]["dispatchable"], ["TASK-0100"])
            self.assertEqual(dry_status, HTTPStatus.OK)

    def test_live_feed_is_exposed_as_loopback_sse(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"], metadata={"TASK-0100": ""})
            state = RunStateStore(root / "state.db")
            feed = LiveAgentFeed(state.path.parent / "logs")
            feed.publish(
                "TASK-0100",
                "codex_started",
                role="builder",
                model="gpt-5.6-terra",
                reasoning_effort="medium",
                service_tier="standard",
            )
            server = LocalDashboardServer(root, state, port=0)
            thread = server.serve_in_thread()
            try:
                with urlopen(server.url + "api/tasks/TASK-0100/stream", timeout=2) as response:
                    self.assertTrue(
                        response.headers.get_content_type() == "text/event-stream"
                    )
                    record = None
                    for _ in range(6):
                        line = response.readline().decode("utf-8")
                        if line.startswith("data: "):
                            record = json.loads(line.removeprefix("data: "))
                            break
                self.assertIsNotNone(record)
                self.assertEqual(record["kind"], "codex_started")
                self.assertEqual(record["model"], "gpt-5.6-terra")
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
            state.update_task("TASK-0100", TaskExecutionState.INITIAL_BUILD)
            server = LocalDashboardServer(root, state, port=0)
            thread = server.serve_in_thread()
            try:
                with urlopen(server.url + "api/status", timeout=2) as response:
                    status = json.load(response)
                self.assertEqual(status["ready_frontier"], ["TASK-0101"])
                active = next(task for task in status["tasks"] if task["id"] == "TASK-0100")
                self.assertEqual(active["current_actor"], "BUILDER")
                self.assertIn("BuilderResultManifest/v1", active["next_transition_condition"])
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
