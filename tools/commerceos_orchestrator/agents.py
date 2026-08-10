from __future__ import annotations

import os
import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol

from .models import AgentResult, CanonicalTask, ReviewResult


class AgentRunner(Protocol):
    def run_builder(
        self,
        task: CanonicalTask,
        worktree: Path,
        *,
        attempt: int,
        feedback: str | None = None,
    ) -> AgentResult: ...

    def run_reviewer(self, task: CanonicalTask, worktree: Path, *, diff: str) -> ReviewResult: ...

    def run_conflict_resolver(
        self,
        task: CanonicalTask,
        integration_root: Path,
        conflicted_files: list[str],
    ) -> AgentResult: ...


@dataclass(frozen=True)
class CodexModelRouting:
    default_model: str | None = None
    strong_model: str | None = None

    @classmethod
    def from_environment(cls) -> "CodexModelRouting":
        return cls(
            default_model=os.environ.get("COMMERCEOS_CODEX_MODEL_DEFAULT") or None,
            strong_model=os.environ.get("COMMERCEOS_CODEX_MODEL_STRONG") or None,
        )

    def resolve(self, model_class: str) -> str | None:
        if model_class == "strong":
            return self.strong_model or self.default_model
        return self.default_model


class CodexRunner:
    """Non-interactive Codex CLI adapter.

    V1 intentionally keeps CLI flags centralized. The command can be overridden with
    COMMERCEOS_CODEX_EXECUTABLE and model-class environment variables without editing tasks.
    """

    def __init__(
        self,
        root: Path,
        logs_root: Path,
        routing: CodexModelRouting | None = None,
        *,
        cloud_authorized: bool = False,
    ):
        self.root = root.resolve()
        self.logs_root = logs_root.resolve()
        self.logs_root.mkdir(parents=True, exist_ok=True)
        self.routing = routing or CodexModelRouting.from_environment()
        self.cloud_authorized = cloud_authorized
        self.executable = os.environ.get("COMMERCEOS_CODEX_EXECUTABLE", "codex")

    def run_builder(
        self,
        task: CanonicalTask,
        worktree: Path,
        *,
        attempt: int,
        feedback: str | None = None,
    ) -> AgentResult:
        prompt = self._builder_prompt(task, feedback)
        return self._run(
            task,
            role="builder",
            worktree=worktree,
            prompt=prompt,
            writable=True,
            attempt=attempt,
        )

    def run_reviewer(self, task: CanonicalTask, worktree: Path, *, diff: str) -> ReviewResult:
        prompt = self._reviewer_prompt(task, diff)
        raw = self._run(
            task,
            role="reviewer",
            worktree=worktree,
            prompt=prompt,
            writable=False,
            attempt=0,
        )
        combined = f"{raw.stdout}\n{raw.stderr}"
        if "REVIEW_RESULT: PASS" in combined:
            return ReviewResult(True, combined.strip(), raw)
        return ReviewResult(False, combined.strip(), raw)

    def run_conflict_resolver(
        self,
        task: CanonicalTask,
        integration_root: Path,
        conflicted_files: list[str],
    ) -> AgentResult:
        files = "\n".join(f"- {name}" for name in conflicted_files)
        prompt = f"""You are the CommerceOS Conflict Resolver for {task.id}.

Read AGENTS.md, the task specification at {task.spec_path}, relevant architecture rules,
and the current Git conflict. Only resolve implementation-level textual/structural conflicts.
Do not choose between incompatible accepted business/domain/architecture/security contracts.
If the conflict requires a new decision, do not resolve it and finish with:
CONFLICT_RESULT: HUMAN_REQUIRED

Conflicted files:
{files}

If you can preserve both accepted outcomes safely, resolve the conflict in the current
integration checkout, leave no unmerged files, and finish with:
CONFLICT_RESULT: RESOLVED
"""
        return self._run(
            task,
            role="conflict-resolver",
            worktree=integration_root,
            prompt=prompt,
            writable=True,
            attempt=0,
        )

    def _run(
        self,
        task: CanonicalTask,
        *,
        role: str,
        worktree: Path,
        prompt: str,
        writable: bool,
        attempt: int,
    ) -> AgentResult:
        if shutil.which(self.executable) is None:
            return AgentResult(
                success=False,
                exit_code=127,
                stdout="",
                stderr=f"Codex executable not found: {self.executable}",
                log_path="",
                marker="ENVIRONMENT_UNAVAILABLE",
            )

        model = self.routing.resolve(task.model_class)
        command = [
            self.executable,
            "exec",
            "--json",
            "--ephemeral",
            "-C",
            str(worktree),
            "--sandbox",
            "workspace-write" if writable else "read-only",
        ]
        if model:
            command.extend(["-m", model])
        command.append(prompt)

        log_path = self.logs_root / f"{task.id}-{role}-{attempt}.log"
        result = subprocess.run(command, text=True, capture_output=True, check=False)
        log_path.write_text(
            f"COMMAND: {' '.join(command[:-1])} <prompt>\n\nSTDOUT\n{result.stdout}\n\nSTDERR\n{result.stderr}",
            encoding="utf-8",
        )
        return AgentResult(
            success=result.returncode == 0,
            exit_code=result.returncode,
            stdout=result.stdout,
            stderr=result.stderr,
            log_path=str(log_path),
        )

    def _builder_prompt(self, task: CanonicalTask, feedback: str | None) -> str:
        feedback_text = ""
        if feedback:
            feedback_text = f"\nPrevious verification/review feedback to address:\n{feedback}\n"
        cloud = "YES" if self.cloud_authorized else "NO"
        return f"""Act as the CommerceOS Builder.

Read, in repository order:
- AGENTS.md
- docs/development/15-planning-factory-and-task-maturity.md
- docs/agents/builder.md
- {task.spec_path}
- every domain/architecture/ADR artifact referenced by that Ready task
- docs/development/03-architecture-rules.md
- docs/development/14-codex-multi-agent-and-worktrees.md
- docs/development/02-definition-of-done.md

Implement {task.id} completely inside this task worktree. Do not expand scope or invent a
product/domain/architecture decision. Add/update tests and task-related documentation.
Do not merge or push main. Do not weaken a guardrail to make verification green.
Cloud execution authorization for this Orchestrator run: {cloud}. Never deploy/invoke real AWS
when this value is NO, even when cloud verification would otherwise be useful.
{feedback_text}
Before finishing, summarize changes and any blocker in your final response. The Orchestrator
will run deterministic verification and independent review after you exit.
"""

    def _reviewer_prompt(self, task: CanonicalTask, diff: str) -> str:
        clipped = diff[-60000:]
        return f"""Act as the independent CommerceOS Reviewer for {task.id}.

Read AGENTS.md, docs/agents/reviewer.md, docs/development/02-definition-of-done.md,
docs/development/03-architecture-rules.md, and the Ready task at {task.spec_path}.
Review the current Builder worktree read-only against all acceptance criteria, architecture,
security, reliability/idempotency, cost, and test quality. Do not modify files.

Git diff supplied by the Orchestrator:
---
{clipped}
---

If no blocking finding remains, end exactly with:
REVIEW_RESULT: PASS

If any blocking finding remains, list concrete actionable findings and end exactly with:
REVIEW_RESULT: FIX_REQUIRED
"""


class FakeAgentRunner:
    """Deterministic runner for unit/integration tests; consumes no Codex quota."""

    def __init__(
        self,
        *,
        builder_results: list[AgentResult] | None = None,
        review_results: list[ReviewResult] | None = None,
        conflict_results: list[AgentResult] | None = None,
        builder_hook=None,
    ):
        self.builder_results = list(builder_results or [])
        self.review_results = list(review_results or [])
        self.conflict_results = list(conflict_results or [])
        self.builder_hook = builder_hook
        self.builder_calls = 0
        self.reviewer_calls = 0
        self.conflict_calls = 0

    @staticmethod
    def _ok() -> AgentResult:
        return AgentResult(True, 0, "", "", "")

    def run_builder(
        self,
        task: CanonicalTask,
        worktree: Path,
        *,
        attempt: int,
        feedback: str | None = None,
    ) -> AgentResult:
        self.builder_calls += 1
        if self.builder_hook:
            self.builder_hook(task, worktree, attempt, feedback)
        if self.builder_results:
            return self.builder_results.pop(0)
        return self._ok()

    def run_reviewer(self, task: CanonicalTask, worktree: Path, *, diff: str) -> ReviewResult:
        self.reviewer_calls += 1
        if self.review_results:
            return self.review_results.pop(0)
        raw = self._ok()
        return ReviewResult(True, "REVIEW_RESULT: PASS", raw)

    def run_conflict_resolver(
        self,
        task: CanonicalTask,
        integration_root: Path,
        conflicted_files: list[str],
    ) -> AgentResult:
        self.conflict_calls += 1
        if self.conflict_results:
            return self.conflict_results.pop(0)
        return AgentResult(True, 0, "CONFLICT_RESULT: RESOLVED", "", "")
