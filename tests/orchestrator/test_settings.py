from __future__ import annotations

import json
import tempfile
import unittest
from dataclasses import asdict
from pathlib import Path
from unittest.mock import patch

from commerceos_orchestrator.settings import (
    SettingsStore,
    SettingsValidationError,
    default_settings,
    parse_settings,
)


CAPABILITIES = {
    "codex": {
        "available": True,
        "supported_roles": ["planning", "builder", "reviewer", "conflict_resolver"],
    },
    "antigravity": {
        "available": True,
        "supported_roles": ["planning", "builder", "conflict_resolver"],
    },
}


class SettingsTests(unittest.TestCase):
    def test_defaults_preserve_sol_planning_and_terra_coding(self):
        value = default_settings()
        self.assertEqual(value.profiles["planning"].model, "gpt-5.6-sol")
        self.assertEqual(value.profiles["builder"].model, "gpt-5.6-terra")
        self.assertEqual(value.profiles["reviewer"].provider, "codex")
        self.assertFalse(value.allow_cloud)

    def test_antigravity_builder_is_valid_but_reviewer_fails_closed(self):
        raw = asdict(default_settings())
        raw["profiles"]["builder"] = {
            "provider": "antigravity",
            "model": "gemini-3.1-pro",
            "reasoning_effort": "high",
            "service_tier": "priority",
        }
        value = parse_settings(raw, capabilities=CAPABILITIES)
        self.assertEqual(value.profiles["builder"].provider, "antigravity")
        self.assertEqual(value.profiles["builder"].reasoning_effort, "medium")
        raw["profiles"]["reviewer"]["provider"] = "antigravity"
        with self.assertRaisesRegex(SettingsValidationError, "cannot preserve"):
            parse_settings(raw, capabilities=CAPABILITIES)

    def test_arbitrary_model_or_runtime_values_are_rejected(self):
        raw = asdict(default_settings())
        raw["profiles"]["builder"]["model"] = "terra; Remove-Item"
        with self.assertRaisesRegex(SettingsValidationError, "unsupported characters"):
            parse_settings(raw, capabilities=CAPABILITIES)
        raw = asdict(default_settings())
        raw["max_builders"] = 3
        with self.assertRaisesRegex(SettingsValidationError, "between 1 and 2"):
            parse_settings(raw, capabilities=CAPABILITIES)

    def test_store_writes_atomically_and_reset_restores_defaults(self):
        with tempfile.TemporaryDirectory() as td:
            store = SettingsStore(Path(td))
            raw = asdict(default_settings())
            raw["catalog"] = "orchestrator"
            with patch(
                "commerceos_orchestrator.settings.provider_capabilities",
                return_value=CAPABILITIES,
            ):
                saved = store.save(raw)
                self.assertEqual(store.load().catalog, "orchestrator")
            self.assertEqual(json.loads(store.path.read_text())["schema_version"], 1)
            self.assertFalse(list(store.path.parent.glob("settings-*.tmp")))
            self.assertEqual(store.reset().catalog, "commerceos")
            self.assertFalse(store.path.exists())


if __name__ == "__main__":
    unittest.main()
