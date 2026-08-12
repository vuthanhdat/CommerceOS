from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[2] / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from commerceos_orchestrator.models import TERMINAL_TASK_STATES, TaskExecutionState
from commerceos_orchestrator.observability import WORKFLOW_STATUS, evidence_counters


class WorkflowObservabilityTests(unittest.TestCase):
    def test_every_non_terminal_state_has_exactly_one_owner_and_condition(self):
        expected = set(TaskExecutionState) - TERMINAL_TASK_STATES
        self.assertEqual(set(WORKFLOW_STATUS), expected)
        for owner, condition in WORKFLOW_STATUS.values():
            self.assertTrue(owner.strip())
            self.assertTrue(condition.strip())
        observed = {
            TaskExecutionState.INITIAL_BUILD,
            TaskExecutionState.REPAIR_BUILD,
            TaskExecutionState.PRE_REVIEW_VERIFICATION,
            TaskExecutionState.REPAIR_VERIFICATION,
            TaskExecutionState.FIRST_REVIEW,
            TaskExecutionState.RE_REVIEW,
            TaskExecutionState.INTEGRATING,
            TaskExecutionState.FINALIZING,
        }
        self.assertEqual(len({state.value for state in observed}), len(observed))

    def test_evidence_counters_equal_persisted_contract_artifacts(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            evidence = root / ".commerceos/orchestrator/commerceos/evidence/TASK-0100"
            evidence.mkdir(parents=True)
            artifacts = {
                "builder-manifest-0.json": {
                    "contractVersion": "BuilderResultManifest/v1", "taskId": "TASK-0100",
                    "taskCommitSha": "abc",
                    "acceptanceCriteria": [
                        {"acId": "AC01", "verdict": "SATISFIED", "evidenceIds": ["one"]},
                        {"acId": "AC02", "verdict": "BLOCKED", "evidenceIds": ["two"]},
                    ],
                    "changedFiles": ["a.py", "b.py"],
                    "requiredCommandIds": ["task-verification"],
                    "additionalCommands": [], "limitations": [], "followUps": [],
                },
                "verification-report-0.json": {
                    "contractVersion": "VerificationReport/v1", "taskId": "TASK-0100",
                    "taskCommitSha": "abc",
                    "commandResults": [{
                        "commandId": "task-verification", "argv": ["verify"],
                        "exitCode": 0, "logArtifact": "verify.log",
                    }],
                    "testTotals": {
                        "discovered": 7, "passed": 7, "failed": 0,
                        "skipped_required": 0,
                    },
                    "success": True,
                },
                "review-ledger-0.json": {
                    "contractVersion": "ReviewLedger/v1", "taskId": "TASK-0100",
                    "reviewedCommitSha": "abc", "reviewRound": "INITIAL",
                    "acceptanceCriteria": [
                        {"acId": "AC01", "verdict": "PASS"},
                        {"acId": "AC02", "verdict": "FAIL"},
                    ],
                    "changedFiles": [
                        {"path": "a.py", "classification": "IN_SCOPE"},
                        {"path": "b.py", "classification": "IN_SCOPE"},
                    ],
                    "findings": [
                        {"findingId": "F-001", "status": "OPEN", "severity": "HIGH", "owner": "BUILDER", "route": "BUILDER_FIX", "title": "one", "evidenceRefs": ["one"], "affectedPaths": ["a.py"], "acceptanceCondition": "fix one"},
                        {"findingId": "F-002", "status": "OPEN", "severity": "HIGH", "owner": "TECHNICAL_ARCHITECT", "route": "PLANNING_REQUIRED", "title": "two", "evidenceRefs": ["two"], "affectedPaths": ["b.py"], "acceptanceCondition": "fix two"},
                    ],
                    "verdict": "FIX_REQUIRED",
                },
            }
            for name, payload in artifacts.items():
                (evidence / name).write_text(json.dumps(payload), encoding="utf-8")
            prefix = ".commerceos/orchestrator/commerceos/evidence/TASK-0100"
            (evidence / "completion-entry-gate.json").write_text(
                json.dumps({
                    "contractVersion": "CompletionEntryGate/v1", "taskId": "TASK-0100",
                    "taskCommitSha": "abc",
                    "builderManifestPath": f"{prefix}/builder-manifest-0.json",
                    "verificationReportPath": f"{prefix}/verification-report-0.json",
                    "reviewLedgerPath": f"{prefix}/review-ledger-0.json",
                    "acceptanceCriterionIds": ["AC01", "AC02"],
                    "changedFiles": ["a.py", "b.py"],
                    "requiredCommandIds": ["task-verification"],
                    "allowedEvidenceRefs": ["one", "two", "verify.log"],
                }),
                encoding="utf-8",
            )
            counters = evidence_counters(root, "commerceos", "TASK-0100")
            self.assertEqual(counters["status"], "VALID")
            self.assertEqual(counters["acceptance_criteria"], {"satisfied": 1, "total": 2})
            self.assertEqual(counters["changed_files"], {"covered": 2, "total": 2})
            self.assertEqual(counters["test_totals"]["passed"], 7)
            self.assertEqual(
                counters["open_findings_by_owner"],
                {"BUILDER": 1, "TECHNICAL_ARCHITECT": 1},
            )

    def test_malformed_evidence_fails_closed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            evidence = root / ".commerceos/orchestrator/commerceos/evidence/TASK-0100"
            evidence.mkdir(parents=True)
            (evidence / "completion-entry-gate.json").write_text("{bad", encoding="utf-8")
            self.assertEqual(
                evidence_counters(root, "commerceos", "TASK-0100")["status"],
                "INVALID",
            )

    def test_incomplete_evidence_is_not_reported_valid(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            evidence = root / ".commerceos/orchestrator/commerceos/evidence/TASK-0100"
            evidence.mkdir(parents=True)
            (evidence / "builder-manifest-0.json").write_text("{}", encoding="utf-8")
            self.assertEqual(
                evidence_counters(root, "commerceos", "TASK-0100")["status"],
                "INCOMPLETE",
            )


if __name__ == "__main__":
    unittest.main()
