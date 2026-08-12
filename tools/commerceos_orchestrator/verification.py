from __future__ import annotations

import subprocess
import sys
import re
from pathlib import Path

from .evidence import (
    TestTotals,
    VERIFICATION_REPORT_VERSION,
    VerificationCommandResult,
    VerificationReport,
)
from .models import CanonicalTask, VerificationResult


class VerificationRunner:
    """Runs the repository-owned deterministic task verification entrypoint only."""

    def __init__(self, logs_root: Path):
        self.logs_root = logs_root.resolve()
        self.logs_root.mkdir(parents=True, exist_ok=True)
        # Use the exact Python interpreter that launched the Orchestrator. This keeps
        # the verification entrypoint fixed/trusted while remaining portable across
        # Windows (`python`) and Unix-like environments (`python3`). Per-task verification
        # intentionally excludes Orchestrator self-tests; those remain in full harness CI.
        self.command = (sys.executable, "scripts/task_verification.py")
        self.required_command_ids = ("task-verification",)

    def run(
        self, task: CanonicalTask, worktree: Path, *, phase: str, commit_sha: str | None = None
    ) -> VerificationResult:
        log_path = self.logs_root / f"{task.id}-verify-{phase}.log"
        result = subprocess.run(
            list(self.command),
            cwd=worktree,
            text=True,
            capture_output=True,
            check=False,
        )
        log_path.write_text(
            f"COMMAND: {' '.join(self.command)}\n\nSTDOUT\n{result.stdout}\n\nSTDERR\n{result.stderr}",
            encoding="utf-8",
        )
        success = result.returncode == 0
        report = VerificationReport(
            contract_version=VERIFICATION_REPORT_VERSION,
            task_id=task.id,
            task_commit_sha=commit_sha or self._commit_sha(worktree),
            command_results=(
                VerificationCommandResult(
                    "task-verification", self.command, result.returncode, str(log_path)
                ),
            ),
            test_totals=self._test_totals(result.stdout, success),
            success=success,
        )
        return VerificationResult(
            success=success,
            exit_code=result.returncode,
            command=self.command,
            stdout=result.stdout,
            stderr=result.stderr,
            log_path=str(log_path),
            report=report,
        )

    @staticmethod
    def _commit_sha(worktree: Path) -> str:
        result = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=worktree,
            text=True,
            capture_output=True,
            check=False,
        )
        return result.stdout.strip() if result.returncode == 0 else "UNKNOWN"

    @staticmethod
    def _test_totals(stdout: str, success: bool) -> TestTotals:
        discovered = passed = failed = skipped = 0
        for match in re.finditer(
            r"Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)",
            stdout,
        ):
            failed += int(match.group(1))
            passed += int(match.group(2))
            skipped += int(match.group(3))
            discovered += int(match.group(4))
        for match in re.finditer(r"Tests\s+(\d+)\s+passed", stdout):
            count = int(match.group(1))
            discovered += count
            passed += count
        if discovered == 0:
            discovered = 1
            passed = 1 if success else 0
            failed = 0 if success else 1
        return TestTotals(discovered, passed, failed, skipped)


class FakeVerificationRunner:
    def __init__(self, results: list[bool] | None = None):
        self.results = list(results or [])
        self.calls: list[tuple[str, str]] = []
        self.required_command_ids = ("task-verification",)

    def run(
        self, task: CanonicalTask, worktree: Path, *, phase: str, commit_sha: str | None = None
    ) -> VerificationResult:
        self.calls.append((task.id, phase))
        success = self.results.pop(0) if self.results else True
        report = VerificationReport(
            VERIFICATION_REPORT_VERSION,
            task.id,
            commit_sha or "abc",
            (VerificationCommandResult("task-verification", ("fake-verify",), 0 if success else 1, "fake-log"),),
            TestTotals(1, 1 if success else 0, 0 if success else 1, 0),
            success,
        )
        return VerificationResult(
            success=success,
            exit_code=0 if success else 1,
            command=("fake-verify",),
            stdout="PASS" if success else "FAIL",
            stderr="",
            log_path="",
            report=report,
        )
