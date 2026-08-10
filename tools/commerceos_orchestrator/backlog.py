from __future__ import annotations

import re
from dataclasses import replace
from pathlib import Path, PurePosixPath

from .models import BacklogSnapshot, CanonicalTask
from .yaml_subset import parse_document, render_inline_sequence


class BacklogValidationError(ValueError):
    pass


def _resolve_repo_path(
    root: Path,
    raw_path: str,
    *,
    label: str,
    allowed_roots: tuple[str, ...],
) -> Path:
    """Resolve a canonical Git path without allowing repository escape/traversal."""
    if not raw_path or "\\" in raw_path:
        raise BacklogValidationError(f"{label} must be a non-empty repository-relative POSIX path")
    candidate = PurePosixPath(raw_path)
    if candidate.is_absolute() or any(part in {"", ".", ".."} for part in candidate.parts):
        raise BacklogValidationError(f"{label} must not be absolute or contain traversal: {raw_path}")

    relative = Path(*candidate.parts)
    allowed = tuple(Path(*PurePosixPath(value).parts) for value in allowed_roots)
    if not any(relative == prefix or prefix in relative.parents for prefix in allowed):
        raise BacklogValidationError(
            f"{label} must stay under {', '.join(allowed_roots)}: {raw_path}"
        )

    resolved = (root / relative).resolve()
    try:
        resolved.relative_to(root.resolve())
    except ValueError as exc:
        raise BacklogValidationError(f"{label} escapes repository root: {raw_path}") from exc
    return resolved


class BacklogReader:
    MASTER_PATH = Path("tasks/BACKLOG.v2.yaml")

    def __init__(self, root: Path):
        self.root = root.resolve()

    def load(self) -> BacklogSnapshot:
        master_path = self.root / self.MASTER_PATH
        if not master_path.is_file():
            raise BacklogValidationError(f"missing canonical backlog: {self.MASTER_PATH}")
        master = parse_document(master_path.read_text(encoding="utf-8"))

        task_fields_raw = master.get("task_fields")
        if not isinstance(task_fields_raw, list) or not task_fields_raw:
            raise BacklogValidationError("task_fields must be a non-empty list")
        task_fields = tuple(str(value) for value in task_fields_raw)

        shard_paths_raw = master.get("task_shards")
        if not isinstance(shard_paths_raw, list) or not shard_paths_raw:
            raise BacklogValidationError("task_shards must be a non-empty list")
        shard_paths = tuple(str(value) for value in shard_paths_raw)

        task_defaults = master.get("task_defaults") or {}
        if not isinstance(task_defaults, dict):
            raise BacklogValidationError("task_defaults must be a mapping")
        execution_metadata = master.get("execution_metadata") or {}
        if not isinstance(execution_metadata, dict):
            raise BacklogValidationError("execution_metadata must be a mapping")

        tasks: dict[str, CanonicalTask] = {}
        for shard_path in shard_paths:
            path = _resolve_repo_path(
                self.root,
                shard_path,
                label="backlog shard",
                allowed_roots=("tasks/backlog-v2",),
            )
            if not path.is_file():
                raise BacklogValidationError(f"missing backlog shard: {shard_path}")
            shard = parse_document(path.read_text(encoding="utf-8"))
            rows = shard.get("tasks")
            if not isinstance(rows, list):
                raise BacklogValidationError(f"{shard_path}: tasks must be a list")
            for row in rows:
                if not isinstance(row, list):
                    raise BacklogValidationError(f"{shard_path}: task row must be an inline list")
                if len(row) != len(task_fields):
                    raise BacklogValidationError(
                        f"{shard_path}: task row has {len(row)} values; expected {len(task_fields)}"
                    )
                values = dict(zip(task_fields, row, strict=True))
                task_id = str(values["id"])
                if task_id in tasks:
                    raise BacklogValidationError(f"duplicate canonical task id: {task_id}")
                metadata = execution_metadata.get(task_id) or {}
                if not isinstance(metadata, dict):
                    raise BacklogValidationError(f"execution metadata for {task_id} must be a mapping")
                lifecycle = str(metadata.get("lifecycle_state", task_defaults.get("lifecycle_state", "Backlog")))
                resources = metadata.get("exclusive_resources", task_defaults.get("exclusive_resources", [])) or []
                if not isinstance(resources, list):
                    raise BacklogValidationError(f"exclusive_resources for {task_id} must be a list")
                merge_policy = str(metadata.get("merge_policy", task_defaults.get("merge_policy", "verified_serial_main")))
                task = CanonicalTask(
                    id=task_id,
                    maturity=str(values["maturity"]),
                    type=str(values["type"]),
                    domain=str(values["domain"]),
                    title=str(values["title"]),
                    goal=str(values["goal"]),
                    depends_on=tuple(str(item) for item in (values["depends_on"] or [])),
                    gates=tuple(str(item) for item in (values["gates"] or [])),
                    owner_role=str(values["owner_role"]),
                    model_class=str(values["model_class"]),
                    cloud_verification=str(values["cloud_verification"]),
                    spec_path=str(values["spec_path"]),
                    lifecycle_state=lifecycle,
                    exclusive_resources=tuple(str(item) for item in resources),
                    merge_policy=merge_policy,
                    shard_path=shard_path,
                )
                tasks[task_id] = task

        completed_roots: set[str] = set()
        roots_raw = master.get("completed_roots") or []
        if not isinstance(roots_raw, list):
            raise BacklogValidationError("completed_roots must be a list")
        for value in roots_raw:
            if not isinstance(value, list) or not value:
                raise BacklogValidationError("completed_roots entries must be non-empty inline lists")
            completed_roots.add(str(value[0]))

        dispatch_policy = master.get("dispatch_policy") or {}
        if not isinstance(dispatch_policy, dict):
            raise BacklogValidationError("dispatch_policy must be a mapping")
        ready_declared = tuple(str(value) for value in (master.get("ready_frontier") or []))
        snapshot = BacklogSnapshot(
            root=self.root,
            tasks=tasks,
            task_fields=task_fields,
            completed_roots=frozenset(completed_roots),
            max_writable_builders=int(dispatch_policy.get("max_writable_builders", 2)),
            merge_lane_concurrency=int(dispatch_policy.get("merge_lane_concurrency", 1)),
            cloud_requires_explicit_gate=bool(dispatch_policy.get("cloud_requires_explicit_gate", True)),
            ready_frontier_declared=ready_declared,
            shard_paths=shard_paths,
            raw_master=master,
        )
        self.validate(snapshot)
        return snapshot

    def validate(self, snapshot: BacklogSnapshot) -> None:
        allowed_maturity = {"Outline", "Refined", "Ready"}
        allowed_lifecycle = {"Backlog", "Active", "Completed", "Blocked"}
        for task in snapshot.tasks.values():
            if not re.fullmatch(r"TASK-\d{4,}", task.id):
                raise BacklogValidationError(f"invalid task id: {task.id}")
            if task.maturity not in allowed_maturity:
                raise BacklogValidationError(f"{task.id}: unsupported maturity {task.maturity}")
            if task.lifecycle_state not in allowed_lifecycle:
                raise BacklogValidationError(
                    f"{task.id}: unsupported lifecycle state {task.lifecycle_state}"
                )
            spec_path = None
            if task.spec_path:
                spec_path = _resolve_repo_path(
                    snapshot.root,
                    task.spec_path,
                    label=f"{task.id} spec_path",
                    allowed_roots=("tasks/backlog", "tasks/active", "tasks/completed"),
                )
            if task.maturity == "Ready":
                if spec_path is None:
                    raise BacklogValidationError(f"{task.id}: Ready task has no spec_path")
                if not spec_path.is_file():
                    raise BacklogValidationError(
                        f"{task.id}: Ready task spec does not exist: {task.spec_path}"
                    )
                if task.id not in (snapshot.raw_master.get("execution_metadata") or {}):
                    raise BacklogValidationError(
                        f"{task.id}: Ready task must have explicit execution_metadata"
                    )
            for dependency in task.depends_on:
                if dependency not in snapshot.tasks and dependency not in snapshot.completed_roots:
                    raise BacklogValidationError(
                        f"{task.id}: missing dependency {dependency}"
                    )

        self._validate_acyclic(snapshot)
        computed = tuple(task.id for task in self.ready_frontier(snapshot, active_resources=set()))
        declared_set = set(snapshot.ready_frontier_declared)
        if declared_set != set(computed):
            raise BacklogValidationError(
                "declared ready_frontier does not match mechanically dispatchable tasks: "
                f"declared={sorted(declared_set)} computed={sorted(computed)}"
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
            task = snapshot.tasks[task_id]
            for dep in task.depends_on:
                if dep in snapshot.tasks:
                    visit(dep)
            visiting.remove(task_id)
            visited.add(task_id)

        for task_id in snapshot.tasks:
            visit(task_id)

    @staticmethod
    def dependency_satisfied(snapshot: BacklogSnapshot, dependency: str) -> bool:
        if dependency in snapshot.completed_roots:
            return True
        task = snapshot.tasks.get(dependency)
        return task is not None and task.lifecycle_state == "Completed"

    @classmethod
    def is_dispatchable(
        cls,
        snapshot: BacklogSnapshot,
        task: CanonicalTask,
        active_resources: set[str],
    ) -> bool:
        if task.maturity != "Ready" or task.lifecycle_state != "Backlog":
            return False
        if task.gates:
            return False
        if any(not cls.dependency_satisfied(snapshot, dep) for dep in task.depends_on):
            return False
        if set(task.exclusive_resources) & active_resources:
            return False
        return True

    @classmethod
    def ready_frontier(
        cls, snapshot: BacklogSnapshot, active_resources: set[str]
    ) -> list[CanonicalTask]:
        return [
            task
            for task in sorted(snapshot.tasks.values(), key=lambda value: value.id)
            if cls.is_dispatchable(snapshot, task, active_resources)
        ]


class BacklogWriter:
    """Deterministic lifecycle bookkeeping; never changes task semantics/maturity."""

    def __init__(self, root: Path):
        self.root = root.resolve()

    def finalize_task(
        self,
        snapshot: BacklogSnapshot,
        task: CanonicalTask,
        completion_summary: str,
    ) -> str:
        if not task.spec_path:
            raise BacklogValidationError(f"{task.id}: cannot finalize task without spec_path")
        source = _resolve_repo_path(
            self.root,
            task.spec_path,
            label=f"{task.id} spec_path",
            allowed_roots=("tasks/backlog", "tasks/active", "tasks/completed"),
        )
        if not source.is_file():
            raise BacklogValidationError(f"{task.id}: task spec missing at {task.spec_path}")

        completed_relative = str(Path("tasks/completed") / source.name)
        destination = self.root / completed_relative
        destination.parent.mkdir(parents=True, exist_ok=True)

        text = source.read_text(encoding="utf-8")
        text = re.sub(r"^Status:\s*.*$", "Status: Completed", text, count=1, flags=re.MULTILINE)
        text = re.sub(
            r"^Specification maturity:\s*.*$",
            "Specification maturity: Completed",
            text,
            count=1,
            flags=re.MULTILINE,
        )
        text = re.sub(
            r"^Execution permission:\s*.*$",
            "Execution permission: NO — completed",
            text,
            count=1,
            flags=re.MULTILINE,
        )
        if "## Completion summary" not in text:
            text = text.rstrip() + "\n\n## Completion summary\n\n" + completion_summary.strip() + "\n"
        destination.write_text(text, encoding="utf-8")
        if destination != source:
            source.unlink()

        self._update_shard_spec_path(task, completed_relative)
        self._update_master_lifecycle_and_frontier(snapshot, task.id)
        return completed_relative

    def _update_shard_spec_path(self, task: CanonicalTask, completed_relative: str) -> None:
        path = _resolve_repo_path(
            self.root,
            task.shard_path,
            label=f"{task.id} shard_path",
            allowed_roots=("tasks/backlog-v2",),
        )
        lines = path.read_text(encoding="utf-8").splitlines()
        updated = False
        for index, line in enumerate(lines):
            if line.lstrip().startswith("- [") and task.id in line:
                from .yaml_subset import parse_inline_sequence

                prefix = line[: len(line) - len(line.lstrip())]
                row = parse_inline_sequence(line.strip()[2:].strip())
                row[-1] = completed_relative
                lines[index] = f"{prefix}- {render_inline_sequence(row)}"
                updated = True
                break
        if not updated:
            raise BacklogValidationError(f"{task.id}: canonical shard row not found")
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")

    def _update_master_lifecycle_and_frontier(
        self, snapshot: BacklogSnapshot, completed_task_id: str
    ) -> None:
        path = self.root / BacklogReader.MASTER_PATH
        lines = path.read_text(encoding="utf-8").splitlines()

        task_line = None
        for index, line in enumerate(lines):
            if line == f"  {completed_task_id}:":
                task_line = index
                break
        if task_line is None:
            raise BacklogValidationError(
                f"{completed_task_id}: explicit execution_metadata block not found"
            )
        for index in range(task_line + 1, min(task_line + 8, len(lines))):
            if lines[index].startswith("  ") and not lines[index].startswith("    "):
                break
            if lines[index].strip().startswith("lifecycle_state:"):
                lines[index] = "    lifecycle_state: Completed"
                break
        else:
            raise BacklogValidationError(
                f"{completed_task_id}: lifecycle_state not found in execution_metadata"
            )

        # Recompute frontier from an in-memory lifecycle update without promoting Refined/Outline work.
        updated_tasks = dict(snapshot.tasks)
        updated_tasks[completed_task_id] = replace(
            updated_tasks[completed_task_id], lifecycle_state="Completed"
        )
        updated_snapshot = replace(snapshot, tasks=updated_tasks)
        ready_ids = [
            task.id for task in BacklogReader.ready_frontier(updated_snapshot, active_resources=set())
        ]

        start = None
        end = None
        for index, line in enumerate(lines):
            if line == "ready_frontier:":
                start = index
                end = index + 1
                while end < len(lines) and lines[end].startswith("  - "):
                    end += 1
                break
        if start is None or end is None:
            raise BacklogValidationError("ready_frontier block not found")
        replacement = ["ready_frontier:", *[f"  - {task_id}" for task_id in ready_ids]]
        lines[start:end] = replacement
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
