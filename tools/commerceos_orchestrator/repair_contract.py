from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import PurePosixPath
from typing import Any

from .review_contract import FindingOwner, FindingRoute, ReviewFinding, ReviewLedger


REPAIR_PACKET_VERSION = "RepairPacket/v1"
REPAIR_MANIFEST_VERSION = "RepairManifest/v1"


class RepairContractError(ValueError):
    pass


@dataclass(frozen=True)
class RepairPacket:
    task_id: str
    baseline_sha: str
    ledger_artifact: str
    findings: tuple[ReviewFinding, ...]

    @classmethod
    def from_ledger(cls, ledger: ReviewLedger, ledger_artifact: str) -> "RepairPacket":
        findings = tuple(
            finding for finding in ledger.findings
            if finding.status == "OPEN"
            and finding.owner == FindingOwner.BUILDER
            and finding.route == FindingRoute.BUILDER_FIX
        )
        if not findings:
            raise RepairContractError("repair packet has no open Builder findings")
        return cls(ledger.task_id, ledger.reviewed_commit_sha, ledger_artifact, findings)

    def to_dict(self) -> dict[str, Any]:
        return {
            "contractVersion": REPAIR_PACKET_VERSION,
            "taskId": self.task_id,
            "baselineSha": self.baseline_sha,
            "ledgerArtifact": self.ledger_artifact,
            "findings": [
                {
                    "findingId": finding.finding_id,
                    "allowedPaths": list(finding.affected_paths),
                    "evidenceRefs": list(finding.evidence_refs),
                    "acceptanceCondition": finding.acceptance_condition,
                }
                for finding in self.findings
            ],
        }


@dataclass(frozen=True)
class RepairManifest:
    task_id: str
    baseline_sha: str
    repaired_sha: str
    dispositions: tuple[tuple[str, str], ...]
    changed_files: tuple[tuple[str, tuple[str, ...]], ...]

    @classmethod
    def from_dict(
        cls,
        value: Any,
        *,
        packet: RepairPacket,
        repaired_sha: str,
        repair_delta: tuple[str, ...],
    ) -> "RepairManifest":
        fields = {
            "contractVersion", "taskId", "baselineSha", "repairedSha",
            "findingDispositions", "changedFiles",
        }
        if not isinstance(value, dict) or set(value) != fields:
            raise RepairContractError("invalid RepairManifest/v1 fields")
        if value["contractVersion"] != REPAIR_MANIFEST_VERSION:
            raise RepairContractError("unsupported repair manifest version")
        if value["taskId"] != packet.task_id or value["baselineSha"] != packet.baseline_sha or value["repairedSha"] != repaired_sha:
            raise RepairContractError("repair manifest task/commit binding mismatch")
        ids = tuple(finding.finding_id for finding in packet.findings)
        dispositions_raw = value["findingDispositions"]
        if not isinstance(dispositions_raw, list):
            raise RepairContractError("finding dispositions must be a list")
        dispositions: list[tuple[str, str]] = []
        for row in dispositions_raw:
            if not isinstance(row, dict) or set(row) != {"findingId", "disposition"}:
                raise RepairContractError("invalid finding disposition")
            finding_id, disposition = row["findingId"], row["disposition"]
            if not isinstance(finding_id, str) or disposition not in {"ADDRESSED", "BLOCKED"}:
                raise RepairContractError("invalid finding disposition value")
            dispositions.append((finding_id, disposition))
        if tuple(item[0] for item in dispositions) != ids:
            raise RepairContractError("repair finding coverage is not exact or ordered")

        rows_raw = value["changedFiles"]
        if not isinstance(rows_raw, list):
            raise RepairContractError("repair changedFiles must be a list")
        rows: list[tuple[str, tuple[str, ...]]] = []
        packet_findings = {finding.finding_id: finding for finding in packet.findings}
        for row in rows_raw:
            if not isinstance(row, dict) or set(row) != {"path", "findingIds"}:
                raise RepairContractError("invalid repair changed-file row")
            path, finding_ids = row["path"], row["findingIds"]
            if not isinstance(path, str) or not cls._safe_path(path):
                raise RepairContractError("unsafe repair changed path")
            if not isinstance(finding_ids, list) or not finding_ids or not all(isinstance(item, str) for item in finding_ids):
                raise RepairContractError("repair changed path has no finding mapping")
            if len(set(finding_ids)) != len(finding_ids):
                raise RepairContractError("duplicate finding mapping for repair path")
            if not set(finding_ids).issubset(packet_findings):
                raise RepairContractError("repair changed path references unknown finding")
            if not all(
                any(cls._matches(path, pattern) for pattern in packet_findings[finding_id].affected_paths)
                for finding_id in finding_ids
            ):
                raise RepairContractError(f"repair path is outside finding allow-list: {path}")
            rows.append((path, tuple(finding_ids)))
        if tuple(item[0] for item in rows) != repair_delta or len(set(repair_delta)) != len(repair_delta):
            raise RepairContractError("repair changed-file coverage is not exact or ordered")
        if any(disposition == "BLOCKED" for _, disposition in dispositions):
            raise RepairContractError("repair manifest contains BLOCKED finding")
        mapped_ids = {finding_id for _, finding_ids in rows for finding_id in finding_ids}
        if any(
            disposition == "ADDRESSED" and finding_id not in mapped_ids
            for finding_id, disposition in dispositions
        ):
            raise RepairContractError("ADDRESSED finding has no repair-delta path")
        return cls(packet.task_id, packet.baseline_sha, repaired_sha, tuple(dispositions), tuple(rows))

    @staticmethod
    def _safe_path(path: str) -> bool:
        pure = PurePosixPath(path)
        return bool(path) and not pure.is_absolute() and ".." not in pure.parts

    @staticmethod
    def _matches(path: str, pattern: str) -> bool:
        if not RepairManifest._safe_path(pattern):
            return False
        expression = re.escape(pattern).replace(r"\*\*", ".*").replace(r"\*", "[^/]*")
        return re.fullmatch(expression, path) is not None

    def to_dict(self) -> dict[str, Any]:
        return {
            "contractVersion": REPAIR_MANIFEST_VERSION,
            "taskId": self.task_id,
            "baselineSha": self.baseline_sha,
            "repairedSha": self.repaired_sha,
            "findingDispositions": [
                {"findingId": finding_id, "disposition": disposition}
                for finding_id, disposition in self.dispositions
            ],
            "changedFiles": [
                {"path": path, "findingIds": list(finding_ids)}
                for path, finding_ids in self.changed_files
            ],
        }
