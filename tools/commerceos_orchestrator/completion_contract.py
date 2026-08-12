from __future__ import annotations

from dataclasses import dataclass
from pathlib import PurePosixPath
from typing import Any


COMPLETION_TRANSACTION_VERSION = "CompletionTransaction/v1"


class CompletionContractError(ValueError):
    pass


@dataclass(frozen=True)
class CompletionEntryGate:
    task_id: str
    task_commit_sha: str
    builder_manifest_path: str
    verification_report_path: str
    review_ledger_path: str
    acceptance_criterion_ids: tuple[str, ...]
    changed_files: tuple[str, ...]
    required_command_ids: tuple[str, ...]
    allowed_evidence_refs: tuple[str, ...]

    @classmethod
    def from_dict(cls, value: Any) -> "CompletionEntryGate":
        fields = {
            "contractVersion", "taskId", "taskCommitSha", "builderManifestPath",
            "verificationReportPath", "reviewLedgerPath",
            "acceptanceCriterionIds", "changedFiles", "requiredCommandIds",
            "allowedEvidenceRefs",
        }
        if not isinstance(value, dict) or set(value) != fields:
            raise CompletionContractError("invalid CompletionEntryGate/v1 fields")
        if value["contractVersion"] != "CompletionEntryGate/v1":
            raise CompletionContractError("unsupported completion entry-gate version")
        names = (
            "taskId", "taskCommitSha", "builderManifestPath", "verificationReportPath",
            "reviewLedgerPath",
        )
        if any(not isinstance(value[name], str) or not value[name].strip() for name in names):
            raise CompletionContractError("completion entry gate has an empty field")
        for name in names[2:]:
            path = PurePosixPath(value[name])
            if path.is_absolute() or ".." in path.parts:
                raise CompletionContractError("completion entry gate has an unsafe artifact path")
        sequences = (
            "acceptanceCriterionIds", "changedFiles", "requiredCommandIds",
            "allowedEvidenceRefs",
        )
        if any(not isinstance(value[name], list) or not all(
            isinstance(item, str) and item for item in value[name]
        ) or len(value[name]) != len(set(value[name])) for name in sequences):
            raise CompletionContractError("completion entry gate has invalid binding inventory")
        return cls(
            value["taskId"], value["taskCommitSha"], value["builderManifestPath"],
            value["verificationReportPath"], value["reviewLedgerPath"],
            tuple(value["acceptanceCriterionIds"]), tuple(value["changedFiles"]),
            tuple(value["requiredCommandIds"]),
            tuple(value["allowedEvidenceRefs"]),
        )

    @property
    def evidence_artifact_ids(self) -> tuple[str, ...]:
        return (
            self.builder_manifest_path,
            self.verification_report_path,
            self.review_ledger_path,
        )

    def to_dict(self) -> dict[str, Any]:
        return {
            "contractVersion": "CompletionEntryGate/v1",
            "taskId": self.task_id,
            "taskCommitSha": self.task_commit_sha,
            "builderManifestPath": self.builder_manifest_path,
            "verificationReportPath": self.verification_report_path,
            "reviewLedgerPath": self.review_ledger_path,
            "acceptanceCriterionIds": list(self.acceptance_criterion_ids),
            "changedFiles": list(self.changed_files),
            "requiredCommandIds": list(self.required_command_ids),
            "allowedEvidenceRefs": list(self.allowed_evidence_refs),
        }


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
        pre_finalization_lifecycle: str,
        canonical_validation: str,
        authoritative_verification: str,
        rollback_outcome: str,
        push_eligible: bool,
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
                    "lifecycle": pre_finalization_lifecycle,
                    "taskPath": original_task_path,
                },
                "evidenceArtifactIds": list(evidence_artifact_ids),
                "canonicalValidation": canonical_validation,
                "authoritativeVerification": authoritative_verification,
                "rollbackOutcome": rollback_outcome,
                "pushEligible": push_eligible,
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
