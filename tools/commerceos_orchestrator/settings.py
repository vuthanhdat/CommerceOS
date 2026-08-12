from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import tempfile
from dataclasses import asdict, dataclass
from functools import lru_cache
from pathlib import Path

from .agents import (
    CODING_CODEX_PROFILE,
    PLANNING_CODEX_PROFILE,
    CodexExecutionProfile,
    antigravity_supports_reviewer_audit,
    antigravity_supports_stream_json,
)


SETTINGS_VERSION = 1
ROLE_KEYS = ("planning", "builder", "reviewer", "conflict_resolver")
PROVIDERS = ("codex", "antigravity")
MODEL_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._:/-]{0,99}$")
REASONING_EFFORTS = ("low", "medium", "high", "xhigh")
SERVICE_TIERS = ("standard", "priority")


class SettingsValidationError(ValueError):
    pass


@dataclass(frozen=True)
class AgentProfileSettings:
    provider: str
    model: str
    reasoning_effort: str = "medium"
    service_tier: str = "standard"

    def codex_profile(self) -> CodexExecutionProfile:
        return CodexExecutionProfile(
            self.model,
            reasoning_effort=self.reasoning_effort,
            service_tier=self.service_tier,
        )


@dataclass(frozen=True)
class LocalOrchestratorSettings:
    catalog: str
    max_builders: int
    max_fix_attempts: int
    allow_cloud: bool
    profiles: dict[str, AgentProfileSettings]
    schema_version: int = SETTINGS_VERSION


def default_settings() -> LocalOrchestratorSettings:
    planning = AgentProfileSettings(
        "codex",
        PLANNING_CODEX_PROFILE.model,
        PLANNING_CODEX_PROFILE.reasoning_effort,
        PLANNING_CODEX_PROFILE.service_tier,
    )
    coding = AgentProfileSettings(
        "codex",
        CODING_CODEX_PROFILE.model,
        CODING_CODEX_PROFILE.reasoning_effort,
        CODING_CODEX_PROFILE.service_tier,
    )
    return LocalOrchestratorSettings(
        catalog="commerceos",
        max_builders=2,
        max_fix_attempts=2,
        allow_cloud=False,
        profiles={
            "planning": planning,
            "builder": coding,
            "reviewer": coding,
            "conflict_resolver": coding,
        },
    )


def discover_antigravity() -> str | None:
    found = shutil.which("agy")
    if found:
        return found
    if os.name == "nt":
        local_app_data = os.environ.get("LOCALAPPDATA")
        if local_app_data:
            candidate = Path(local_app_data) / "agy" / "bin" / "agy.exe"
            if candidate.is_file():
                return str(candidate)
    return None


def _probe_version(executable: str | None) -> str | None:
    if not executable:
        return None
    try:
        result = subprocess.run(
            [executable, "--version"],
            text=True,
            capture_output=True,
            check=False,
            timeout=3,
        )
    except (OSError, subprocess.TimeoutExpired):
        return None
    value = (result.stdout or result.stderr).strip().splitlines()
    return value[0][:80] if result.returncode == 0 and value else None


def _probe_antigravity_models(executable: str | None) -> list[dict[str, str]]:
    if not executable:
        return []
    try:
        result = subprocess.run(
            [executable, "models"],
            text=True,
            capture_output=True,
            check=False,
            timeout=10,
        )
    except (OSError, subprocess.TimeoutExpired):
        return []
    models: list[dict[str, str]] = []
    for line in result.stdout.splitlines():
        identifier, separator, label = line.partition("\t")
        if separator and MODEL_PATTERN.fullmatch(identifier.strip()):
            models.append({"id": identifier.strip(), "label": label.strip()[:100]})
    return models


@lru_cache(maxsize=1)
def provider_capabilities() -> dict[str, dict[str, object]]:
    codex = shutil.which("codex")
    agy = discover_antigravity()
    agy_stream = antigravity_supports_stream_json(agy)
    agy_reviewer_audit = antigravity_supports_reviewer_audit(agy)
    return {
        "codex": {
            "available": bool(codex),
            "executable": codex,
            "version": _probe_version(codex),
            "supported_roles": list(ROLE_KEYS),
            "supports_reasoning_effort": True,
            "supports_service_tier": True,
            "models": [
                {"id": "gpt-5.6-sol", "label": "GPT-5.6 Sol"},
                {"id": "gpt-5.6-terra", "label": "GPT-5.6 Terra"},
            ],
        },
        "antigravity": {
            "available": bool(agy),
            "executable": agy,
            "version": _probe_version(agy),
            "supported_roles": list(ROLE_KEYS) if agy_reviewer_audit else [
                "planning", "builder", "conflict_resolver"
            ],
            "supports_reasoning_effort": agy_stream,
            "supports_service_tier": False,
            "supports_stream_json": agy_stream,
            "supports_reviewer_command_audit": agy_reviewer_audit,
            "models": _probe_antigravity_models(agy),
        },
    }


def _validate_profile(
    role: str,
    raw: object,
    capabilities: dict[str, dict[str, object]],
) -> AgentProfileSettings:
    if not isinstance(raw, dict):
        raise SettingsValidationError(f"{role}: profile must be an object")
    provider = str(raw.get("provider", ""))
    if provider not in PROVIDERS:
        raise SettingsValidationError(f"{role}: unsupported provider {provider!r}")
    capability = capabilities[provider]
    if not capability["available"]:
        raise SettingsValidationError(f"{role}: provider {provider} is not available")
    if role not in capability["supported_roles"]:
        raise SettingsValidationError(
            f"{role}: provider {provider} cannot preserve this role contract"
        )
    model = str(raw.get("model", "")).strip()
    if provider == "codex" and not model:
        raise SettingsValidationError(f"{role}: Codex model is required")
    if model and not MODEL_PATTERN.fullmatch(model):
        raise SettingsValidationError(f"{role}: model contains unsupported characters")
    known_models = capability.get("models") or []
    known_ids = {
        item.get("id") for item in known_models if isinstance(item, dict) and item.get("id")
    }
    if provider == "antigravity" and model and known_ids and model not in known_ids:
        raise SettingsValidationError(f"{role}: model is not reported by Antigravity")
    reasoning = str(raw.get("reasoning_effort", "medium"))
    service = str(raw.get("service_tier", "standard"))
    if reasoning not in REASONING_EFFORTS:
        raise SettingsValidationError(f"{role}: unsupported reasoning effort")
    if service not in SERVICE_TIERS:
        raise SettingsValidationError(f"{role}: unsupported service tier")
    if provider == "antigravity":
        if not capability.get("supports_reasoning_effort", False):
            reasoning = "medium"
        service = "standard"
    return AgentProfileSettings(provider, model, reasoning, service)


def parse_settings(
    raw: object,
    *,
    capabilities: dict[str, dict[str, object]] | None = None,
) -> LocalOrchestratorSettings:
    if not isinstance(raw, dict):
        raise SettingsValidationError("settings must be an object")
    if raw.get("schema_version", SETTINGS_VERSION) != SETTINGS_VERSION:
        raise SettingsValidationError("unsupported settings schema version")
    catalog = str(raw.get("catalog", ""))
    if catalog not in {"commerceos", "orchestrator"}:
        raise SettingsValidationError("catalog must be commerceos or orchestrator")
    max_builders = raw.get("max_builders")
    max_fix_attempts = raw.get("max_fix_attempts")
    allow_cloud = raw.get("allow_cloud")
    if type(max_builders) is not int or not 1 <= max_builders <= 2:
        raise SettingsValidationError("max_builders must be between 1 and 2")
    if type(max_fix_attempts) is not int or not 0 <= max_fix_attempts <= 10:
        raise SettingsValidationError("max_fix_attempts must be between 0 and 10")
    if type(allow_cloud) is not bool:
        raise SettingsValidationError("allow_cloud must be true or false")
    profiles = raw.get("profiles")
    if not isinstance(profiles, dict) or set(profiles) != set(ROLE_KEYS):
        raise SettingsValidationError("profiles must contain every supported role exactly once")
    caps = capabilities or provider_capabilities()
    return LocalOrchestratorSettings(
        catalog=catalog,
        max_builders=max_builders,
        max_fix_attempts=max_fix_attempts,
        allow_cloud=allow_cloud,
        profiles={role: _validate_profile(role, profiles[role], caps) for role in ROLE_KEYS},
    )


class SettingsStore:
    def __init__(self, root: Path):
        self.path = root.resolve() / ".commerceos" / "orchestrator" / "settings.json"

    def load(self) -> LocalOrchestratorSettings:
        if not self.path.is_file():
            return default_settings()
        try:
            raw = json.loads(self.path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            raise SettingsValidationError(f"unable to read local settings: {exc}") from exc
        return parse_settings(raw)

    def save(self, raw: object) -> LocalOrchestratorSettings:
        value = parse_settings(raw)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        payload = json.dumps(asdict(value), ensure_ascii=False, indent=2) + "\n"
        descriptor, temporary = tempfile.mkstemp(
            dir=self.path.parent,
            prefix="settings-",
            suffix=".tmp",
            text=True,
        )
        try:
            with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as handle:
                handle.write(payload)
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(temporary, self.path)
        finally:
            if os.path.exists(temporary):
                os.unlink(temporary)
        return value

    def reset(self) -> LocalOrchestratorSettings:
        if self.path.exists():
            self.path.unlink()
        return default_settings()

    def public_view(self) -> dict[str, object]:
        settings = self.load()
        return {
            "settings": asdict(settings),
            "defaults": asdict(default_settings()),
            "capabilities": provider_capabilities(),
            "restart_required": False,
            "path": str(self.path),
        }
