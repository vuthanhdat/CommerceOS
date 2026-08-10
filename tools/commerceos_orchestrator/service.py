from __future__ import annotations

import subprocess
import threading
import time
from concurrent.futures import Future, ThreadPoolExecutor
from dataclasses import dataclass
from pathlib import Path

from .agents import AgentRunner
from .backlog import BacklogReader, BacklogWriter, BacklogValidationError
from .models import (
    CanonicalTask,
    OrchestratorState,
    TaskExecutionState,
    TaskRun,
)
from .scheduler import Scheduler
from .state import RunStateStore
from .verification import VerificationRunner
from .workspace import GitIntegrationManager, GitWorkspaceManager, IntegrationError, WorkspaceError


@dataclass(frozen=True)
class OrchestratorConfig:
    max_builders: int = 2
    max_fix_attempts: int = 2
    poll_seconds: float = 0.5
    allow_cloud: bool = False


class TaskOrchestrator:
    def __init__(
        self,
        root: Path,
        state_store: RunStateStore,
        agent_runner: AgentRunner,
        verification_runner: VerificationRunner,
        *,
        workspace_manager: GitWorkspaceManager | None = None,
        integration_manager: GitIntegrationManager | None = None,
        config: OrchestratorConfig | None = None,
    ):
        self.root = root.resolve()
        self.state = state_store
        self.agent_runner = agent_runner
        self.verification = verification_runner
        self.workspace = workspace_manager or GitWorkspaceManager(self.root)
        self.integration = integration_manager or GitIntegrationManager(self.root)
        self.config = config or OrchestratorConfig()
        self.scheduler = Scheduler(self.state, max_builders=self.config.max_builders)
        self._merge_lock = threading.Lock()
        self._futures: dict[str, Future[None]] = {}

    def validate(self):
        return BacklogReader(self.root).load()

    def plan(self) -> list[CanonicalTask]:
        snapshot = self.validate()
        return list(self.scheduler.plan(snapshot).dispatchable)

    def dry_run(self) -> dict[str, object]:
        snapshot = self.validate()
        decision = self.scheduler.plan(snapshot)
        return {
            "control_state": self.state.control_state().value,
            "dispatchable": [task.id for task in decision.dispatchable],
            "active_resources": sorted(decision.active_resources),
            "capacity": decision.capacity,
            "actions": [
                {
                    "task": task.id,
                    "role": task.owner_role,
                    "model_class": task.model_class,
                    "branch": f"agent/{task.id}-{task.slug}",
                    "cloud_verification": task.cloud_verification,
                    "would_create_worktree": True,
                    "would_run_verification": True,
                    "would_review": True,
                    "would_enter_serial_merge_queue": True,
                }
                for task in decision.dispatchable
            ],
        }

    def request_stop(self) -> list[str]:
        return self.state.request_stop()

    def run(self, *, resume: bool = False) -> None:
        snapshot = self.validate()
        current = self.state.control_state()
        if current in {OrchestratorState.STOP_REQUESTED, OrchestratorState.STOPPING}:
            # A persisted graceful stop survives restart. Only recover/drain previously active tasks.
            if resume:
                self.state.reset_retryable_terminal_runs()
                self.state.clear_stop_and_run()
            else:
                self.state.set_control_state(OrchestratorState.STOPPING)
        elif resume:
            self.state.reset_retryable_terminal_runs()
            self.state.clear_stop_and_run()
        elif current in {OrchestratorState.IDLE, OrchestratorState.STOPPED}:
            self.state.clear_stop_and_run()
        elif current == OrchestratorState.HUMAN_REQUIRED:
            return
        elif current != OrchestratorState.RUNNING:
            self.state.set_control_state(OrchestratorState.RUNNING)

        with ThreadPoolExecutor(max_workers=self.config.max_builders) as executor:
            self._restore_active_runs(executor, snapshot)
            while True:
                self._reap_futures()
                control = self.state.control_state()
                snapshot = self.validate()

                if control in {OrchestratorState.STOP_REQUESTED, OrchestratorState.STOPPING}:
                    self.state.set_control_state(OrchestratorState.STOPPING)
                    self._restore_drain_runs(executor, snapshot)
                    if not self._futures and not self.state.drain_task_runs():
                        self.state.set_control_state(OrchestratorState.STOPPED)
                        return
                    time.sleep(self.config.poll_seconds)
                    continue

                if control == OrchestratorState.HUMAN_REQUIRED:
                    return

                decision = self.scheduler.plan(snapshot)
                for task in decision.dispatchable:
                    if len(self._futures) >= self.config.max_builders:
                        break
                    if task.id in self._futures:
                        continue
                    if task.cloud_verification.lower() == "required" and not self.config.allow_cloud:
                        # Cloud permission is explicit runtime operator consent in addition to canonical task gates.
                        if self.state.claim_task(task.id):
                            self.state.update_task(
                                task.id,
                                TaskExecutionState.HUMAN_REQUIRED,
                                blocker_code="CLOUD_EXECUTION_NOT_AUTHORIZED",
                                blocker_detail=(
                                    "task requires/permits real cloud verification; restart with explicit --allow-cloud "
                                    "only after repository cloud gates are satisfied"
                                ),
                            )
                        continue
                    if not self.state.claim_task(task.id):
                        continue
                    future = executor.submit(self._execute_task, task, False)
                    self._futures[task.id] = future

                if not self._futures:
                    # Reload once more because a completion may have changed canonical lifecycle/frontier.
                    snapshot = self.validate()
                    if not self.scheduler.plan(snapshot).dispatchable:
                        if self.state.blocked_task_runs():
                            self.state.set_control_state(OrchestratorState.HUMAN_REQUIRED)
                        else:
                            self.state.set_control_state(OrchestratorState.IDLE)
                        return
                time.sleep(self.config.poll_seconds)

    def _restore_active_runs(self, executor: ThreadPoolExecutor, snapshot) -> None:
        for run in self.state.active_task_runs():
            task = snapshot.tasks.get(run.task_id)
            if not task:
                self.state.update_task(
                    run.task_id,
                    TaskExecutionState.HUMAN_REQUIRED,
                    blocker_code="BACKLOG_TASK_MISSING",
                    blocker_detail="active local run no longer exists in canonical backlog",
                )
                continue
            if run.task_id not in self._futures:
                self._futures[run.task_id] = executor.submit(self._execute_task, task, True)

    def _restore_drain_runs(self, executor: ThreadPoolExecutor, snapshot) -> None:
        for run in self.state.drain_task_runs():
            task = snapshot.tasks.get(run.task_id)
            if not task:
                self.state.update_task(
                    run.task_id,
                    TaskExecutionState.HUMAN_REQUIRED,
                    blocker_code="BACKLOG_TASK_MISSING",
                    blocker_detail="draining local run no longer exists in canonical backlog",
                )
                continue
            if run.task_id not in self._futures:
                self._futures[run.task_id] = executor.submit(self._execute_task, task, True)

    def _reap_futures(self) -> None:
        completed = [task_id for task_id, future in self._futures.items() if future.done()]
        for task_id in completed:
            future = self._futures.pop(task_id)
            try:
                future.result()
            except Exception as exc:  # defensive fail-closed path
                run = self.state.task_run(task_id)
                if run and run.execution_state not in {
                    TaskExecutionState.COMPLETED,
                    TaskExecutionState.BLOCKED,
                    TaskExecutionState.HUMAN_REQUIRED,
                }:
                    self.state.update_task(
                        task_id,
                        TaskExecutionState.HUMAN_REQUIRED,
                        blocker_code="ORCHESTRATOR_UNHANDLED_ERROR",
                        blocker_detail=repr(exc),
                    )

    def _execute_task(self, task: CanonicalTask, resume: bool) -> None:
        prior_run = self.state.task_run(task.id)
        try:
            workspace = self.workspace.workspace_for(task)
            self.state.update_task(
                task.id,
                prior_run.execution_state if resume and prior_run else TaskExecutionState.QUEUED,
                branch=workspace.branch,
                worktree=str(workspace.path),
            )
        except WorkspaceError as exc:
            self._block(task, "WORKTREE_ERROR", str(exc))
            return

        run = self.state.task_run(task.id)
        feedback: str | None = None
        if resume and prior_run:
            feedback = f"Resume safely after interrupted local state {prior_run.execution_state.value}. Inspect existing worktree before editing."

        # Conservative recovery: integration states re-enter the merge lane; everything earlier re-runs
        # Builder/verification deterministically in the existing worktree.
        if prior_run and prior_run.execution_state in {
            TaskExecutionState.MERGE_QUEUED,
            TaskExecutionState.INTEGRATING,
        }:
            self._integrate(task, workspace.branch, workspace.path)
            return

        for fix_round in range(self.config.max_fix_attempts + 1):
            self.state.update_task(
                task.id,
                TaskExecutionState.BUILDING,
                attempt_delta=1,
                fix_attempt_delta=1 if fix_round > 0 else 0,
            )
            agent = self.agent_runner.run_builder(
                task,
                workspace.path,
                attempt=(self.state.task_run(task.id) or run).attempt,
                feedback=feedback,
            )
            if not agent.success:
                code = "EXTERNAL_ENVIRONMENT" if agent.marker == "ENVIRONMENT_UNAVAILABLE" else "BUILDER_FAILED"
                self._block(task, code, agent.stderr or agent.stdout)
                return

            self.state.update_task(task.id, TaskExecutionState.VERIFYING)
            verification = self.verification.run(task, workspace.path, phase=f"builder-{fix_round}")
            if not verification.success:
                if fix_round >= self.config.max_fix_attempts:
                    self._block(
                        task,
                        "RETRY_LIMIT_EXCEEDED",
                        f"verification failed after bounded repair; log={verification.log_path}",
                    )
                    return
                feedback = (
                    "Deterministic verification failed. Fix the implementation; do not weaken checks.\n"
                    + (verification.stdout + "\n" + verification.stderr)[-20000:]
                )
                self.state.update_task(task.id, TaskExecutionState.FIX_REQUIRED)
                continue

            try:
                self.workspace.ensure_committed(task, workspace)
                diff = self.workspace.diff_text(workspace)
                if not diff.strip():
                    self._block(
                        task,
                        "BUILDER_PRODUCED_NO_DIFF",
                        "Builder/verification produced no change relative to origin/main; refusing to mark task complete without inspectable implementation evidence",
                    )
                    return
            except WorkspaceError as exc:
                self._block(task, "GIT_TASK_BRANCH_ERROR", str(exc))
                return

            self.state.update_task(task.id, TaskExecutionState.REVIEWING)
            review = self.agent_runner.run_reviewer(task, workspace.path, diff=diff)
            if review.passed:
                self.state.update_task(task.id, TaskExecutionState.MERGE_QUEUED)
                self._integrate(task, workspace.branch, workspace.path)
                return

            if fix_round >= self.config.max_fix_attempts:
                self._block(
                    task,
                    "RETRY_LIMIT_EXCEEDED",
                    "independent review still has blocking findings after bounded repair: "
                    + review.findings[-20000:],
                )
                return
            feedback = "Independent Reviewer findings:\n" + review.findings[-20000:]
            self.state.update_task(task.id, TaskExecutionState.FIX_REQUIRED)

    def _integrate(self, task: CanonicalTask, branch: str, worktree: Path) -> None:
        with self._merge_lock:
            self.state.update_task(task.id, TaskExecutionState.INTEGRATING)
            integration_checkout_prepared = False
            try:
                self.integration.prepare_main()
                integration_checkout_prepared = True
                already_remote = self.integration.branch_is_on_remote_main(branch)
                if not already_remote:
                    clean_merge = self.integration.merge_branch(task, branch)
                    if not clean_merge:
                        conflicted = self.integration.conflicted_files()
                        if not conflicted:
                            self.integration.abort_merge()
                            raise IntegrationError("merge failed without identifiable conflicted files")
                        resolution = self.agent_runner.run_conflict_resolver(
                            task, self.root, conflicted
                        )
                        unresolved = self.integration.conflicted_files()
                        combined = f"{resolution.stdout}\n{resolution.stderr}"
                        if (
                            not resolution.success
                            or "CONFLICT_RESULT: RESOLVED" not in combined
                            or unresolved
                        ):
                            self.integration.abort_merge()
                            self._block(
                                task,
                                "MERGE_CONFLICT_REQUIRES_HUMAN",
                                f"conflicts={conflicted}; resolver={combined[-10000:]}",
                            )
                            return
                        self.integration.commit_current_merge(task)

                    post_merge = self.verification.run(task, self.root, phase="post-integration")
                    if not post_merge.success:
                        self.integration.rollback_unpushed_main()
                        self._block(
                            task,
                            "POST_INTEGRATION_VERIFICATION_FAILED",
                            f"log={post_merge.log_path}",
                        )
                        return

                    snapshot = BacklogReader(self.root).load()
                    merged_task = snapshot.tasks.get(task.id)
                    if not merged_task:
                        self.integration.rollback_unpushed_main()
                        raise IntegrationError(f"task disappeared after merge: {task.id}")
                    summary = self._completion_summary(task)
                    BacklogWriter(self.root).finalize_task(snapshot, merged_task, summary)
                    self.integration.commit_bookkeeping(task)
                    final_verification = self.verification.run(
                        task, self.root, phase="post-bookkeeping"
                    )
                    if not final_verification.success:
                        self.integration.rollback_unpushed_main()
                        self._block(
                            task,
                            "COMPLETION_BOOKKEEPING_VERIFICATION_FAILED",
                            f"log={final_verification.log_path}",
                        )
                        return
                    self.integration.push_main()
                else:
                    # Crash recovery after a previous successful push: verify authoritative main
                    # and require lifecycle bookkeeping to already be complete or finish it safely.
                    snapshot = BacklogReader(self.root).load()
                    current_task = snapshot.tasks.get(task.id)
                    if current_task and current_task.lifecycle_state != "Completed":
                        summary = self._completion_summary(task)
                        BacklogWriter(self.root).finalize_task(snapshot, current_task, summary)
                        self.integration.commit_bookkeeping(task)
                        final_verification = self.verification.run(
                            task, self.root, phase="recovery-bookkeeping"
                        )
                        if not final_verification.success:
                            self.integration.rollback_unpushed_main()
                            self._block(
                                task,
                                "COMPLETION_BOOKKEEPING_VERIFICATION_FAILED",
                                f"log={final_verification.log_path}",
                            )
                            return
                        self.integration.push_main()

                self.state.update_task(task.id, TaskExecutionState.COMPLETED)
                try:
                    refreshed = BacklogReader(self.root).load()
                    completed_task = refreshed.tasks.get(task.id, task)
                    self.workspace.cleanup(completed_task)
                except Exception as cleanup_error:
                    self.state.add_event(task.id, "CLEANUP_WARNING", repr(cleanup_error))
            except (IntegrationError, BacklogValidationError, WorkspaceError) as exc:
                try:
                    if integration_checkout_prepared:
                        self.integration.rollback_unpushed_main()
                    else:
                        self.integration.abort_merge()
                except Exception as rollback_error:
                    self.state.add_event(task.id, "ROLLBACK_WARNING", repr(rollback_error))
                self._block(task, "INTEGRATION_ERROR", str(exc))

    def _block(self, task: CanonicalTask, code: str, detail: str) -> None:
        self.state.update_task(
            task.id,
            TaskExecutionState.HUMAN_REQUIRED,
            blocker_code=code,
            blocker_detail=detail[-20000:],
        )

    def _completion_summary(self, task: CanonicalTask) -> str:
        run = self.state.task_run(task.id)
        attempts = run.attempt if run else 0
        fixes = run.fix_attempt if run else 0
        return f"""### Orchestrator evidence

- Task executed through isolated task branch/worktree and serialized integration lane.
- Builder attempts: {attempts}; bounded fix attempts: {fixes}.
- Deterministic verification passed before review and again against integrated latest `main`.
- Independent Reviewer passed before merge queue entry.
- Completion bookkeeping was verified before non-force push to `origin/main`.
- Cloud verification: {'operator-authorized according to task metadata' if task.cloud_verification.lower() != 'no' else 'N/A — task metadata says no'}.
"""
