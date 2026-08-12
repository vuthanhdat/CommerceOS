#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT / "tools") not in sys.path:
    sys.path.insert(0, str(ROOT / "tools"))

from commerceos_orchestrator.agents import CodexRunner  # noqa: E402
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
        default="commerceos",
        help="isolated task catalog to operate (default: commerceos)",
    )
    parser.add_argument("--max-builders", type=int, default=2)
    parser.add_argument("--max-fix-attempts", type=int, default=2)
    parser.add_argument(
        "--allow-cloud",
        action="store_true",
        help="operator consent for canonical cloud-eligible implementation tasks",
    )
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--no-browser", action="store_true")
    sub = parser.add_subparsers(dest="command", required=True)
    for name in (
        "status",
        "validate",
        "plan",
        "dry-run",
        "run",
        "stop",
        "resume",
        "cleanup",
        "ui",
        "start",
    ):
        sub.add_parser(name)
    return parser


def build_orchestrator(args) -> tuple[PlanningAwareTaskOrchestrator, RunStateStore]:
    root = args.repo.resolve()
    state_path = (
        args.state or root / ".commerceos" / "orchestrator" / args.catalog / "state.db"
    ).resolve()
    logs_root = state_path.parent / "logs"
    state = RunStateStore(state_path)
    verification = VerificationRunner(logs_root)

    implementation = TaskOrchestrator(
        root,
        state,
        # Autonomous implementation/review/conflict execution is pinned by CodexRunner
        # to Luna / medium / Standard. Interactive Codex TUI settings are not inherited.
        CodexRunner(root, logs_root, cloud_authorized=args.allow_cloud),
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
        # Planning roles are a separate execution boundary pinned to
        # Sol / medium / Standard and never receive cloud authorization.
        CodexPlanningAgentRunner(root, logs_root),
        verification,
        catalog=args.catalog,
    )
    return PlanningAwareTaskOrchestrator(implementation, planning), state


def print_json(value: object) -> None:
    print(json.dumps(value, indent=2, ensure_ascii=False))


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
    if args.max_builders < 1 or args.max_builders > 2:
        print("--max-builders must be between 1 and 2 for V1", file=sys.stderr)
        return 2
    orchestrator, state = build_orchestrator(args)
    try:
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
            print_json({"dispatchable": [task.id for task in orchestrator.plan()]})
            return 0
        if args.command == "dry-run":
            print_json(orchestrator.dry_run())
            return 0
        if args.command == "stop":
            print_json({"accepted": True, "draining": orchestrator.request_stop()})
            return 0
        if args.command == "run":
            orchestrator.run()
            return 0
        if args.command == "resume":
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
    except BacklogValidationError as exc:
        print(f"BACKLOG INVALID: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
