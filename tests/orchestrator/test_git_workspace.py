from __future__ import annotations

import subprocess
import tempfile
import unittest
from pathlib import Path

from helpers import TOOLS
from commerceos_orchestrator.models import CanonicalTask, Workspace
from commerceos_orchestrator.workspace import GitIntegrationManager, GitWorkspaceManager, WorkspaceError


def git(cwd: Path, *args: str) -> str:
    result = subprocess.run(["git", *args], cwd=cwd, text=True, capture_output=True, check=True)
    return result.stdout.strip()


class GitWorkspaceTests(unittest.TestCase):
    def test_restore_task_lifecycle_preserves_implementation_changes(self):
        with tempfile.TemporaryDirectory() as td:
            repo = Path(td)
            git(repo, "init", "-b", "main")
            git(repo, "config", "user.email", "test@example.com")
            git(repo, "config", "user.name", "Test")
            for directory in ("tasks/backlog", "tasks/backlog-v2", "tasks/completed"):
                (repo / directory).mkdir(parents=True, exist_ok=True)
            spec = repo / "tasks/backlog/TASK-0100.md"
            shard = repo / "tasks/backlog-v2/00.yaml"
            master = repo / "tasks/BACKLOG.v2.yaml"
            index = repo / "tasks/BACKLOG.md"
            spec.write_text("Status: Backlog\n", encoding="utf-8")
            shard.write_text("tasks: backlog\n", encoding="utf-8")
            master.write_text("state: backlog\n", encoding="utf-8")
            index.write_text("TASK-0100 Ready\n", encoding="utf-8")
            (repo / "implementation.txt").write_text("before\n", encoding="utf-8")
            git(repo, "add", ".")
            git(repo, "commit", "-m", "base")

            spec.unlink()
            (repo / "tasks/completed/TASK-0100.md").write_text(
                "Status: Completed\n", encoding="utf-8"
            )
            shard.write_text("tasks: completed\n", encoding="utf-8")
            master.write_text("state: completed\n", encoding="utf-8")
            index.write_text("TASK-0100 Completed\n", encoding="utf-8")
            (repo / "implementation.txt").write_text("after\n", encoding="utf-8")

            task = CanonicalTask(
                id="TASK-0100", maturity="Ready", type="engineering", domain="Harness",
                title="Workspace test", goal="test", depends_on=(), gates=(), owner_role="Builder",
                model_class="default", cloud_verification="no", spec_path="tasks/backlog/TASK-0100.md",
                shard_path="tasks/backlog-v2/00.yaml",
            )
            GitWorkspaceManager(repo).restore_task_lifecycle(task, repo)

            self.assertEqual(spec.read_text(encoding="utf-8"), "Status: Backlog\n")
            self.assertFalse((repo / "tasks/completed/TASK-0100.md").exists())
            self.assertEqual((repo / "implementation.txt").read_text(encoding="utf-8"), "after\n")

    def test_task_worktree_commit_and_serial_integration_primitives(self):
        with tempfile.TemporaryDirectory() as td:
            base = Path(td)
            seed = base / "seed"; seed.mkdir()
            git(seed, "init", "-b", "main")
            git(seed, "config", "user.email", "test@example.com")
            git(seed, "config", "user.name", "Test")
            (seed / "README.md").write_text("base\n", encoding="utf-8")
            git(seed, "add", "."); git(seed, "commit", "-m", "base")
            bare = base / "origin.git"
            subprocess.run(["git", "clone", "--bare", str(seed), str(bare)], check=True, capture_output=True)
            repo = base / "CommerceOS"
            subprocess.run(["git", "clone", str(bare), str(repo)], check=True, capture_output=True)
            git(repo, "config", "user.email", "test@example.com"); git(repo, "config", "user.name", "Test")
            task = CanonicalTask(
                id="TASK-0100", maturity="Ready", type="engineering", domain="Harness",
                title="Workspace test", goal="test", depends_on=(), gates=(), owner_role="Builder",
                model_class="default", cloud_verification="no", spec_path="tasks/backlog/TASK-0100.md",
                exclusive_resources=(), shard_path="tasks/backlog-v2/00.yaml",
            )
            wm = GitWorkspaceManager(repo)
            ws = wm.workspace_for(task)
            self.assertEqual(ws.path.name, "TASK-0100")
            self.assertEqual(ws.path.parent, wm.worktrees_root.resolve())
            (ws.path / "change.txt").write_text("change\n", encoding="utf-8")
            wm.ensure_committed(task, ws)
            self.assertIn("change.txt", wm.diff_text(ws))
            self.assertEqual(wm.ensure_committed(task, ws), git(ws.path, "rev-parse", "HEAD"))
            im = GitIntegrationManager(repo)
            im.prepare_main()
            self.assertTrue(im.merge_branch(task, ws.branch))
            self.assertTrue((repo / "change.txt").exists())
            im.push_main()
            self.assertTrue(im.branch_is_on_remote_main(ws.branch))
            wm.cleanup(task)

    def test_committed_builder_completion_is_reversed_against_trusted_baseline(self):
        with tempfile.TemporaryDirectory() as td:
            repo = Path(td)
            git(repo, "init", "-b", "main")
            git(repo, "config", "user.email", "test@example.com")
            git(repo, "config", "user.name", "Test")
            for directory in (
                "tasks/commerceos/backlog",
                "tasks/commerceos/backlog-v2",
                "tasks/commerceos/completed",
            ):
                (repo / directory).mkdir(parents=True, exist_ok=True)
            spec_relative = "tasks/commerceos/backlog/TASK-0100.md"
            shard_relative = "tasks/commerceos/backlog-v2/00.yaml"
            spec = repo / spec_relative
            shard = repo / shard_relative
            master = repo / "tasks/BACKLOG.v2.yaml"
            index = repo / "tasks/commerceos/BACKLOG.md"
            spec.write_text("Status: Backlog\n", encoding="utf-8")
            shard.write_text("tasks: backlog\n", encoding="utf-8")
            master.write_text("state: backlog\n", encoding="utf-8")
            index.write_text("TASK-0100 Ready\n", encoding="utf-8")
            (repo / "implementation.txt").write_text("before\n", encoding="utf-8")
            git(repo, "add", ".")
            git(repo, "commit", "-m", "base")
            git(repo, "update-ref", "refs/remotes/origin/main", "HEAD")

            spec.unlink()
            (repo / "tasks/commerceos/completed/TASK-0100.md").write_text(
                "Status: Completed\n", encoding="utf-8"
            )
            shard.write_text("tasks: completed\n", encoding="utf-8")
            master.write_text("state: completed\n", encoding="utf-8")
            index.write_text("TASK-0100 Completed\n", encoding="utf-8")
            (repo / "implementation.txt").write_text("after\n", encoding="utf-8")
            git(repo, "add", ".")
            git(repo, "commit", "-m", "builder attempted completion")

            task = CanonicalTask(
                id="TASK-0100", maturity="Ready", type="engineering", domain="Harness",
                title="Workspace test", goal="test", depends_on=(), gates=(), owner_role="Builder",
                model_class="default", cloud_verification="no", spec_path=spec_relative,
                shard_path=shard_relative, catalog="commerceos",
            )
            manager = GitWorkspaceManager(repo)
            manager.restore_task_lifecycle(task, repo)
            manager.ensure_committed(task, Workspace("main", repo, False))

            self.assertEqual(spec.read_text(encoding="utf-8"), "Status: Backlog\n")
            self.assertFalse((repo / "tasks/commerceos/completed/TASK-0100.md").exists())
            self.assertEqual((repo / "implementation.txt").read_text(encoding="utf-8"), "after\n")
            changed = git(repo, "diff", "--name-only", "origin/main...HEAD").splitlines()
            self.assertEqual(changed, ["implementation.txt"])

    def test_invalid_task_id_cannot_construct_worktree_path(self):
        with tempfile.TemporaryDirectory() as td:
            repo = Path(td)
            task = CanonicalTask(
                id="TASK-0100/../../escape", maturity="Ready", type="engineering", domain="Harness",
                title="Unsafe", goal="test", depends_on=(), gates=(), owner_role="Builder",
                model_class="default", cloud_verification="no", spec_path="tasks/backlog/TASK-0100.md",
            )
            manager = GitWorkspaceManager(repo)
            with self.assertRaisesRegex(WorkspaceError, "invalid canonical task id"):
                manager._task_directory(task)


if __name__ == "__main__":
    unittest.main()
