from __future__ import annotations

import json
import re
import threading
from datetime import datetime, timezone
from pathlib import Path


_TASK_ID = re.compile(r"TASK-\d{4,}")


class LiveAgentFeed:
    """Append-only local JSONL feed for live Codex observability."""

    def __init__(self, logs_root: Path):
        self.logs_root = logs_root.resolve()
        self.logs_root.mkdir(parents=True, exist_ok=True)
        self._lock = threading.Lock()

    def path_for(self, task_id: str) -> Path:
        if not _TASK_ID.fullmatch(task_id):
            raise ValueError(f"invalid task id for live feed: {task_id}")
        path = (self.logs_root / f"{task_id}-live.jsonl").resolve()
        if path.parent != self.logs_root:
            raise ValueError("live feed path escaped logs root")
        return path

    def publish(self, task_id: str, kind: str, **payload: object) -> Path:
        record = {
            "at": datetime.now(timezone.utc).isoformat(),
            "task_id": task_id,
            "kind": kind,
            **payload,
        }
        path = self.path_for(task_id)
        encoded = json.dumps(record, ensure_ascii=False, separators=(",", ":"))
        with self._lock:
            with path.open("a", encoding="utf-8", newline="\n") as handle:
                handle.write(encoded + "\n")
                handle.flush()
        return path
