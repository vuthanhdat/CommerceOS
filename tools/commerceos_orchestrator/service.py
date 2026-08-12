from __future__ import annotations

import json
import re
import subprocess
import threading
import time
from concurrent.futures import Future, ThreadPoolExecutor
from dataclasses import dataclass
from pathlib import Path

from .agents import AgentRunner
from .backlog import BacklogReader, BacklogWriter, BacklogValidationError
from .evidence import (
    AdditionalVerificationCommand,
    BuilderResultManifest,
    EvidenceValidationError,
    VerificationReport,
    acceptance_criterion_ids,
    write_evidence_artifact,
)
from .models import (
    AgentResult,
    CanonicalTask,
    OrchestratorState,
    ReviewResult,
    TaskExecutionState,
    TaskRun,
    TERMINAL_TASK_STATES,
    VerificationResult,
)
from .scheduler import Scheduler
from .state import RunStateStore
from .stage_contracts import CONTRACT_VERSION, StageContractError, stage_contract
from .verification import VerificationRunner
from .workspace import GitIntegrationManager, GitWorkspaceManager, IntegrationError, WorkspaceError
from .review_contract import (
    FindingOwner,
    FindingRoute,
    ReviewLedger,
    ReviewLedgerError,
    next_hop,
)
from .repair_contract import RepairContractError, RepairManifest, RepairPacket


@dataclass(frozen=True)
class OrchestratorConfig:
    max_builders: int = 2
    max_fix_attempts: int = 2
    poll_seconds: float = 0.5
    allow_cloud: bool = False
    max_capacity_retries: int = 3
    capacity_backoff_seconds: float = 15.0


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
        catalog: str = "commerceos",
    ):
        self.root = root.resolve()
        self.state = state_store
        self.agent_runner = agent_runner
        self.verification = verification_runner
        self.workspace = workspace_manager or GitWorkspaceManager(self.root)
        self.integration = integration_manager or GitIntegrationManager(self.root)
        self.config = config or OrchestratorConfig()
        self.catalog = catalog
        self.backlog = BacklogReader(self.root, catalog)
        self.scheduler = Scheduler(self.state, max_builders=self.config.max_builders)
        self._merge_lock = threading.Lock()
        self._futures: dict[str, Future[None]] = {}

    def validate(self):
        return self.backlog.load()

    def plan(self) -> list[CanonicalTask]:
        snapshot = self.validate()
        return list(self.scheduler.plan(snapshot).dispatchable)

    def dry_run(self) -> dict[str, object]:
        snapshot = self.validate()
        decision = self.scheduler.plan(snapshot)
        return {
            "catalog": self.catalog,
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
                    # ADR-012 makes LocalStack the only infrastructure target. The
                    # backlog field cloud_verification describes whether infrastructure
                    # verification is required; it is not authorization to use real AWS.
                    # Do not block LocalStack tasks behind the legacy --allow-cloud flag.
                    if not self.state.claim_task(task.id):
                        continue
                    future = executor.submit(self._execute_task, task, False)
                    self._futures[task.id] = future

                if not self._futures:
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
            except Exception as exc:
                run = self.state.task_run(task_id)
                if run and run.execution_state not in TERMINAL_TASK_STATES:
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
        review_context: str | None = None
        previous_ledger: ReviewLedger | None = None
        previous_review_commit: str | None = None
        repair_packet: RepairPacket | None = None
        repair_packet_path: str | None = None
        repair_resume = False
        if resume and prior_run:
            feedback = f"Resume safely after interrupted local state {prior_run.execution_state.value}. Inspect existing worktree before editing."
            repair_resume = prior_run.execution_state in {
                TaskExecutionState.PRE_REVIEW_VERIFICATION,
                TaskExecutionState.FIRST_REVIEW,
                TaskExecutionState.REPAIR_REQUIRED,
                TaskExecutionState.REPAIR_BUILD,
                TaskExecutionState.REPAIR_VERIFICATION,
                TaskExecutionState.RE_REVIEW,
            }

        if prior_run and prior_run.execution_state in {
            TaskExecutionState.MERGE_QUEUED,
            TaskExecutionState.INTEGRATING,
            TaskExecutionState.FINALIZING,
        }:
            self._integrate(task, workspace.branch, workspace.path)
            return

        if repair_resume and prior_run and prior_run.execution_state in {
            TaskExecutionState.PRE_REVIEW_VERIFICATION,
            TaskExecutionState.FIRST_REVIEW,
            TaskExecutionState.REPAIR_VERIFICATION,
            TaskExecutionState.RE_REVIEW,
        }:
            self.state.update_task(task.id, TaskExecutionState.REPAIR_REQUIRED)

        if repair_resume:
            self._block(
                task,
                "REPAIR_CONTEXT_RESTORE_REQUIRED",
                "Persisted RepairPacket/v1 and ReviewLedger/v1 continuity is unavailable; "
                "refusing to dispatch an unscoped repair Builder after restart.",
            )
            return

        for fix_round in range(self.config.max_fix_attempts + 1):
            is_repair = repair_resume or fix_round > 0
            self.state.update_task(
                task.id,
                TaskExecutionState.REPAIR_BUILD
                if is_repair
                else TaskExecutionState.INITIAL_BUILD,
                fix_attempt_delta=1 if is_repair else 0,
            )
            agent = self._run_builder_with_capacity_retry(task, workspace.path, feedback=feedback)
            if agent is None:
                return
            builder_stage = "repair_builder" if is_repair else "builder"
            if not agent.success:
                if self._validated_stage_output(
                    task,
                    builder_stage,
                    success=False,
                    evidence_artifact_ids=(agent.log_path or f"{task.id}:{builder_stage}:audit",),
                    failure_route=TaskExecutionState.HUMAN_REQUIRED,
                ) is None:
                    return
                code = "EXTERNAL_ENVIRONMENT" if agent.marker == "ENVIRONMENT_UNAVAILABLE" else "BUILDER_FAILED"
                self._block(task, code, agent.stderr or agent.stdout)
                return
            if not self._builder_left_task_open(task, workspace.path):
                return

            try:
                commit_sha = self.workspace.ensure_committed(task, workspace)
                diff = self.workspace.diff_text(workspace)
                changed_files = tuple(self.workspace.changed_files(workspace))
                if not diff.strip():
                    self._block(
                        task,
                        "BUILDER_PRODUCED_NO_DIFF",
                        "Builder produced no inspectable implementation diff relative to origin/main",
                    )
                    return
                if agent.evidence is None:
                    raise EvidenceValidationError("Builder result manifest is missing")
                builder_evidence = dict(agent.evidence)
                repair_evidence = builder_evidence.pop("repairManifest", None)
                manifest = BuilderResultManifest.from_dict(
                    builder_evidence,
                    expected_task_id=task.id,
                    expected_commit_sha=commit_sha,
                    expected_ac_ids=acceptance_criterion_ids(workspace.path / task.spec_path),
                    expected_changed_files=changed_files,
                    expected_required_command_ids=tuple(self.verification.required_command_ids),
                )
                if not manifest.all_satisfied:
                    raise EvidenceValidationError(
                        "Builder manifest contains BLOCKED acceptance criteria"
                    )
                repair_manifest_path: str | None = None
                if is_repair and repair_packet is not None:
                    if previous_review_commit is None:
                        raise RepairContractError("repair baseline is missing")
                    repair_delta = tuple(
                        self.workspace.changed_files_between(
                            workspace, previous_review_commit, commit_sha
                        )
                    )
                    repair_manifest = RepairManifest.from_dict(
                        repair_evidence,
                        packet=repair_packet,
                        repaired_sha=commit_sha,
                        repair_delta=repair_delta,
                    )
                    repair_manifest_path = write_evidence_artifact(
                        workspace.path,
                        task.catalog,
                        task.id,
                        f"repair-manifest-{fix_round}.json",
                        repair_manifest.to_dict(),
                    )
                manifest_path = write_evidence_artifact(
                    workspace.path,
                    task.catalog,
                    task.id,
                    f"builder-manifest-{fix_round}.json",
                    manifest.to_dict(),
                )
            except (WorkspaceError, EvidenceValidationError, RepairContractError, AttributeError) as exc:
                self._block(task, "BUILDER_EVIDENCE_INVALID", str(exc))
                return

            builder_output_id = self._validated_stage_output(
                task,
                builder_stage,
                success=True,
                evidence_artifact_ids=(
                    agent.log_path or f"{task.id}:{builder_stage}:audit",
                    manifest_path,
                    *(() if repair_manifest_path is None else (repair_manifest_path,)),
                ),
            )
            if builder_output_id is None:
                return

            self.state.update_task(
                task.id,
                TaskExecutionState.PRE_REVIEW_VERIFICATION
                if not is_repair
                else TaskExecutionState.REPAIR_VERIFICATION,
                input_artifact_id=builder_output_id,
            )
            verification = self.verification.run(
                task,
                workspace.path,
                phase=f"builder-{fix_round}",
                commit_sha=commit_sha,
                additional_commands=manifest.additional_commands,
            )
            try:
                report = self._authoritative_verification_report(
                    task,
                    verification,
                    expected_commit_sha=commit_sha,
                    additional_commands=manifest.additional_commands,
                )
            except EvidenceValidationError as exc:
                self._block(task, "INVALID_VERIFICATION_REPORT", str(exc))
                return
            verification_report_path = write_evidence_artifact(
                workspace.path,
                task.catalog,
                task.id,
                f"verification-report-{fix_round}.json",
                report.to_dict(),
            )
            verification_route = (
                None
                if verification.success
                else (
                    TaskExecutionState.BLOCKED
                    if fix_round >= self.config.max_fix_attempts
                    else TaskExecutionState.REPAIR_REQUIRED
                )
            )
            verification_output_id = self._validated_stage_output(
                task,
                "verification",
                success=verification.success,
                evidence_artifact_ids=(
                    verification.log_path or f"{task.id}:verification:audit",
                    verification_report_path,
                ),
                failure_route=verification_route,
            )
            if verification_output_id is None:
                return
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
                self.state.update_task(
                    task.id,
                    TaskExecutionState.REPAIR_REQUIRED,
                    input_artifact_id=verification_output_id,
                )
                continue

            self.state.update_task(
                task.id,
                TaskExecutionState.FIRST_REVIEW
                if not is_repair
                else TaskExecutionState.RE_REVIEW,
                input_artifact_id=verification_output_id,
            )
            review = self.agent_runner.run_reviewer(
                task,
                workspace.path,
                diff=diff,
                review_context=review_context,
                final_review=fix_round == self.config.max_fix_attempts,
                builder_manifest_path=manifest_path,
                verification_report_path=verification_report_path,
                reviewed_commit_sha=commit_sha,
                acceptance_ids=tuple(item.ac_id for item in manifest.acceptance_criteria),
                changed_files=changed_files,
                repair_changed_files=(
                    tuple(self.workspace.changed_files_between(workspace, previous_review_commit, commit_sha))
                    if previous_review_commit else ()
                ),
                repair_packet_path=repair_packet_path,
                repair_manifest_path=repair_manifest_path,
                repair_baseline_sha=previous_review_commit or "",
                repaired_sha=commit_sha,
            )
            if review.raw.marker == "ENVIRONMENT_UNAVAILABLE":
                if self._validated_stage_output(
                    task,
                    "reviewer",
                    success=False,
                    evidence_artifact_ids=(review.raw.log_path or f"{task.id}:reviewer:audit",),
                    failure_route=TaskExecutionState.HUMAN_REQUIRED,
                ) is None:
                    return
                self._block(
                    task,
                    "REVIEWER_ENVIRONMENT_UNAVAILABLE",
                    "Reviewer could not start or access the task worktree; no review ledger was established. "
                    + review.findings[-20000:],
                )
                return
            if review.raw.marker == "REVIEWER_WRITE_ATTEMPT":
                self._block(
                    task,
                    "REVIEWER_POLICY_VIOLATION",
                    "Reviewer attempted to modify the read-only task worktree; all mutations were discarded.",
                )
                return
            try:
                if not isinstance(review.ledger, dict):
                    raise ReviewLedgerError("Reviewer returned no ReviewLedger/v1")
                ledger = ReviewLedger.from_dict(
                    review.ledger,
                    expected_task_id=task.id,
                    expected_commit_sha=commit_sha,
                    expected_ac_ids=tuple(item.ac_id for item in manifest.acceptance_criteria),
                    expected_changed_files=changed_files,
                    allowed_evidence_refs=(
                        manifest_path,
                        verification_report_path,
                        *(result.log_artifact for result in report.command_results),
                        *(
                            reference
                            for finding in (previous_ledger.findings if previous_ledger else ())
                            for reference in finding.evidence_refs
                        ),
                    ),
                    previous=previous_ledger,
                    repair_changed_files=(
                        tuple(self.workspace.changed_files_between(workspace, previous_review_commit, commit_sha))
                        if previous_review_commit else ()
                    ),
                )
                ledger_path = write_evidence_artifact(
                    workspace.path,
                    task.catalog,
                    task.id,
                    f"review-ledger-{fix_round}.json",
                    ledger.to_dict(),
                )
            except (ReviewLedgerError, WorkspaceError, AttributeError) as exc:
                self._block(task, "INVALID_REVIEW_LEDGER", str(exc))
                return
            self.state.add_event(
                task.id,
                "REVIEW_LEDGER",
                f"round={fix_round}; final={fix_round == self.config.max_fix_attempts}; "
                + review.findings[-12000:],
            )
            review_passed = ledger.verdict == "PASS"
            if review_passed:
                review_output_id = self._validated_stage_output(
                    task,
                    "reviewer",
                    success=True,
                    evidence_artifact_ids=(
                        review.raw.log_path or f"{task.id}:reviewer:audit",
                        ledger_path,
                    ),
                )
                if review_output_id is None:
                    return
                self.state.update_task(
                    task.id,
                    TaskExecutionState.MERGE_QUEUED,
                    input_artifact_id=review_output_id,
                )
                self._integrate(task, workspace.branch, workspace.path)
                return

            routed = tuple(
                finding for finding in ledger.findings
                if finding.status == "OPEN" and finding.owner != FindingOwner.BUILDER
            )
            if routed:
                first = routed[0]
                self.state.add_event(
                    task.id,
                    "REVIEW_ROUTED",
                    "; ".join(
                        f"{finding.finding_id} owner={finding.owner.value} route={finding.route.value}"
                        for finding in routed
                    ),
                )
                route_code = (
                    "PLANNING_REQUIRED"
                    if first.route == FindingRoute.PLANNING_REQUIRED
                    else first.route.value
                )
                route_state = (
                    TaskExecutionState.PLANNING_REQUIRED
                    if route_code == "PLANNING_REQUIRED"
                    else TaskExecutionState.ORCHESTRATOR_ACTION_REQUIRED
                    if route_code == "ORCHESTRATOR_ACTION_REQUIRED"
                    else TaskExecutionState.HUMAN_REQUIRED
                )
                if self._validated_stage_output(
                    task,
                    "reviewer",
                    success=False,
                    evidence_artifact_ids=(
                        review.raw.log_path or f"{task.id}:reviewer:audit",
                    ),
                    failure_route=route_state,
                ) is None:
                    return
                self._block(
                    task,
                    route_code,
                    f"Review finding {first.finding_id} is owned by {first.owner.value}; "
                    f"next hop: {next_hop(first)}. Do not dispatch Builder repair until that route resolves.",
                )
                return

            if fix_round >= self.config.max_fix_attempts:
                if self._validated_stage_output(
                    task,
                    "reviewer",
                    success=False,
                    evidence_artifact_ids=(
                        review.raw.log_path or f"{task.id}:reviewer:audit",
                    ),
                    failure_route=TaskExecutionState.BLOCKED,
                ) is None:
                    return
                self._block(
                    task,
                    "RETRY_LIMIT_EXCEEDED",
                    "independent review still has blocking findings after bounded repair: "
                    + review.findings[-20000:],
                )
                return
            feedback = "Validated ReviewLedger/v1 findings:\n" + json.dumps(
                ledger.to_dict(), ensure_ascii=False
            )[-20000:]
            review_context = ledger_path
            previous_ledger = ledger
            previous_review_commit = commit_sha
            try:
                repair_packet = RepairPacket.from_ledger(ledger, ledger_path)
                repair_packet_path = write_evidence_artifact(
                    workspace.path,
                    task.catalog,
                    task.id,
                    f"repair-packet-{fix_round}.json",
                    repair_packet.to_dict(),
                )
                feedback = (
                    f"REPAIR_PACKET_PATH: {repair_packet_path}\n"
                    "REPAIR_PACKET_JSON: "
                    + json.dumps(repair_packet.to_dict(), separators=(",", ":"))
                )
            except RepairContractError as exc:
                self._block(task, "INVALID_REPAIR_PACKET", str(exc))
                return
            review_output_id = self._validated_stage_output(
                task,
                "reviewer",
                success=False,
                evidence_artifact_ids=(
                    review.raw.log_path or f"{task.id}:reviewer:audit",
                    ledger_path,
                    repair_packet_path,
                ),
                failure_route=TaskExecutionState.REPAIR_REQUIRED,
            )
            if review_output_id is None:
                return
            self.state.update_task(
                task.id,
                TaskExecutionState.REPAIR_REQUIRED,
                input_artifact_id=review_output_id,
            )

    @staticmethod
    def _write_review_context(task: CanonicalTask, worktree: Path, findings: str) -> str:
        """Persist the review ledger for the next bounded repair review.

        The file is intentionally passed as untrusted evidence. It gives the next Reviewer
        continuity without interpolating arbitrary review output into the controlling prompt.
        """
        relative = (
            Path(".commerceos/orchestrator") / task.catalog / "review-context" / f"{task.id}.txt"
        )
        path = worktree / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(findings[-50000:], encoding="utf-8")
        return relative.as_posix()

    def _builder_left_task_open(self, task: CanonicalTask, worktree: Path) -> bool:
        """Reject premature task finalization before verification/review can proceed."""
        spec_parts = Path(task.spec_path).parts
        completed_root = (
            worktree / "tasks" / task.catalog / "completed"
            if len(spec_parts) >= 3 and spec_parts[1] in BacklogReader.CATALOGS
            else worktree / "tasks" / "completed"
        )
        completed_spec = completed_root / Path(task.spec_path).name
        if completed_spec.is_file():
            try:
                self.workspace.restore_task_lifecycle(task, worktree)
            except (AttributeError, WorkspaceError) as exc:
                self._block(task, "BUILDER_PREMATURE_COMPLETION", str(exc))
                return False
            self.state.add_event(
                task.id,
                "BUILDER_LIFECYCLE_RESTORED",
                "discarded premature completion bookkeeping; implementation changes preserved",
            )
            return True
        try:
            snapshot = BacklogReader(worktree, self.catalog).load()
        except Exception as exc:
            self._block(
                task,
                "TASK_LIFECYCLE_CHECK_FAILED",
                f"could not inspect task lifecycle after Builder exit: {exc!r}",
            )
            return False
        current = snapshot.tasks.get(task.id)
        if current is None:
            self._block(task, "BUILDER_REMOVED_TASK", "task disappeared from the Builder worktree")
            return False
        if current.lifecycle_state == "Completed" or "/completed/" in current.spec_path:
            try:
                self.workspace.restore_task_lifecycle(task, worktree)
            except (AttributeError, WorkspaceError) as exc:
                self._block(task, "BUILDER_PREMATURE_COMPLETION", str(exc))
                return False
            self.state.add_event(
                task.id,
                "BUILDER_LIFECYCLE_RESTORED",
                "discarded premature completion bookkeeping; implementation changes preserved",
            )
            return True
        return True

    def _run_builder_with_capacity_retry(
        self,
        task: CanonicalTask,
        worktree: Path,
        *,
        feedback: str | None,
    ) -> AgentResult | None:
        """Retry transient model-capacity failures without changing the pinned model policy."""
        for capacity_retry in range(self.config.max_capacity_retries + 1):
            current_state = self.state.task_run(task.id)
            build_state = (
                current_state.execution_state
                if current_state
                and current_state.execution_state
                in {TaskExecutionState.INITIAL_BUILD, TaskExecutionState.REPAIR_BUILD}
                else TaskExecutionState.INITIAL_BUILD
            )
            self.state.update_task(task.id, build_state, attempt_delta=1)
            current = self.state.task_run(task.id)
            agent = self.agent_runner.run_builder(
                task,
                worktree,
                attempt=current.attempt if current else capacity_retry + 1,
                feedback=feedback,
            )
            if agent.success or not self._is_model_capacity_failure(agent):
                return agent

            detail = self._agent_failure_detail(agent)
            if capacity_retry >= self.config.max_capacity_retries:
                current_state = self.state.task_run(task.id)
                stage = (
                    "repair_builder"
                    if current_state
                    and current_state.execution_state == TaskExecutionState.REPAIR_BUILD
                    else "builder"
                )
                if self._validated_stage_output(
                    task,
                    stage,
                    success=False,
                    evidence_artifact_ids=(agent.log_path or f"{task.id}:{stage}:audit",),
                    failure_route=TaskExecutionState.BLOCKED,
                ) is None:
                    return None
                self.state.update_task(
                    task.id,
                    TaskExecutionState.BLOCKED,
                    blocker_code="MODEL_CAPACITY_EXHAUSTED",
                    blocker_detail=(
                        f"Pinned coding model remained at capacity after {capacity_retry + 1} attempts. "
                        f"Retry later; model fallback is disabled by policy. Last error: {detail}"
                    )[-20000:],
                )
                return None

            delay = self.config.capacity_backoff_seconds * (2**capacity_retry)
            self.state.add_event(
                task.id,
                "MODEL_CAPACITY_RETRY",
                f"attempt={capacity_retry + 1}; retry_in_seconds={delay:g}; pinned_model_unchanged=true",
            )
            time.sleep(delay)
        raise AssertionError("unreachable capacity retry loop")

    @staticmethod
    def _agent_failure_detail(agent: AgentResult) -> str:
        return (agent.stderr or agent.stdout or "Codex agent failed without output")[-20000:]

    @staticmethod
    def _is_model_capacity_failure(agent: AgentResult) -> bool:
        if agent.marker == "MODEL_CAPACITY":
            return True
        combined = f"{agent.stdout}\n{agent.stderr}".lower()
        return (
            "selected model is at capacity" in combined
            or ("model" in combined and "at capacity" in combined and "try a different model" in combined)
        )

    def _authoritative_verification_report(
        self,
        task: CanonicalTask,
        verification: VerificationResult,
        *,
        expected_commit_sha: str,
        additional_commands: tuple[AdditionalVerificationCommand, ...] = (),
    ) -> VerificationReport:
        if not isinstance(verification.report, VerificationReport):
            raise EvidenceValidationError(
                "Verification Runner returned no authoritative VerificationReport/v1"
            )
        report = verification.report
        if report.task_id != task.id or report.task_commit_sha != expected_commit_sha:
            raise EvidenceValidationError("Verification report task/commit binding mismatch")
        if report.success != verification.success:
            raise EvidenceValidationError("Verification result/report success mismatch")
        if any(not Path(result.log_artifact).is_file() for result in report.command_results):
            raise EvidenceValidationError(
                "Verification report references a missing command log artifact"
            )
        if verification.success:
            report.validate(
                expected_commands=self.verification.expected_commands(additional_commands),
                expected_commit_sha=expected_commit_sha,
            )
        return report

    def _integrate(self, task: CanonicalTask, branch: str, worktree: Path) -> None:
        with self._merge_lock:
            current_run = self.state.task_run(task.id)
            if not current_run or current_run.execution_state != TaskExecutionState.FINALIZING:
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
                        resolution = self.agent_runner.run_conflict_resolver(task, self.root, conflicted)
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

                    post_merge_commit = self.integration.current_commit()
                    post_merge = self.verification.run(
                        task,
                        self.root,
                        phase="post-integration",
                        commit_sha=post_merge_commit,
                    )
                    try:
                        self._authoritative_verification_report(
                            task,
                            post_merge,
                            expected_commit_sha=post_merge_commit,
                        )
                    except EvidenceValidationError as exc:
                        self.integration.rollback_unpushed_main()
                        self._block(task, "INVALID_VERIFICATION_REPORT", str(exc))
                        return
                    integration_output_id = self._validated_stage_output(
                        task,
                        "integration",
                        success=post_merge.success,
                        evidence_artifact_ids=(
                            post_merge.log_path or f"{task.id}:post-integration:audit",
                        ),
                        failure_route=(
                            None
                            if post_merge.success
                            else TaskExecutionState.ORCHESTRATOR_ACTION_REQUIRED
                        ),
                    )
                    if integration_output_id is None:
                        return
                    if not post_merge.success:
                        self.integration.rollback_unpushed_main()
                        self._block(task, "POST_INTEGRATION_VERIFICATION_FAILED", f"log={post_merge.log_path}")
                        return

                    self.state.update_task(
                        task.id,
                        TaskExecutionState.FINALIZING,
                        input_artifact_id=integration_output_id,
                    )

                    snapshot = BacklogReader(self.root, self.catalog).load()
                    merged_task = snapshot.tasks.get(task.id)
                    if not merged_task:
                        self.integration.rollback_unpushed_main()
                        raise IntegrationError(f"task disappeared after merge: {task.id}")
                    summary = self._completion_summary(task)
                    BacklogWriter(self.root).finalize_task(snapshot, merged_task, summary)
                    self.integration.commit_bookkeeping(task)
                    final_commit = self.integration.current_commit()
                    final_verification = self.verification.run(
                        task,
                        self.root,
                        phase="post-bookkeeping",
                        commit_sha=final_commit,
                    )
                    completion_transaction_path = self._completion_transaction_artifact(
                        task,
                        integrated_sha=post_merge_commit,
                        bookkeeping_sha=final_commit,
                        completed_path=merged_task.spec_path.replace("/backlog/", "/completed/"),
                        push_eligible=final_verification.success,
                    )
                    try:
                        self._authoritative_verification_report(
                            task,
                            final_verification,
                            expected_commit_sha=final_commit,
                        )
                    except EvidenceValidationError as exc:
                        self.integration.rollback_unpushed_main()
                        self._block(task, "INVALID_VERIFICATION_REPORT", str(exc))
                        return
                    finalization_output_id = self._validated_stage_output(
                        task,
                        "finalization",
                        success=final_verification.success,
                        evidence_artifact_ids=(
                            final_verification.log_path or f"{task.id}:post-bookkeeping:audit",
                            completion_transaction_path,
                        ),
                        failure_route=(
                            None
                            if final_verification.success
                            else TaskExecutionState.ORCHESTRATOR_ACTION_REQUIRED
                        ),
                    )
                    if finalization_output_id is None:
                        return
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
                    integration_output_id = self._validated_stage_output(
                        task,
                        "integration",
                        success=True,
                        evidence_artifact_ids=(f"{task.id}:already-on-remote-main",),
                    )
                    if integration_output_id is None:
                        return
                    self.state.update_task(
                        task.id,
                        TaskExecutionState.FINALIZING,
                        input_artifact_id=integration_output_id,
                    )
                    snapshot = BacklogReader(self.root, self.catalog).load()
                    current_task = snapshot.tasks.get(task.id)
                    finalization_output_id: str | None = None
                    if current_task and current_task.lifecycle_state != "Completed":
                        summary = self._completion_summary(task)
                        BacklogWriter(self.root).finalize_task(snapshot, current_task, summary)
                        self.integration.commit_bookkeeping(task)
                        recovery_commit = self.integration.current_commit()
                        final_verification = self.verification.run(
                            task,
                            self.root,
                            phase="recovery-bookkeeping",
                            commit_sha=recovery_commit,
                        )
                        completion_transaction_path = self._completion_transaction_artifact(
                            task,
                            integrated_sha=recovery_commit,
                            bookkeeping_sha=recovery_commit,
                            completed_path=current_task.spec_path.replace("/backlog/", "/completed/"),
                            push_eligible=final_verification.success,
                        )
                        try:
                            self._authoritative_verification_report(
                                task,
                                final_verification,
                                expected_commit_sha=recovery_commit,
                            )
                        except EvidenceValidationError as exc:
                            self.integration.rollback_unpushed_main()
                            self._block(task, "INVALID_VERIFICATION_REPORT", str(exc))
                            return
                        finalization_output_id = self._validated_stage_output(
                            task,
                            "finalization",
                            success=final_verification.success,
                            evidence_artifact_ids=(
                                final_verification.log_path
                                or f"{task.id}:recovery-bookkeeping:audit",
                                completion_transaction_path,
                            ),
                            failure_route=(
                                None
                                if final_verification.success
                                else TaskExecutionState.ORCHESTRATOR_ACTION_REQUIRED
                            ),
                        )
                        if finalization_output_id is None:
                            return
                        if not final_verification.success:
                            self.integration.rollback_unpushed_main()
                            self._block(
                                task,
                                "COMPLETION_BOOKKEEPING_VERIFICATION_FAILED",
                                f"log={final_verification.log_path}",
                            )
                            return
                        self.integration.push_main()

                    if finalization_output_id is None:
                        finalization_output_id = self._validated_stage_output(
                            task,
                            "finalization",
                            success=True,
                            evidence_artifact_ids=(f"{task.id}:canonical-completed",),
                        )
                        if finalization_output_id is None:
                            return

                self.state.update_task(
                    task.id,
                    TaskExecutionState.COMPLETED,
                    input_artifact_id=finalization_output_id,
                )
                try:
                    refreshed = BacklogReader(self.root, self.catalog).load()
                    completed_task = refreshed.tasks.get(task.id, task)
                    self.workspace.cleanup(completed_task)
                except Exception as cleanup_error:
                    self.state.add_event(task.id, "CLEANUP_WARNING", repr(cleanup_error))
            except Exception as exc:
                try:
                    if integration_checkout_prepared:
                        self.integration.rollback_unpushed_main()
                    else:
                        self.integration.abort_merge()
                except Exception as rollback_error:
                    self.state.add_event(task.id, "ROLLBACK_WARNING", repr(rollback_error))
                self._block(task, "INTEGRATION_ERROR", str(exc))

    def _completion_transaction_artifact(
        self,
        task: CanonicalTask,
        *,
        integrated_sha: str,
        bookkeeping_sha: str,
        completed_path: str,
        push_eligible: bool,
    ) -> str:
        return write_evidence_artifact(
            self.root,
            task.catalog,
            task.id,
            "completion-transaction.json",
            {
                "contractVersion": "CompletionTransaction/v1",
                "taskId": task.id,
                "catalog": task.catalog,
                "integratedSha": integrated_sha,
                "bookkeepingSha": bookkeeping_sha,
                "completedPath": completed_path,
                "canonicalValidation": "PASS" if push_eligible else "BLOCKED",
                "pushEligible": push_eligible,
            },
        )

    def _block(self, task: CanonicalTask, code: str, detail: str) -> None:
        if code == "PLANNING_REQUIRED":
            target = TaskExecutionState.PLANNING_REQUIRED
        elif code in {
            "WORKTREE_ERROR",
            "GIT_TASK_BRANCH_ERROR",
            "POST_INTEGRATION_VERIFICATION_FAILED",
            "COMPLETION_BOOKKEEPING_VERIFICATION_FAILED",
            "INTEGRATION_ERROR",
        }:
            target = TaskExecutionState.ORCHESTRATOR_ACTION_REQUIRED
        elif code in {"MODEL_CAPACITY_EXHAUSTED", "RETRY_LIMIT_EXCEEDED"}:
            target = TaskExecutionState.BLOCKED
        else:
            target = TaskExecutionState.HUMAN_REQUIRED
        self.state.update_task(
            task.id,
            target,
            blocker_code=code,
            blocker_detail=detail[-20000:],
        )

    def _validated_stage_output(
        self,
        task: CanonicalTask,
        stage: str,
        *,
        success: bool,
        evidence_artifact_ids: tuple[str, ...],
        failure_route: TaskExecutionState | None = None,
    ) -> str | None:
        run = self.state.task_run(task.id)
        attempt = run.attempt if run else 0
        fix_attempt = run.fix_attempt if run else 0
        artifact_id = f"{task.id}:{stage}:output:{attempt}:{fix_attempt}"
        payload = self._stage_output_payload(
            task,
            stage,
            artifact_id=artifact_id,
            success=success,
            evidence_artifact_ids=evidence_artifact_ids,
            failure_route=failure_route,
        )
        try:
            record = stage_contract(stage).output_type.from_dict(payload)
        except StageContractError as exc:
            self._block(task, "INVALID_STAGE_OUTPUT", f"stage={stage}: {exc}")
            return None
        self.state.add_event(
            task.id,
            "STAGE_OUTPUT_VALIDATED",
            json.dumps(
                {
                    "stage": stage,
                    "artifact_id": record.artifact_id,
                    "contract_version": record.contract_version,
                    "success": record.success,
                    "failure_route": record.failure_route,
                },
                sort_keys=True,
            ),
        )
        return record.artifact_id

    @staticmethod
    def _stage_output_payload(
        task: CanonicalTask,
        stage: str,
        *,
        artifact_id: str,
        success: bool,
        evidence_artifact_ids: tuple[str, ...],
        failure_route: TaskExecutionState | None,
    ) -> dict[str, object]:
        return {
            "contract_version": CONTRACT_VERSION,
            "task_id": task.id,
            "stage": stage,
            "artifact_id": artifact_id,
            "success": success,
            "commit_sha": "workspace-pending",
            "evidence_artifact_ids": list(evidence_artifact_ids),
            "failure_route": failure_route.value if failure_route else None,
        }

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
