from __future__ import annotations

import subprocess
import tempfile
import unittest
from pathlib import Path

from helpers import TOOLS
from commerceos_orchestrator.models import CanonicalTask
from commerceos_orchestrator.workspace import GitIntegrationManager, GitWorkspaceManager, WorkspaceError


def git(cwd: Path, *args: str) -> str:
    result = subprocess.run(["git", *args], cwd=cwd, text=True, capture_output=True, check=True)
    return result.stdout.strip()


class GitWorkspaceTests(unittest.TestCase):
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
            im = GitIntegrationManager(repo)
            im.prepare_main()
            self.assertTrue(im.merge_branch(task, ws.branch))
            self.assertTrue((repo / "change.txt").exists())
            im.push_main()
            self.assertTrue(im.branch_is_on_remote_main(ws.branch))
            wm.cleanup(task)

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
