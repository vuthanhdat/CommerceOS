#!/usr/bin/env python3
"""Task-instance-aware local launcher for CommerceOS developer processes."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys


def parse_instance(raw: str) -> int:
    if not raw.isdigit():
        raise argparse.ArgumentTypeError("instance must contain digits only, for example 0003")

    value = int(raw)
    if not 0 <= value <= 99:
        raise argparse.ArgumentTypeError("instance must be between 0000 and 0099")
    return value


def ports(instance: int) -> dict[str, int]:
    base = 14170 + (instance * 100)
    return {
        "storefront": base,
        "api": base + 1,
        "mock_payment": base + 2,
        "backoffice": base + 3,
        "dynamodb": base + 4,
    }


def run_api(port: int) -> int:
    environment = os.environ.copy()
    environment["ASPNETCORE_URLS"] = f"http://127.0.0.1:{port}"
    environment["ASPNETCORE_ENVIRONMENT"] = "Development"
    return subprocess.call(
        ["dotnet", "run", "--project", "src/CommerceOS.Api/CommerceOS.Api.csproj", "--no-launch-profile"],
        env=environment,
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("ports", "api"))
    parser.add_argument(
        "--instance",
        default=os.environ.get("COMMERCEOS_INSTANCE", "0000"),
        type=parse_instance,
        help="worktree/task instance (default: COMMERCEOS_INSTANCE or 0000)",
    )
    args = parser.parse_args()
    allocated = ports(args.instance)

    if args.command == "ports":
        print(json.dumps(allocated, indent=2))
        return 0

    return run_api(allocated["api"])


if __name__ == "__main__":
    sys.exit(main())

