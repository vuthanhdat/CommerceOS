from __future__ import annotations

import json
import sys
import threading
import time
import webbrowser
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlparse

from .backlog import BacklogReader, BacklogValidationError
from .dashboard_ui import DASHBOARD_HTML
from .live_feed import LiveAgentFeed
from .models import TaskExecutionState
from .scheduler import Scheduler
from .service import TaskOrchestrator
from .state import RunStateStore
from .observability import evidence_counters, workflow_status


class _QuietThreadingHTTPServer(ThreadingHTTPServer):
    """Do not log normal browser disconnects as server failures on Windows."""

    def handle_error(self, request, client_address):  # noqa: N802
        exc_type, exc, _ = sys.exc_info()
        if isinstance(exc, (BrokenPipeError, ConnectionResetError, ConnectionAbortedError)):
            return
        super().handle_error(request, client_address)


class DashboardReadModel:
    def __init__(self, root: Path, state_store: RunStateStore, catalog: str = "commerceos"):
        self.root = root.resolve()
        self.state = state_store
        self.catalog = catalog

    def status(self) -> dict[str, object]:
        snapshot = BacklogReader(self.root, self.catalog).load()
        runs = {run.task_id: run for run in self.state.task_runs()}
        tasks: list[dict[str, object]] = []
        lanes: dict[str, int] = {}
        completed = 0
        blockers = 0
        for task in snapshot.tasks.values():
            run = runs.get(task.id)
            completed += int(task.lifecycle_state == "Completed")
            if run:
                name = run.execution_state.value
                lanes[name] = lanes.get(name, 0) + 1
                blockers += int(
                    run.execution_state
                    in {
                        TaskExecutionState.PLANNING_REQUIRED,
                        TaskExecutionState.ORCHESTRATOR_ACTION_REQUIRED,
                        TaskExecutionState.BLOCKED,
                        TaskExecutionState.HUMAN_REQUIRED,
                    }
                )
            tasks.append(self._summary(task, run))

        ready = Scheduler(self.state).plan(snapshot).dispatchable
        total = len(snapshot.tasks)
        return {
            "catalog": self.catalog,
            "orchestrator_state": self.state.control_state().value,
            "progress": {
                "completed": completed,
                "total": total,
                "percent": round(completed / total * 100, 1) if total else 100.0,
            },
            "ready_frontier": [task.id for task in ready],
            "active_builders": sum(
                lanes.get(name, 0)
                for name in (
                    "QUEUED",
                    "INITIAL_BUILD",
                    "PRE_REVIEW_VERIFICATION",
                    "REPAIR_REQUIRED",
                    "REPAIR_BUILD",
                    "REPAIR_VERIFICATION",
                )
            ),
            "active_reviewers": lanes.get("FIRST_REVIEW", 0) + lanes.get("RE_REVIEW", 0),
            "merge_queue_length": (
                lanes.get("MERGE_QUEUED", 0)
                + lanes.get("INTEGRATING", 0)
                + lanes.get("FINALIZING", 0)
            ),
            "blocker_count": blockers,
            "tasks": tasks,
            "events": self.state.recent_events(50),
        }

    def task_detail(self, task_id: str) -> dict[str, object] | None:
        snapshot = BacklogReader(self.root, self.catalog).load()
        task = snapshot.tasks.get(task_id)
        if not task:
            return None
        result = self._summary(task, self.state.task_run(task_id))
        result.update(
            goal=task.goal,
            exclusive_resources=list(task.exclusive_resources),
            merge_policy=task.merge_policy,
            events=[
                event
                for event in self.state.recent_events(300)
                if event.get("task_id") == task_id
            ],
            logs=self._logs(task_id),
        )
        return result

    def _logs(self, task_id: str) -> list[dict[str, object]]:
        root = self.state.path.parent / "logs"
        if not root.is_dir():
            return []
        values = []
        for path in root.glob(f"{task_id}-*.log"):
            stat = path.stat()
            values.append(
                {
                    "name": path.name,
                    "path": str(path),
                    "size_bytes": stat.st_size,
                    "modified_at_epoch": stat.st_mtime,
                }
            )
        return sorted(values, key=lambda item: float(item["modified_at_epoch"]), reverse=True)

    def _summary(self, task, run) -> dict[str, object]:
        owner, next_condition = workflow_status(run.execution_state if run else None)
        return {
            "id": task.id,
            "title": task.title,
            "domain": task.domain,
            "catalog": task.catalog,
            "maturity": task.maturity,
            "lifecycle_state": task.lifecycle_state,
            "execution_state": run.execution_state.value if run else None,
            "depends_on": list(task.depends_on),
            "gates": list(task.gates),
            "spec_path": task.spec_path,
            "owner_role": task.owner_role,
            "model_class": task.model_class,
            "cloud_verification": task.cloud_verification,
            "branch": run.branch if run else None,
            "worktree": run.worktree if run else None,
            "attempt": run.attempt if run else 0,
            "fix_attempt": run.fix_attempt if run else 0,
            "blocker_code": run.blocker_code if run else None,
            "blocker_detail": run.blocker_detail if run else None,
            "drain_at_stop": run.drain_at_stop if run else False,
            "updated_at": run.updated_at if run else None,
            "contract_version": run.contract_version if run else None,
            "input_artifact_id": run.input_artifact_id if run else None,
            "output_artifact_id": run.output_artifact_id if run else None,
            "current_actor": owner,
            "next_transition_condition": next_condition,
            "evidence_counters": evidence_counters(self.root, task.catalog, task.id),
        }


class RuntimeController:
    def __init__(self, orchestrator: TaskOrchestrator):
        self.orchestrator = orchestrator
        self._lock = threading.Lock()
        self._thread: threading.Thread | None = None

    def start(self, *, resume: bool = False) -> bool:
        with self._lock:
            if self._thread and self._thread.is_alive():
                if resume:
                    self.orchestrator.state.clear_stop_and_run()
                return False
            self._thread = threading.Thread(
                target=self.orchestrator.run,
                kwargs={"resume": resume},
                name="commerceos-orchestrator",
                daemon=True,
            )
            self._thread.start()
            return True

    def stop(self) -> list[str]:
        return self.orchestrator.request_stop()

    def resume(self) -> bool:
        return self.start(resume=True)


class LocalDashboardServer:
    def __init__(
        self,
        root: Path,
        state_store: RunStateStore,
        *,
        host: str = "127.0.0.1",
        port: int = 8765,
        runtime: RuntimeController | None = None,
        catalog: str = "commerceos",
    ):
        if host not in {"127.0.0.1", "localhost", "::1"}:
            raise ValueError("V1 dashboard must bind to a local loopback interface")
        self.root = root.resolve()
        self.state = state_store
        self.read_model = DashboardReadModel(self.root, self.state, catalog)
        self.live_feed = LiveAgentFeed(self.state.path.parent / "logs")
        self.runtime = runtime
        self.httpd = _QuietThreadingHTTPServer((host, port), self._handler())
        self.host = host
        self.port = int(self.httpd.server_address[1])

    @property
    def url(self) -> str:
        host = "127.0.0.1" if self.host in {"localhost", "::1"} else self.host
        return f"http://{host}:{self.port}/"

    def serve_forever(self, *, open_browser: bool = False) -> None:
        if open_browser:
            threading.Timer(0.2, lambda: webbrowser.open(self.url)).start()
        self.httpd.serve_forever(poll_interval=0.25)

    def serve_in_thread(self) -> threading.Thread:
        thread = threading.Thread(target=self.serve_forever, daemon=True)
        thread.start()
        return thread

    def shutdown(self) -> None:
        self.httpd.shutdown()
        self.httpd.server_close()

    def _handler(self):
        server = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, format: str, *args) -> None:  # noqa: A003
                return

            def do_GET(self) -> None:  # noqa: N802
                parsed = urlparse(self.path)
                try:
                    if parsed.path == "/":
                        self._send(DASHBOARD_HTML.encode(), "text/html; charset=utf-8")
                        return
                    if parsed.path == "/api/status":
                        self._json(server.read_model.status())
                        return
                    prefix = "/api/tasks/"
                    if parsed.path.startswith(prefix) and parsed.path.endswith("/stream"):
                        task_id = unquote(parsed.path[len(prefix) : -len("/stream")].rstrip("/"))
                        if server.read_model.task_detail(task_id) is None:
                            self._json({"error": "task not found"}, HTTPStatus.NOT_FOUND)
                        else:
                            self._stream(task_id)
                        return
                    if parsed.path.startswith(prefix):
                        task_id = unquote(parsed.path.removeprefix(prefix))
                        detail = server.read_model.task_detail(task_id)
                        if detail is None:
                            self._json({"error": "task not found"}, HTTPStatus.NOT_FOUND)
                        else:
                            self._json(detail)
                        return
                    self._json({"error": "not found"}, HTTPStatus.NOT_FOUND)
                except BacklogValidationError as exc:
                    self._json({"error": "BACKLOG_INVALID", "detail": str(exc)}, HTTPStatus.CONFLICT)
                # Browsers and VS Code routinely close an EventSource connection while
                # reconnecting or switching tasks. Windows reports that disconnect as
                # ConnectionAbortedError (WinError 10053); it is a normal client-side
                # disconnect and must not trigger a second error response on the dead socket.
                except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
                    return
                except Exception as exc:
                    self._json({"error": "INTERNAL", "detail": repr(exc)}, HTTPStatus.INTERNAL_SERVER_ERROR)

            def do_POST(self) -> None:  # noqa: N802
                try:
                    path = urlparse(self.path).path
                    if path == "/api/stop":
                        ids = server.runtime.stop() if server.runtime else server.state.request_stop()
                        self._json({"accepted": True, "draining": ids}, HTTPStatus.ACCEPTED)
                        return
                    if path == "/api/resume":
                        if server.runtime:
                            started = server.runtime.resume()
                        else:
                            server.state.clear_stop_and_run()
                            started = False
                        self._json({"accepted": True, "scheduler_started": started}, HTTPStatus.ACCEPTED)
                        return
                    self._json({"error": "not found"}, HTTPStatus.NOT_FOUND)
                except Exception as exc:
                    self._json({"error": "CONTROL_FAILED", "detail": repr(exc)}, HTTPStatus.CONFLICT)

            def _stream(self, task_id: str) -> None:
                path = server.live_feed.path_for(task_id)
                try:
                    offset = max(0, int(self.headers.get("Last-Event-ID", "0") or "0"))
                except ValueError:
                    offset = 0
                self.send_response(HTTPStatus.OK)
                self.send_header("Content-Type", "text/event-stream; charset=utf-8")
                self.send_header("Cache-Control", "no-store")
                self.send_header("Connection", "keep-alive")
                self.end_headers()
                next_keepalive = time.monotonic()
                # Keep the EventSource open while the task is running. The previous
                # 15-second deadline made a healthy stream look frozen while the
                # browser was repeatedly reconnecting between chunks of activity.
                while True:
                    if path.is_file():
                        size = path.stat().st_size
                        if offset > size:
                            offset = 0
                        with path.open("rb") as handle:
                            handle.seek(offset)
                            while True:
                                raw = handle.readline()
                                if not raw:
                                    break
                                offset = handle.tell()
                                data = raw.decode("utf-8", errors="replace").rstrip("\r\n")
                                if data:
                                    self.wfile.write(f"id: {offset}\ndata: {data}\n\n".encode())
                                    self.wfile.flush()
                    now = time.monotonic()
                    if now >= next_keepalive:
                        self.wfile.write(b": keepalive\n\n")
                        self.wfile.flush()
                        next_keepalive = now + 2.0
                    time.sleep(0.25)

            def _json(self, value: object, status: HTTPStatus = HTTPStatus.OK) -> None:
                self._send(
                    json.dumps(value, ensure_ascii=False).encode(),
                    "application/json; charset=utf-8",
                    status,
                )

            def _send(
                self,
                payload: bytes,
                content_type: str,
                status: HTTPStatus = HTTPStatus.OK,
            ) -> None:
                self.send_response(status)
                self.send_header("Content-Type", content_type)
                self.send_header("Content-Length", str(len(payload)))
                self.send_header("Cache-Control", "no-store")
                self.end_headers()
                self.wfile.write(payload)

        return Handler
