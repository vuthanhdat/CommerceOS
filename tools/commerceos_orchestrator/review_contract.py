from __future__ import annotations

import re
from dataclasses import dataclass
from enum import StrEnum
from pathlib import PurePosixPath
from typing import Any


REVIEW_LEDGER_VERSION = "ReviewLedger/v1"


class ReviewLedgerError(ValueError):
    pass


class FindingOwner(StrEnum):
    BUILDER = "BUILDER"
    DOMAIN_ARCHITECT = "DOMAIN_ARCHITECT"
    TECHNICAL_ARCHITECT = "TECHNICAL_ARCHITECT"
    BACKLOG_PLANNER = "BACKLOG_PLANNER"
    ORCHESTRATOR = "ORCHESTRATOR"
    HUMAN = "HUMAN"


class FindingRoute(StrEnum):
    BUILDER_FIX = "BUILDER_FIX"
    PLANNING_REQUIRED = "PLANNING_REQUIRED"
    ORCHESTRATOR_ACTION_REQUIRED = "ORCHESTRATOR_ACTION_REQUIRED"
    HUMAN_REQUIRED = "HUMAN_REQUIRED"


OWNER_ROUTES = {
    FindingOwner.BUILDER: FindingRoute.BUILDER_FIX,
    FindingOwner.DOMAIN_ARCHITECT: FindingRoute.PLANNING_REQUIRED,
    FindingOwner.TECHNICAL_ARCHITECT: FindingRoute.PLANNING_REQUIRED,
    FindingOwner.BACKLOG_PLANNER: FindingRoute.PLANNING_REQUIRED,
    FindingOwner.ORCHESTRATOR: FindingRoute.ORCHESTRATOR_ACTION_REQUIRED,
    FindingOwner.HUMAN: FindingRoute.HUMAN_REQUIRED,
}


@dataclass(frozen=True)
class ReviewFinding:
    finding_id: str
    status: str
    owner: FindingOwner
    route: FindingRoute
    title: str
    severity: str = "MEDIUM"
    evidence_refs: tuple[str, ...] = ()
    affected_paths: tuple[str, ...] = ()
    acceptance_condition: str = ""


@dataclass(frozen=True)
class ReviewLedger:
    task_id: str
    reviewed_commit_sha: str
    review_round: str
    acceptance_criteria: tuple[tuple[str, str], ...]
    changed_files: tuple[tuple[str, str], ...]
    findings: tuple[ReviewFinding, ...]
    verdict: str
    contract_version: str = REVIEW_LEDGER_VERSION

    @classmethod
    def from_dict(
        cls,
        value: dict[str, Any],
        *,
        expected_task_id: str,
        expected_commit_sha: str,
        expected_ac_ids: tuple[str, ...],
        expected_changed_files: tuple[str, ...],
        allowed_evidence_refs: tuple[str, ...],
        previous: "ReviewLedger | None" = None,
        repair_changed_files: tuple[str, ...] = (),
    ) -> "ReviewLedger":
        required = {
            "contractVersion", "taskId", "reviewedCommitSha", "reviewRound",
            "acceptanceCriteria", "changedFiles", "findings", "verdict",
        }
        if set(value) != required:
            raise ReviewLedgerError("review ledger fields do not match ReviewLedger/v1")
        if value["contractVersion"] != REVIEW_LEDGER_VERSION:
            raise ReviewLedgerError("unsupported review ledger contract version")
        if value["taskId"] != expected_task_id or value["reviewedCommitSha"] != expected_commit_sha:
            raise ReviewLedgerError("review ledger task/commit binding mismatch")
        expected_round = "REPAIR" if previous else "INITIAL"
        if value["reviewRound"] != expected_round:
            raise ReviewLedgerError(f"reviewRound must be {expected_round}")

        ac_rows = cls._coverage_rows(value["acceptanceCriteria"], "acId", "verdict", {"PASS", "FAIL"})
        if tuple(row[0] for row in ac_rows) != expected_ac_ids:
            raise ReviewLedgerError("review ledger AC coverage is not exact or ordered")
        file_rows = cls._coverage_rows(
            value["changedFiles"], "path", "classification",
            {"IN_SCOPE", "OUT_OF_SCOPE", "GENERATED", "EVIDENCE"},
        )
        if tuple(row[0] for row in file_rows) != expected_changed_files:
            raise ReviewLedgerError("review ledger changed-file coverage is not exact or ordered")

        raw_findings = value["findings"]
        if not isinstance(raw_findings, list):
            raise ReviewLedgerError("findings must be a list")
        findings: list[ReviewFinding] = []
        seen: set[str] = set()
        allowed_paths = set(expected_changed_files)
        evidence = set(allowed_evidence_refs)
        for raw in raw_findings:
            finding = cls._finding(raw, allowed_paths, evidence)
            if finding.finding_id in seen:
                raise ReviewLedgerError(f"duplicate finding ID: {finding.finding_id}")
            seen.add(finding.finding_id)
            findings.append(finding)

        if previous:
            old = {finding.finding_id: finding for finding in previous.findings}
            if not set(old).issubset(seen):
                raise ReviewLedgerError("re-review omitted a previous finding ID")
            repair_paths = set(repair_changed_files)
            for finding in findings:
                if (
                    finding.finding_id in old
                    and old[finding.finding_id].status == "OPEN"
                    and finding.status not in {"OPEN", "RESOLVED"}
                ):
                    raise ReviewLedgerError(
                        "an OPEN tracked finding may only remain OPEN or become RESOLVED"
                    )
                if finding.finding_id not in old and finding.status == "OPEN":
                    if not set(finding.affected_paths).issubset(repair_paths):
                        raise ReviewLedgerError(
                            "new unrelated re-review observations must be FOLLOW_UP"
                        )

        verdict = value["verdict"]
        if not isinstance(verdict, str) or verdict not in {"PASS", "FIX_REQUIRED"}:
            raise ReviewLedgerError("invalid review verdict")
        blocking = any(finding.status == "OPEN" for finding in findings)
        pass_allowed = (
            all(verdict == "PASS" for _, verdict in ac_rows)
            and all(scope != "OUT_OF_SCOPE" for _, scope in file_rows)
            and not blocking
        )
        if (verdict == "PASS") != pass_allowed:
            raise ReviewLedgerError("review verdict contradicts ledger coverage/findings")
        return cls(
            expected_task_id, expected_commit_sha, expected_round, ac_rows, file_rows,
            tuple(findings), verdict,
        )

    @staticmethod
    def _coverage_rows(raw: Any, key: str, value_key: str, allowed: set[str]) -> tuple[tuple[str, str], ...]:
        if not isinstance(raw, list):
            raise ReviewLedgerError(f"{key} coverage must be a list")
        rows: list[tuple[str, str]] = []
        for item in raw:
            if not isinstance(item, dict) or set(item) != {key, value_key}:
                raise ReviewLedgerError(f"invalid {key} coverage row")
            identifier, verdict = item[key], item[value_key]
            if (
                not isinstance(identifier, str)
                or not identifier
                or not isinstance(verdict, str)
                or verdict not in allowed
            ):
                raise ReviewLedgerError(f"invalid {key} coverage value")
            rows.append((identifier, verdict))
        if len({row[0] for row in rows}) != len(rows):
            raise ReviewLedgerError(f"duplicate {key} coverage")
        return tuple(rows)

    @staticmethod
    def _finding(raw: Any, allowed_paths: set[str], evidence: set[str]) -> ReviewFinding:
        fields = {
            "findingId", "status", "severity", "owner", "route", "title",
            "evidenceRefs", "affectedPaths", "acceptanceCondition",
        }
        if not isinstance(raw, dict) or set(raw) != fields:
            raise ReviewLedgerError("invalid review finding fields")
        finding_id = raw["findingId"]
        if not isinstance(finding_id, str) or not re.fullmatch(r"F-\d{3,}", finding_id):
            raise ReviewLedgerError("invalid finding ID")
        try:
            owner = FindingOwner(raw["owner"])
            route = FindingRoute(raw["route"])
        except (ValueError, TypeError) as exc:
            raise ReviewLedgerError("invalid finding owner/route") from exc
        if OWNER_ROUTES[owner] != route:
            raise ReviewLedgerError("finding owner/route mismatch")
        status, severity = raw["status"], raw["severity"]
        if (
            not isinstance(status, str)
            or status not in {"OPEN", "RESOLVED", "FOLLOW_UP"}
            or not isinstance(severity, str)
            or severity not in {"HIGH", "MEDIUM", "LOW"}
        ):
            raise ReviewLedgerError("invalid finding status/severity")
        refs, paths = raw["evidenceRefs"], raw["affectedPaths"]
        if (
            not isinstance(refs, list)
            or not refs
            or not all(isinstance(value, str) for value in refs)
            or not set(refs).issubset(evidence)
        ):
            raise ReviewLedgerError("finding evidence references are unknown or empty")
        if (
            not isinstance(paths, list)
            or not paths
            or not all(isinstance(value, str) for value in paths)
            or not set(paths).issubset(allowed_paths)
        ):
            raise ReviewLedgerError("finding affected paths are unknown or empty")
        for path in paths:
            pure = PurePosixPath(path)
            if pure.is_absolute() or ".." in pure.parts:
                raise ReviewLedgerError("unsafe finding affected path")
        title, condition = raw["title"], raw["acceptanceCondition"]
        if not isinstance(title, str) or not title.strip() or not isinstance(condition, str) or not condition.strip():
            raise ReviewLedgerError("finding title/acceptance condition is required")
        return ReviewFinding(
            finding_id, status, owner, route, title.strip(), severity,
            tuple(refs), tuple(paths), condition.strip(),
        )

    def to_dict(self) -> dict[str, Any]:
        return {
            "contractVersion": self.contract_version,
            "taskId": self.task_id,
            "reviewedCommitSha": self.reviewed_commit_sha,
            "reviewRound": self.review_round,
            "acceptanceCriteria": [
                {"acId": ac_id, "verdict": verdict} for ac_id, verdict in self.acceptance_criteria
            ],
            "changedFiles": [
                {"path": path, "classification": classification}
                for path, classification in self.changed_files
            ],
            "findings": [
                {
                    "findingId": finding.finding_id, "status": finding.status,
                    "severity": finding.severity, "owner": finding.owner.value,
                    "route": finding.route.value, "title": finding.title,
                    "evidenceRefs": list(finding.evidence_refs),
                    "affectedPaths": list(finding.affected_paths),
                    "acceptanceCondition": finding.acceptance_condition,
                }
                for finding in self.findings
            ],
            "verdict": self.verdict,
        }


_FINDING_RE = re.compile(
    r"^FINDING (?P<id>F-\d+) STATUS: (?P<status>OPEN|RESOLVED|FOLLOW_UP) "
    r"OWNER: (?P<owner>[A-Z_]+) ROUTE: (?P<route>[A-Z_]+) TITLE: (?P<title>.+)$",
    re.MULTILINE,
)


def parse_review_findings(text: str) -> tuple[ReviewFinding, ...]:
    """Legacy text parser retained for existing records; new decisions use ReviewLedger/v1."""
    findings: list[ReviewFinding] = []
    for match in _FINDING_RE.finditer(text):
        try:
            findings.append(
                ReviewFinding(
                    match.group("id"), match.group("status"), FindingOwner(match.group("owner")),
                    FindingRoute(match.group("route")), match.group("title").strip(),
                )
            )
        except ValueError:
            continue
    return tuple(findings)


def next_hop(finding: ReviewFinding) -> str:
    if finding.owner == FindingOwner.BUILDER:
        return "Builder"
    if finding.owner in {FindingOwner.DOMAIN_ARCHITECT, FindingOwner.TECHNICAL_ARCHITECT, FindingOwner.BACKLOG_PLANNER}:
        return "Backlog Planner"
    if finding.owner == FindingOwner.ORCHESTRATOR:
        return "Orchestrator"
    return "Human"
