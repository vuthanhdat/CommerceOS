from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass
from pathlib import Path, PurePosixPath
from typing import Any


BUILDER_MANIFEST_VERSION = "BuilderResultManifest/v1"
VERIFICATION_REPORT_VERSION = "VerificationReport/v1"
AC_ID = re.compile(r"AC\d{2,}")


class EvidenceValidationError(ValueError):
    pass


@dataclass(frozen=True)
class AcceptanceCriterionVerdict:
    ac_id: str
    verdict: str
    evidence_ids: tuple[str, ...]


@dataclass(frozen=True)
class BuilderResultManifest:
    contract_version: str
    task_id: str
    task_commit_sha: str
    acceptance_criteria: tuple[AcceptanceCriterionVerdict, ...]
    changed_files: tuple[str, ...]
    required_command_ids: tuple[str, ...]
    limitations: tuple[str, ...]
    follow_ups: tuple[str, ...]

    @classmethod
    def from_dict(
        cls,
        payload: dict[str, Any],
        *,
        expected_task_id: str,
        expected_commit_sha: str,
        expected_ac_ids: tuple[str, ...],
        expected_changed_files: tuple[str, ...],
        expected_required_command_ids: tuple[str, ...],
    ) -> BuilderResultManifest:
        fields = (
            "contractVersion",
            "taskId",
            "taskCommitSha",
            "acceptanceCriteria",
            "changedFiles",
            "requiredCommandIds",
            "limitations",
            "followUps",
        )
        values = _required_object(payload, fields, "Builder manifest")
        if values["contractVersion"] != BUILDER_MANIFEST_VERSION:
            raise EvidenceValidationError("unsupported Builder manifest contractVersion")
        if values["taskId"] != expected_task_id:
            raise EvidenceValidationError("Builder manifest taskId mismatch")
        if values["taskCommitSha"] != expected_commit_sha:
            raise EvidenceValidationError("Builder manifest taskCommitSha mismatch")

        ac_rows = _required_list(values["acceptanceCriteria"], "acceptanceCriteria")
        verdicts: list[AcceptanceCriterionVerdict] = []
        for index, row in enumerate(ac_rows):
            item = _required_object(
                row,
                ("acId", "verdict", "evidenceIds"),
                f"acceptanceCriteria[{index}]",
            )
            ac_id = _required_string(item["acId"], f"acceptanceCriteria[{index}].acId")
            if not AC_ID.fullmatch(ac_id):
                raise EvidenceValidationError(f"invalid acceptance criterion id: {ac_id}")
            verdict = _required_string(item["verdict"], f"acceptanceCriteria[{index}].verdict")
            if verdict not in {"SATISFIED", "BLOCKED"}:
                raise EvidenceValidationError(f"invalid verdict for {ac_id}: {verdict}")
            evidence_ids = _string_tuple(item["evidenceIds"], f"{ac_id}.evidenceIds", allow_empty=False)
            verdicts.append(AcceptanceCriterionVerdict(ac_id, verdict, evidence_ids))

        actual_ac_ids = tuple(verdict.ac_id for verdict in verdicts)
        _require_exact_unique_ids("acceptance criteria", actual_ac_ids, expected_ac_ids)
        changed_files = _path_tuple(values["changedFiles"], "changedFiles")
        if set(changed_files) != set(expected_changed_files) or len(changed_files) != len(
            expected_changed_files
        ):
            raise EvidenceValidationError("Builder manifest changedFiles mismatch Git inventory")
        command_ids = _string_tuple(values["requiredCommandIds"], "requiredCommandIds")
        _require_exact_unique_ids(
            "required command IDs", command_ids, expected_required_command_ids
        )
        limitations = _string_tuple(values["limitations"], "limitations")
        follow_ups = _string_tuple(values["followUps"], "followUps")
        return cls(
            BUILDER_MANIFEST_VERSION,
            expected_task_id,
            expected_commit_sha,
            tuple(verdicts),
            changed_files,
            command_ids,
            limitations,
            follow_ups,
        )

    @property
    def all_satisfied(self) -> bool:
        return all(verdict.verdict == "SATISFIED" for verdict in self.acceptance_criteria)

    def to_dict(self) -> dict[str, Any]:
        return {
            "contractVersion": self.contract_version,
            "taskId": self.task_id,
            "taskCommitSha": self.task_commit_sha,
            "acceptanceCriteria": [
                {
                    "acId": verdict.ac_id,
                    "verdict": verdict.verdict,
                    "evidenceIds": list(verdict.evidence_ids),
                }
                for verdict in self.acceptance_criteria
            ],
            "changedFiles": list(self.changed_files),
            "requiredCommandIds": list(self.required_command_ids),
            "limitations": list(self.limitations),
            "followUps": list(self.follow_ups),
        }


@dataclass(frozen=True)
class VerificationCommandResult:
    command_id: str
    argv: tuple[str, ...]
    exit_code: int
    log_artifact: str


@dataclass(frozen=True)
class TestTotals:
    discovered: int
    passed: int
    failed: int
    skipped_required: int


@dataclass(frozen=True)
class VerificationReport:
    contract_version: str
    task_id: str
    task_commit_sha: str
    command_results: tuple[VerificationCommandResult, ...]
    test_totals: TestTotals
    success: bool

    def validate(
        self, *, expected_command_ids: tuple[str, ...], expected_commit_sha: str
    ) -> None:
        if self.contract_version != VERIFICATION_REPORT_VERSION:
            raise EvidenceValidationError("unsupported Verification report contractVersion")
        if self.task_commit_sha != expected_commit_sha:
            raise EvidenceValidationError("Verification report taskCommitSha mismatch")
        ids = tuple(result.command_id for result in self.command_results)
        _require_exact_unique_ids("verification command results", ids, expected_command_ids)
        if any(result.exit_code != 0 for result in self.command_results):
            raise EvidenceValidationError("required verification command failed")
        totals = self.test_totals
        if totals.discovered < 1:
            raise EvidenceValidationError("Verification discovered no required checks/tests")
        if totals.failed != 0 or totals.skipped_required != 0:
            raise EvidenceValidationError("Verification has failed or skipped required tests")
        if totals.passed != totals.discovered:
            raise EvidenceValidationError("Verification pass rate is below 100%")
        if not self.success:
            raise EvidenceValidationError("Verification report success predicate is false")

    def to_dict(self) -> dict[str, Any]:
        return {
            "contractVersion": self.contract_version,
            "taskId": self.task_id,
            "taskCommitSha": self.task_commit_sha,
            "commandResults": [
                {
                    "commandId": result.command_id,
                    "argv": list(result.argv),
                    "exitCode": result.exit_code,
                    "logArtifact": result.log_artifact,
                }
                for result in self.command_results
            ],
            "testTotals": asdict(self.test_totals),
            "success": self.success,
        }


def acceptance_criterion_ids(spec_path: Path) -> tuple[str, ...]:
    text = spec_path.read_text(encoding="utf-8")
    ids = tuple(re.findall(r"^###\s+(AC\d{2,})\b", text, flags=re.MULTILINE))
    if len(ids) != len(set(ids)):
        raise EvidenceValidationError("task specification contains duplicate acceptance criterion IDs")
    return ids


def write_evidence_artifact(
    worktree: Path, catalog: str, task_id: str, name: str, payload: dict[str, Any]
) -> str:
    relative = Path(".commerceos/orchestrator") / catalog / "evidence" / task_id / name
    destination = (worktree / relative).resolve()
    root = worktree.resolve()
    try:
        destination.relative_to(root)
    except ValueError as exc:
        raise EvidenceValidationError("evidence artifact escaped worktree") from exc
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")
    return relative.as_posix()


def _required_object(value: Any, fields: tuple[str, ...], label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise EvidenceValidationError(f"{label} must be an object")
    missing = [field for field in fields if field not in value]
    if missing:
        raise EvidenceValidationError(f"{label} missing fields: {', '.join(missing)}")
    unknown = sorted(set(value) - set(fields))
    if unknown:
        raise EvidenceValidationError(f"{label} has unknown fields: {', '.join(unknown)}")
    return {field: value[field] for field in fields}


def _required_list(value: Any, label: str) -> list[Any]:
    if not isinstance(value, list):
        raise EvidenceValidationError(f"{label} must be a list")
    return value


def _required_string(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise EvidenceValidationError(f"{label} must be a non-empty string")
    return value


def _string_tuple(value: Any, label: str, *, allow_empty: bool = True) -> tuple[str, ...]:
    values = _required_list(value, label)
    if not allow_empty and not values:
        raise EvidenceValidationError(f"{label} must not be empty")
    result = tuple(_required_string(item, label) for item in values)
    if len(result) != len(set(result)):
        raise EvidenceValidationError(f"{label} contains duplicates")
    return result


def _path_tuple(value: Any, label: str) -> tuple[str, ...]:
    values = _string_tuple(value, label)
    normalized: list[str] = []
    for item in values:
        path = PurePosixPath(item.replace("\\", "/"))
        if path.is_absolute() or ".." in path.parts or str(path) in {"", "."}:
            raise EvidenceValidationError(f"{label} contains unsafe path: {item}")
        normalized.append(str(path))
    if len(normalized) != len(set(normalized)):
        raise EvidenceValidationError(f"{label} contains duplicate paths")
    return tuple(normalized)


def _require_exact_unique_ids(label: str, actual: tuple[str, ...], expected: tuple[str, ...]) -> None:
    if len(actual) != len(set(actual)):
        raise EvidenceValidationError(f"{label} contains duplicates")
    if set(actual) != set(expected) or len(actual) != len(expected):
        unknown = sorted(set(actual) - set(expected))
        missing = sorted(set(expected) - set(actual))
        raise EvidenceValidationError(
            f"{label} mismatch; missing={missing}, unknown={unknown}"
        )
