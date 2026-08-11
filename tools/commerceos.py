#!/usr/bin/env python3
"""Task-instance-aware local launcher and LocalStack lifecycle for CommerceOS."""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from urllib.error import URLError
from urllib.request import Request, urlopen


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
        "localstack": 14566 + instance,
        "storefront": base,
        "api": base + 1,
        "mock_payment": base + 2,
        "backoffice": base + 3,
        "dynamodb": base + 4,
    }


@dataclass(frozen=True)
class LocalStackConfig:
    """Infrastructure-only settings shared by lifecycle and CDK commands."""

    instance: int
    profile: str = "localstack-test"

    @property
    def ports(self) -> dict[str, int]:
        return ports(self.instance)

    @property
    def endpoint(self) -> str:
        return f"http://127.0.0.1:{self.ports['localstack']}"

    @property
    def resource_prefix(self) -> str:
        return f"commerceos-{self.profile}-{self.instance:04d}"

    @property
    def container_name(self) -> str:
        return f"{self.resource_prefix}-localstack"

    def as_dict(self) -> dict[str, object]:
        return {
            "profile": self.profile,
            "instance": f"{self.instance:04d}",
            "endpoint": self.endpoint,
            "region": "us-east-1",
            "account_id": "000000000000",
            "synthetic_access_key": "test",
            "synthetic_secret_key": "test",
            "resource_prefix": self.resource_prefix,
            "container_name": self.container_name,
            "reset_policy": "clean-container",
            "localstack_image": os.environ.get("COMMERCEOS_LOCALSTACK_IMAGE", "localstack/localstack:4.8.1"),
            "ports": self.ports,
        }


def config_from_args(args: argparse.Namespace) -> LocalStackConfig:
    return LocalStackConfig(args.instance, args.profile)


def lifecycle_environment(config: LocalStackConfig) -> dict[str, str]:
    environment = os.environ.copy()
    environment.update(
        {
            "AWS_ACCESS_KEY_ID": "test",
            "AWS_SECRET_ACCESS_KEY": "test",
            "AWS_DEFAULT_REGION": "us-east-1",
            "AWS_REGION": "us-east-1",
            "AWS_ACCOUNT_ID": "000000000000",
            "AWS_ENDPOINT_URL": config.endpoint,
            "COMMERCEOS_INSTANCE": f"{config.instance:04d}",
            "COMMERCEOS_RESOURCE_PREFIX": config.resource_prefix,
            "COMMERCEOS_LOCALSTACK_ENDPOINT": config.endpoint,
        }
    )
    return environment


def run(command: list[str], config: LocalStackConfig | None = None) -> int:
    return subprocess.call(command, env=lifecycle_environment(config) if config else None)


def require_docker() -> str:
    docker = shutil.which("docker")
    if docker is None:
        raise RuntimeError("Docker is required for LocalStack lifecycle commands.")
    return docker


def localstack_ready(config: LocalStackConfig) -> bool:
    try:
        request = Request(f"{config.endpoint}/_localstack/health")
        with urlopen(request, timeout=2) as response:
            return response.status == 200
    except (OSError, URLError):
        return False


def wait_for_localstack(config: LocalStackConfig, timeout: int) -> int:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if localstack_ready(config):
            print(json.dumps({"endpoint": config.endpoint, "ready": True}))
            return 0
        time.sleep(1)
    print(f"LocalStack did not become ready at {config.endpoint} within {timeout}s", file=sys.stderr)
    return 1


def start_localstack(config: LocalStackConfig, timeout: int) -> int:
    docker = require_docker()
    if localstack_ready(config):
        return 0
    image = os.environ.get("COMMERCEOS_LOCALSTACK_IMAGE", "localstack/localstack:4.8.1")
    image_check = subprocess.run([docker, "image", "inspect", image], check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if image_check.returncode != 0:
        print(
            f"LocalStack image '{image}' is not available locally; pull it before starting "
            f"(for example: docker pull {image}).",
            file=sys.stderr,
        )
        return 1
    subprocess.run([docker, "rm", "-f", config.container_name], check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    result = subprocess.run(
        [
            docker,
            "run",
            "-d",
            "--name",
            config.container_name,
            "-p",
            f"{config.ports['localstack']}:4566",
            "-e",
            "SERVICES=cloudformation,logs,s3,iam,sts,ssm",
            "-e",
            "DEBUG=0",
            os.environ.get("COMMERCEOS_LOCALSTACK_IMAGE", "localstack/localstack:4.8.1"),
        ],
        check=False,
    )
    if result.returncode != 0:
        return result.returncode
    return wait_for_localstack(config, timeout)


def stop_localstack(config: LocalStackConfig) -> int:
    docker = require_docker()
    return subprocess.call([docker, "rm", "-f", config.container_name])


def cdk_command(config: LocalStackConfig, action: str) -> int:
    context = ["--context", f"environment=dev", "--context", f"instance={config.instance:04d}"]
    stack = f"{config.resource_prefix}-foundation"
    cdk = "npx.cmd" if os.name == "nt" else "npx"
    if action == "synth":
        return run([cdk, "cdk", "synth", stack, *context, "--quiet"], config)
    if action == "bootstrap":
        return run([cdk, "cdk", "bootstrap", *context, "--cloudformation-execution-policies", "arn:aws:iam::aws:policy/AdministratorAccess"], config)
    return run([cdk, "cdk", "deploy", stack, *context, "--require-approval", "never"], config)


def inspect_localstack(config: LocalStackConfig) -> int:
    if not localstack_ready(config):
        print("LocalStack is not ready", file=sys.stderr)
        return 1
    request = Request(f"{config.endpoint}/_localstack/health")
    with urlopen(request, timeout=5) as response:
        print(response.read().decode("utf-8"))
    return 0


def smoke_localstack(config: LocalStackConfig) -> int:
    return 0 if localstack_ready(config) else 1


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
    parser.add_argument(
        "command",
        choices=("ports", "config", "api", "start", "readiness", "synth", "bootstrap", "deploy", "inspect", "smoke", "reset", "destroy", "redeploy", "lifecycle"),
    )
    parser.add_argument(
        "--instance",
        default=os.environ.get("COMMERCEOS_INSTANCE", "0000"),
        type=parse_instance,
        help="worktree/task instance (default: COMMERCEOS_INSTANCE or 0000)",
    )
    parser.add_argument("--profile", default=os.environ.get("COMMERCEOS_PROFILE", "localstack-test"))
    parser.add_argument("--timeout", type=int, default=60)
    args = parser.parse_args()
    allocated = ports(args.instance)
    config = config_from_args(args)

    if args.command == "ports":
        print(json.dumps(allocated, indent=2))
        return 0

    if args.command == "config":
        print(json.dumps(config.as_dict(), indent=2))
        return 0

    if args.command == "api":
        return run_api(allocated["api"])
    if args.command == "start":
        return start_localstack(config, args.timeout)
    if args.command == "readiness":
        return wait_for_localstack(config, args.timeout)
    if args.command in {"synth", "bootstrap", "deploy"}:
        return cdk_command(config, args.command)
    if args.command == "inspect":
        return inspect_localstack(config)
    if args.command == "smoke":
        return smoke_localstack(config)
    if args.command == "reset":
        stop_localstack(config)
        return start_localstack(config, args.timeout)
    if args.command == "destroy":
        return stop_localstack(config)
    if args.command == "redeploy":
        result = start_localstack(config, args.timeout)
        if result == 0:
            result = cdk_command(config, "bootstrap")
        if result == 0:
            result = cdk_command(config, "deploy")
        return result
    result = start_localstack(config, args.timeout)
    for action in ("synth", "bootstrap", "deploy") if result == 0 else ():
        result = cdk_command(config, action)
        if result != 0:
            break
    if result == 0:
        result = smoke_localstack(config)
    return result


if __name__ == "__main__":
    sys.exit(main())

