from __future__ import annotations

import json
import os
import shlex
import signal
import subprocess
import sys
import time
import uuid
from contextlib import contextmanager
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterator

from .state import RunStateStore, utc_now


class RuntimeControlError(RuntimeError):
    pass


@dataclass(frozen=True)
class WorkerRegistration:
    pid: int
    token: str
    repository: str
    catalog: str
    state_path: str
    command: str
    started_at: str


class WorkerRuntimeRegistry:
    """Repository/catalog-scoped identity for the one active scheduler worker."""

    FILE_NAME = "worker-runtime.json"

    def __init__(self, root: Path, state_path: Path, catalog: str):
        self.root = root.resolve()
        self.state_path = state_path.resolve()
        self.catalog = catalog
        self.path = self.state_path.parent / self.FILE_NAME

    @contextmanager
    def registered_worker(
        self, command: str, *, token: str | None = None
    ) -> Iterator[WorkerRegistration]:
        registration = WorkerRegistration(
            pid=os.getpid(),
            token=token or uuid.uuid4().hex,
            repository=str(self.root),
            catalog=self.catalog,
            state_path=str(self.state_path),
            command=command,
            started_at=utc_now(),
        )
        current = self.load(required=False)
        if current and current.pid != registration.pid and self._pid_alive(current.pid):
            if self._identity_matches(current):
                raise RuntimeControlError(
                    f"worker already running for catalog {self.catalog} (PID {current.pid})"
                )
            raise RuntimeControlError("runtime registration points to an unrelated live process")
        self._write(registration)
        try:
            yield registration
        finally:
            self.clear(registration.token)

    def load(self, *, required: bool = True) -> WorkerRegistration | None:
        if not self.path.is_file():
            if required:
                raise RuntimeControlError("no registered Orchestrator worker is running")
            return None
        try:
            payload = json.loads(self.path.read_text(encoding="utf-8"))
            registration = WorkerRegistration(**payload)
        except (OSError, json.JSONDecodeError, TypeError, ValueError) as exc:
            raise RuntimeControlError("worker runtime registration is malformed") from exc
        if (
            Path(registration.repository).resolve() != self.root
            or Path(registration.state_path).resolve() != self.state_path
            or registration.catalog != self.catalog
            or registration.pid < 1
            or not registration.token
            or registration.command not in {"run", "resume"}
        ):
            raise RuntimeControlError("worker runtime registration does not match this repository/catalog")
        return registration

    def force_stop(self, state: RunStateStore) -> dict[str, object]:
        try:
            registration = self.load()
        except RuntimeControlError as exc:
            code = "REGISTRATION_MISSING" if not self.path.is_file() else "REGISTRATION_INVALID"
            self._record_force_stop_rejection(state, code, str(exc))
            raise
        assert registration is not None
        if registration.pid == os.getpid():
            self._record_force_stop_rejection(
                state, "SELF_TARGET_REJECTED", "registered PID is the control process"
            )
            raise RuntimeControlError("refusing to terminate the dashboard/control process")
        if not self._pid_alive(registration.pid):
            self._record_force_stop_rejection(
                state, "WORKER_NOT_RUNNING", "registered worker process is not running"
            )
            raise RuntimeControlError("registered Orchestrator worker is no longer running")
        if not self._identity_matches(registration):
            self._record_force_stop_rejection(
                state, "IDENTITY_MISMATCH", "registered PID failed worker identity validation"
            )
            raise RuntimeControlError("registered PID no longer identifies the expected Orchestrator worker")

        fence = state.begin_force_stop(registration.pid)
        try:
            self._terminate_tree(registration.pid)
            deadline = time.monotonic() + 5
            while self._pid_alive(registration.pid) and time.monotonic() < deadline:
                time.sleep(0.05)
            if self._pid_alive(registration.pid):
                raise RuntimeControlError("Orchestrator worker process tree did not terminate")
        except Exception as exc:
            state.abort_force_stop(fence, repr(exc))
            raise

        state.complete_force_stop(fence)
        self.clear(registration.token)
        return {
            "stopped_pid": registration.pid,
            "preserved_tasks": list(fence.preserved_tasks),
            "control_state": "STOPPED",
            "worktrees_removed": False,
        }

    @staticmethod
    def _record_force_stop_rejection(state: RunStateStore, code: str, detail: str) -> None:
        state.add_event(
            None,
            "FORCE_STOP_REJECTED",
            json.dumps({"code": code, "detail": detail[:300]}, sort_keys=True),
        )

    def status(self) -> dict[str, object]:
        try:
            registration = self.load(required=False)
        except RuntimeControlError as exc:
            return {"state": "INVALID", "pid": None, "detail": str(exc)}
        if registration is None:
            return {"state": "UNREGISTERED", "pid": None, "detail": None}
        if not self._pid_alive(registration.pid):
            return {
                "state": "STALE",
                "pid": registration.pid,
                "detail": "registered worker process is not running",
            }
        if not self._identity_matches(registration):
            return {
                "state": "IDENTITY_MISMATCH",
                "pid": registration.pid,
                "detail": "registered PID does not match the expected worker identity",
            }
        return {"state": "RUNNING", "pid": registration.pid, "detail": None}

    def clear(self, token: str) -> None:
        registration = self.load(required=False)
        if registration and registration.token == token:
            self.path.unlink(missing_ok=True)

    def _write(self, registration: WorkerRegistration) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.path.with_suffix(f".{registration.token}.tmp")
        temporary.write_text(json.dumps(asdict(registration), indent=2), encoding="utf-8")
        temporary.replace(self.path)

    @staticmethod
    def _pid_alive(pid: int) -> bool:
        stat = Path(f"/proc/{pid}/stat")
        if stat.is_file():
            try:
                if stat.read_text(encoding="utf-8").split()[2] == "Z":
                    return False
            except (OSError, IndexError):
                pass
        try:
            os.kill(pid, 0)
            return True
        except (OSError, ProcessLookupError):
            return False

    def _identity_matches(self, registration: WorkerRegistration) -> bool:
        try:
            args = self._split_command_line(self._command_line(registration.pid))
        except (ValueError, OSError):
            return False
        expected_script = (self.root / "tools" / "orchestrator.py").resolve()
        script_indexes = [
            index
            for index, value in enumerate(args)
            if Path(value).is_absolute() and Path(value).resolve() == expected_script
        ]
        if len(script_indexes) != 1 or args[-1] != registration.command:
            return False
        tail = args[script_indexes[0] + 1 : -1]
        return (
            self._exact_option(tail, "--repo") == str(self.root)
            and self._exact_option(tail, "--state") == str(self.state_path)
            and self._exact_option(tail, "--catalog") == registration.catalog
            and self._exact_option(tail, "--worker-token") == registration.token
        )

    @staticmethod
    def _exact_option(args: list[str], name: str) -> str | None:
        values: list[str] = []
        for index, value in enumerate(args):
            if value == name and index + 1 < len(args):
                values.append(args[index + 1])
            elif value.startswith(name + "="):
                values.append(value.split("=", 1)[1])
        return values[0] if len(values) == 1 else None

    @staticmethod
    def _split_command_line(command_line: str) -> list[str]:
        if not command_line:
            return []
        if os.name != "nt":
            return shlex.split(command_line)
        import ctypes

        argc = ctypes.c_int()
        shell32 = ctypes.windll.shell32
        shell32.CommandLineToArgvW.argtypes = [ctypes.c_wchar_p, ctypes.POINTER(ctypes.c_int)]
        shell32.CommandLineToArgvW.restype = ctypes.POINTER(ctypes.c_wchar_p)
        argv = shell32.CommandLineToArgvW(command_line, ctypes.byref(argc))
        if not argv:
            raise OSError("CommandLineToArgvW failed")
        try:
            return [argv[index] for index in range(argc.value)]
        finally:
            ctypes.windll.kernel32.LocalFree.argtypes = [ctypes.c_void_p]
            ctypes.windll.kernel32.LocalFree(argv)

    @staticmethod
    def _command_line(pid: int) -> str:
        if os.name == "nt":
            result = subprocess.run(
                [
                    "powershell.exe",
                    "-NoProfile",
                    "-NonInteractive",
                    "-Command",
                    f"(Get-CimInstance Win32_Process -Filter 'ProcessId = {pid}').CommandLine",
                ],
                text=True,
                capture_output=True,
                check=False,
                timeout=5,
            )
            return result.stdout.strip()
        proc = Path(f"/proc/{pid}/cmdline")
        if proc.is_file():
            return proc.read_bytes().replace(b"\0", b" ").decode("utf-8", errors="replace")
        result = subprocess.run(
            ["ps", "-p", str(pid), "-o", "command="],
            text=True,
            capture_output=True,
            check=False,
            timeout=5,
        )
        return result.stdout.strip()

    @staticmethod
    def _terminate_tree(pid: int) -> None:
        if os.name == "nt":
            result = subprocess.run(
                ["taskkill.exe", "/PID", str(pid), "/T", "/F"],
                text=True,
                capture_output=True,
                check=False,
                timeout=15,
            )
            if result.returncode != 0 and WorkerRuntimeRegistry._pid_alive(pid):
                raise RuntimeControlError(result.stderr.strip() or result.stdout.strip())
            return

        result = subprocess.run(
            ["ps", "-eo", "pid=,ppid="], text=True, capture_output=True, check=False, timeout=5
        )
        children: dict[int, list[int]] = {}
        for line in result.stdout.splitlines():
            try:
                child, parent = (int(value) for value in line.split())
            except (ValueError, TypeError):
                continue
            children.setdefault(parent, []).append(child)

        ordered: list[int] = []

        def visit(parent: int) -> None:
            for child in children.get(parent, []):
                visit(child)
                ordered.append(child)

        visit(pid)
        ordered.append(pid)
        for target in ordered:
            try:
                os.kill(target, signal.SIGTERM)
            except ProcessLookupError:
                pass
        time.sleep(0.2)
        for target in ordered:
            if WorkerRuntimeRegistry._pid_alive(target):
                try:
                    os.kill(target, signal.SIGKILL)
                except ProcessLookupError:
                    pass
