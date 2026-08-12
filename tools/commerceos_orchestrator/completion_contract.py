from __future__ import annotations

from dataclasses import dataclass
from pathlib import PurePosixPath
from typing import Any


COMPLETION_TRANSACTION_VERSION = "CompletionTransaction/v1"


class CompletionContractError(ValueError):
    pass


@dataclass(frozen=True)
class CompletionTransaction:
    task_id: str
    catalog: str
    integrated_sha: str
    bookkeeping_sha: str
    completed_path: str
    original_task_path: str
    evidence_artifact_ids: tuple[str, ...]

    @classmethod
    def create(
        cls,
        *,
        task_id: str,
        catalog: str,
        integrated_sha: str,
        bookkeeping_sha: str,
        completed_path: str,
        original_task_path: str,
        evidence_artifact_ids: tuple[str, ...],
    ) -> "CompletionTransaction":
        return cls.from_dict(
            {
                "contractVersion": COMPLETION_TRANSACTION_VERSION,
                "taskId": task_id,
                "catalog": catalog,
                "integratedSha": integrated_sha,
                "bookkeepingSha": bookkeeping_sha,
                "completedPath": completed_path,
                "preFinalizationSnapshot": {
                    "lifecycle": "Backlog",
                    "taskPath": original_task_path,
                },
                "evidenceArtifactIds": list(evidence_artifact_ids),
                "canonicalValidation": "PASS",
                "authoritativeVerification": "PASS",
                "rollbackOutcome": "NOT_REQUIRED",
                "pushEligible": True,
            }
        )

    @classmethod
    def from_dict(cls, value: Any) -> "CompletionTransaction":
        fields = {
            "contractVersion", "taskId", "catalog", "integratedSha", "bookkeepingSha",
            "completedPath", "preFinalizationSnapshot", "evidenceArtifactIds",
            "canonicalValidation", "authoritativeVerification", "rollbackOutcome",
            "pushEligible",
        }
        if not isinstance(value, dict) or set(value) != fields:
            raise CompletionContractError("invalid CompletionTransaction/v1 fields")
        if value["contractVersion"] != COMPLETION_TRANSACTION_VERSION:
            raise CompletionContractError("unsupported completion transaction version")
        strings = ("taskId", "catalog", "integratedSha", "bookkeepingSha", "completedPath")
        if any(not isinstance(value[field], str) or not value[field].strip() for field in strings):
            raise CompletionContractError("completion transaction has an empty identity field")
        completed_path = PurePosixPath(value["completedPath"])
        if completed_path.is_absolute() or ".." in completed_path.parts or "completed" not in completed_path.parts:
            raise CompletionContractError("completion transaction has an unsafe completed path")
        snapshot = value["preFinalizationSnapshot"]
        if not isinstance(snapshot, dict) or set(snapshot) != {"lifecycle", "taskPath"}:
            raise CompletionContractError("invalid pre-finalization snapshot")
        task_path = snapshot["taskPath"]
        if snapshot["lifecycle"] != "Backlog" or not isinstance(task_path, str) or "/backlog/" not in task_path:
            raise CompletionContractError("pre-finalization snapshot is not a Backlog task")
        evidence = value["evidenceArtifactIds"]
        if not isinstance(evidence, list) or len(evidence) < 2 or not all(
            isinstance(item, str) and item.strip() for item in evidence
        ) or len(evidence) != len(set(evidence)):
            raise CompletionContractError("completion evidence must contain unique integration and verification IDs")
        if value["canonicalValidation"] != "PASS" or value["authoritativeVerification"] != "PASS":
            raise CompletionContractError("completion transaction is not verified")
        if value["rollbackOutcome"] != "NOT_REQUIRED" or value["pushEligible"] is not True:
            raise CompletionContractError("completion transaction is not push eligible")
        return cls(
            value["taskId"], value["catalog"], value["integratedSha"],
            value["bookkeepingSha"], value["completedPath"], task_path, tuple(evidence),
        )

    def to_dict(self) -> dict[str, Any]:
        return {
            "contractVersion": COMPLETION_TRANSACTION_VERSION,
            "taskId": self.task_id,
            "catalog": self.catalog,
            "integratedSha": self.integrated_sha,
            "bookkeepingSha": self.bookkeeping_sha,
            "completedPath": self.completed_path,
            "preFinalizationSnapshot": {
                "lifecycle": "Backlog",
                "taskPath": self.original_task_path,
            },
            "evidenceArtifactIds": list(self.evidence_artifact_ids),
            "canonicalValidation": "PASS",
            "authoritativeVerification": "PASS",
            "rollbackOutcome": "NOT_REQUIRED",
            "pushEligible": True,
        }
