import json
import os
import unittest
from argparse import Namespace
from unittest.mock import patch

from tools.commerceos import (
    LocalStackConfig,
    config_from_args,
    cdk_command,
    ports,
    stop_localstack,
)


class CommerceOsLauncherTests(unittest.TestCase):
    def test_task_instances_have_distinct_ports_and_resource_names(self):
        first = LocalStackConfig(1)
        second = LocalStackConfig(2)

        self.assertNotEqual(first.endpoint, second.endpoint)
        self.assertNotEqual(first.resource_prefix, second.resource_prefix)
        self.assertNotEqual(ports(1)["api"], ports(2)["api"])

    def test_configuration_exposes_runtime_feature_switches(self):
        with patch.dict(
            os.environ,
            {
                "COMMERCEOS_LOCALSTACK_SERVICES": "cloudformation,logs",
                "COMMERCEOS_LOCALSTACK_DEBUG": "1",
            },
        ):
            config = LocalStackConfig(94)
            values = json.loads(json.dumps(config.as_dict()))
            self.assertEqual("cloudformation,logs", values["localstack_services"])
            self.assertEqual("1", values["localstack_debug"])

    @patch("tools.commerceos.require_docker", return_value="docker")
    @patch("tools.commerceos.subprocess.run")
    def test_destroy_is_idempotent_when_container_is_absent(self, run, _docker):
        run.return_value.returncode = 1

        result = stop_localstack(LocalStackConfig(94))

        self.assertEqual(0, result)
        run.assert_called_once()

    def test_config_from_args_preserves_profile(self):
        config = config_from_args(Namespace(instance=94, profile="localstack-dev"))

        self.assertEqual("localstack-dev", config.profile)
        self.assertEqual("commerceos-localstack-dev-0094", config.resource_prefix)
        self.assertEqual("localstack-dev", config.cdk_environment)

        test_config = LocalStackConfig(94)
        self.assertEqual("localstack-test", test_config.cdk_environment)

    @patch("tools.commerceos.shutil.which", return_value=None)
    def test_cdk_commands_fail_closed_without_cdklocal(self, _which):
        self.assertEqual(1, cdk_command(LocalStackConfig(94), "deploy"))

    @patch("tools.commerceos.run", return_value=0)
    @patch("tools.commerceos.shutil.which", return_value="cdklocal")
    def test_cdk_commands_use_localstack_wrapper(self, _which, run):
        self.assertEqual(0, cdk_command(LocalStackConfig(94), "deploy"))
        command, config = run.call_args.args
        self.assertEqual("cdklocal", command[0])
        self.assertEqual("deploy", command[1])
        self.assertEqual("commerceos-localstack-test-0094-foundation", command[2])
        self.assertIn("environment=localstack-test", command)
        self.assertEqual(LocalStackConfig(94), config)

    def test_lifecycle_environment_configures_s3_endpoint_for_cdklocal(self):
        from tools.commerceos import lifecycle_environment

        environment = lifecycle_environment(LocalStackConfig(94))

        self.assertEqual("http://127.0.0.1:14660", environment["AWS_ENDPOINT_URL"])
        self.assertEqual(environment["AWS_ENDPOINT_URL"], environment["AWS_ENDPOINT_URL_S3"])

    @patch("tools.commerceos.shutil.which", return_value="aws")
    @patch("tools.commerceos.subprocess.run")
    def test_smoke_checks_foundation_stack_and_log_group(self, run, _which):
        from tools.commerceos import smoke_localstack

        run.side_effect = [
            Namespace(returncode=0, stdout='{"Stacks":[{"StackStatus":"CREATE_COMPLETE"}]}', stderr=""),
            Namespace(returncode=0, stdout='{"logGroups":[{"logGroupName":"/commerceos-localstack-test-0094/foundation"}]}', stderr=""),
        ]
        with patch("tools.commerceos.localstack_ready", return_value=True):
            result = smoke_localstack(LocalStackConfig(94))

        self.assertEqual(0, result)
        self.assertEqual("cloudformation", run.call_args_list[0].args[0][1])
        self.assertEqual("logs", run.call_args_list[1].args[0][1])

    @patch("tools.commerceos.shutil.which", return_value="aws")
    @patch("tools.commerceos.subprocess.run")
    def test_inspect_collects_health_stack_resources_and_logs(self, run, _which):
        from tools.commerceos import inspect_localstack

        run.side_effect = [
            Namespace(returncode=0, stdout='{"Stacks": [{"StackStatus": "CREATE_COMPLETE", "Tags": [{"Key": "Project", "Value": "CommerceOS"}]}]}', stderr=""),
            Namespace(returncode=0, stdout='{"StackResources": [{"ResourceType": "AWS::Logs::LogGroup"}]}', stderr=""),
            Namespace(returncode=0, stdout='{"logGroups": [{"logGroupName": "/commerceos-localstack-test-0094/foundation"}]}', stderr=""),
        ]
        health = patch("tools.commerceos.urlopen")
        response = health.start()
        response.return_value.__enter__.return_value.read.return_value = b'{"services": {"cloudformation": "available"}}'
        response.return_value.__enter__.return_value.status = 200
        try:
            with patch("builtins.print") as output:
                result = inspect_localstack(LocalStackConfig(94))
        finally:
            health.stop()

        self.assertEqual(0, result)
        self.assertEqual(3, run.call_count)
        payload = json.loads(output.call_args.args[0])
        self.assertIn("health", payload)
        self.assertIn("resources", payload)
        self.assertIn("log_groups", payload)


if __name__ == "__main__":
    unittest.main()
