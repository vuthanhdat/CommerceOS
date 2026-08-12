from __future__ import annotations

import unittest
import ast
from pathlib import Path

REQUIRED_E2E_MATRIX = {
    "happy_path": ("test_service_pipeline.py", "test_builder_verify_review_merge_and_bookkeeping"),
    "verification_repair": ("test_service_pipeline.py", "test_verification_failure_enters_bounded_fix_loop"),
    "reviewer_repair": ("test_service_pipeline.py", "test_reviewer_finding_returns_to_builder"),
    "non_builder_routing": ("test_service_pipeline.py", "test_non_builder_review_finding_routes_to_planning_root"),
    "malformed_output": ("test_service_pipeline.py", "test_malformed_production_stage_output_fails_closed_before_verification"),
    "retry_exhaustion": ("test_service_pipeline.py", "test_verification_retry_exhaustion_blocks_without_review_or_integration"),
    "completion_failure": ("test_service_pipeline.py", "test_late_completion_failures_never_leave_an_unhandled_push"),
    "completion_recovery": ("test_service_pipeline.py", "test_valid_finalization_entry_gate_resumes_after_restart"),
    "graceful_stop": ("test_service_stop.py", "test_stop_drains_two_active_tasks_and_does_not_start_third"),
    "restart": ("test_service_stop.py", "test_persisted_stop_survives_restart_without_fresh_dispatch"),
}


class WorkflowE2EMatrixContractTests(unittest.TestCase):
    def test_every_required_scenario_is_an_executable_test(self):
        self.assertEqual(
            set(REQUIRED_E2E_MATRIX),
            {
                "happy_path", "verification_repair", "reviewer_repair",
                "non_builder_routing", "malformed_output", "retry_exhaustion",
                "completion_failure", "completion_recovery", "graceful_stop", "restart",
            },
        )
        root = Path(__file__).parent
        for scenario, (filename, method) in REQUIRED_E2E_MATRIX.items():
            with self.subTest(scenario=scenario):
                tree = ast.parse((root / filename).read_text(encoding="utf-8"))
                methods = {
                    node.name
                    for parent in ast.walk(tree)
                    if isinstance(parent, ast.ClassDef)
                    for node in parent.body
                    if isinstance(node, ast.FunctionDef)
                }
                self.assertIn(method, methods)


if __name__ == "__main__":
    unittest.main()
