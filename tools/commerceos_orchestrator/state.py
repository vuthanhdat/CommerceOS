from __future__ import annotations

import json
import sqlite3
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterator

from .models import OrchestratorState, TaskExecutionState, TaskRun, TERMINAL_TASK_STATES
from .stage_contracts import CONTRACT_VERSION, transition_rule


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


class InvalidTransitionError(RuntimeError):
    pass


class RunStateStore:
    def __init__(self, path: Path):
        self.path = path.resolve()
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self._init_schema()

    @contextmanager
    def _connect(self) -> Iterator[sqlite3.Connection]:
        connection = sqlite3.connect(self.path, timeout=30, isolation_level=None)
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA journal_mode=WAL")
        connection.execute("PRAGMA foreign_keys=ON")
        try:
            yield connection
        finally:
            connection.close()

    def _init_schema(self) -> None:
        with self._connect() as connection:
            connection.executescript(
                """
                CREATE TABLE IF NOT EXISTS control_state (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    state TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                INSERT OR IGNORE INTO control_state(id, state, updated_at)
                VALUES (1, 'IDLE', CURRENT_TIMESTAMP);

                CREATE TABLE IF NOT EXISTS task_runs (
                    task_id TEXT PRIMARY KEY,
                    execution_state TEXT NOT NULL,
                    branch TEXT,
                    worktree TEXT,
                    attempt INTEGER NOT NULL DEFAULT 0,
                    fix_attempt INTEGER NOT NULL DEFAULT 0,
                    blocker_code TEXT,
                    blocker_detail TEXT,
                    activated_at TEXT,
                    updated_at TEXT NOT NULL,
                    drain_at_stop INTEGER NOT NULL DEFAULT 0,
                    contract_version TEXT,
                    input_artifact_id TEXT,
                    output_artifact_id TEXT
                );

                CREATE TABLE IF NOT EXISTS events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    task_id TEXT,
                    kind TEXT NOT NULL,
                    detail TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_events_created_at ON events(created_at DESC);
                """
            )
            self._migrate_task_runs(connection)

    @staticmethod
    def _migrate_task_runs(connection: sqlite3.Connection) -> None:
        columns = {
            row["name"]
            for row in connection.execute("PRAGMA table_info(task_runs)").fetchall()
        }
        for name in ("contract_version", "input_artifact_id", "output_artifact_id"):
            if name not in columns:
                connection.execute(f"ALTER TABLE task_runs ADD COLUMN {name} TEXT")
        legacy_states = {
            "BUILDING": TaskExecutionState.INITIAL_BUILD.value,
            "VERIFYING": TaskExecutionState.PRE_REVIEW_VERIFICATION.value,
            "FIX_REQUIRED": TaskExecutionState.REPAIR_REQUIRED.value,
            "REVIEWING": TaskExecutionState.FIRST_REVIEW.value,
        }
        for legacy, current in legacy_states.items():
            connection.execute(
                "UPDATE task_runs SET execution_state = ? WHERE execution_state = ?",
                (current, legacy),
            )
        connection.execute(
            "UPDATE task_runs SET contract_version = ? WHERE contract_version IS NULL",
            (CONTRACT_VERSION,),
        )

    def control_state(self) -> OrchestratorState:
        with self._connect() as connection:
            row = connection.execute("SELECT state FROM control_state WHERE id = 1").fetchone()
            return OrchestratorState(row["state"])

    def set_control_state(self, state: OrchestratorState) -> None:
        now = utc_now()
        changed = False
        with self._connect() as connection:
            current = connection.execute("SELECT state FROM control_state WHERE id = 1").fetchone()[0]
            if current != state.value:
                connection.execute(
                    "UPDATE control_state SET state = ?, updated_at = ? WHERE id = 1",
                    (state.value, now),
                )
                changed = True
        if changed:
            self.add_event(None, "CONTROL", state.value)

    def request_stop(self) -> list[str]:
        now = utc_now()
        with self._connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            active = connection.execute(
                """
                SELECT task_id FROM task_runs
                WHERE execution_state NOT IN (
                    'PLANNING_COMPLETED', 'COMPLETED', 'PLANNING_REQUIRED',
                    'ORCHESTRATOR_ACTION_REQUIRED', 'BLOCKED', 'HUMAN_REQUIRED'
                )
                """
            ).fetchall()
            ids = [row["task_id"] for row in active]
            target_state = (
                OrchestratorState.STOP_REQUESTED.value if ids else OrchestratorState.STOPPED.value
            )
            connection.execute(
                "UPDATE control_state SET state = ?, updated_at = ? WHERE id = 1",
                (target_state, now),
            )
            if ids:
                connection.executemany(
                    "UPDATE task_runs SET drain_at_stop = 1, updated_at = ? WHERE task_id = ?",
                    [(now, task_id) for task_id in ids],
                )
            connection.execute("COMMIT")
        self.add_event(None, "STOP_REQUESTED", json.dumps(ids))
        if not ids:
            self.add_event(None, "CONTROL", OrchestratorState.STOPPED.value)
        return ids

    def clear_stop_and_run(self) -> None:
        now = utc_now()
        with self._connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            connection.execute(
                "UPDATE control_state SET state = ?, updated_at = ? WHERE id = 1",
                (OrchestratorState.RUNNING.value, now),
            )
            connection.execute("UPDATE task_runs SET drain_at_stop = 0")
            connection.execute("COMMIT")
        self.add_event(None, "CONTROL", "RUNNING")


    def reset_retryable_terminal_runs(self) -> list[str]:
        """Explicit operator retry clears local Blocked/Human Required claims only.

        Canonical maturity/dependency/gate state is not modified; scheduler eligibility is
        recomputed from the repository after the reset. Completed evidence is never reset.
        """
        now = utc_now()
        with self._connect() as connection:
            rows = connection.execute(
                """
                SELECT task_id FROM task_runs
                WHERE execution_state IN (
                    'PLANNING_REQUIRED', 'ORCHESTRATOR_ACTION_REQUIRED',
                    'BLOCKED', 'HUMAN_REQUIRED'
                )
                """
            ).fetchall()
            ids = [row["task_id"] for row in rows]
            if ids:
                connection.executemany(
                    "DELETE FROM task_runs WHERE task_id = ?", [(task_id,) for task_id in ids]
                )
        for task_id in ids:
            self.add_event(task_id, "RETRY_RESET", "explicit operator resume cleared local blocker")
        return ids

    def blocked_task_runs(self) -> list[TaskRun]:
        return [
            run
            for run in self.task_runs()
            if run.execution_state
            in {
                TaskExecutionState.PLANNING_REQUIRED,
                TaskExecutionState.ORCHESTRATOR_ACTION_REQUIRED,
                TaskExecutionState.BLOCKED,
                TaskExecutionState.HUMAN_REQUIRED,
            }
        ]

    def claim_task(self, task_id: str, branch: str | None = None, worktree: str | None = None) -> bool:
        now = utc_now()
        with self._connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            control = connection.execute("SELECT state FROM control_state WHERE id = 1").fetchone()[0]
            if control in {
                OrchestratorState.STOP_REQUESTED.value,
                OrchestratorState.STOPPING.value,
                OrchestratorState.STOPPED.value,
            }:
                connection.execute("ROLLBACK")
                return False
            existing = connection.execute(
                "SELECT execution_state, output_artifact_id FROM task_runs WHERE task_id = ?", (task_id,)
            ).fetchone()
            if existing and TaskExecutionState(existing["execution_state"]) not in TERMINAL_TASK_STATES:
                connection.execute("ROLLBACK")
                return False
            if existing:
                connection.execute(
                    """
                    UPDATE task_runs
                    SET execution_state = ?, branch = ?, worktree = ?, blocker_code = NULL,
                        blocker_detail = NULL, activated_at = ?, updated_at = ?, drain_at_stop = 0,
                        contract_version = ?, input_artifact_id = NULL, output_artifact_id = ?
                    WHERE task_id = ?
                    """,
                    (
                        TaskExecutionState.QUEUED.value,
                        branch,
                        worktree,
                        now,
                        now,
                        CONTRACT_VERSION,
                        f"{task_id}:queued:0",
                        task_id,
                    ),
                )
            else:
                connection.execute(
                    """
                    INSERT INTO task_runs(
                        task_id, execution_state, branch, worktree, attempt, fix_attempt,
                        blocker_code, blocker_detail, activated_at, updated_at, drain_at_stop,
                        contract_version, input_artifact_id, output_artifact_id
                    ) VALUES (?, ?, ?, ?, 0, 0, NULL, NULL, ?, ?, 0, ?, NULL, ?)
                    """,
                    (
                        task_id,
                        TaskExecutionState.QUEUED.value,
                        branch,
                        worktree,
                        now,
                        now,
                        CONTRACT_VERSION,
                        f"{task_id}:queued:0",
                    ),
                )
            connection.execute("COMMIT")
        self.add_event(
            task_id,
            "CLAIMED",
            json.dumps(
                {
                    "task_id": task_id,
                    "from": existing["execution_state"] if existing else "ABSENT",
                    "to": TaskExecutionState.QUEUED.value,
                    "actor": "ORCHESTRATOR",
                    "contract_version": CONTRACT_VERSION,
                    "input_artifact_id": existing["output_artifact_id"] if existing else None,
                    "output_artifact_id": f"{task_id}:queued:0",
                },
                sort_keys=True,
            ),
        )
        return True

    def update_task(
        self,
        task_id: str,
        execution_state: TaskExecutionState,
        *,
        branch: str | None = None,
        worktree: str | None = None,
        attempt_delta: int = 0,
        fix_attempt_delta: int = 0,
        blocker_code: str | None = None,
        blocker_detail: str | None = None,
        actor: str | None = None,
        input_artifact_id: str | None = None,
        output_artifact_id: str | None = None,
    ) -> None:
        now = utc_now()
        event_detail: dict[str, str | None]
        with self._connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            existing = connection.execute(
                "SELECT * FROM task_runs WHERE task_id = ?", (task_id,)
            ).fetchone()
            if existing is None:
                connection.execute("ROLLBACK")
                raise KeyError(f"task run not found: {task_id}")
            source = TaskExecutionState(existing["execution_state"])
            rule = transition_rule(source, execution_state)
            if source != execution_state and rule is None:
                detail = f"undeclared transition {source.value} -> {execution_state.value}"
                connection.execute(
                    """
                    UPDATE task_runs
                    SET execution_state = ?, blocker_code = ?, blocker_detail = ?,
                        contract_version = ?, updated_at = ?
                    WHERE task_id = ?
                    """,
                    (
                        TaskExecutionState.HUMAN_REQUIRED.value,
                        "INVALID_TRANSITION",
                        detail,
                        CONTRACT_VERSION,
                        now,
                        task_id,
                    ),
                )
                connection.execute("COMMIT")
                self.add_event(
                    task_id,
                    "TRANSITION_REJECTED",
                    json.dumps(
                        {
                            "task_id": task_id,
                            "from": source.value,
                            "to": execution_state.value,
                            "actor": actor or "UNKNOWN",
                            "contract_version": CONTRACT_VERSION,
                            "input_artifact_id": existing["output_artifact_id"],
                            "output_artifact_id": None,
                            "reason": detail,
                        },
                        sort_keys=True,
                    ),
                )
                raise InvalidTransitionError(detail)
            expected_actor = rule.actor if rule else actor or "ORCHESTRATOR"
            if actor is not None and rule is not None and actor != rule.actor:
                detail = (
                    f"actor {actor!r} cannot perform {source.value} -> {execution_state.value}; "
                    f"expected {rule.actor}"
                )
                connection.execute(
                    """
                    UPDATE task_runs
                    SET execution_state = ?, blocker_code = ?, blocker_detail = ?,
                        contract_version = ?, updated_at = ?
                    WHERE task_id = ?
                    """,
                    (
                        TaskExecutionState.HUMAN_REQUIRED.value,
                        "INVALID_TRANSITION_ACTOR",
                        detail,
                        CONTRACT_VERSION,
                        now,
                        task_id,
                    ),
                )
                connection.execute("COMMIT")
                self.add_event(
                    task_id,
                    "TRANSITION_REJECTED",
                    json.dumps(
                        {
                            "task_id": task_id,
                            "from": source.value,
                            "to": execution_state.value,
                            "actor": actor,
                            "contract_version": CONTRACT_VERSION,
                            "input_artifact_id": existing["output_artifact_id"],
                            "output_artifact_id": None,
                            "reason": detail,
                        },
                        sort_keys=True,
                    ),
                )
                raise InvalidTransitionError(detail)
            resolved_input = input_artifact_id or existing["output_artifact_id"]
            next_attempt = int(existing["attempt"]) + attempt_delta
            next_fix = int(existing["fix_attempt"]) + fix_attempt_delta
            resolved_output = output_artifact_id or (
                f"{task_id}:{execution_state.value.lower()}:{next_attempt}:{next_fix}"
            )
            connection.execute(
                """
                UPDATE task_runs
                SET execution_state = ?,
                    branch = COALESCE(?, branch),
                    worktree = COALESCE(?, worktree),
                    attempt = attempt + ?,
                    fix_attempt = fix_attempt + ?,
                    blocker_code = ?,
                    blocker_detail = ?,
                    contract_version = ?,
                    input_artifact_id = ?,
                    output_artifact_id = ?,
                    updated_at = ?
                WHERE task_id = ?
                """,
                (
                    execution_state.value,
                    branch,
                    worktree,
                    attempt_delta,
                    fix_attempt_delta,
                    blocker_code,
                    blocker_detail,
                    CONTRACT_VERSION,
                    resolved_input,
                    resolved_output,
                    now,
                    task_id,
                ),
            )
            connection.execute("COMMIT")
            event_detail = {
                "task_id": task_id,
                "from": source.value,
                "to": execution_state.value,
                "actor": expected_actor,
                "contract_version": CONTRACT_VERSION,
                "input_artifact_id": resolved_input,
                "output_artifact_id": resolved_output,
            }
        detail = execution_state.value
        if blocker_code:
            detail += f" {blocker_code}: {blocker_detail or ''}".rstrip()
        event_detail["detail"] = detail
        self.add_event(task_id, "TASK_STATE", json.dumps(event_detail, sort_keys=True))

    def task_run(self, task_id: str) -> TaskRun | None:
        with self._connect() as connection:
            row = connection.execute("SELECT * FROM task_runs WHERE task_id = ?", (task_id,)).fetchone()
        return self._to_task_run(row) if row else None

    def task_runs(self) -> list[TaskRun]:
        with self._connect() as connection:
            rows = connection.execute("SELECT * FROM task_runs ORDER BY task_id").fetchall()
        return [self._to_task_run(row) for row in rows]

    def active_task_runs(self) -> list[TaskRun]:
        return [run for run in self.task_runs() if run.execution_state not in TERMINAL_TASK_STATES]

    def drain_task_runs(self) -> list[TaskRun]:
        return [
            run
            for run in self.task_runs()
            if run.drain_at_stop and run.execution_state not in TERMINAL_TASK_STATES
        ]

    def add_event(self, task_id: str | None, kind: str, detail: str) -> None:
        with self._connect() as connection:
            connection.execute(
                "INSERT INTO events(task_id, kind, detail, created_at) VALUES (?, ?, ?, ?)",
                (task_id, kind, detail, utc_now()),
            )

    def recent_events(self, limit: int = 100) -> list[dict[str, str | int | None]]:
        with self._connect() as connection:
            rows = connection.execute(
                "SELECT id, task_id, kind, detail, created_at FROM events ORDER BY id DESC LIMIT ?",
                (limit,),
            ).fetchall()
        return [dict(row) for row in rows]

    @staticmethod
    def _to_task_run(row: sqlite3.Row) -> TaskRun:
        return TaskRun(
            task_id=row["task_id"],
            execution_state=TaskExecutionState(row["execution_state"]),
            branch=row["branch"],
            worktree=row["worktree"],
            attempt=int(row["attempt"]),
            fix_attempt=int(row["fix_attempt"]),
            blocker_code=row["blocker_code"],
            blocker_detail=row["blocker_detail"],
            activated_at=row["activated_at"],
            updated_at=row["updated_at"],
            drain_at_stop=bool(row["drain_at_stop"]),
            contract_version=row["contract_version"],
            input_artifact_id=row["input_artifact_id"],
            output_artifact_id=row["output_artifact_id"],
        )
