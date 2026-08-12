import unittest

from commerceos_orchestrator.review_contract import (
    FindingOwner,
    FindingRoute,
    next_hop,
    parse_review_findings,
    ReviewLedger,
    ReviewLedgerError,
)


class ReviewContractTests(unittest.TestCase):
    def _ledger(self):
        return {
            "contractVersion": "ReviewLedger/v1",
            "taskId": "TASK-0100",
            "reviewedCommitSha": "abc",
            "reviewRound": "INITIAL",
            "acceptanceCriteria": [{"acId": "AC01", "verdict": "PASS"}],
            "changedFiles": [{"path": "src/x.py", "classification": "IN_SCOPE"}],
            "findings": [],
            "verdict": "PASS",
        }

    def test_valid_ledger_has_exact_ac_and_file_coverage(self):
        ledger = ReviewLedger.from_dict(
            self._ledger(), expected_task_id="TASK-0100", expected_commit_sha="abc",
            expected_ac_ids=("AC01",), expected_changed_files=("src/x.py",),
            allowed_evidence_refs=("builder.json",),
        )
        self.assertEqual(ledger.verdict, "PASS")

    def test_stale_duplicate_incomplete_and_pass_with_open_finding_fail_closed(self):
        cases = []
        stale = self._ledger(); stale["reviewedCommitSha"] = "old"; cases.append(stale)
        duplicate = self._ledger(); duplicate["acceptanceCriteria"] *= 2; cases.append(duplicate)
        incomplete = self._ledger(); incomplete["changedFiles"] = []; cases.append(incomplete)
        open_finding = self._ledger(); open_finding["findings"] = [{
            "findingId": "F-001", "status": "OPEN", "severity": "HIGH",
            "owner": "BUILDER", "route": "BUILDER_FIX", "title": "broken",
            "evidenceRefs": ["builder.json"], "affectedPaths": ["src/x.py"],
            "acceptanceCondition": "Test demonstrates the fix.",
        }]; cases.append(open_finding)
        for value in cases:
            with self.subTest(value=value), self.assertRaises(ReviewLedgerError):
                ReviewLedger.from_dict(
                    value, expected_task_id="TASK-0100", expected_commit_sha="abc",
                    expected_ac_ids=("AC01",), expected_changed_files=("src/x.py",),
                    allowed_evidence_refs=("builder.json",),
                )

    def test_owner_route_unknown_evidence_and_unsafe_path_fail_closed(self):
        base = self._ledger()
        finding = {
            "findingId": "F-001", "status": "OPEN", "severity": "MEDIUM",
            "owner": "BUILDER", "route": "PLANNING_REQUIRED", "title": "broken",
            "evidenceRefs": ["unknown"], "affectedPaths": ["../x"],
            "acceptanceCondition": "A measurable condition.",
        }
        base["findings"] = [finding]; base["verdict"] = "FIX_REQUIRED"
        with self.assertRaises(ReviewLedgerError):
            ReviewLedger.from_dict(
                base, expected_task_id="TASK-0100", expected_commit_sha="abc",
                expected_ac_ids=("AC01",), expected_changed_files=("src/x.py",),
                allowed_evidence_refs=("builder.json",),
            )

    def test_rereview_preserves_ids_and_new_unrelated_blockers_are_follow_up(self):
        first = self._ledger()
        first["acceptanceCriteria"][0]["verdict"] = "FAIL"
        first["findings"] = [{
            "findingId": "F-001", "status": "OPEN", "severity": "MEDIUM",
            "owner": "BUILDER", "route": "BUILDER_FIX", "title": "broken",
            "evidenceRefs": ["builder.json"], "affectedPaths": ["src/x.py"],
            "acceptanceCondition": "A measurable condition.",
        }]
        first["verdict"] = "FIX_REQUIRED"
        previous = ReviewLedger.from_dict(
            first, expected_task_id="TASK-0100", expected_commit_sha="abc",
            expected_ac_ids=("AC01",), expected_changed_files=("src/x.py",),
            allowed_evidence_refs=("builder.json",),
        )
        repair = self._ledger(); repair["reviewRound"] = "REPAIR"; repair["reviewedCommitSha"] = "def"
        repair["findings"] = [{**first["findings"][0], "status": "RESOLVED"}, {
            **first["findings"][0], "findingId": "F-002", "affectedPaths": ["src/x.py"],
        }]
        with self.assertRaisesRegex(ReviewLedgerError, "FOLLOW_UP"):
            ReviewLedger.from_dict(
                repair, expected_task_id="TASK-0100", expected_commit_sha="def",
                expected_ac_ids=("AC01",), expected_changed_files=("src/x.py",),
                allowed_evidence_refs=("builder.json",), previous=previous,
                repair_changed_files=(),
            )

        repair["findings"] = [{**first["findings"][0], "status": "FOLLOW_UP"}]
        repair["verdict"] = "PASS"
        with self.assertRaisesRegex(ReviewLedgerError, "OPEN tracked"):
            ReviewLedger.from_dict(
                repair, expected_task_id="TASK-0100", expected_commit_sha="def",
                expected_ac_ids=("AC01",), expected_changed_files=("src/x.py",),
                allowed_evidence_refs=("builder.json",), previous=previous,
                repair_changed_files=("src/x.py",),
            )

    def test_unhashable_json_values_raise_review_ledger_error(self):
        value = self._ledger()
        value["acceptanceCriteria"][0]["verdict"] = []
        with self.assertRaises(ReviewLedgerError):
            ReviewLedger.from_dict(
                value, expected_task_id="TASK-0100", expected_commit_sha="abc",
                expected_ac_ids=("AC01",), expected_changed_files=("src/x.py",),
                allowed_evidence_refs=("builder.json",),
            )
        value = self._ledger()
        value["acceptanceCriteria"][0]["verdict"] = "FAIL"
        value["findings"] = [{
            "findingId": "F-001", "status": "OPEN", "severity": "MEDIUM",
            "owner": "BUILDER", "route": "BUILDER_FIX", "title": "broken",
            "evidenceRefs": [["unhashable"]], "affectedPaths": ["src/x.py"],
            "acceptanceCondition": "A measurable condition.",
        }]
        value["verdict"] = "FIX_REQUIRED"
        with self.assertRaises(ReviewLedgerError):
            ReviewLedger.from_dict(
                value, expected_task_id="TASK-0100", expected_commit_sha="abc",
                expected_ac_ids=("AC01",), expected_changed_files=("src/x.py",),
                allowed_evidence_refs=("builder.json",),
            )
    def test_domain_and_technical_findings_route_through_backlog_planner(self):
        findings = parse_review_findings(
            """FINDING F-001 STATUS: OPEN OWNER: DOMAIN_ARCHITECT ROUTE: PLANNING_REQUIRED TITLE: invariant conflict
FINDING F-002 STATUS: OPEN OWNER: TECHNICAL_ARCHITECT ROUTE: PLANNING_REQUIRED TITLE: contract conflict
FINDING F-003 STATUS: FOLLOW_UP OWNER: BUILDER ROUTE: BUILDER_FIX TITLE: optional cleanup"""
        )
        self.assertEqual(findings[0].owner, FindingOwner.DOMAIN_ARCHITECT)
        self.assertEqual(findings[1].route, FindingRoute.PLANNING_REQUIRED)
        self.assertEqual(next_hop(findings[0]), "Backlog Planner")
        self.assertEqual(next_hop(findings[1]), "Backlog Planner")
        self.assertEqual(len(findings), 3)

    def test_builder_finding_routes_to_builder(self):
        finding = parse_review_findings(
            "FINDING F-004 STATUS: OPEN OWNER: BUILDER ROUTE: BUILDER_FIX TITLE: missing regression test"
        )[0]
        self.assertEqual(next_hop(finding), "Builder")


if __name__ == "__main__":
    unittest.main()
