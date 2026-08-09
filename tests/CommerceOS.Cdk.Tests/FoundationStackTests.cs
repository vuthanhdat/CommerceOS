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
                ["LogGroupName"] = "/commerceos/dev/foundation",
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
}
