from __future__ import annotations

import re
import shutil
import subprocess
from pathlib import Path

from .models import CanonicalTask, Workspace


class WorkspaceError(RuntimeError):
    pass


class GitWorkspaceManager:
    def __init__(self, root: Path):
        self.root = root.resolve()
        self.worktrees_root = self.root.parent / f"{self.root.name}.worktrees"

    def _run(self, args: list[str], cwd: Path | None = None, check: bool = True) -> subprocess.CompletedProcess[str]:
        result = subprocess.run(
            ["git", *args],
            cwd=cwd or self.root,
            text=True,
            capture_output=True,
            check=False,
        )
        if check and result.returncode != 0:
            raise WorkspaceError(
                f"git {' '.join(args)} failed ({result.returncode}): "
                f"{result.stderr.strip() or result.stdout.strip()}"
            )
        return result

    def ensure_repository(self) -> None:
        if shutil.which("git") is None:
            raise WorkspaceError("git executable not found")
        result = self._run(["rev-parse", "--show-toplevel"])
        actual = Path(result.stdout.strip()).resolve()
        if actual != self.root:
            raise WorkspaceError(f"repository root mismatch: expected {self.root}, got {actual}")

    def primary_is_clean(self) -> bool:
        return self._run(["status", "--porcelain"]).stdout.strip() == ""

    def workspace_for(self, task: CanonicalTask) -> Workspace:
        self.ensure_repository()
        self.worktrees_root.mkdir(parents=True, exist_ok=True)
        branch = self._task_branch(task)
        directory = self._task_directory(task)

        existing = self._find_worktree(directory)
        if existing:
            actual_branch = self.current_branch(directory)
            if actual_branch != branch:
                raise WorkspaceError(
                    f"existing worktree {directory} is on {actual_branch}, expected {branch}"
                )
            return Workspace(branch=branch, path=directory, created=False)

        self._run(["fetch", "origin", "main"])
        branch_exists = self._run(["show-ref", "--verify", f"refs/heads/{branch}"], check=False).returncode == 0
        if branch_exists:
            self._run(["worktree", "add", str(directory), branch])
        else:
            self._run(["worktree", "add", str(directory), "-b", branch, "origin/main"])
        return Workspace(branch=branch, path=directory, created=True)

    def current_branch(self, directory: Path) -> str:
        return self._run(["branch", "--show-current"], cwd=directory).stdout.strip()

    def is_clean(self, directory: Path) -> bool:
        return self._run(["status", "--porcelain"], cwd=directory).stdout.strip() == ""

    def has_changes(self, directory: Path) -> bool:
        return not self.is_clean(directory)

    def ensure_committed(self, task: CanonicalTask, workspace: Workspace) -> str:
        status = self._run(["status", "--porcelain"], cwd=workspace.path).stdout.strip()
        if status:
            self._run(["add", "-A"], cwd=workspace.path)
            staged = self._run(["diff", "--cached", "--quiet"], cwd=workspace.path, check=False)
            if staged.returncode == 1:
                self._run(
                    ["commit", "-m", f"{task.id}: {task.title}"],
                    cwd=workspace.path,
                )
            elif staged.returncode != 0:
                raise WorkspaceError("could not determine whether task changes are staged")
        sha = self._run(["rev-parse", "HEAD"], cwd=workspace.path).stdout.strip()
        return sha

    def diff_text(self, workspace: Workspace, base: str = "origin/main") -> str:
        return self._run(["diff", f"{base}...HEAD"], cwd=workspace.path).stdout

    def changed_files(self, workspace: Workspace, base: str = "origin/main") -> list[str]:
        result = self._run(
            ["diff", "--name-only", f"{base}...HEAD"],
            cwd=workspace.path,
        )
        return [line for line in result.stdout.splitlines() if line.strip()]

    def changed_files_between(self, workspace: Workspace, base: str, head: str) -> list[str]:
        result = self._run(["diff", "--name-only", f"{base}..{head}"], cwd=workspace.path)
        return [line for line in result.stdout.splitlines() if line.strip()]

    def restore_task_lifecycle(self, task: CanonicalTask, directory: Path) -> None:
        """Discard Builder-owned lifecycle bookkeeping while preserving implementation work.

        A Builder may write task-related documentation, but only the Orchestrator may move a
        Ready task to completed or update canonical lifecycle indexes. Restore only those
        narrow files from trusted `origin/main`; implementation files remain untouched.
        """
        directory = directory.resolve()
        repository_root = Path(self._run(["rev-parse", "--show-toplevel"], cwd=directory).stdout.strip()).resolve()
        if repository_root != directory:
            raise WorkspaceError(f"task lifecycle restore requires worktree root: {directory}")
        baseline = (
            "origin/main"
            if self._run(
                ["rev-parse", "--verify", "origin/main"], cwd=directory, check=False
            ).returncode
            == 0
            else "HEAD"
        )

        lifecycle_paths = [
            task.spec_path,
            task.shard_path,
            "tasks/BACKLOG.v2.yaml",
            f"tasks/{task.catalog}/BACKLOG.md",
        ]
        lifecycle_paths = [
            path
            for path in lifecycle_paths
            if path
            and self._run(
                ["cat-file", "-e", f"{baseline}:{path}"],
                cwd=directory,
                check=False,
            ).returncode
            == 0
        ]
        if lifecycle_paths:
            self._run(
                ["restore", f"--source={baseline}", "--staged", "--worktree", "--", *lifecycle_paths],
                cwd=directory,
            )

        spec_parts = Path(task.spec_path).parts
        if len(spec_parts) >= 3 and spec_parts[0] == "tasks" and spec_parts[1] in {
            "commerceos",
            "orchestrator",
        }:
            completed_relative = f"tasks/{spec_parts[1]}/completed/{Path(task.spec_path).name}"
        else:
            completed_relative = f"tasks/completed/{Path(task.spec_path).name}"
        completed_path = (directory / completed_relative).resolve()
        try:
            completed_path.relative_to(directory)
        except ValueError as exc:
            raise WorkspaceError(f"unsafe completed task path: {completed_path}") from exc
        present_on_baseline = self._run(
            ["cat-file", "-e", f"{baseline}:{completed_relative}"],
            cwd=directory,
            check=False,
        )
        if present_on_baseline.returncode == 0:
            self._run(
                [
                    "restore",
                    f"--source={baseline}",
                    "--staged",
                    "--worktree",
                    "--",
                    completed_relative,
                ],
                cwd=directory,
            )
        elif completed_path.is_file():
            tracked = self._run(
                ["ls-files", "--error-unmatch", "--", completed_relative],
                cwd=directory,
                check=False,
            )
            if tracked.returncode == 0:
                self._run(["rm", "-f", "--", completed_relative], cwd=directory)
            else:
                completed_path.unlink()

    def cleanup(self, task: CanonicalTask, force: bool = False) -> bool:
        branch = self._task_branch(task)
        directory = self._task_directory(task)
        if not directory.exists():
            return False
        if not force and not self.is_clean(directory):
            raise WorkspaceError(f"refusing to remove dirty worktree: {directory}")
        args = ["worktree", "remove"]
        if force:
            args.append("--force")
        args.extend(["--", str(directory)])
        self._run(args)
        merged = self._run(["branch", "--merged", "main"]).stdout.splitlines()
        if any(line.strip().lstrip("* ") == branch for line in merged):
            self._run(["branch", "-d", branch], check=False)
        self._run(["worktree", "prune"], check=False)
        return True

    @staticmethod
    def _task_branch(task: CanonicalTask) -> str:
        if not re.fullmatch(r"TASK-\d{4,}", task.id):
            raise WorkspaceError(f"invalid canonical task id for Git workspace: {task.id}")
        return f"agent/{task.id}-{task.slug}"

    def _task_directory(self, task: CanonicalTask) -> Path:
        # Worktree filesystem paths intentionally use only the canonical numeric task id,
        # never the task title/slug. Validate again at the filesystem trust boundary and
        # require the resolved path to be one direct child of the fixed sibling root.
        if not re.fullmatch(r"TASK-\d{4,}", task.id):
            raise WorkspaceError(f"invalid canonical task id for Git worktree path: {task.id}")
        root = self.worktrees_root.resolve()
        directory = (root / task.id).resolve()
        if directory.parent != root:
            raise WorkspaceError(f"unsafe task worktree path: {directory}")
        return directory

    def _find_worktree(self, directory: Path) -> bool:
        result = self._run(["worktree", "list", "--porcelain"])
        expected = str(directory.resolve())
        return any(
            line.startswith("worktree ") and str(Path(line.split(" ", 1)[1]).resolve()) == expected
            for line in result.stdout.splitlines()
        )


class IntegrationError(RuntimeError):
    pass


class GitIntegrationManager:
    def __init__(self, root: Path):
        self.root = root.resolve()

    def _run(self, args: list[str], check: bool = True) -> subprocess.CompletedProcess[str]:
        result = subprocess.run(
            ["git", *args], cwd=self.root, text=True, capture_output=True, check=False
        )
        if check and result.returncode != 0:
            raise IntegrationError(
                f"git {' '.join(args)} failed ({result.returncode}): "
                f"{result.stderr.strip() or result.stdout.strip()}"
            )
        return result

    def prepare_main(self) -> None:
        merge_head = self._run(["rev-parse", "-q", "--verify", "MERGE_HEAD"], check=False)
        if merge_head.returncode == 0:
            self._run(["merge", "--abort"], check=False)
        status = self._run(["status", "--porcelain"]).stdout.strip()
        if status:
            raise IntegrationError("primary main checkout is dirty; refusing integration")
        self._run(["fetch", "origin", "main"])
        self._run(["switch", "main"])
        self._run(["pull", "--ff-only", "origin", "main"])

    def current_commit(self) -> str:
        return self._run(["rev-parse", "HEAD"]).stdout.strip()


    def branch_is_on_remote_main(self, branch: str) -> bool:
        self._run(["fetch", "origin", "main"])
        result = self._run(["merge-base", "--is-ancestor", branch, "origin/main"], check=False)
        return result.returncode == 0

    def rollback_unpushed_main(self) -> None:
        self._run(["merge", "--abort"], check=False)
        self._run(["fetch", "origin", "main"], check=False)
        self._run(["reset", "--hard", "origin/main"])
        self._run(["clean", "-fd"], check=False)

    def merge_branch(self, task: CanonicalTask, branch: str) -> bool:
        result = self._run(
            ["merge", "--no-ff", branch, "-m", f"Merge {task.id}: {task.title}"],
            check=False,
        )
        return result.returncode == 0

    def conflicted_files(self) -> list[str]:
        result = self._run(["diff", "--name-only", "--diff-filter=U"], check=False)
        return [line for line in result.stdout.splitlines() if line.strip()]

    def abort_merge(self) -> None:
        self._run(["merge", "--abort"], check=False)

    def commit_current_merge(self, task: CanonicalTask) -> None:
        if self.conflicted_files():
            raise IntegrationError("cannot commit merge with unresolved conflicts")
        status = self._run(["status", "--porcelain"]).stdout.strip()
        if not status:
            return
        self._run(["add", "-A"])
        self._run(["commit", "-m", f"Merge {task.id}: {task.title}"])

    def commit_bookkeeping(self, task: CanonicalTask) -> None:
        status = self._run(["status", "--porcelain"]).stdout.strip()
        if not status:
            raise IntegrationError("completion bookkeeping produced no changes")
        self._run(["add", "-A"])
        self._run(["commit", "-m", f"tasks: complete {task.id}"])

    def push_main(self) -> None:
        result = self._run(["push", "origin", "main"], check=False)
        if result.returncode != 0:
            raise IntegrationError(
                "push origin main failed; remote may have advanced. "
                + (result.stderr.strip() or result.stdout.strip())
            )
