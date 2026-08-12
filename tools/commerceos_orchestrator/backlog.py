from __future__ import annotations

import os
import re
import tempfile
import threading
import time
from datetime import date
from dataclasses import replace
from pathlib import Path, PurePosixPath

from .models import BacklogSnapshot, CanonicalTask
from .yaml_subset import parse_document, parse_inline_sequence, render_inline_sequence


class BacklogValidationError(ValueError):
    pass


_BACKLOG_IO_LOCK = threading.RLock()


def _synchronized(method):
    def locked(*args, **kwargs):
        with _BACKLOG_IO_LOCK:
            return method(*args, **kwargs)
    return locked


def _atomic_write_text(path: Path, text: str) -> None:
    """Replace a canonical backlog file without exposing a partial document."""
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        dir=path.parent,
        prefix=f".{path.name}.",
        suffix=".tmp",
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="") as stream:
            stream.write(text)
            stream.flush()
            os.fsync(stream.fileno())
        for attempt in range(100):
            try:
                os.replace(temporary_path, path)
                break
            except PermissionError:
                if attempt == 99:
                    raise
                # Windows can briefly deny replacement while another process has
                # the old file open for reading. Retrying still exposes only the
                # complete old or complete new document.
                time.sleep(0.005)
    finally:
        temporary_path.unlink(missing_ok=True)


def _read_text(path: Path) -> str:
    """Read through the brief Windows sharing window of an atomic replace."""
    for attempt in range(100):
        try:
            return path.read_text(encoding="utf-8")
        except PermissionError:
            if attempt == 99:
                raise
            time.sleep(0.005)
    raise AssertionError("unreachable")


def _repo_path(root: Path, raw: str, *, label: str, roots: tuple[str, ...]) -> Path:
    if not raw or "\\" in raw:
        raise BacklogValidationError(f"{label} must be a non-empty repository-relative POSIX path")
    value = PurePosixPath(raw)
    if value.is_absolute() or any(part in {"", ".", ".."} for part in value.parts):
        raise BacklogValidationError(f"{label} must not be absolute or contain traversal: {raw}")
    relative = Path(*value.parts)
    allowed = tuple(Path(*PurePosixPath(item).parts) for item in roots)
    if not any(relative == prefix or prefix in relative.parents for prefix in allowed):
        raise BacklogValidationError(f"{label} must stay under {', '.join(roots)}: {raw}")
    resolved = (root / relative).resolve()
    try:
        resolved.relative_to(root.resolve())
    except ValueError as exc:
        raise BacklogValidationError(f"{label} escapes repository root: {raw}") from exc
    return resolved


class BacklogReader:
    MASTER_PATH = Path("tasks/BACKLOG.v2.yaml")
    CATALOGS = {"commerceos", "orchestrator"}
    SHARD_ROOTS = (
        "tasks/commerceos/backlog-v2",
        "tasks/orchestrator/backlog-v2",
        "tasks/backlog-v2",  # legacy test fixtures
    )
    SPEC_ROOTS = (
        "tasks/commerceos/backlog",
        "tasks/commerceos/active",
        "tasks/commerceos/completed",
        "tasks/orchestrator/backlog",
        "tasks/orchestrator/active",
        "tasks/orchestrator/completed",
        "tasks/backlog",  # legacy test fixtures
        "tasks/active",
        "tasks/completed",
    )

    def __init__(self, root: Path, catalog: str | None = None):
        self.root = root.resolve()
        if catalog is not None and catalog not in self.CATALOGS:
            raise BacklogValidationError(f"unsupported task catalog: {catalog}")
        self.catalog = catalog

    @_synchronized
    def load(self) -> BacklogSnapshot:
        master_path = self.root / self.MASTER_PATH
        if not master_path.is_file():
            raise BacklogValidationError(f"missing canonical backlog: {self.MASTER_PATH}")
        master = parse_document(_read_text(master_path))
        fields = self._string_list(master.get("task_fields"), "task_fields", required=True)
        shards = self._string_list(master.get("task_shards"), "task_shards", required=True)
        defaults = master.get("task_defaults") or {}
        metadata = master.get("execution_metadata") or {}
        if not isinstance(defaults, dict) or not isinstance(metadata, dict):
            raise BacklogValidationError("task_defaults/execution_metadata must be mappings")

        tasks: dict[str, CanonicalTask] = {}
        for shard_name in shards:
            shard_path = _repo_path(
                self.root,
                shard_name,
                label="backlog shard",
                roots=self.SHARD_ROOTS,
            )
            if not shard_path.is_file():
                raise BacklogValidationError(f"missing backlog shard: {shard_name}")
            rows = parse_document(_read_text(shard_path)).get("tasks")
            if not isinstance(rows, list):
                raise BacklogValidationError(f"{shard_name}: tasks must be a list")
            for row in rows:
                if not isinstance(row, list) or len(row) != len(fields):
                    raise BacklogValidationError(f"{shard_name}: malformed task row")
                values = dict(zip(fields, row, strict=True))
                task_id = str(values["id"])
                if task_id in tasks:
                    raise BacklogValidationError(f"duplicate canonical task id: {task_id}")
                meta = metadata.get(task_id) or {}
                if not isinstance(meta, dict):
                    raise BacklogValidationError(f"execution metadata for {task_id} must be a mapping")
                resources = meta.get("exclusive_resources", defaults.get("exclusive_resources", [])) or []
                if not isinstance(resources, list):
                    raise BacklogValidationError(f"exclusive_resources for {task_id} must be a list")
                tasks[task_id] = CanonicalTask(
                    id=task_id,
                    maturity=str(values["maturity"]),
                    type=str(values["type"]),
                    domain=str(values["domain"]),
                    title=str(values["title"]),
                    goal=str(values["goal"]),
                    depends_on=tuple(str(x) for x in (values["depends_on"] or [])),
                    gates=tuple(str(x) for x in (values["gates"] or [])),
                    owner_role=str(values["owner_role"]),
                    model_class=str(values["model_class"]),
                    cloud_verification=str(values["cloud_verification"]),
                    spec_path="" if values["spec_path"] is None else str(values["spec_path"]),
                    lifecycle_state=str(meta.get("lifecycle_state", defaults.get("lifecycle_state", "Backlog"))),
                    exclusive_resources=tuple(str(x) for x in resources),
                    merge_policy=str(meta.get("merge_policy", defaults.get("merge_policy", "verified_serial_main"))),
                    shard_path=shard_name,
                    catalog=self._catalog_for_path(shard_name),
                )

        completed = set()
        for value in master.get("completed_roots") or []:
            if not isinstance(value, list) or not value:
                raise BacklogValidationError("completed_roots entries must be non-empty inline lists")
            completed.add(str(value[0]))
        policy = master.get("dispatch_policy") or {}
        if not isinstance(policy, dict):
            raise BacklogValidationError("dispatch_policy must be a mapping")
        snapshot = BacklogSnapshot(
            root=self.root,
            tasks=tasks,
            task_fields=tuple(fields),
            completed_roots=frozenset(completed),
            max_writable_builders=int(policy.get("max_writable_builders", 2)),
            merge_lane_concurrency=int(policy.get("merge_lane_concurrency", 1)),
            cloud_requires_explicit_gate=bool(policy.get("cloud_requires_explicit_gate", True)),
            ready_frontier_declared=tuple(str(x) for x in (master.get("ready_frontier") or [])),
            shard_paths=tuple(shards),
            catalog=None,
            raw_master=master,
        )
        self.validate(snapshot)
        if self.catalog is not None:
            selected = {
                task_id: task
                for task_id, task in snapshot.tasks.items()
                if task.catalog == self.catalog
            }
            snapshot = replace(
                snapshot,
                tasks=selected,
                ready_frontier_declared=tuple(
                    task_id
                    for task_id in snapshot.ready_frontier_declared
                    if task_id in selected
                ),
                catalog=self.catalog,
            )
        return snapshot

    @staticmethod
    def _catalog_for_path(path: str) -> str:
        return "orchestrator" if path.startswith("tasks/orchestrator/") else "commerceos"

    @staticmethod
    def _string_list(value, label: str, *, required: bool = False) -> list[str]:
        if not isinstance(value, list) or (required and not value):
            raise BacklogValidationError(f"{label} must be {'a non-empty ' if required else 'a '}list")
        return [str(item) for item in value]

    def validate(self, snapshot: BacklogSnapshot) -> None:
        metadata = snapshot.raw_master.get("execution_metadata") or {}
        for task in snapshot.tasks.values():
            if not re.fullmatch(r"TASK-\d{4,}", task.id):
                raise BacklogValidationError(f"invalid task id: {task.id}")
            if task.maturity not in {"Outline", "Refined", "Ready", "Completed"}:
                raise BacklogValidationError(f"{task.id}: unsupported maturity {task.maturity}")
            if task.lifecycle_state not in {"Backlog", "Active", "Completed", "Blocked"}:
                raise BacklogValidationError(f"{task.id}: unsupported lifecycle {task.lifecycle_state}")
            spec = None
            if task.spec_path:
                spec = _repo_path(
                    snapshot.root,
                    task.spec_path,
                    label=f"{task.id} spec_path",
                    roots=self.SPEC_ROOTS,
                )
            if task.maturity == "Ready":
                if spec is None or not spec.is_file():
                    raise BacklogValidationError(
                        f"{task.id}: Ready task spec does not exist: {task.spec_path}"
                    )
                if task.id not in metadata:
                    raise BacklogValidationError(f"{task.id}: Ready task needs explicit execution_metadata")
            for dependency in task.depends_on:
                if dependency not in snapshot.tasks and dependency not in snapshot.completed_roots:
                    raise BacklogValidationError(f"{task.id}: missing dependency {dependency}")
        self._validate_acyclic(snapshot)
        computed = {task.id for task in self.ready_frontier(snapshot, set())}
        declared = set(snapshot.ready_frontier_declared)
        if computed != declared:
            raise BacklogValidationError(
                f"declared ready_frontier mismatch: declared={sorted(declared)} computed={sorted(computed)}"
            )

    @staticmethod
    def _validate_acyclic(snapshot: BacklogSnapshot) -> None:
        visiting: set[str] = set()
        visited: set[str] = set()

        def visit(task_id: str) -> None:
            if task_id in visited or task_id in snapshot.completed_roots:
                return
            if task_id in visiting:
                raise BacklogValidationError(f"dependency cycle detected at {task_id}")
            visiting.add(task_id)
            for dependency in snapshot.tasks[task_id].depends_on:
                if dependency in snapshot.tasks:
                    visit(dependency)
            visiting.remove(task_id)
            visited.add(task_id)

        for task_id in snapshot.tasks:
            visit(task_id)

    @staticmethod
    def dependency_satisfied(snapshot: BacklogSnapshot, dependency: str) -> bool:
        task = snapshot.tasks.get(dependency)
        return dependency in snapshot.completed_roots or (
            task is not None and task.lifecycle_state == "Completed"
        )

    @classmethod
    def is_dispatchable(
        cls, snapshot: BacklogSnapshot, task: CanonicalTask, active_resources: set[str]
    ) -> bool:
        return (
            task.maturity == "Ready"
            and task.lifecycle_state == "Backlog"
            and not task.gates
            and all(cls.dependency_satisfied(snapshot, dep) for dep in task.depends_on)
            and not (set(task.exclusive_resources) & active_resources)
        )

    @classmethod
    def ready_frontier(
        cls, snapshot: BacklogSnapshot, active_resources: set[str]
    ) -> list[CanonicalTask]:
        return [
            task
            for task in sorted(snapshot.tasks.values(), key=lambda item: item.id)
            if cls.is_dispatchable(snapshot, task, active_resources)
        ]


class BacklogWriter:
    """Lifecycle bookkeeping only; task semantics/maturity are never invented here."""

    def __init__(self, root: Path):
        self.root = root.resolve()

    @_synchronized
    def finalize_task(
        self, snapshot: BacklogSnapshot, task: CanonicalTask, completion_summary: str
    ) -> str:
        source = _repo_path(
            self.root,
            task.spec_path,
            label=f"{task.id} spec_path",
            roots=BacklogReader.SPEC_ROOTS,
        )
        if not source.is_file():
            raise BacklogValidationError(f"{task.id}: task spec missing at {task.spec_path}")
        source_posix = PurePosixPath(task.spec_path)
        if len(source_posix.parts) >= 3 and source_posix.parts[:2] == ("tasks", "orchestrator"):
            completed_root = PurePosixPath("tasks/orchestrator/completed")
        elif len(source_posix.parts) >= 3 and source_posix.parts[:2] == ("tasks", "commerceos"):
            completed_root = PurePosixPath("tasks/commerceos/completed")
        else:
            completed_root = PurePosixPath("tasks/completed")
        completed_relative = str(completed_root / source.name)
        destination = self.root / completed_relative
        catalog_index = self.root / "tasks" / task.catalog / "BACKLOG.md"
        guarded_paths = (source, destination, self.root / task.shard_path, self.root / BacklogReader.MASTER_PATH, catalog_index)
        before = {
            path: path.read_bytes() if path.is_file() else None for path in guarded_paths
        }
        full_snapshot_before = BacklogReader(self.root).load()
        destination.parent.mkdir(parents=True, exist_ok=True)
        try:
            text = _read_text(source)
            text = re.sub(r"^Status:\s*.*$", "Status: Completed", text, count=1, flags=re.MULTILINE)
            text = re.sub(
                r"^Specification maturity:\s*.*$", "Specification maturity: Completed",
                text, count=1, flags=re.MULTILINE,
            )
            text = re.sub(
                r"^Execution permission:\s*.*$", "Execution permission: NO — completed",
                text, count=1, flags=re.MULTILINE,
            )
            if not re.search(r"^Completed:\s*", text, flags=re.MULTILINE):
                updated_text = re.sub(
                    r"^(Created:\s*.*)$", rf"\1\nCompleted: {date.today().isoformat()}",
                    text, count=1, flags=re.MULTILINE,
                )
                if updated_text == text:
                    updated_text = re.sub(
                        r"^(Execution permission:\s*.*)$",
                        rf"\1\nCompleted: {date.today().isoformat()}",
                        text, count=1, flags=re.MULTILINE,
                    )
                text = updated_text
            if "## Completion summary" not in text:
                text = text.rstrip() + "\n\n## Completion summary\n\n" + completion_summary.strip() + "\n"
            _atomic_write_text(destination, text)
            self._update_shard(task, completed_relative)
            self._update_master(full_snapshot_before, task.id)
            self._update_catalog_index(task)
            if destination != source:
                source.unlink()
            self.validate_completed(task, completed_relative)
        except Exception:
            for path, content in before.items():
                if content is None:
                    path.unlink(missing_ok=True)
                else:
                    _atomic_write_text(path, content.decode("utf-8"))
            raise
        return completed_relative

    def _update_catalog_index(self, task: CanonicalTask) -> None:
        path = self.root / "tasks" / task.catalog / "BACKLOG.md"
        if not path.is_file():
            return
        text = _read_text(path)
        completed_line = f"- `{task.id}` — {task.title} (`Completed`)."
        text = "\n".join(
            line for line in text.splitlines()
            if not (line.lstrip().startswith("-") and f"`{task.id}`" in line)
        ) + "\n"
        marker = "Recently completed:\n"
        if marker not in text:
            raise BacklogValidationError(f"{task.catalog}: Recently completed index not found")
        _atomic_write_text(path, text.replace(marker, marker + "\n" + completed_line + "\n", 1))

    def validate_completed(self, task: CanonicalTask, completed_relative: str) -> None:
        source = self.root / task.spec_path
        destination = self.root / completed_relative
        if source != destination and source.exists():
            raise BacklogValidationError(f"{task.id}: backlog completion copy still exists")
        if not destination.is_file():
            raise BacklogValidationError(f"{task.id}: completed task spec is missing")
        text = _read_text(destination)
        required = (
            "Status: Completed", "Specification maturity: Completed",
            "Execution permission: NO — completed", "Completed:", "## Completion summary",
        )
        if not all(value in text for value in required):
            raise BacklogValidationError(f"{task.id}: completed spec metadata is inconsistent")
        snapshot = BacklogReader(self.root).load()
        completed = snapshot.tasks.get(task.id)
        if not completed or completed.lifecycle_state != "Completed" or completed.spec_path != completed_relative:
            raise BacklogValidationError(f"{task.id}: canonical lifecycle/path is inconsistent")
        index = self.root / "tasks" / task.catalog / "BACKLOG.md"
        if completed.maturity != "Completed":
            raise BacklogValidationError(f"{task.id}: canonical maturity is inconsistent")
        if index.is_file():
            entries = [
                line for line in _read_text(index).splitlines()
                if line.lstrip().startswith("-") and f"`{task.id}`" in line
            ]
            if len(entries) != 1 or "(`Completed`)" not in entries[0]:
                raise BacklogValidationError(f"{task.id}: catalog completion index is inconsistent")

    def _update_shard(self, task: CanonicalTask, completed_relative: str) -> None:
        path = _repo_path(
            self.root,
            task.shard_path,
            label=f"{task.id} shard_path",
            roots=BacklogReader.SHARD_ROOTS,
        )
        lines = _read_text(path).splitlines()
        for index, line in enumerate(lines):
            if line.lstrip().startswith("- [") and task.id in line:
                prefix = line[: len(line) - len(line.lstrip())]
                row = parse_inline_sequence(line.strip()[2:].strip())
                row[1] = "Completed"
                row[-1] = completed_relative
                lines[index] = f"{prefix}- {render_inline_sequence(row)}"
                _atomic_write_text(path, "\n".join(lines) + "\n")
                return
        raise BacklogValidationError(f"{task.id}: canonical shard row not found")

    def _update_master(self, snapshot: BacklogSnapshot, completed_task_id: str) -> None:
        path = self.root / BacklogReader.MASTER_PATH
        lines = _read_text(path).splitlines()
        block = next(
            (index for index, line in enumerate(lines) if line == f"  {completed_task_id}:"),
            None,
        )
        if block is None:
            raise BacklogValidationError(f"{completed_task_id}: execution_metadata block not found")
        lifecycle = None
        for index in range(block + 1, min(block + 8, len(lines))):
            if lines[index].startswith("  ") and not lines[index].startswith("    "):
                break
            if lines[index].strip().startswith("lifecycle_state:"):
                lifecycle = index
                break
        if lifecycle is None:
            raise BacklogValidationError(f"{completed_task_id}: lifecycle_state not found")
        lines[lifecycle] = "    lifecycle_state: Completed"

        # Completion may be running against one filtered catalog, but the shared registry's
        # Ready frontier covers every catalog. Recompute from the full canonical snapshot so
        # finalizing a CommerceOS task cannot erase an Orchestrator-ready task (or vice versa).
        full_snapshot = snapshot
        tasks = dict(snapshot.tasks)
        tasks[completed_task_id] = replace(tasks[completed_task_id], lifecycle_state="Completed")
        updated = replace(full_snapshot, tasks=tasks)
        ready = [task.id for task in BacklogReader.ready_frontier(updated, set())]
        start = next((i for i, line in enumerate(lines) if line == "ready_frontier:"), None)
        if start is None:
            raise BacklogValidationError("ready_frontier block not found")
        end = start + 1
        while end < len(lines) and lines[end].startswith("  - "):
            end += 1
        lines[start:end] = ["ready_frontier:", *[f"  - {task_id}" for task_id in ready]]
        _atomic_write_text(path, "\n".join(lines) + "\n")
