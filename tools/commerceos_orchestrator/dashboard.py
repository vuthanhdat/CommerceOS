from __future__ import annotations

import json
import threading
import webbrowser
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlparse

from .backlog import BacklogReader, BacklogValidationError
from .models import OrchestratorState, TaskExecutionState
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
        maturity_counts: dict[str, int] = {}
        lifecycle_counts: dict[str, int] = {}
        lane_counts: dict[str, int] = {}
        blockers = 0
        completed = 0

        for task in snapshot.tasks.values():
            maturity_counts[task.maturity] = maturity_counts.get(task.maturity, 0) + 1
            lifecycle_counts[task.lifecycle_state] = lifecycle_counts.get(task.lifecycle_state, 0) + 1
            if task.lifecycle_state == "Completed":
                completed += 1
            run = runs.get(task.id)
            execution_state = run.execution_state.value if run else None
            if execution_state:
                lane_counts[execution_state] = lane_counts.get(execution_state, 0) + 1
            if run and run.execution_state in {
                TaskExecutionState.BLOCKED,
                TaskExecutionState.HUMAN_REQUIRED,
            }:
                blockers += 1
            tasks.append(self._task_summary(task, run))

        ready = BacklogReader.ready_frontier(snapshot, active_resources=set())
        return {
            "orchestrator_state": self.state.control_state().value,
            "progress": {
                "completed": completed,
                "total": len(snapshot.tasks),
                "percent": round((completed / len(snapshot.tasks) * 100), 1) if snapshot.tasks else 100.0,
            },
            "ready_frontier": [task.id for task in ready],
            "maturity_counts": maturity_counts,
            "lifecycle_counts": lifecycle_counts,
            "lane_counts": lane_counts,
            "active_builders": sum(
                count
                for name, count in lane_counts.items()
                if name in {"QUEUED", "BUILDING", "VERIFYING", "FIX_REQUIRED"}
            ),
            "active_reviewers": lane_counts.get("REVIEWING", 0),
            "merge_queue_length": lane_counts.get("MERGE_QUEUED", 0) + lane_counts.get("INTEGRATING", 0),
            "blocker_count": blockers,
            "tasks": tasks,
            "events": self.state.recent_events(50),
        }

    def task_detail(self, task_id: str) -> dict[str, object] | None:
        snapshot = BacklogReader(self.root).load()
        task = snapshot.tasks.get(task_id)
        if not task:
            return None
        run = self.state.task_run(task_id)
        result = self._task_summary(task, run)
        result["goal"] = task.goal
        result["exclusive_resources"] = list(task.exclusive_resources)
        result["merge_policy"] = task.merge_policy
        result["events"] = [
            event for event in self.state.recent_events(300) if event.get("task_id") == task_id
        ]
        return result

    @staticmethod
    def _task_summary(task, run) -> dict[str, object]:
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
    """Small process-local controller shared by dashboard and CLI start mode."""

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

    @property
    def running(self) -> bool:
        return bool(self._thread and self._thread.is_alive())


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
        handler = self._make_handler()
        self.httpd = ThreadingHTTPServer((host, port), handler)
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
        thread = threading.Thread(target=self.serve_forever, name="commerceos-dashboard", daemon=True)
        thread.start()
        return thread

    def shutdown(self) -> None:
        self.httpd.shutdown()
        self.httpd.server_close()

    def _make_handler(self):
        server = self

        class Handler(BaseHTTPRequestHandler):
            server_version = "CommerceOSOrchestrator/1"

            def log_message(self, format: str, *args) -> None:  # noqa: A003
                return

            def do_GET(self) -> None:  # noqa: N802
                parsed = urlparse(self.path)
                try:
                    if parsed.path == "/":
                        self._html(_DASHBOARD_HTML)
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
                except Exception as exc:  # fail visibly, never hide dashboard control failures
                    self._json({"error": "INTERNAL", "detail": repr(exc)}, HTTPStatus.INTERNAL_SERVER_ERROR)

            def do_POST(self) -> None:  # noqa: N802
                parsed = urlparse(self.path)
                try:
                    if parsed.path == "/api/stop":
                        ids = server.runtime.stop() if server.runtime else server.state.request_stop()
                        self._json({"accepted": True, "draining": ids}, HTTPStatus.ACCEPTED)
                        return
                    if parsed.path == "/api/resume":
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
                payload = json.dumps(value, ensure_ascii=False).encode("utf-8")
                self.send_response(status)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Content-Length", str(len(payload)))
                self.send_header("Cache-Control", "no-store")
                self.end_headers()
                self.wfile.write(payload)

            def _html(self, value: str) -> None:
                payload = value.encode("utf-8")
                self.send_response(HTTPStatus.OK)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(payload)))
                self.send_header("Cache-Control", "no-store")
                self.end_headers()
                self.wfile.write(payload)

        return Handler


_DASHBOARD_HTML = r'''<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width,initial-scale=1" />
<title>CommerceOS Orchestrator</title>
<style>
:root { color-scheme: light dark; font-family: Inter, ui-sans-serif, system-ui, sans-serif; }
body { margin:0; background:#101216; color:#e8ebf0; }
header { position:sticky; top:0; z-index:3; display:flex; justify-content:space-between; align-items:center; gap:16px; padding:18px 24px; background:#171a20; border-bottom:1px solid #2b3038; }
button { border:0; border-radius:9px; padding:10px 16px; cursor:pointer; font-weight:700; }
.stop { background:#e14b4b; color:white; } .resume { background:#d6dde8; color:#111; }
main { padding:20px 24px 48px; display:grid; gap:18px; }
.cards { display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); gap:12px; }
.card,.panel { background:#171a20; border:1px solid #2b3038; border-radius:12px; padding:14px; }
.big { font-size:26px; font-weight:800; margin-top:4px; }
.board { display:grid; grid-template-columns:repeat(5,minmax(180px,1fr)); gap:12px; overflow:auto; }
.column { min-height:120px; }
.task { background:#20242c; margin:8px 0; padding:9px; border-radius:8px; cursor:pointer; border-left:4px solid #6d7684; }
.task.ready { border-color:#47b881; } .task.active { border-color:#5794f2; } .task.review { border-color:#e0a72f; } .task.merge { border-color:#a879e8; } .task.blocked { border-color:#e14b4b; }
.small { font-size:12px; color:#9da7b4; } pre { white-space:pre-wrap; word-break:break-word; }
#dag { display:flex; flex-wrap:wrap; gap:7px; } .node { padding:5px 8px; border-radius:16px; background:#242933; font-size:12px; }
.node.completed { opacity:.55; } .node.ready { outline:1px solid #47b881; } .node.active { outline:1px solid #5794f2; } .node.blocked { outline:1px solid #e14b4b; }
a { color:#8bb8ff; }
@media(max-width:900px){ .board{grid-template-columns:1fr 1fr;} }
</style>
</head>
<body>
<header><div><strong>CommerceOS Orchestrator</strong><div id="state" class="small">Loading…</div></div><div><button class="resume" onclick="resumeRun()">Resume</button> <button class="stop" onclick="stopRun()">Stop gracefully</button></div></header>
<main>
<section class="cards">
<div class="card"><div class="small">Progress</div><div id="progress" class="big">—</div></div>
<div class="card"><div class="small">Ready</div><div id="ready" class="big">—</div></div>
<div class="card"><div class="small">Builders</div><div id="builders" class="big">—</div></div>
<div class="card"><div class="small">Reviewers</div><div id="reviewers" class="big">—</div></div>
<div class="card"><div class="small">Merge queue</div><div id="merge" class="big">—</div></div>
<div class="card"><div class="small">Blocked</div><div id="blocked" class="big">—</div></div>
</section>
<section class="panel"><h3>Workflow</h3><div class="board"><div class="column"><b>Ready</b><div id="col-ready"></div></div><div class="column"><b>Active</b><div id="col-active"></div></div><div class="column"><b>Review</b><div id="col-review"></div></div><div class="column"><b>Merge</b><div id="col-merge"></div></div><div class="column"><b>Blocked</b><div id="col-blocked"></div></div></div></section>
<section class="panel"><h3>DAG / progress</h3><div id="dag"></div></section>
<section class="panel"><h3>Task detail</h3><pre id="detail" class="small">Select a task.</pre></section>
<section class="panel"><h3>Recent activity</h3><pre id="events" class="small"></pre></section>
</main>
<script>
const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[m]));
function lane(t){const s=t.execution_state;if(['QUEUED','BUILDING','VERIFYING','FIX_REQUIRED'].includes(s))return'active';if(s==='REVIEWING')return'review';if(['MERGE_QUEUED','INTEGRATING'].includes(s))return'merge';if(['BLOCKED','HUMAN_REQUIRED'].includes(s))return'blocked';if(t.maturity==='Ready'&&t.lifecycle_state==='Backlog')return'ready';return'other';}
function taskHtml(t){return `<div class="task ${lane(t)}" onclick="detail('${esc(t.id)}')"><b>${esc(t.id)}</b><div>${esc(t.title)}</div><div class="small">${esc(t.execution_state||t.maturity)} · ${esc(t.domain)}</div></div>`}
async function refresh(){try{const r=await fetch('/api/status',{cache:'no-store'});const d=await r.json();if(!r.ok)throw Error(JSON.stringify(d));
state.textContent=`State: ${d.orchestrator_state}`;progress.textContent=`${d.progress.completed}/${d.progress.total} (${d.progress.percent}%)`;ready.textContent=d.ready_frontier.length;builders.textContent=d.active_builders;reviewers.textContent=d.active_reviewers;merge.textContent=d.merge_queue_length;blocked.textContent=d.blocker_count;
for(const id of ['ready','active','review','merge','blocked'])document.getElementById('col-'+id).innerHTML=d.tasks.filter(t=>lane(t)===id).map(taskHtml).join('')||'<div class="small">None</div>';
dag.innerHTML=d.tasks.map(t=>`<span title="deps: ${esc(t.depends_on.join(', '))}" class="node ${lane(t)} ${t.lifecycle_state==='Completed'?'completed':''}">${esc(t.id)}</span>`).join('');
events.textContent=d.events.map(e=>`${e.created_at} ${e.task_id||'-'} ${e.kind} ${e.detail}`).join('\n');
}catch(e){state.textContent='Dashboard error: '+e}}
async function detail(id){const r=await fetch('/api/tasks/'+encodeURIComponent(id));detailEl=document.getElementById('detail');detailEl.textContent=JSON.stringify(await r.json(),null,2)}
async function stopRun(){await fetch('/api/stop',{method:'POST'});await refresh()}
async function resumeRun(){await fetch('/api/resume',{method:'POST'});await refresh()}
refresh();setInterval(refresh,1500);
</script>
</body></html>'''
