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


if __name__ == "__main__":
    unittest.main()
