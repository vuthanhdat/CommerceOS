using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Logs;
using Constructs;

namespace CommerceOS.Cdk;

public sealed class FoundationStack : Stack
{
    public FoundationStack(
        Construct scope,
        string id,
        EnvironmentProfile profile,
        IStackProps? props = null)
        : base(scope, id, props)
    {
        Amazon.CDK.Tags.Of(this).Add("Project", "CommerceOS");
        Amazon.CDK.Tags.Of(this).Add("Environment", profile.Name);
        Amazon.CDK.Tags.Of(this).Add("ManagedBy", "CDK");
        Amazon.CDK.Tags.Of(this).Add("Owner", "personal-learning");
        Amazon.CDK.Tags.Of(this).Add("CostProfile", profile.CostProfile);
        Amazon.CDK.Tags.Of(this).Add("Ephemeral", profile.IsEphemeral.ToString().ToLowerInvariant());

        var logGroup = new LogGroup(
            this,
            "FoundationLogGroup",
            new LogGroupProps
            {
                LogGroupName = $"/{profile.ResourcePrefix}/foundation",
                Retention = profile.LogRetention
            });

        logGroup.ApplyRemovalPolicy(profile.RemovalPolicy);

        _ = new Table(
            this,
            "TenancyTable",
            new TableProps
            {
                TableName = $"{profile.ResourcePrefix}-tenancy",
                PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "PK", Type = AttributeType.STRING },
                SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "SK", Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                Encryption = TableEncryption.AWS_MANAGED,
                RemovalPolicy = profile.RemovalPolicy
            });

        _ = new Table(
            this,
            "SubscriptionBillingTable",
            new TableProps
            {
                TableName = $"{profile.ResourcePrefix}-subscription-billing",
                PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "PK", Type = AttributeType.STRING },
                SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "SK", Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                Encryption = TableEncryption.AWS_MANAGED,
                RemovalPolicy = profile.RemovalPolicy
            });
    }
}
