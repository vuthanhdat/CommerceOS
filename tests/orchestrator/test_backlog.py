from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from helpers import row, write_backlog
from commerceos_orchestrator.backlog import BacklogReader, BacklogValidationError
from commerceos_orchestrator.yaml_subset import parse_document, parse_inline_sequence


class YamlSubsetTests(unittest.TestCase):
    def test_nested_inline_lists_and_quotes(self):
        value = parse_inline_sequence('[TASK-1, "hello, world", [A, B], [], true]')
        self.assertEqual(value, ["TASK-1", "hello, world", ["A", "B"], [], True])

    def test_mapping_and_sequence(self):
        doc = parse_document("root:\n  value: 2\n  items:\n    - A\n    - B\n")
        self.assertEqual(doc["root"]["items"], ["A", "B"])


class BacklogReaderTests(unittest.TestCase):
    def test_valid_frontier_and_completed_dependency(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(
                root,
                [
                    row("TASK-0100", deps="[TASK-0001]"),
                    row("TASK-0101", maturity="Outline", deps="[TASK-0100]"),
                ],
                ready=["TASK-0100"],
            )
            snap = BacklogReader(root).load()
            self.assertEqual([t.id for t in BacklogReader.ready_frontier(snap, set())], ["TASK-0100"])

    def test_missing_dependency_fails_closed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100", deps="[TASK-9999]")], ready=[])
            with self.assertRaisesRegex(BacklogValidationError, "missing dependency"):
                BacklogReader(root).load()

    def test_cycle_fails_closed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(
                root,
                [row("TASK-0100", deps="[TASK-0101]"), row("TASK-0101", deps="[TASK-0100]")],
                ready=[],
            )
            with self.assertRaisesRegex(BacklogValidationError, "cycle"):
                BacklogReader(root).load()

    def test_declared_frontier_must_match_mechanical_frontier(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=[])
            with self.assertRaisesRegex(BacklogValidationError, "ready_frontier"):
                BacklogReader(root).load()

    def test_shard_path_cannot_escape_repository_planning_directory(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"])
            master = root / "tasks/BACKLOG.v2.yaml"
            text = master.read_text(encoding="utf-8").replace(
                "tasks/backlog-v2/00.yaml", "../outside.yaml"
            )
            master.write_text(text, encoding="utf-8")
            with self.assertRaisesRegex(BacklogValidationError, "backlog shard"):
                BacklogReader(root).load()

    def test_spec_path_cannot_escape_task_directories(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100")], ready=["TASK-0100"])
            shard = root / "tasks/backlog-v2/00.yaml"
            text = shard.read_text(encoding="utf-8").replace(
                "tasks/backlog/TASK-0100-spec.md", "../outside.md"
            )
            shard.write_text(text, encoding="utf-8")
            with self.assertRaisesRegex(BacklogValidationError, "spec_path"):
                BacklogReader(root).load()


if __name__ == "__main__":
    unittest.main()
