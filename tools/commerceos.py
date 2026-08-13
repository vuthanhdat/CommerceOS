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


DEFAULT_LOCALSTACK_IMAGE = "localstack/localstack:4.8.1"
DEFAULT_LOCALSTACK_SERVICES = "cloudformation,logs,s3,iam,sts,ssm"


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

    @property
    def cdk_environment(self) -> str:
        return self.profile

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
            "localstack_image": os.environ.get("COMMERCEOS_LOCALSTACK_IMAGE", DEFAULT_LOCALSTACK_IMAGE),
            "localstack_services": os.environ.get("COMMERCEOS_LOCALSTACK_SERVICES", DEFAULT_LOCALSTACK_SERVICES),
            "localstack_debug": os.environ.get("COMMERCEOS_LOCALSTACK_DEBUG", "0"),
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
            "AWS_ENDPOINT_URL_S3": config.endpoint,
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
    daemon = subprocess.run(
        [docker, "info"],
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    if daemon.returncode != 0:
        print(
            "Docker daemon is not available; start Docker Desktop before running "
            "LocalStack lifecycle commands.",
            file=sys.stderr,
        )
        return 1
    existing = subprocess.run(
        [docker, "container", "inspect", config.container_name],
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    if existing.returncode == 0:
        policy = subprocess.run(
            [docker, "update", "--restart", "unless-stopped", config.container_name],
            check=False,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        if policy.returncode != 0:
            return policy.returncode
        started = subprocess.run([docker, "start", config.container_name], check=False)
        if started.returncode != 0:
            return started.returncode
        return wait_for_localstack(config, timeout)
    image = os.environ.get("COMMERCEOS_LOCALSTACK_IMAGE", DEFAULT_LOCALSTACK_IMAGE)
    image_check = subprocess.run([docker, "image", "inspect", image], check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if image_check.returncode != 0:
        print(
            f"LocalStack image '{image}' is not available locally; pull it before starting "
            f"(for example: docker pull {image}).",
            file=sys.stderr,
        )
        return 1
    result = subprocess.run(
        [
            docker,
            "run",
            "-d",
            "--restart",
            "unless-stopped",
            "--name",
            config.container_name,
            "-p",
            f"{config.ports['localstack']}:4566",
            "-e",
            f"SERVICES={os.environ.get('COMMERCEOS_LOCALSTACK_SERVICES', DEFAULT_LOCALSTACK_SERVICES)}",
            "-e",
            f"DEBUG={os.environ.get('COMMERCEOS_LOCALSTACK_DEBUG', '0')}",
            image,
        ],
        check=False,
    )
    if result.returncode != 0:
        return result.returncode
    return wait_for_localstack(config, timeout)


def stop_localstack(config: LocalStackConfig) -> int:
    docker = require_docker()
    existing = subprocess.run(
        [docker, "container", "inspect", config.container_name],
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    if existing.returncode != 0:
        return 0
    return subprocess.call([docker, "rm", "-f", config.container_name])


def cdk_command(config: LocalStackConfig, action: str) -> int:
    context = [
        "--context",
        f"environment={config.cdk_environment}",
        "--context",
        f"instance={config.instance:04d}",
    ]
    stack = f"{config.resource_prefix}-foundation"
    cdk = shutil.which("cdklocal.cmd" if os.name == "nt" else "cdklocal")
    if cdk is None:
        print(
            "cdklocal is required for LocalStack CDK commands. Install it with "
            "'npm install --global aws-cdk-local aws-cdk'.",
            file=sys.stderr,
        )
        return 1
    if action == "synth":
        return run([cdk, "synth", stack, *context, "--quiet"], config)
    if action == "bootstrap":
        return run([cdk, "bootstrap", *context, "--cloudformation-execution-policies", "arn:aws:iam::aws:policy/AdministratorAccess"], config)
    return run([cdk, "deploy", stack, *context, "--require-approval", "never"], config)


def inspect_localstack(config: LocalStackConfig) -> int:
    if not localstack_ready(config):
        print("LocalStack is not ready", file=sys.stderr)
        return 1

    aws = shutil.which("aws.exe" if os.name == "nt" else "aws")
    if aws is None:
        print("AWS CLI is required for FoundationStack inspection.", file=sys.stderr)
        return 1

    environment = lifecycle_environment(config)
    stack = f"{config.resource_prefix}-foundation"
    common = ["--endpoint-url", config.endpoint, "--region", "us-east-1", "--output", "json"]
    inspections = {
        "health": ["curl", f"{config.endpoint}/_localstack/health"],
        "stack": [aws, "cloudformation", "describe-stacks", "--stack-name", stack, *common],
        "resources": [aws, "cloudformation", "describe-stack-resources", "--stack-name", stack, *common],
        "log_groups": [aws, "logs", "describe-log-groups", "--log-group-name-prefix", f"/{config.resource_prefix}/", *common],
    }
    results: dict[str, object] = {}
    for name, command in inspections.items():
        if command[0] == "curl":
            request = Request(command[1])
            with urlopen(request, timeout=5) as response:
                results[name] = json.loads(response.read().decode("utf-8"))
            continue
        result = subprocess.run(command, check=False, capture_output=True, text=True, env=environment)
        if result.returncode != 0:
            print(f"FoundationStack inspection failed for {name}.", file=sys.stderr)
            if result.stderr:
                print(result.stderr.strip(), file=sys.stderr)
            return result.returncode or 1
        try:
            results[name] = json.loads(result.stdout)
        except json.JSONDecodeError as error:
            print(f"Invalid {name} inspection response: {error}", file=sys.stderr)
            return 1
    print(json.dumps(results))
    return 0


def smoke_localstack(config: LocalStackConfig) -> int:
    if not localstack_ready(config):
        print("LocalStack is not ready", file=sys.stderr)
        return 1
    aws = shutil.which("aws.exe" if os.name == "nt" else "aws")
    if aws is None:
        print("AWS CLI is required for FoundationStack smoke checks.", file=sys.stderr)
        return 1

    environment = lifecycle_environment(config)
    stack = f"{config.resource_prefix}-foundation"
    common = ["--endpoint-url", config.endpoint, "--region", "us-east-1"]
    stack_result = subprocess.run(
        [aws, "cloudformation", "describe-stacks", "--stack-name", stack, *common, "--output", "json"],
        check=False, capture_output=True, text=True, env=environment,
    )
    if stack_result.returncode != 0:
        print("FoundationStack was not found in LocalStack.", file=sys.stderr)
        if stack_result.stderr:
            print(stack_result.stderr.strip(), file=sys.stderr)
        return stack_result.returncode or 1
    try:
        stacks = json.loads(stack_result.stdout).get("Stacks", [])
        status = stacks[0]["StackStatus"]
    except (KeyError, IndexError, json.JSONDecodeError) as error:
        print(f"Invalid CloudFormation smoke response: {error}", file=sys.stderr)
        return 1
    if status not in {"CREATE_COMPLETE", "UPDATE_COMPLETE"}:
        print(f"FoundationStack is not healthy: {status}", file=sys.stderr)
        return 1

    log_group = f"/{config.resource_prefix}/foundation"
    logs_result = subprocess.run(
        [aws, "logs", "describe-log-groups", "--log-group-name-prefix", log_group, *common, "--output", "json"],
        check=False, capture_output=True, text=True, env=environment,
    )
    if logs_result.returncode != 0:
        print("Foundation log group was not found in LocalStack.", file=sys.stderr)
        return logs_result.returncode or 1
    try:
        groups = json.loads(logs_result.stdout).get("logGroups", [])
    except json.JSONDecodeError as error:
        print(f"Invalid CloudWatch Logs smoke response: {error}", file=sys.stderr)
        return 1
    if not any(group.get("logGroupName") == log_group for group in groups):
        print(f"Foundation log group is missing: {log_group}", file=sys.stderr)
        return 1
    print(json.dumps({"stack": stack, "status": status, "log_group": log_group}))
    return 0


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
        result = stop_localstack(config)
        if result != 0:
            return result
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

