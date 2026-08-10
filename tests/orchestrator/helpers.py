from __future__ import annotations

import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[2] / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))


def write_backlog(root: Path, rows: list[str], *, ready: list[str], metadata: dict[str, str] | None = None, completed_roots: list[str] | None = None) -> None:
    (root / "tasks/backlog-v2").mkdir(parents=True, exist_ok=True)
    (root / "tasks/backlog").mkdir(parents=True, exist_ok=True)
    metadata = metadata or {}
    completed_roots = completed_roots or ["TASK-0001"]
    ids = [row.split(",", 1)[0].strip().lstrip("[") for row in rows]
    for task_id in ids:
        spec = root / "tasks/backlog" / f"{task_id}-spec.md"
        spec.write_text(
            f"# {task_id}\n\nStatus: Backlog\nSpecification maturity: Ready\nExecution permission: YES\n\n## Goal\nTest\n",
            encoding="utf-8",
        )
    meta_lines = []
    for task_id in ids:
        resource = metadata.get(task_id, f"resource-{task_id}")
        meta_lines.extend(
            [
                f"  {task_id}:",
                "    lifecycle_state: Backlog",
                f"    exclusive_resources: [{resource}]" if resource else "    exclusive_resources: []",
                "    merge_policy: verified_serial_main",
            ]
        )
    root_lines = [f"  - [{task}, tasks/completed/{task}.md]" for task in completed_roots]
    frontier_lines = [f"  - {task}" for task in ready]
    (root / "tasks/BACKLOG.v2.yaml").write_text(
        "\n".join(
            [
                "schema_version: 1",
                "authority: tasks/BACKLOG.v2.yaml",
                "task_fields:",
                "  - id",
                "  - maturity",
                "  - type",
                "  - domain",
                "  - title",
                "  - goal",
                "  - depends_on",
                "  - gates",
                "  - owner_role",
                "  - model_class",
                "  - cloud_verification",
                "  - spec_path",
                "task_defaults:",
                "  lifecycle_state: Backlog",
                "  exclusive_resources: []",
                "  merge_policy: verified_serial_main",
                "execution_metadata:",
                *meta_lines,
                "dispatch_policy:",
                "  max_writable_builders: 2",
                "  merge_lane_concurrency: 1",
                "  cloud_requires_explicit_gate: true",
                "completed_roots:",
                *root_lines,
                "task_shards:",
                "  - tasks/backlog-v2/00.yaml",
                "ready_frontier:",
                *frontier_lines,
            ]
        ) + "\n",
        encoding="utf-8",
    )
    shard_rows = []
    for row in rows:
        task_id = row.split(",", 1)[0].strip().lstrip("[")
        spec_path = f"tasks/backlog/{task_id}-spec.md"
        values = row.rstrip().rstrip("]") + f', "{spec_path}"]'
        shard_rows.append(f"  - {values}")
    (root / "tasks/backlog-v2/00.yaml").write_text(
        "tasks:\n" + "\n".join(shard_rows) + "\n", encoding="utf-8"
    )


def row(task_id: str, *, maturity: str = "Ready", deps: str = "[]", gates: str = "[]", cloud: str = '"no"', title: str | None = None) -> str:
    title = title or task_id
    return (
        f'[{task_id}, {maturity}, "engineering", "Harness", "{title}", "goal", '
        f'{deps}, {gates}, "Builder", "default", {cloud}'
    )
