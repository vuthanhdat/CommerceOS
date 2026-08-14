using Amazon.CDK;
using Amazon.CDK.Assertions;
using CommerceOS.Cdk;

namespace CommerceOS.Cdk.Tests;

public sealed class FoundationStackTests
{
    [Fact]
    public void DevStackHasBoundedLogsAndCostTags()
    {
        var app = new App();
        var stack = new FoundationStack(app, "test-foundation", EnvironmentProfile.Create("dev"));
        var template = Template.FromStack(stack);

        template.ResourceCountIs("AWS::Logs::LogGroup", 1);
        template.HasResourceProperties(
            "AWS::Logs::LogGroup",
            new Dictionary<string, object>
            {
                ["LogGroupName"] = "/commerceos-dev-0000/foundation",
                ["RetentionInDays"] = 7,
                ["Tags"] = Match.ArrayWith(
                [
                    new Dictionary<string, object>
                    {
                        ["Key"] = "Project",
                        ["Value"] = "CommerceOS"
                    }
                ])
            });
        template.HasResourceProperties(
            "AWS::Logs::LogGroup",
            new Dictionary<string, object>
            {
                ["Tags"] = Match.ArrayWith(
                [
                    new Dictionary<string, object>
                    {
                        ["Key"] = "CostProfile",
                        ["Value"] = "free-tier"
                    }
                ])
            });
    }

    [Fact]
    public void UnknownEnvironmentIsRejected()
    {
        Assert.Throws<ArgumentException>(() => EnvironmentProfile.Create("personal"));
    }

    [Fact]
    public void LocalStackProfilesDeriveStableTaskIsolation()
    {
        var profile = EnvironmentProfile.Create("localstack-test", "0042");

        Assert.Equal("test", profile.Name);
        Assert.Equal("commerceos-test-0042", profile.ResourcePrefix);
        Assert.Equal("000000000000", profile.AccountId);
        Assert.Equal("us-east-1", profile.Region);
    }

    [Fact]
    public void FoundationStackCreatesAnIsolatedTenancyTable()
    {
        var app = new App();
        var stack = new FoundationStack(app, "test-foundation", EnvironmentProfile.Create("localstack-test", "0042"));
        var template = Template.FromStack(stack);

        template.HasResourceProperties(
            "AWS::DynamoDB::Table",
            new Dictionary<string, object>
            {
                ["TableName"] = "commerceos-test-0042-tenancy",
                ["BillingMode"] = "PAY_PER_REQUEST",
                ["KeySchema"] = Match.ArrayWith(
                [
                    new Dictionary<string, object> { ["AttributeName"] = "PK", ["KeyType"] = "HASH" },
                    new Dictionary<string, object> { ["AttributeName"] = "SK", ["KeyType"] = "RANGE" }
                ])
            });
    }

    [Fact]
    public void FoundationStackCreatesAnIsolatedSubscriptionBillingTable()
    {
        var app = new App();
        var stack = new FoundationStack(app, "test-foundation", EnvironmentProfile.Create("localstack-test", "0042"));
        var template = Template.FromStack(stack);

        template.HasResourceProperties(
            "AWS::DynamoDB::Table",
            new Dictionary<string, object>
            {
                ["TableName"] = "commerceos-test-0042-subscription-billing",
                ["BillingMode"] = "PAY_PER_REQUEST",
                ["KeySchema"] = Match.ArrayWith(
                [
                    new Dictionary<string, object> { ["AttributeName"] = "PK", ["KeyType"] = "HASH" },
                    new Dictionary<string, object> { ["AttributeName"] = "SK", ["KeyType"] = "RANGE" }
                ])
            });
    }

    [Theory]
    [InlineData("catalog")]
    [InlineData("inventory")]
    [InlineData("files-media")]
    [InlineData("procurement")]
    [InlineData("product-data-ingestion")]
    [InlineData("audit")]
    [InlineData("mock-payment-provider")]
    public void FoundationStackCreatesEachReadyModuleOwnedTable(string moduleName)
    {
        var app = new App();
        var template = Template.FromStack(new FoundationStack(app, "test-foundation", EnvironmentProfile.Create("localstack-test", "0042")));
        template.HasResourceProperties("AWS::DynamoDB::Table", new Dictionary<string, object> { ["TableName"] = $"commerceos-test-0042-{moduleName}", ["BillingMode"] = "PAY_PER_REQUEST" });
    }

    [Fact]
    public void FoundationStackProvidesAStreamAndOnePurposeBuiltOnboardingRecoveryQueue()
    {
        var app = new App();
        var stack = new FoundationStack(app, "test-foundation", EnvironmentProfile.Create("localstack-test", "0042"));
        var template = Template.FromStack(stack);

        template.HasResourceProperties(
            "AWS::DynamoDB::Table",
            new Dictionary<string, object> { ["StreamSpecification"] = new Dictionary<string, object> { ["StreamViewType"] = "NEW_IMAGE" } });
        template.HasResourceProperties(
            "AWS::SQS::Queue",
            new Dictionary<string, object> { ["QueueName"] = "commerceos-test-0042-onboarding-trial-recovery" });
        template.HasResourceProperties(
            "AWS::SQS::Queue",
            new Dictionary<string, object> { ["QueueName"] = "commerceos-test-0042-onboarding-trial-recovery-dlq" });
    }

    [Fact]
    public void FoundationStackDefinesTheAdr010StandardWorkflowWithUnknownWaitRetryAndCatch()
    {
        var template = Template.FromStack(new FoundationStack(new App(), "test-foundation", EnvironmentProfile.Create("localstack-test", "0042")));
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            ["StateMachineName"] = "commerceos-test-0042-order-payment-allocation",
            ["StateMachineType"] = "STANDARD",
            ["DefinitionString"] = Match.SerializedJson(Match.ObjectLike(new Dictionary<string, object>
            {
                ["StartAt"] = "RouteScenario",
                ["States"] = Match.ObjectLike(new Dictionary<string, object>
                {
                    ["WaitBeforeReconciliation"] = Match.ObjectLike(new Dictionary<string, object> { ["Type"] = "Wait" }),
                    ["ReconciliationProbe"] = Match.ObjectLike(new Dictionary<string, object> { ["Type"] = "Task", ["Retry"] = Match.AnyValue(), ["Catch"] = Match.AnyValue() })
                })
            }))
        });
    }
}
