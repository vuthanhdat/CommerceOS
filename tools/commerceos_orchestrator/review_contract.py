from __future__ import annotations

import re
from dataclasses import dataclass
from enum import StrEnum


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


@dataclass(frozen=True)
class ReviewFinding:
    finding_id: str
    status: str
    owner: FindingOwner
    route: FindingRoute
    title: str


_FINDING_RE = re.compile(
    r"^FINDING (?P<id>F-\d+) STATUS: (?P<status>OPEN|RESOLVED|FOLLOW_UP) "
    r"OWNER: (?P<owner>[A-Z_]+) ROUTE: (?P<route>[A-Z_]+) TITLE: (?P<title>.+)$",
    re.MULTILINE,
)


def parse_review_findings(text: str) -> tuple[ReviewFinding, ...]:
    findings: list[ReviewFinding] = []
    for match in _FINDING_RE.finditer(text):
        try:
            findings.append(
                ReviewFinding(
                    finding_id=match.group("id"),
                    status=match.group("status"),
                    owner=FindingOwner(match.group("owner")),
                    route=FindingRoute(match.group("route")),
                    title=match.group("title").strip(),
                )
            )
        except ValueError:
            # An invalid protocol line is left for the normal review evidence path; it
            # must never silently become an instruction to dispatch a different agent.
            continue
    return tuple(findings)


def next_hop(finding: ReviewFinding) -> str:
    if finding.owner == FindingOwner.BUILDER:
        return "Builder"
    if finding.owner in {
        FindingOwner.DOMAIN_ARCHITECT,
        FindingOwner.TECHNICAL_ARCHITECT,
        FindingOwner.BACKLOG_PLANNER,
    }:
        return "Backlog Planner"
    if finding.owner == FindingOwner.ORCHESTRATOR:
        return "Orchestrator"
    return "Human"

