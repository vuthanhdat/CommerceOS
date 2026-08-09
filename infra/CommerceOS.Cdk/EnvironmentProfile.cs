using Amazon.CDK;
using Amazon.CDK.AWS.Logs;

namespace CommerceOS.Cdk;

public sealed record EnvironmentProfile(
    string Name,
    bool IsProduction,
    bool IsEphemeral,
    RetentionDays LogRetention,
    RemovalPolicy RemovalPolicy,
    string CostProfile)
{
    public static EnvironmentProfile Create(string name) => name.ToLowerInvariant() switch
    {
        "dev" => new("dev", false, false, RetentionDays.ONE_WEEK, RemovalPolicy.DESTROY, "free-tier"),
        "preview" => new("preview", false, true, RetentionDays.THREE_DAYS, RemovalPolicy.DESTROY, "free-tier"),
        "staging" => new("staging", false, true, RetentionDays.ONE_WEEK, RemovalPolicy.DESTROY, "credit-funded"),
        "prod" => new("prod", true, false, RetentionDays.ONE_MONTH, RemovalPolicy.RETAIN, "production"),
        _ => throw new ArgumentException($"Unsupported environment '{name}'.", nameof(name))
    };
}

