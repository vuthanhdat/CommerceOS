#!/usr/bin/env python3
"""Deterministic verification used by the local Task Orchestrator.

This intentionally verifies repository/application contracts without running the
Orchestrator's own unit/integration tests. Orchestrator self-tests remain part of
`scripts/harness_check.py` and the Harness Verification CI workflow.

Keeping these boundaries separate prevents a task implementation from being
blocked by an unrelated test-fixture defect in the orchestration tool itself,
while preserving full tool coverage at repository/CI level.
"""

from __future__ import annotations

import sys

from harness_check import (
    ROOT,
    check_adrs,
    check_development_strategy,
    check_h0_definition,
    check_local_markdown_links,
    check_planning_factory,
    check_required_files,
    check_task_specs,
    run_application_checks,
)


def main() -> int:
    errors: list[str] = []

    check_required_files(errors)
    check_task_specs(errors)
    check_adrs(errors)
    check_h0_definition(errors)
    check_development_strategy(errors)
    check_planning_factory(errors)

    for relative in ["README.md", "AGENTS.md"]:
        check_local_markdown_links(ROOT / relative, errors)

    if not errors:
        run_application_checks(errors)

    if errors:
        print("CommerceOS Task Verification: FAIL")
        for index, error in enumerate(errors, start=1):
            print(f"{index}. {error}")
        return 1

    print("CommerceOS Task Verification: PASS")
    print("Task/ADR structure checks: PASS")
    print("README/AGENTS local-link checks: PASS")
    print("Phase H0 definition check: PASS")
    print("Environment/IaC/Free-Tier/Codex strategy checks: PASS")
    print("Planning factory/task-maturity/agent-role checks: PASS")
    print("Application build/test/architecture/CDK checks: PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
