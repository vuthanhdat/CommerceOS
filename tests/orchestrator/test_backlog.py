from __future__ import annotations

import tempfile
import threading
import unittest
from pathlib import Path

from helpers import row, write_backlog
from commerceos_orchestrator.backlog import (
    BacklogReader,
    BacklogValidationError,
    BacklogWriter,
    _atomic_write_text,
    _read_text,
)
from commerceos_orchestrator.yaml_subset import parse_document, parse_inline_sequence


class YamlSubsetTests(unittest.TestCase):
    def test_nested_inline_lists_and_quotes(self):
        value = parse_inline_sequence('[TASK-1, "hello, world", [A, B], [], true]')
        self.assertEqual(value, ["TASK-1", "hello, world", ["A", "B"], [], True])

    def test_mapping_and_sequence(self):
        doc = parse_document("root:\n  value: 2\n  items:\n    - A\n    - B\n")
        self.assertEqual(doc["root"]["items"], ["A", "B"])


class BacklogReaderTests(unittest.TestCase):
    def test_atomic_canonical_write_never_exposes_partial_document(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "BACKLOG.v2.yaml"
            documents = (
                "schema_version: 1\ntask_fields:\n  - id\nmarker: alpha\n",
                "schema_version: 1\ntask_fields:\n  - id\nmarker: beta\n",
            )
            _atomic_write_text(path, documents[0])
            observed: list[str] = []
            finished = threading.Event()

            def read_while_writing() -> None:
                while not finished.is_set():
                    observed.append(_read_text(path))

            reader = threading.Thread(target=read_while_writing)
            reader.start()
            try:
                for index in range(100):
                    _atomic_write_text(path, documents[index % 2])
            finally:
                finished.set()
                reader.join()

            self.assertTrue(observed)
            self.assertTrue(set(observed).issubset(set(documents)))

    def test_named_catalogs_are_strictly_filtered(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(
                root,
                [row("TASK-0100"), row("TASK-0101")],
                ready=["TASK-0100", "TASK-0101"],
            )
            legacy = root / "tasks/backlog-v2/00.yaml"
            lines = legacy.read_text(encoding="utf-8").splitlines()
            tool_dir = root / "tasks/orchestrator/backlog-v2"
            tool_dir.mkdir(parents=True)
            tool_spec_dir = root / "tasks/orchestrator/backlog"
            tool_spec_dir.mkdir(parents=True)
            tool_spec = tool_spec_dir / "TASK-0101-spec.md"
            (root / "tasks/backlog/TASK-0101-spec.md").rename(tool_spec)
            tool_row = lines.pop().replace(
                "tasks/backlog/TASK-0101-spec.md",
                "tasks/orchestrator/backlog/TASK-0101-spec.md",
            )
            legacy.write_text("\n".join(lines) + "\n", encoding="utf-8")
            (tool_dir / "00.yaml").write_text("tasks:\n" + tool_row + "\n", encoding="utf-8")
            master = root / "tasks/BACKLOG.v2.yaml"
            master.write_text(
                master.read_text(encoding="utf-8").replace(
                    "  - tasks/backlog-v2/00.yaml",
                    "  - tasks/backlog-v2/00.yaml\n  - tasks/orchestrator/backlog-v2/00.yaml",
                ),
                encoding="utf-8",
            )

            commerceos = BacklogReader(root, "commerceos").load()
            orchestrator = BacklogReader(root, "orchestrator").load()

            self.assertEqual(set(commerceos.tasks), {"TASK-0100"})
            self.assertEqual(set(orchestrator.tasks), {"TASK-0101"})

            destination = BacklogWriter(root).finalize_task(
                orchestrator, orchestrator.tasks["TASK-0101"], "catalog completion"
            )

            self.assertEqual(
                destination,
                "tasks/orchestrator/completed/TASK-0101-spec.md",
            )
            self.assertTrue((root / destination).is_file())
            self.assertFalse((root / "tasks/orchestrator/backlog/TASK-0101-spec.md").exists())
            self.assertEqual(
                [task.id for task in BacklogReader.ready_frontier(BacklogReader(root).load(), set())],
                ["TASK-0100"],
            )

    def test_unknown_catalog_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            with self.assertRaisesRegex(BacklogValidationError, "unsupported task catalog"):
                BacklogReader(Path(td), "mixed")

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

    def test_outline_without_spec_path_is_valid(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            write_backlog(root, [row("TASK-0100", maturity="Outline")], ready=[])
            shard = root / "tasks/backlog-v2/00.yaml"
            text = shard.read_text(encoding="utf-8").replace(
                '"tasks/backlog/TASK-0100-spec.md"]', '""]'
            )
            shard.write_text(text, encoding="utf-8")

            snapshot = BacklogReader(root).load()

            self.assertEqual(snapshot.tasks["TASK-0100"].spec_path, "")

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
