from __future__ import annotations

from dataclasses import dataclass

from .backlog import BacklogReader
from .models import BacklogSnapshot, CanonicalTask, TaskExecutionState
from .state import RunStateStore


@dataclass(frozen=True)
class ScheduleDecision:
    dispatchable: tuple[CanonicalTask, ...]
    active_resources: frozenset[str]
    capacity: int


class Scheduler:
    def __init__(self, state_store: RunStateStore, max_builders: int | None = None):
        self.state_store = state_store
        self.max_builders_override = max_builders

    def plan(self, snapshot: BacklogSnapshot) -> ScheduleDecision:
        active = self.state_store.active_task_runs()
        active_task_ids = {run.task_id for run in active}
        locally_blocked_ids = {run.task_id for run in self.state_store.blocked_task_runs()}
        active_resources: set[str] = set()
        for task_id in active_task_ids:
            task = snapshot.tasks.get(task_id)
            if task:
                active_resources.update(task.exclusive_resources)

        max_builders = self.max_builders_override or snapshot.max_writable_builders
        active_builder_count = sum(
            1
            for run in active
            if run.execution_state
            in {
                TaskExecutionState.QUEUED,
                TaskExecutionState.INITIAL_BUILD,
                TaskExecutionState.PRE_REVIEW_VERIFICATION,
                TaskExecutionState.FIRST_REVIEW,
                TaskExecutionState.REPAIR_REQUIRED,
                TaskExecutionState.REPAIR_BUILD,
                TaskExecutionState.REPAIR_VERIFICATION,
                TaskExecutionState.RE_REVIEW,
                TaskExecutionState.MERGE_QUEUED,
                TaskExecutionState.INTEGRATING,
                TaskExecutionState.FINALIZING,
            }
        )
        capacity = max(0, max_builders - active_builder_count)
        if capacity == 0:
            return ScheduleDecision((), frozenset(active_resources), 0)

        chosen: list[CanonicalTask] = []
        reserved = set(active_resources)
        for task in BacklogReader.ready_frontier(snapshot, active_resources=reserved):
            if len(chosen) >= capacity:
                break
            if task.id in active_task_ids or task.id in locally_blocked_ids:
                continue
            if set(task.exclusive_resources) & reserved:
                continue
            chosen.append(task)
            reserved.update(task.exclusive_resources)
        return ScheduleDecision(tuple(chosen), frozenset(active_resources), capacity)
