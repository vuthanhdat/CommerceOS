#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import subprocess
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT / "tools") not in sys.path:
    sys.path.insert(0, str(ROOT / "tools"))

from commerceos_orchestrator.agents import (  # noqa: E402
    AntigravityRunner,
    CodexRunner,
    RoleRoutedAgentRunner,
)
from commerceos_orchestrator.backlog import BacklogReader, BacklogValidationError  # noqa: E402
from commerceos_orchestrator.dashboard import (  # noqa: E402
    DashboardReadModel,
    LocalDashboardServer,
    RuntimeController,
)
from commerceos_orchestrator.models import TaskExecutionState  # noqa: E402
from commerceos_orchestrator.planning import (  # noqa: E402
    CodexPlanningAgentRunner,
    PlanningAwareTaskOrchestrator,
    PlanningCoordinator,
)
from commerceos_orchestrator.service import OrchestratorConfig, TaskOrchestrator  # noqa: E402
from commerceos_orchestrator.settings import (  # noqa: E402
    AgentProfileSettings,
    LocalOrchestratorSettings,
    SettingsStore,
    SettingsValidationError,
)
from commerceos_orchestrator.runtime_control import (  # noqa: E402
    RuntimeControlError,
    WorkerRuntimeRegistry,
)
from commerceos_orchestrator.state import RunStateStore  # noqa: E402
from commerceos_orchestrator.verification import VerificationRunner  # noqa: E402
from commerceos_orchestrator.workspace import GitWorkspaceManager, WorkspaceError  # noqa: E402


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="CommerceOS local Task Orchestrator V1")
    parser.add_argument("--repo", type=Path, default=ROOT, help="CommerceOS repository root")
    parser.add_argument("--state", type=Path, help="SQLite run-state path")
    parser.add_argument(
        "--catalog",
        choices=("commerceos", "orchestrator"),
        default=None,
        help="isolated task catalog (default: saved setting or commerceos)",
    )
    parser.add_argument("--max-builders", type=int, default=None)
    parser.add_argument("--max-fix-attempts", type=int, default=None)
    parser.add_argument(
        "--allow-cloud",
        action=argparse.BooleanOptionalAction,
        default=None,
        help="operator consent for canonical cloud-eligible implementation tasks",
    )
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--no-browser", action="store_true")
    parser.add_argument("--worker-token", help=argparse.SUPPRESS)
    sub = parser.add_subparsers(dest="command", required=True)
    for name in (
        "status",
        "validate",
        "plan",
        "dry-run",
        "run",
        "stop",
        "force-stop",
        "resume",
        "cleanup",
        "ui",
        "start",
    ):
        sub.add_parser(name)
    return parser


def _provider_runner(
    root: Path,
    logs_root: Path,
    profile: AgentProfileSettings,
    *,
    cloud_authorized: bool,
):
    runner_type = AntigravityRunner if profile.provider == "antigravity" else CodexRunner
    return runner_type(
        root,
        logs_root,
        profile=profile.codex_profile(),
        cloud_authorized=cloud_authorized,
    )


def build_orchestrator(args) -> tuple[PlanningAwareTaskOrchestrator, RunStateStore]:
    root = args.repo.resolve()
    saved = SettingsStore(root).load()
    effective = LocalOrchestratorSettings(
        catalog=args.catalog or saved.catalog,
        max_builders=args.max_builders if args.max_builders is not None else saved.max_builders,
        max_fix_attempts=(
            args.max_fix_attempts
            if args.max_fix_attempts is not None
            else saved.max_fix_attempts
        ),
        allow_cloud=args.allow_cloud if args.allow_cloud is not None else saved.allow_cloud,
        profiles=saved.profiles,
    )
    args.catalog = effective.catalog
    args.max_builders = effective.max_builders
    args.max_fix_attempts = effective.max_fix_attempts
    args.allow_cloud = effective.allow_cloud
    state_path = (
        args.state or root / ".commerceos" / "orchestrator" / args.catalog / "state.db"
    ).resolve()
    logs_root = state_path.parent / "logs"
    state = RunStateStore(state_path)
    verification = VerificationRunner(logs_root)

    implementation = TaskOrchestrator(
        root,
        state,
        RoleRoutedAgentRunner(
            _provider_runner(
                root,
                logs_root,
                effective.profiles["builder"],
                cloud_authorized=effective.allow_cloud,
            ),
            _provider_runner(
                root,
                logs_root,
                effective.profiles["reviewer"],
                cloud_authorized=False,
            ),
            _provider_runner(
                root,
                logs_root,
                effective.profiles["conflict_resolver"],
                cloud_authorized=effective.allow_cloud,
            ),
        ),
        verification,
        config=OrchestratorConfig(
            max_builders=args.max_builders,
            max_fix_attempts=args.max_fix_attempts,
            allow_cloud=args.allow_cloud,
        ),
        catalog=args.catalog,
    )
    planning = PlanningCoordinator(
        root,
        state,
        CodexPlanningAgentRunner(
            root,
            logs_root,
            runner=_provider_runner(
                root,
                logs_root,
                effective.profiles["planning"],
                cloud_authorized=False,
            ),
        ),
        verification,
        catalog=args.catalog,
    )
    return PlanningAwareTaskOrchestrator(implementation, planning), state


def print_json(value: object) -> None:
    print(json.dumps(value, indent=2, ensure_ascii=False))


def spawn_registered_worker(args, state: RunStateStore) -> int:
    token = uuid.uuid4().hex
    command = [
        sys.executable,
        str(args.repo.resolve() / "tools" / "orchestrator.py"),
        "--repo", str(args.repo.resolve()),
        "--state", str(state.path),
        "--catalog", args.catalog,
        "--max-builders", str(args.max_builders),
        "--max-fix-attempts", str(args.max_fix_attempts),
        "--allow-cloud" if args.allow_cloud else "--no-allow-cloud",
        "--worker-token", token,
        args.command,
    ]
    process = subprocess.Popen(command, cwd=args.repo.resolve())
    try:
        return process.wait()
    except KeyboardInterrupt:
        try:
            process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            WorkerRuntimeRegistry._terminate_tree(process.pid)
            process.wait(timeout=5)
        state.force_stop(process.pid)
        WorkerRuntimeRegistry(args.repo.resolve(), state.path, args.catalog).clear(token)
        return 130


def cleanup(orchestrator, state: RunStateStore) -> int:
    snapshot = BacklogReader(orchestrator.root, orchestrator.catalog).load()
    cleaned: list[str] = []
    warnings: list[str] = []
    for run in state.task_runs():
        if run.execution_state not in {
            TaskExecutionState.COMPLETED,
            TaskExecutionState.BLOCKED,
            TaskExecutionState.HUMAN_REQUIRED,
        }:
            continue
        task = snapshot.tasks.get(run.task_id)
        if task is None:
            continue
        try:
            orchestrator.workspace.cleanup(task)
            cleaned.append(task.id)
        except WorkspaceError as exc:
            warnings.append(f"{task.id}: {exc}")
    print_json({"cleaned": cleaned, "warnings": warnings})
    return 0 if not warnings else 1


def main() -> int:
    args = build_parser().parse_args()
    try:
        orchestrator, state = build_orchestrator(args)
        if args.max_builders < 1 or args.max_builders > 2:
            print("--max-builders must be between 1 and 2", file=sys.stderr)
            return 2
        if args.max_fix_attempts < 0 or args.max_fix_attempts > 10:
            print("--max-fix-attempts must be between 0 and 10", file=sys.stderr)
            return 2
        if args.command == "status":
            print_json(DashboardReadModel(orchestrator.root, state, args.catalog).status())
            return 0
        if args.command == "validate":
            snapshot = orchestrator.validate()
            print_json(
                {
                    "valid": True,
                    "tasks": len(snapshot.tasks),
                    "ready_frontier": [
                        task.id
                        for task in BacklogReader.ready_frontier(
                            snapshot, active_resources=set()
                        )
                    ],
                }
            )
            return 0
        if args.command == "plan":
            print_json(orchestrator.plan_report())
            return 0
        if args.command == "dry-run":
            print_json(orchestrator.dry_run())
            return 0
        if args.command == "stop":
            print_json({"accepted": True, "draining": orchestrator.request_stop()})
            return 0
        registry = WorkerRuntimeRegistry(orchestrator.root, state.path, args.catalog)
        if args.command == "force-stop":
            print_json({"accepted": True, **registry.force_stop(state)})
            return 0
        if args.command == "run":
            if not args.worker_token:
                return spawn_registered_worker(args, state)
            with registry.registered_worker("run", token=args.worker_token):
                orchestrator.run()
            return 0
        if args.command == "resume":
            if not args.worker_token:
                return spawn_registered_worker(args, state)
            with registry.registered_worker("resume", token=args.worker_token):
                orchestrator.run(resume=True)
            return 0
        if args.command == "cleanup":
            return cleanup(orchestrator, state)
        if args.command in {"ui", "start"}:
            runtime = RuntimeController(orchestrator)
            if args.command == "start":
                runtime.start()
            server = LocalDashboardServer(
                orchestrator.root,
                state,
                host=args.host,
                port=args.port,
                runtime=runtime,
                catalog=args.catalog,
            )
            print(f"CommerceOS Orchestrator dashboard: {server.url}")
            try:
                server.serve_forever(open_browser=not args.no_browser)
            except KeyboardInterrupt:
                print("\nDashboard stopped. In-flight Orchestrator work is not force-killed.")
            finally:
                server.shutdown()
            return 0
        raise AssertionError(args.command)
    except (BacklogValidationError, SettingsValidationError, RuntimeControlError) as exc:
        print(f"CONFIGURATION INVALID: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
