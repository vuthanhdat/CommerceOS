from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .models import TaskExecutionState
from .evidence import BuilderResultManifest, EvidenceValidationError, VerificationReport
from .review_contract import ReviewLedger, ReviewLedgerError
from .completion_contract import CompletionContractError, CompletionEntryGate


WORKFLOW_STATUS: dict[TaskExecutionState, tuple[str, str]] = {
    TaskExecutionState.QUEUED: ("ORCHESTRATOR", "dispatch a valid planning or Builder input"),
    TaskExecutionState.PLANNING: ("BACKLOG_PLANNER", "produce a valid planning result"),
    TaskExecutionState.INITIAL_BUILD: ("BUILDER", "produce a valid BuilderResultManifest/v1"),
    TaskExecutionState.PRE_REVIEW_VERIFICATION: ("VERIFICATION_RUNNER", "pass every required verification command"),
    TaskExecutionState.FIRST_REVIEW: ("REVIEWER", "produce ReviewLedger/v1 with no open blocking finding"),
    TaskExecutionState.REPAIR_REQUIRED: ("ORCHESTRATOR", "persist a finding-scoped RepairPacket/v1"),
    TaskExecutionState.REPAIR_BUILD: ("REPAIR_BUILDER", "address every packet finding within its path allow-list"),
    TaskExecutionState.REPAIR_VERIFICATION: ("VERIFICATION_RUNNER", "pass every required repair verification command"),
    TaskExecutionState.RE_REVIEW: ("REVIEWER", "resolve prior findings with a PASS ledger"),
    TaskExecutionState.MERGE_QUEUED: ("ORCHESTRATOR", "acquire the serialized latest-main merge lane"),
    TaskExecutionState.INTEGRATING: ("ORCHESTRATOR", "merge cleanly and pass post-integration verification"),
    TaskExecutionState.FINALIZING: ("ORCHESTRATOR", "validate canonical completion and post-bookkeeping verification before push"),
}


def workflow_status(state: TaskExecutionState | None) -> tuple[str | None, str | None]:
    return WORKFLOW_STATUS.get(state, (None, None))


def evidence_counters(root: Path, catalog: str, task_id: str) -> dict[str, Any]:
    evidence_root = root / ".commerceos/orchestrator" / catalog / "evidence" / task_id
    counters: dict[str, Any] = {
        "status": "MISSING",
        "acceptance_criteria": {"satisfied": 0, "total": 0},
        "changed_files": {"covered": 0, "total": 0},
        "test_totals": {"discovered": 0, "passed": 0, "failed": 0, "skipped_required": 0},
        "open_findings_by_owner": {},
    }
    if not evidence_root.is_dir():
        return counters
    try:
        gate_path = evidence_root / "completion-entry-gate.json"
        any_contract = any(evidence_root.glob("*.json"))
        if not any_contract:
            return counters
        if not gate_path.is_file():
            counters["status"] = "INCOMPLETE"
            return counters
        gate = CompletionEntryGate.from_dict(
            json.loads(gate_path.read_text(encoding="utf-8"))
        )
        if gate.task_id != task_id:
            raise ValueError("evidence gate task binding mismatch")
        manifest_payload = _gated_payload(root, gate.builder_manifest_path)
        report_payload = _gated_payload(root, gate.verification_report_path)
        ledger_payload = _gated_payload(root, gate.review_ledger_path)
        manifest = BuilderResultManifest.from_dict(
            manifest_payload,
            expected_task_id=task_id,
            expected_commit_sha=gate.task_commit_sha,
            expected_ac_ids=gate.acceptance_criterion_ids,
            expected_changed_files=gate.changed_files,
            expected_required_command_ids=gate.required_command_ids,
        )
        report = VerificationReport.from_dict(report_payload)
        if report.task_id != task_id or report.task_commit_sha != manifest.task_commit_sha:
            raise ValueError("verification task/commit binding mismatch")
        expected_commands = {result.command_id: result.argv for result in report.command_results}
        expected_command_ids = (
            *gate.required_command_ids,
            *(command.command_id for command in manifest.additional_commands),
        )
        if tuple(expected_commands) != expected_command_ids:
            raise ValueError("verification command inventory differs from entry gate")
        report.validate(expected_commands=expected_commands, expected_commit_sha=manifest.task_commit_sha)
        ledger = ReviewLedger.from_dict(
            ledger_payload,
            expected_task_id=task_id,
            expected_commit_sha=manifest.task_commit_sha,
            expected_ac_ids=tuple(item.ac_id for item in manifest.acceptance_criteria),
            expected_changed_files=manifest.changed_files,
            allowed_evidence_refs=gate.allowed_evidence_refs,
            expected_review_round=_string(ledger_payload, "reviewRound"),
        )
        counters["acceptance_criteria"] = {
            "satisfied": sum(item.verdict == "SATISFIED" for item in manifest.acceptance_criteria),
            "total": len(manifest.acceptance_criteria),
        }
        counters["changed_files"] = {
            "covered": len(ledger.changed_files), "total": len(manifest.changed_files)
        }
        counters["test_totals"] = {
            "discovered": report.test_totals.discovered, "passed": report.test_totals.passed,
            "failed": report.test_totals.failed,
            "skipped_required": report.test_totals.skipped_required,
        }
        owners: dict[str, int] = {}
        for finding in ledger.findings:
            if finding.status == "OPEN":
                owners[finding.owner.value] = owners.get(finding.owner.value, 0) + 1
        counters["open_findings_by_owner"] = owners
        counters["status"] = "VALID"
    except (
        OSError, ValueError, json.JSONDecodeError, TypeError, EvidenceValidationError,
        ReviewLedgerError, CompletionContractError,
    ):
        counters["status"] = "INVALID"
    return counters


def _gated_payload(root: Path, relative: str) -> dict[str, Any]:
    path = (root / relative).resolve()
    path.relative_to(root.resolve())
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError("evidence artifact must be an object")
    return value


def _list(value: dict[str, Any], key: str) -> list[Any]:
    result = value.get(key)
    if not isinstance(result, list):
        raise ValueError(f"{key} must be a list")
    return result


def _string(value: dict[str, Any], key: str) -> str:
    result = value.get(key)
    if not isinstance(result, str) or not result:
        raise ValueError(f"{key} must be a non-empty string")
    return result
