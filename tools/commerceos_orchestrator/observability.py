from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

from .models import TaskExecutionState
from .evidence import BuilderResultManifest, EvidenceValidationError, VerificationReport
from .review_contract import ReviewLedger, ReviewLedgerError


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
        artifacts = (
            _latest_json(evidence_root, "builder-manifest-*.json"),
            _latest_json(evidence_root, "verification-report-*.json"),
            _latest_json(evidence_root, "review-ledger-*.json"),
        )
        if all(item is None for item in artifacts):
            return counters
        if any(item is None for item in artifacts):
            counters["status"] = "INCOMPLETE"
            return counters
        (manifest_path, manifest_payload), (report_path, report_payload), (ledger_path, ledger_payload) = artifacts  # type: ignore[misc]
        rounds = {_round(path) for path in (manifest_path, report_path, ledger_path)}
        if len(rounds) != 1:
            raise ValueError("evidence artifacts are from different rounds")
        criteria_rows = _list(manifest_payload, "acceptanceCriteria")
        changed_rows = _list(manifest_payload, "changedFiles")
        required_ids = _list(manifest_payload, "requiredCommandIds")
        manifest = BuilderResultManifest.from_dict(
            manifest_payload,
            expected_task_id=task_id,
            expected_commit_sha=_string(manifest_payload, "taskCommitSha"),
            expected_ac_ids=tuple(_string(row, "acId") for row in criteria_rows),
            expected_changed_files=tuple(_string_value(item, "changedFiles") for item in changed_rows),
            expected_required_command_ids=tuple(_string_value(item, "requiredCommandIds") for item in required_ids),
        )
        report = VerificationReport.from_dict(report_payload)
        if report.task_id != task_id or report.task_commit_sha != manifest.task_commit_sha:
            raise ValueError("verification task/commit binding mismatch")
        expected_commands = {result.command_id: result.argv for result in report.command_results}
        report.validate(expected_commands=expected_commands, expected_commit_sha=manifest.task_commit_sha)
        ledger_ac = tuple(_string(row, "acId") for row in _list(ledger_payload, "acceptanceCriteria"))
        ledger_files = tuple(_string(row, "path") for row in _list(ledger_payload, "changedFiles"))
        evidence_refs = tuple(dict.fromkeys(
            reference
            for finding in _list(ledger_payload, "findings")
            for reference in _list(finding, "evidenceRefs")
            if isinstance(reference, str)
        ))
        ledger = ReviewLedger.from_dict(
            ledger_payload,
            expected_task_id=task_id,
            expected_commit_sha=manifest.task_commit_sha,
            expected_ac_ids=tuple(item.ac_id for item in manifest.acceptance_criteria),
            expected_changed_files=manifest.changed_files,
            allowed_evidence_refs=evidence_refs,
            expected_review_round=_string(ledger_payload, "reviewRound"),
        )
        if ledger_ac != tuple(item.ac_id for item in manifest.acceptance_criteria) or ledger_files != manifest.changed_files:
            raise ValueError("evidence coverage mismatch")
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
        ReviewLedgerError,
    ):
        counters["status"] = "INVALID"
    return counters


def _latest_json(root: Path, pattern: str) -> tuple[Path, dict[str, Any]] | None:
    paths = sorted(root.glob(pattern), key=lambda path: (path.stat().st_mtime_ns, path.name))
    if not paths:
        return None
    value = json.loads(paths[-1].read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError("evidence artifact must be an object")
    return paths[-1], value


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


def _string_value(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise ValueError(f"{label} must contain non-empty strings")
    return value


def _round(path: Path) -> int:
    match = re.search(r"-(\d+)\.json$", path.name)
    if not match:
        raise ValueError("evidence artifact has no round")
    return int(match.group(1))
