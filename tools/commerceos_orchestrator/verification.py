from __future__ import annotations

import subprocess
from pathlib import Path

from .models import CanonicalTask, VerificationResult


class VerificationRunner:
    """Runs the repository-owned deterministic verification entrypoint only."""

    COMMAND = ("python3", "scripts/harness_check.py")

    def __init__(self, logs_root: Path):
        self.logs_root = logs_root.resolve()
        self.logs_root.mkdir(parents=True, exist_ok=True)
        self.command = self.COMMAND

    def run(self, task: CanonicalTask, worktree: Path, *, phase: str) -> VerificationResult:
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
        return VerificationResult(
            success=result.returncode == 0,
            exit_code=result.returncode,
            command=self.command,
            stdout=result.stdout,
            stderr=result.stderr,
            log_path=str(log_path),
        )


class FakeVerificationRunner:
    def __init__(self, results: list[bool] | None = None):
        self.results = list(results or [])
        self.calls: list[tuple[str, str]] = []

    def run(self, task: CanonicalTask, worktree: Path, *, phase: str) -> VerificationResult:
        self.calls.append((task.id, phase))
        success = self.results.pop(0) if self.results else True
        return VerificationResult(
            success=success,
            exit_code=0 if success else 1,
            command=("fake-verify",),
            stdout="PASS" if success else "FAIL",
            stderr="",
            log_path="",
        )
