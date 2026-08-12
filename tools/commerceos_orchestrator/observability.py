from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .models import TaskExecutionState


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
        manifest = _latest_json(evidence_root, "builder-manifest-*.json")
        report = _latest_json(evidence_root, "verification-report-*.json")
        ledger = _latest_json(evidence_root, "review-ledger-*.json")
        seen = False
        if manifest is not None:
            seen = True
            criteria = _list(manifest, "acceptanceCriteria")
            changed = _list(manifest, "changedFiles")
            counters["acceptance_criteria"] = {
                "satisfied": sum(row.get("verdict") == "SATISFIED" for row in criteria if isinstance(row, dict)),
                "total": len(criteria),
            }
            counters["changed_files"]["total"] = len(changed)
        if report is not None:
            seen = True
            totals = _dict(report, "testTotals")
            required = {"discovered", "passed", "failed", "skipped_required"}
            if set(totals) != required or not all(isinstance(totals[key], int) for key in required):
                raise ValueError("invalid verification testTotals")
            counters["test_totals"] = {key: totals[key] for key in required}
        if ledger is not None:
            seen = True
            files = _list(ledger, "changedFiles")
            counters["changed_files"]["covered"] = len(files)
            owners: dict[str, int] = {}
            for finding in _list(ledger, "findings"):
                if not isinstance(finding, dict):
                    raise ValueError("invalid review finding")
                if finding.get("status") == "OPEN":
                    owner = finding.get("owner")
                    if not isinstance(owner, str) or not owner:
                        raise ValueError("invalid review finding owner")
                    owners[owner] = owners.get(owner, 0) + 1
            counters["open_findings_by_owner"] = owners
        counters["status"] = "VALID" if seen else "MISSING"
    except (OSError, ValueError, json.JSONDecodeError, TypeError):
        counters["status"] = "INVALID"
    return counters


def _latest_json(root: Path, pattern: str) -> dict[str, Any] | None:
    paths = sorted(root.glob(pattern), key=lambda path: (path.stat().st_mtime_ns, path.name))
    if not paths:
        return None
    value = json.loads(paths[-1].read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError("evidence artifact must be an object")
    return value


def _list(value: dict[str, Any], key: str) -> list[Any]:
    result = value.get(key)
    if not isinstance(result, list):
        raise ValueError(f"{key} must be a list")
    return result


def _dict(value: dict[str, Any], key: str) -> dict[str, Any]:
    result = value.get(key)
    if not isinstance(result, dict):
        raise ValueError(f"{key} must be an object")
    return result
