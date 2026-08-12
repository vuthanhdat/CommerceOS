from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

from helpers import TOOLS
from commerceos_orchestrator.verification import VerificationRunner


class VerificationBoundaryTests(unittest.TestCase):
    def test_task_verification_does_not_use_full_harness_entrypoint(self):
        with tempfile.TemporaryDirectory() as td:
            runner = VerificationRunner(Path(td) / "logs")
            self.assertEqual(
                runner.command,
                (sys.executable, "scripts/task_verification.py"),
            )
            self.assertNotIn("harness_check.py", runner.command)
            self.assertEqual(runner.required_command_ids, ("task-verification",))

    def test_test_totals_are_derived_without_accepting_required_skips(self):
        totals = VerificationRunner._test_totals(
            "Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9\nTests 2 passed"
        )
        self.assertEqual(totals.discovered, 11)
        self.assertEqual(totals.passed, 11)
        self.assertEqual(totals.failed, 0)
        self.assertEqual(totals.skipped_required, 0)

    def test_unstructured_success_does_not_fabricate_test_discovery(self):
        totals = VerificationRunner._test_totals("command succeeded")
        self.assertEqual(totals.discovered, 0)
        self.assertEqual(totals.passed, 0)

    def test_additional_commands_are_allow_listed_and_shell_free(self):
        with tempfile.TemporaryDirectory() as td:
            runner = VerificationRunner(Path(td) / "logs")
            normalized = runner._normalize_additional_argv(
                ("python", "-m", "unittest", "tests.orchestrator.test_evidence")
            )
            self.assertEqual(normalized[0], sys.executable)
            with self.assertRaisesRegex(ValueError, "shell syntax"):
                runner._normalize_additional_argv(("python", "-m", "unittest", "&&"))
            with self.assertRaisesRegex(ValueError, "allow-listed"):
                runner._normalize_additional_argv(("python", "-c", "print('unsafe')"))


if __name__ == "__main__":
    unittest.main()
