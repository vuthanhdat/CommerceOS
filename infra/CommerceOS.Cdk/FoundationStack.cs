using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
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

        var tenancyTable = new Table(
            this,
            "TenancyTable",
            new TableProps
            {
                TableName = $"{profile.ResourcePrefix}-tenancy",
                PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "PK", Type = AttributeType.STRING },
                SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "SK", Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                Encryption = TableEncryption.AWS_MANAGED,
                Stream = StreamViewType.NEW_IMAGE,
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

        _ = ModuleTable("CatalogTable", "catalog", profile);
        _ = ModuleTable("InventoryTable", "inventory", profile);
        _ = ModuleTable("SalesTable", "sales", profile);
        _ = ModuleTable("FilesMediaTable", "files-media", profile);
        _ = ModuleTable("ProcurementTable", "procurement", profile);
        _ = ModuleTable("ProductDataIngestionTable", "product-data-ingestion", profile);
        _ = ModuleTable("AuditTable", "audit", profile);
        _ = ModuleTable("MockPaymentProviderTable", "mock-payment-provider", profile);
        _ = new Bucket(this, "FilesMediaBucket", new BucketProps { BucketName = $"{profile.ResourcePrefix}-files-media", Encryption = BucketEncryption.S3_MANAGED, BlockPublicAccess = BlockPublicAccess.BLOCK_ALL, RemovalPolicy = profile.RemovalPolicy, AutoDeleteObjects = profile.IsEphemeral });

        var onboardingDeadLetterQueue = new Queue(
            this,
            "OnboardingTrialRecoveryDeadLetterQueue",
            new QueueProps
            {
                QueueName = $"{profile.ResourcePrefix}-onboarding-trial-recovery-dlq",
                RetentionPeriod = Duration.Days(14),
                RemovalPolicy = profile.RemovalPolicy
            });
        var onboardingRecoveryQueue = new Queue(
            this,
            "OnboardingTrialRecoveryQueue",
            new QueueProps
            {
                QueueName = $"{profile.ResourcePrefix}-onboarding-trial-recovery",
                VisibilityTimeout = Duration.Seconds(30),
                DeadLetterQueue = new DeadLetterQueue
                {
                    Queue = onboardingDeadLetterQueue,
                    MaxReceiveCount = 5
                },
                RemovalPolicy = profile.RemovalPolicy
            });

        // ADR-009 intentionally reserves this stream for the narrowly-scoped
        // work-outbox relay; no cross-domain table access is granted.
        _ = tenancyTable.TableStreamArn;
        _ = onboardingRecoveryQueue.QueueArn;
    }

    private Table ModuleTable(string constructId, string moduleName, EnvironmentProfile profile) => new(
        this,
        constructId,
        new TableProps
        {
            TableName = $"{profile.ResourcePrefix}-{moduleName}",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "PK", Type = AttributeType.STRING },
            SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "SK", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            Encryption = TableEncryption.AWS_MANAGED,
            RemovalPolicy = profile.RemovalPolicy
        });
}
