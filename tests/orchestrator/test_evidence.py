from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from commerceos_orchestrator.evidence import (
    BUILDER_MANIFEST_VERSION,
    VERIFICATION_REPORT_VERSION,
    BuilderResultManifest,
    EvidenceValidationError,
    TestTotals,
    VerificationCommandResult,
    VerificationReport,
    acceptance_criterion_ids,
)


def manifest_payload() -> dict[str, object]:
    return {
        "contractVersion": BUILDER_MANIFEST_VERSION,
        "taskId": "TASK-0170",
        "taskCommitSha": "abc123",
        "acceptanceCriteria": [
            {"acId": "AC01", "verdict": "SATISFIED", "evidenceIds": ["e-1"]},
            {"acId": "AC02", "verdict": "SATISFIED", "evidenceIds": ["e-2"]},
        ],
        "changedFiles": ["tools/a.py", "tests/test_a.py"],
        "requiredCommandIds": ["task-verification"],
        "additionalCommands": [],
        "limitations": [],
        "followUps": [],
    }


def parse(payload: dict[str, object]) -> BuilderResultManifest:
    return BuilderResultManifest.from_dict(
        payload,
        expected_task_id="TASK-0170",
        expected_commit_sha="abc123",
        expected_ac_ids=("AC01", "AC02"),
        expected_changed_files=("tools/a.py", "tests/test_a.py"),
        expected_required_command_ids=("task-verification",),
    )


class BuilderEvidenceTests(unittest.TestCase):
    def test_valid_manifest_has_exact_ac_file_and_command_coverage(self):
        manifest = parse(manifest_payload())
        self.assertTrue(manifest.all_satisfied)
        self.assertEqual(len(manifest.acceptance_criteria), 2)
        self.assertEqual(len(manifest.changed_files), 2)

    def test_missing_duplicate_unknown_and_stale_manifest_data_fail_closed(self):
        mutations = []
        missing = manifest_payload()
        missing["acceptanceCriteria"] = list(missing["acceptanceCriteria"])[:1]
        mutations.append(missing)
        duplicate = manifest_payload()
        duplicate["acceptanceCriteria"] = [
            duplicate["acceptanceCriteria"][0],
            duplicate["acceptanceCriteria"][0],
        ]
        mutations.append(duplicate)
        unknown = manifest_payload()
        unknown["acceptanceCriteria"] = [
            unknown["acceptanceCriteria"][0],
            {"acId": "AC99", "verdict": "SATISFIED", "evidenceIds": ["e-99"]},
        ]
        mutations.append(unknown)
        stale = manifest_payload()
        stale["taskCommitSha"] = "stale"
        mutations.append(stale)
        files = manifest_payload()
        files["changedFiles"] = ["tools/a.py"]
        mutations.append(files)
        traversal = manifest_payload()
        traversal["changedFiles"] = ["../escape", "tests/test_a.py"]
        mutations.append(traversal)

        for payload in mutations:
            with self.subTest(payload=payload):
                with self.assertRaises(EvidenceValidationError):
                    parse(payload)

    def test_blocked_ac_and_required_command_mismatch_are_not_accepted(self):
        blocked = manifest_payload()
        blocked["acceptanceCriteria"][0]["verdict"] = "BLOCKED"
        self.assertFalse(parse(blocked).all_satisfied)
        commands = manifest_payload()
        commands["requiredCommandIds"] = []
        with self.assertRaises(EvidenceValidationError):
            parse(commands)

    def test_additional_commands_are_versioned_and_have_unique_ids(self):
        payload = manifest_payload()
        payload["additionalCommands"] = [
            {
                "commandId": "additional-evidence-tests",
                "argv": ["python", "-m", "unittest", "tests.orchestrator.test_evidence"],
            }
        ]
        manifest = parse(payload)
        self.assertEqual(manifest.additional_commands[0].command_id, "additional-evidence-tests")
        duplicate = manifest_payload()
        duplicate["additionalCommands"] = [payload["additionalCommands"][0]] * 2
        with self.assertRaises(EvidenceValidationError):
            parse(duplicate)

    def test_acceptance_ids_are_read_from_machine_checkable_headings(self):
        with tempfile.TemporaryDirectory() as td:
            spec = Path(td) / "task.md"
            spec.write_text("## Acceptance criteria\n\n### AC01 — One\n### AC02 — Two\n", encoding="utf-8")
            self.assertEqual(acceptance_criterion_ids(spec), ("AC01", "AC02"))


class VerificationEvidenceTests(unittest.TestCase):
    def report(self, totals: TestTotals, *, exit_code: int = 0, success: bool = True):
        return VerificationReport(
            VERIFICATION_REPORT_VERSION,
            "TASK-0170",
            "abc123",
            (
                VerificationCommandResult(
                    "task-verification", ("python", "scripts/task_verification.py"), exit_code, "log"
                ),
            ),
            totals,
            success,
        )

    def test_full_pass_is_accepted(self):
        self.report(TestTotals(7, 7, 0, 0)).validate(
            expected_commands={
                "task-verification": ("python", "scripts/task_verification.py")
            },
            expected_commit_sha="abc123",
        )

    def test_failure_skip_stale_and_missing_command_are_rejected(self):
        reports = [
            self.report(TestTotals(7, 6, 1, 0), exit_code=1, success=False),
            self.report(TestTotals(7, 6, 0, 1)),
            VerificationReport(
                VERIFICATION_REPORT_VERSION,
                "TASK-0170",
                "stale",
                self.report(TestTotals(1, 1, 0, 0)).command_results,
                TestTotals(1, 1, 0, 0),
                True,
            ),
            VerificationReport(
                VERIFICATION_REPORT_VERSION,
                "TASK-0170",
                "abc123",
                (
                    VerificationCommandResult(
                        "task-verification", ("wrong",), 0, "log"
                    ),
                ),
                TestTotals(1, 1, 0, 0),
                True,
            ),
            VerificationReport(
                VERIFICATION_REPORT_VERSION,
                "TASK-0170",
                "abc123",
                (
                    VerificationCommandResult(
                        "task-verification",
                        ("python", "scripts/task_verification.py"),
                        0,
                        "",
                    ),
                ),
                TestTotals(1, 1, 0, 0),
                True,
            ),
            VerificationReport(
                VERIFICATION_REPORT_VERSION,
                "TASK-0170",
                "abc123",
                (),
                TestTotals(1, 1, 0, 0),
                True,
            ),
        ]
        for report in reports:
            with self.subTest(report=report):
                with self.assertRaises(EvidenceValidationError):
                    report.validate(
                        expected_commands={
                            "task-verification": ("python", "scripts/task_verification.py")
                        },
                        expected_commit_sha="abc123",
                    )


if __name__ == "__main__":
    unittest.main()
