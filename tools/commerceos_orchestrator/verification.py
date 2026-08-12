from __future__ import annotations

import re
import shutil
import subprocess
import sys
from pathlib import Path

from .evidence import (
    AdditionalVerificationCommand,
    EvidenceValidationError,
    TestTotals,
    VERIFICATION_REPORT_VERSION,
    VerificationCommandResult,
    VerificationReport,
)
from .models import CanonicalTask, VerificationResult


class VerificationRunner:
    """Runs trusted required and bounded Builder-declared verification commands."""

    def __init__(self, logs_root: Path):
        self.logs_root = logs_root.resolve()
        self.logs_root.mkdir(parents=True, exist_ok=True)
        self.command = (sys.executable, "scripts/task_verification.py")
        self.required_command_ids = ("task-verification",)

    def run(
        self,
        task: CanonicalTask,
        worktree: Path,
        *,
        phase: str,
        commit_sha: str | None = None,
        additional_commands: tuple[AdditionalVerificationCommand, ...] = (),
    ) -> VerificationResult:
        commands = self.expected_commands(additional_commands)
        command_results: list[VerificationCommandResult] = []
        stdout_parts: list[str] = []
        stderr_parts: list[str] = []
        for command_id, argv in commands.items():
            log_path = self.logs_root / f"{task.id}-verify-{phase}-{command_id}.log"
            result = subprocess.run(
                list(argv),
                cwd=worktree,
                text=True,
                capture_output=True,
                check=False,
            )
            log_path.write_text(
                f"COMMAND: {' '.join(argv)}\n\nSTDOUT\n{result.stdout}\n\nSTDERR\n{result.stderr}",
                encoding="utf-8",
            )
            stdout_parts.append(result.stdout)
            stderr_parts.append(result.stderr)
            command_results.append(
                VerificationCommandResult(command_id, argv, result.returncode, str(log_path))
            )

        success = all(result.exit_code == 0 for result in command_results)
        stdout = "\n".join(stdout_parts)
        stderr = "\n".join(stderr_parts)
        summary_path = self.logs_root / f"{task.id}-verify-{phase}.log"
        summary_path.write_text(
            "\n".join(
                f"{result.command_id}: exit={result.exit_code}; log={result.log_artifact}"
                for result in command_results
            ),
            encoding="utf-8",
        )
        report = VerificationReport(
            contract_version=VERIFICATION_REPORT_VERSION,
            task_id=task.id,
            task_commit_sha=commit_sha or self._commit_sha(worktree),
            command_results=tuple(command_results),
            test_totals=self._test_totals(stdout),
            success=success,
        )
        return VerificationResult(
            success=success,
            exit_code=next(
                (result.exit_code for result in command_results if result.exit_code != 0), 0
            ),
            command=self.command,
            stdout=stdout,
            stderr=stderr,
            log_path=str(summary_path),
            report=report,
        )

    def expected_commands(
        self, additional_commands: tuple[AdditionalVerificationCommand, ...]
    ) -> dict[str, tuple[str, ...]]:
        commands = {"task-verification": self.command}
        for command in additional_commands:
            if command.command_id in commands:
                raise EvidenceValidationError(f"duplicate verification command: {command.command_id}")
            commands[command.command_id] = self._normalize_additional_argv(command.argv)
        return commands

    @staticmethod
    def _normalize_additional_argv(argv: tuple[str, ...]) -> tuple[str, ...]:
        if not argv:
            raise EvidenceValidationError("additional verification argv is empty")
        executable, *arguments = argv
        if any("\x00" in value or value in {"&&", "||", ";", "|"} for value in argv):
            raise EvidenceValidationError("additional verification command contains shell syntax")
        if executable in {"python", "python3"}:
            if not arguments:
                raise EvidenceValidationError("Python verification command has no target")
            script = Path(arguments[0])
            allowed = (
                arguments[:2] == ["-m", "unittest"]
                or arguments[:2] == ["-m", "pytest"]
                or (
                    not arguments[0].startswith("-")
                    and script.suffix == ".py"
                    and script.parts
                    and script.parts[0] in {"scripts", "tests"}
                    and ".." not in script.parts
                )
            )
            if not allowed:
                raise EvidenceValidationError("Python verification target is not allow-listed")
            return (sys.executable, *arguments)
        if executable == "dotnet" and arguments[:1] == ["test"]:
            resolved = shutil.which("dotnet")
        elif executable == "npm" and arguments and arguments[0] in {"test", "run"}:
            resolved = shutil.which("npm")
        else:
            raise EvidenceValidationError("additional verification executable is not allow-listed")
        if resolved is None:
            raise EvidenceValidationError(
                f"additional verification executable not found: {executable}"
            )
        return (resolved, *arguments)

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
    def _test_totals(stdout: str) -> TestTotals:
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
        return TestTotals(discovered, passed, failed, skipped)


class FakeVerificationRunner:
    def __init__(self, results: list[bool] | None = None):
        self.results = list(results or [])
        self.calls: list[tuple[str, str]] = []
        self.command_calls: list[tuple[str, ...]] = []
        self.required_command_ids = ("task-verification",)

    def run(
        self,
        task: CanonicalTask,
        worktree: Path,
        *,
        phase: str,
        commit_sha: str | None = None,
        additional_commands: tuple[AdditionalVerificationCommand, ...] = (),
    ) -> VerificationResult:
        self.calls.append((task.id, phase))
        success = self.results.pop(0) if self.results else True
        commands = self.expected_commands(additional_commands)
        self.command_calls.append(tuple(commands))
        fake_log_root = worktree / ".commerceos" / "fake-verification"
        fake_log_root.mkdir(parents=True, exist_ok=True)
        log_paths: dict[str, str] = {}
        for command_id in commands:
            path = fake_log_root / f"{task.id}-{phase}-{command_id}.log"
            path.write_text("PASS" if success else "FAIL", encoding="utf-8")
            log_paths[command_id] = str(path)
        report = VerificationReport(
            VERIFICATION_REPORT_VERSION,
            task.id,
            commit_sha or "abc",
            tuple(
                VerificationCommandResult(
                    command_id,
                    argv,
                    0 if success else 1,
                    log_paths[command_id],
                )
                for command_id, argv in commands.items()
            ),
            TestTotals(1, 1 if success else 0, 0 if success else 1, 0),
            success,
        )
        return VerificationResult(
            success=success,
            exit_code=0 if success else 1,
            command=("fake-verify",),
            stdout="PASS" if success else "FAIL",
            stderr="",
            log_path=log_paths["task-verification"],
            report=report,
        )

    def expected_commands(
        self, additional_commands: tuple[AdditionalVerificationCommand, ...]
    ) -> dict[str, tuple[str, ...]]:
        commands = {"task-verification": ("fake-verify",)}
        commands.update(
            {command.command_id: command.argv for command in additional_commands}
        )
        return commands
