#!/usr/bin/env python3
"""Lightweight repository-level Harness Engineering checks for CommerceOS.

H0 intentionally uses only the Python standard library so the check can run on a
clean machine and in GitHub Actions before the application toolchain exists.
As implementation arrives, this script becomes the stable entry point that also
invokes build, lint, test, architecture, IaC, and security checks.
"""

from __future__ import annotations

import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

REQUIRED_FILES = [
    "README.md",
    "AGENTS.md",
    "docs/00-product-definition.md",
    "docs/01-non-functional-requirements.md",
    "docs/02-business-domains.md",
    "docs/03-serverless-architecture.md",
    "docs/04-cost-model.md",
    "docs/05-product-data-ingestion.md",
    "docs/06-mock-payment-provider.md",
    "docs/07-delivery-roadmap.md",
    "docs/development/00-engineering-harness.md",
    "docs/development/01-task-specification.md",
    "docs/development/02-definition-of-done.md",
    "docs/development/03-architecture-rules.md",
    "docs/development/04-testing-strategy.md",
    "docs/development/05-adr-process.md",
    "docs/development/06-agent-workflow.md",
    "docs/development/07-harness-improvement.md",
    "docs/development/08-h0-exit-checklist.md",
    "docs/development/09-development-environment.md",
    "docs/development/10-testing-and-cloud-verification.md",
    "docs/development/11-ci-cd-pipeline.md",
    "docs/development/12-infrastructure-as-code.md",
    "docs/development/13-free-tier-and-credit-guardrails.md",
    "docs/development/14-codex-multi-agent-and-worktrees.md",
    "docs/adr/ADR-000-template.md",
    "docs/adr/ADR-001-aws-cdk-infrastructure-as-code.md",
    "tasks/TASK-TEMPLATE.md",
    "CommerceOS.slnx",
    "Directory.Build.props",
    "Directory.Packages.props",
    "global.json",
    "package.json",
    "package-lock.json",
    "cdk.json",
    "src/CommerceOS.Api/CommerceOS.Api.csproj",
    "infra/CommerceOS.Cdk/CommerceOS.Cdk.csproj",
    "tests/CommerceOS.ArchitectureTests/CommerceOS.ArchitectureTests.csproj",
    "tools/commerceos.py",
]

TASK_REQUIRED_HEADINGS = [
    "## Goal",
    "## Business context",
    "## In scope",
    "## Out of scope",
    "## Acceptance criteria",
    "## Architecture impact",
    "## Security and tenant impact",
    "## Reliability and idempotency impact",
    "## Observability impact",
    "## Cost impact",
    "## Test plan",
]

ADR_REQUIRED_HEADINGS = [
    "## Context",
    "## Decision",
    "## Alternatives considered",
    "## Consequences",
    "## Security and tenant impact",
    "## Reliability and operability impact",
    "## Cost impact",
    "## Reversibility / migration",
    "## Validation",
]

MARKDOWN_LINK_RE = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")


def fail(message: str, errors: list[str]) -> None:
    errors.append(message)


def check_required_files(errors: list[str]) -> None:
    for relative in REQUIRED_FILES:
        if not (ROOT / relative).is_file():
            fail(f"Missing required harness file: {relative}", errors)


def check_local_markdown_links(path: Path, errors: list[str]) -> None:
    if not path.exists():
        return

    text = path.read_text(encoding="utf-8")
    for raw_target in MARKDOWN_LINK_RE.findall(text):
        target = raw_target.strip().split("#", 1)[0].strip()
        if not target or target.startswith(("http://", "https://", "mailto:", "#")):
            continue

        target = target.split("?", 1)[0]
        resolved = (path.parent / target).resolve()
        try:
            resolved.relative_to(ROOT.resolve())
        except ValueError:
            fail(f"Local link escapes repository in {path.relative_to(ROOT)}: {raw_target}", errors)
            continue

        if not resolved.exists():
            fail(
                f"Broken local markdown link in {path.relative_to(ROOT)}: {raw_target}",
                errors,
            )


def check_task_specs(errors: list[str]) -> None:
    tasks_root = ROOT / "tasks"
    if not tasks_root.exists():
        return

    for path in tasks_root.rglob("*.md"):
        if path.name == "TASK-TEMPLATE.md":
            continue

        if path.parent.name not in {"backlog", "active", "completed"}:
            continue

        if not re.match(r"^TASK-\d{4,}-.+\.md$", path.name):
            fail(
                f"Task filename must match TASK-0001-description.md: {path.relative_to(ROOT)}",
                errors,
            )

        text = path.read_text(encoding="utf-8")
        for heading in TASK_REQUIRED_HEADINGS:
            if heading not in text:
                fail(f"Task missing heading '{heading}': {path.relative_to(ROOT)}", errors)


def check_adrs(errors: list[str]) -> None:
    adr_root = ROOT / "docs" / "adr"
    if not adr_root.exists():
        return

    for path in adr_root.glob("ADR-*.md"):
        if path.name == "ADR-000-template.md":
            continue

        if not re.match(r"^ADR-\d{3,4}-.+\.md$", path.name):
            fail(f"Invalid ADR filename: {path.relative_to(ROOT)}", errors)

        text = path.read_text(encoding="utf-8")
        for heading in ADR_REQUIRED_HEADINGS:
            if heading not in text:
                fail(f"ADR missing heading '{heading}': {path.relative_to(ROOT)}", errors)


def check_h0_definition(errors: list[str]) -> None:
    path = ROOT / "docs" / "development" / "08-h0-exit-checklist.md"
    if not path.exists():
        return

    text = path.read_text(encoding="utf-8")
    if "Phase H0" not in text or "Phase 0" not in text:
        fail("H0 checklist must explicitly define H0 as preceding Phase 0", errors)


def check_development_strategy(errors: list[str]) -> None:
    environment_path = ROOT / "docs" / "development" / "09-development-environment.md"
    if environment_path.exists():
        text = environment_path.read_text(encoding="utf-8")
        for required in ["local", "dev", "staging", "preview"]:
            if required not in text.lower():
                fail(f"Development environment strategy must mention '{required}'", errors)

    iac_path = ROOT / "docs" / "development" / "12-infrastructure-as-code.md"
    if iac_path.exists():
        text = iac_path.read_text(encoding="utf-8")
        if "AWS CDK" not in text or "source of truth" not in text.lower():
            fail("Infrastructure as Code doc must define AWS CDK as source of truth", errors)

    cost_path = ROOT / "docs" / "development" / "13-free-tier-and-credit-guardrails.md"
    if cost_path.exists():
        text = cost_path.read_text(encoding="utf-8")
        if "Free Tier" not in text or "USD 100" not in text:
            fail("Free Tier guardrail doc must preserve the project credit constraint", errors)

    codex_path = ROOT / "docs" / "development" / "14-codex-multi-agent-and-worktrees.md"
    if codex_path.exists():
        text = codex_path.read_text(encoding="utf-8")
        lower = text.lower()
        if "luna" not in lower:
            fail("Codex operating model must preserve the Luna-first policy", errors)
        if "one writable task = one branch = one worktree" not in lower:
            fail("Codex operating model must preserve one-task/one-branch/one-worktree isolation", errors)
        if "maximum **2 active builder-style coding tasks in parallel**" not in lower:
            fail("Codex operating model must preserve the default two-Builder concurrency limit", errors)


def run_application_checks(errors: list[str]) -> None:
    commands = [
        ("Restore .NET dependencies", ["dotnet", "restore", "CommerceOS.slnx"]),
        ("Install locked Node.js dependencies", ["npm", "ci", "--ignore-scripts"]),
        (
            "Verify .NET formatting",
            ["dotnet", "format", "CommerceOS.slnx", "--verify-no-changes", "--no-restore"],
        ),
        ("Build .NET solution", ["dotnet", "build", "CommerceOS.slnx", "--no-restore"]),
        ("Run .NET tests", ["dotnet", "test", "CommerceOS.slnx", "--no-build"]),
        ("Lint, build, and test web applications", ["npm", "run", "verify"]),
        ("Synthesize AWS CDK skeleton", ["npm", "run", "cdk:synth"]),
    ]

    for description, command in commands:
        executable = shutil.which(command[0])
        if executable is None:
            fail(
                f"Required executable '{command[0]}' was not found while trying to: {description}",
                errors,
            )
            return

        resolved_command = [executable, *command[1:]]
        print(f"\n==> {description}", flush=True)
        result = subprocess.run(resolved_command, cwd=ROOT, check=False)
        if result.returncode != 0:
            fail(
                f"Application check failed ({result.returncode}): {' '.join(command)}",
                errors,
            )
            return


def main() -> int:
    errors: list[str] = []

    check_required_files(errors)
    check_task_specs(errors)
    check_adrs(errors)
    check_h0_definition(errors)
    check_development_strategy(errors)

    for relative in ["README.md", "AGENTS.md"]:
        check_local_markdown_links(ROOT / relative, errors)

    if not errors:
        run_application_checks(errors)

    if errors:
        print("CommerceOS Harness Check: FAIL")
        for index, error in enumerate(errors, start=1):
            print(f"{index}. {error}")
        return 1

    print("CommerceOS Harness Check: PASS")
    print(f"Required harness files: {len(REQUIRED_FILES)}")
    print("Task/ADR structure checks: PASS")
    print("README/AGENTS local-link checks: PASS")
    print("Phase H0 definition check: PASS")
    print("Environment/IaC/Free-Tier/Codex strategy checks: PASS")
    print("Application build/test/architecture/CDK checks: PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
