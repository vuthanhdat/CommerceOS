from __future__ import annotations

import json
import threading
import webbrowser
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlparse

from .backlog import BacklogReader, BacklogValidationError
from .models import TaskExecutionState
from .scheduler import Scheduler
from .service import TaskOrchestrator
from .state import RunStateStore


class DashboardReadModel:
    def __init__(self, root: Path, state_store: RunStateStore):
        self.root = root.resolve()
        self.state = state_store

    def status(self) -> dict[str, object]:
        snapshot = BacklogReader(self.root).load()
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
                    in {TaskExecutionState.BLOCKED, TaskExecutionState.HUMAN_REQUIRED}
                )
            tasks.append(self._summary(task, run))

        ready = Scheduler(self.state).plan(snapshot).dispatchable
        total = len(snapshot.tasks)
        return {
            "orchestrator_state": self.state.control_state().value,
            "progress": {
                "completed": completed,
                "total": total,
                "percent": round(completed / total * 100, 1) if total else 100.0,
            },
            "ready_frontier": [task.id for task in ready],
            "active_builders": sum(
                lanes.get(name, 0)
                for name in ("QUEUED", "BUILDING", "VERIFYING", "FIX_REQUIRED")
            ),
            "active_reviewers": lanes.get("REVIEWING", 0),
            "merge_queue_length": lanes.get("MERGE_QUEUED", 0) + lanes.get("INTEGRATING", 0),
            "blocker_count": blockers,
            "tasks": tasks,
            "events": self.state.recent_events(50),
        }

    def task_detail(self, task_id: str) -> dict[str, object] | None:
        snapshot = BacklogReader(self.root).load()
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

    @staticmethod
    def _summary(task, run) -> dict[str, object]:
        return {
            "id": task.id,
            "title": task.title,
            "domain": task.domain,
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
    ):
        if host not in {"127.0.0.1", "localhost", "::1"}:
            raise ValueError("V1 dashboard must bind to a local loopback interface")
        self.root = root.resolve()
        self.state = state_store
        self.read_model = DashboardReadModel(self.root, self.state)
        self.runtime = runtime
        self.httpd = ThreadingHTTPServer((host, port), self._handler())
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
                        self._send(_HTML.encode(), "text/html; charset=utf-8")
                        return
                    if parsed.path == "/api/status":
                        self._json(server.read_model.status())
                        return
                    if parsed.path.startswith("/api/tasks/"):
                        task_id = unquote(parsed.path.removeprefix("/api/tasks/"))
                        detail = server.read_model.task_detail(task_id)
                        if detail is None:
                            self._json({"error": "task not found"}, HTTPStatus.NOT_FOUND)
                        else:
                            self._json(detail)
                        return
                    self._json({"error": "not found"}, HTTPStatus.NOT_FOUND)
                except BacklogValidationError as exc:
                    self._json({"error": "BACKLOG_INVALID", "detail": str(exc)}, HTTPStatus.CONFLICT)
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


_HTML = r'''<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>CommerceOS Orchestrator</title>
<style>body{font:14px system-ui;margin:0;background:#11151b;color:#e8ebf0}header,main{padding:16px 22px}header{display:flex;justify-content:space-between;background:#181d25}.cards,.lanes{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:10px}.card,.panel{background:#181d25;border:1px solid #303746;border-radius:10px;padding:12px;margin-bottom:12px}.big{font-size:24px;font-weight:800}.task{background:#232a35;padding:8px;margin:7px 0;border-radius:7px;cursor:pointer}.small{font-size:12px;color:#a8b0bd}button{padding:9px 14px;border:0;border-radius:7px;font-weight:700}.stop{background:#d84d4d;color:#fff}pre{white-space:pre-wrap;word-break:break-word}#dag{display:flex;flex-wrap:wrap;gap:6px}.node{background:#252d39;padding:5px 8px;border-radius:12px}</style></head><body>
<header><div><b>CommerceOS Orchestrator</b><div id="state" class="small">Loading…</div></div><div><button id="resume">Resume</button> <button id="stop" class="stop">Stop gracefully</button></div></header>
<main><div class="cards"><div class="card">Progress<div id="progress" class="big">—</div></div><div class="card">Ready<div id="ready" class="big">—</div></div><div class="card">Builders<div id="builders" class="big">—</div></div><div class="card">Reviewers<div id="reviewers" class="big">—</div></div><div class="card">Merge<div id="merge" class="big">—</div></div><div class="card">Blocked<div id="blocked" class="big">—</div></div></div>
<div class="panel"><h3>Workflow</h3><div class="lanes"><div>Ready<div id="col-ready"></div></div><div>Active<div id="col-active"></div></div><div>Review<div id="col-review"></div></div><div>Merge<div id="col-merge"></div></div><div>Blocked<div id="col-blocked"></div></div></div></div>
<div class="panel"><h3>DAG / progress</h3><div id="dag"></div></div><div class="panel"><h3>Task detail</h3><pre id="detail">Select a task.</pre></div><div class="panel"><h3>Recent activity</h3><pre id="events"></pre></div></main>
<script>
const $=id=>document.getElementById(id);const lane=t=>{const s=t.execution_state;if(['QUEUED','BUILDING','VERIFYING','FIX_REQUIRED'].includes(s))return'active';if(s==='REVIEWING')return'review';if(['MERGE_QUEUED','INTEGRATING'].includes(s))return'merge';if(['BLOCKED','HUMAN_REQUIRED'].includes(s))return'blocked';if(t.maturity==='Ready'&&t.lifecycle_state==='Backlog')return'ready';return'other'};
function text(tag,value,cls){const e=document.createElement(tag);e.textContent=String(value??'');if(cls)e.className=cls;return e}function card(t){const e=text('div','',`task ${lane(t)}`);e.tabIndex=0;e.append(text('b',t.id),text('div',t.title),text('div',`${t.execution_state||t.maturity} · ${t.domain}`,'small'));const open=()=>detail(t.id);e.addEventListener('click',open);e.addEventListener('keydown',x=>{if(x.key==='Enter'){x.preventDefault();open()}});return e}
function renderColumn(name,tasks){const nodes=tasks.filter(t=>lane(t)===name).map(card);$("col-"+name).replaceChildren(...(nodes.length?nodes:[text('div','None','small')]))}async function refresh(){try{const r=await fetch('/api/status',{cache:'no-store'}),d=await r.json();if(!r.ok)throw Error(JSON.stringify(d));$('state').textContent=`State: ${d.orchestrator_state}`;$('progress').textContent=`${d.progress.completed}/${d.progress.total} (${d.progress.percent}%)`;$('ready').textContent=d.ready_frontier.length;$('builders').textContent=d.active_builders;$('reviewers').textContent=d.active_reviewers;$('merge').textContent=d.merge_queue_length;$('blocked').textContent=d.blocker_count;['ready','active','review','merge','blocked'].forEach(x=>renderColumn(x,d.tasks));$('dag').replaceChildren(...d.tasks.map(t=>{const n=text('span',t.id,'node');n.title=`deps: ${t.depends_on.join(', ')}`;return n}));$('events').textContent=d.events.map(e=>`${e.created_at} ${e.task_id||'-'} ${e.kind} ${e.detail}`).join('\n')}catch(e){$('state').textContent='Dashboard error: '+e}}async function detail(id){const r=await fetch('/api/tasks/'+encodeURIComponent(id));$('detail').textContent=JSON.stringify(await r.json(),null,2)}async function control(path){await fetch(path,{method:'POST'});await refresh()}$('stop').addEventListener('click',()=>control('/api/stop'));$('resume').addEventListener('click',()=>control('/api/resume'));refresh();setInterval(refresh,1500);
</script></body></html>'''
