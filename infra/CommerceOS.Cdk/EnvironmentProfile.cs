using Amazon.CDK;
using Amazon.CDK.AWS.Logs;

namespace CommerceOS.Cdk;

public sealed record EnvironmentProfile(
    string Name,
    bool IsProduction,
    bool IsEphemeral,
    RetentionDays LogRetention,
    RemovalPolicy RemovalPolicy,
    string CostProfile,
    string InstanceId = "0000",
    string ResourcePrefix = "commerceos-dev-0000",
    string Region = "us-east-1",
    string AccountId = "000000000000",
    string? ServiceEndpoint = null)
{
    public static EnvironmentProfile Create(string name, string instanceId = "0000")
    {
        var normalized = name.ToLowerInvariant() switch
        {
            "localstack-dev" => "dev",
            "localstack-test" => "test",
            "localstack-stage" => "stage",
            "dev" or "preview" or "staging" or "prod" => name.ToLowerInvariant(),
            _ => throw new ArgumentException($"Unsupported environment '{name}'.", nameof(name))
        };

        var endpoint = System.Environment.GetEnvironmentVariable("COMMERCEOS_LOCALSTACK_ENDPOINT");
        var prefix = System.Environment.GetEnvironmentVariable("COMMERCEOS_RESOURCE_PREFIX")
            ?? $"commerceos-{normalized}-{instanceId}";
        var region = System.Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? "us-east-1";
        var account = System.Environment.GetEnvironmentVariable("AWS_ACCOUNT_ID") ?? "000000000000";
        return normalized switch
        {
            "dev" => new("dev", false, false, RetentionDays.ONE_WEEK, RemovalPolicy.DESTROY, "free-tier", instanceId, prefix, region, account, endpoint),
            "preview" => new("preview", false, true, RetentionDays.THREE_DAYS, RemovalPolicy.DESTROY, "free-tier", instanceId, prefix, region, account, endpoint),
            "test" => new("test", false, true, RetentionDays.THREE_DAYS, RemovalPolicy.DESTROY, "local-only", instanceId, prefix, region, account, endpoint),
            "staging" or "stage" => new("stage", false, true, RetentionDays.ONE_WEEK, RemovalPolicy.DESTROY, "local-only", instanceId, prefix, region, account, endpoint),
            "prod" => new("prod", true, false, RetentionDays.ONE_MONTH, RemovalPolicy.RETAIN, "local-only", instanceId, prefix, region, account, endpoint),
            _ => throw new ArgumentException($"Unsupported environment '{name}'.", nameof(name))
        };
    }
}

