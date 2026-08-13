from __future__ import annotations

from dataclasses import dataclass, field
from enum import StrEnum
from pathlib import Path
from typing import Any


class OrchestratorState(StrEnum):
    IDLE = "IDLE"
    RUNNING = "RUNNING"
    STOP_REQUESTED = "STOP_REQUESTED"
    STOPPING = "STOPPING"
    FORCE_STOPPING = "FORCE_STOPPING"
    STOPPED = "STOPPED"
    HUMAN_REQUIRED = "HUMAN_REQUIRED"


class TaskExecutionState(StrEnum):
    QUEUED = "QUEUED"
    PLANNING = "PLANNING"
    PLANNING_COMPLETED = "PLANNING_COMPLETED"
    INITIAL_BUILD = "INITIAL_BUILD"
    PRE_REVIEW_VERIFICATION = "PRE_REVIEW_VERIFICATION"
    FIRST_REVIEW = "FIRST_REVIEW"
    REPAIR_REQUIRED = "REPAIR_REQUIRED"
    REPAIR_BUILD = "REPAIR_BUILD"
    REPAIR_VERIFICATION = "REPAIR_VERIFICATION"
    RE_REVIEW = "RE_REVIEW"
    MERGE_QUEUED = "MERGE_QUEUED"
    INTEGRATING = "INTEGRATING"
    FINALIZING = "FINALIZING"
    COMPLETED = "COMPLETED"
    PLANNING_REQUIRED = "PLANNING_REQUIRED"
    ORCHESTRATOR_ACTION_REQUIRED = "ORCHESTRATOR_ACTION_REQUIRED"
    BLOCKED = "BLOCKED"
    HUMAN_REQUIRED = "HUMAN_REQUIRED"


TERMINAL_TASK_STATES = {
    TaskExecutionState.PLANNING_COMPLETED,
    TaskExecutionState.COMPLETED,
    TaskExecutionState.PLANNING_REQUIRED,
    TaskExecutionState.ORCHESTRATOR_ACTION_REQUIRED,
    TaskExecutionState.BLOCKED,
    TaskExecutionState.HUMAN_REQUIRED,
}


@dataclass(frozen=True)
class CanonicalTask:
    id: str
    maturity: str
    type: str
    domain: str
    title: str
    goal: str
    depends_on: tuple[str, ...]
    gates: tuple[str, ...]
    owner_role: str
    model_class: str
    cloud_verification: str
    spec_path: str
    lifecycle_state: str = "Backlog"
    exclusive_resources: tuple[str, ...] = ()
    merge_policy: str = "verified_serial_main"
    shard_path: str = ""
    catalog: str = "commerceos"

    @property
    def numeric_id(self) -> str:
        return self.id.removeprefix("TASK-")

    @property
    def slug(self) -> str:
        normalized = "-".join(
            part.lower()
            for part in "".join(ch if ch.isalnum() else " " for ch in self.title).split()
        )
        return normalized[:48].strip("-") or "task"


@dataclass(frozen=True)
class BacklogSnapshot:
    root: Path
    tasks: dict[str, CanonicalTask]
    task_fields: tuple[str, ...]
    completed_roots: frozenset[str]
    max_writable_builders: int
    merge_lane_concurrency: int
    cloud_requires_explicit_gate: bool
    ready_frontier_declared: tuple[str, ...]
    shard_paths: tuple[str, ...]
    catalog: str | None = None
    raw_master: dict[str, Any] = field(repr=False, default_factory=dict)


@dataclass(frozen=True)
class AgentResult:
    success: bool
    exit_code: int
    stdout: str
    stderr: str
    log_path: str
    marker: str | None = None
    evidence: dict[str, Any] | None = None


@dataclass(frozen=True)
class VerificationResult:
    success: bool
    exit_code: int
    command: tuple[str, ...]
    stdout: str
    stderr: str
    log_path: str
    report: Any | None = None


@dataclass(frozen=True)
class ReviewResult:
    passed: bool
    findings: str
    raw: AgentResult
    ledger: Any | None = None


@dataclass(frozen=True)
class Workspace:
    branch: str
    path: Path
    created: bool


@dataclass(frozen=True)
class TaskRun:
    task_id: str
    execution_state: TaskExecutionState
    branch: str | None
    worktree: str | None
    attempt: int
    fix_attempt: int
    blocker_code: str | None
    blocker_detail: str | None
    activated_at: str | None
    updated_at: str
    drain_at_stop: bool
    contract_version: str | None = None
    input_artifact_id: str | None = None
    output_artifact_id: str | None = None
