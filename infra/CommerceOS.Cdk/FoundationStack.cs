using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using SqsEventTarget = Amazon.CDK.AWS.Events.Targets.SqsQueue;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.StepFunctions;
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
        _ = ModuleTable("PaymentsTable", "payments", profile);
        _ = ModuleTable("FilesMediaTable", "files-media", profile);
        _ = ModuleTable("ProcurementTable", "procurement", profile);
        _ = ModuleTable("ProductDataIngestionTable", "product-data-ingestion", profile);
        _ = ModuleTable("AuditTable", "audit", profile);
        _ = ModuleTable("MockPaymentProviderTable", "mock-payment-provider", profile);
        _ = ModuleTable("AccountingTable", "accounting", profile);
        _ = ModuleTable("ReportingTable", "reporting", profile);
        // CDK's AutoDeleteObjects provider targets nodejs24.x, which the pinned
        // LocalStack image does not currently accept. Test flows clean objects explicitly.
        _ = new Bucket(this, "FilesMediaBucket", new BucketProps { BucketName = $"{profile.ResourcePrefix}-files-media", Encryption = BucketEncryption.S3_MANAGED, BlockPublicAccess = BlockPublicAccess.BLOCK_ALL, RemovalPolicy = profile.RemovalPolicy, AutoDeleteObjects = false });
        _ = new Bucket(this, "ProductDataRawSnapshotsBucket", new BucketProps
        {
            BucketName = $"{profile.ResourcePrefix}-product-data-raw-snapshots",
            Encryption = BucketEncryption.S3_MANAGED,
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            RemovalPolicy = profile.RemovalPolicy,
            AutoDeleteObjects = false,
            LifecycleRules = [new LifecycleRule { Expiration = Duration.Days(7), Enabled = true }]
        });

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
        var pdiDeadLetterQueue = new Queue(this, "ProductDataCrawlDeadLetterQueue", new QueueProps
        {
            QueueName = $"{profile.ResourcePrefix}-product-data-crawl-dlq",
            RetentionPeriod = Duration.Days(14),
            RemovalPolicy = profile.RemovalPolicy
        });
        var pdiCrawlQueue = new Queue(this, "ProductDataCrawlQueue", new QueueProps
        {
            QueueName = $"{profile.ResourcePrefix}-product-data-crawl",
            VisibilityTimeout = Duration.Seconds(60),
            DeadLetterQueue = new DeadLetterQueue { Queue = pdiDeadLetterQueue, MaxReceiveCount = 3 },
            RemovalPolicy = profile.RemovalPolicy
        });
        var accountingDeadLetterQueue = new Queue(this, "AccountingFactsDeadLetterQueue", new QueueProps
        {
            QueueName = $"{profile.ResourcePrefix}-accounting-facts-dlq",
            RetentionPeriod = Duration.Days(14),
            RemovalPolicy = profile.RemovalPolicy
        });
        var accountingFactsQueue = new Queue(this, "AccountingFactsQueue", new QueueProps
        {
            QueueName = $"{profile.ResourcePrefix}-accounting-facts",
            VisibilityTimeout = Duration.Seconds(60),
            DeadLetterQueue = new DeadLetterQueue { Queue = accountingDeadLetterQueue, MaxReceiveCount = 5 },
            RemovalPolicy = profile.RemovalPolicy
        });
        var commerceFacts = new Amazon.CDK.AWS.Events.EventBus(this, "CommerceFactsBus", new Amazon.CDK.AWS.Events.EventBusProps { EventBusName = $"{profile.ResourcePrefix}-commerce-facts" });
        _ = new Rule(this, "AccountingFactsRoute", new RuleProps
        {
            EventBus = commerceFacts,
            EventPattern = new EventPattern { DetailType = ["PaymentCaptured", "OrderFulfilled", "StockIssued", "GoodsReceiptRecorded", "SupplierInvoiceRecorded", "SupplierPaymentRecorded", "StockAdjusted"] },
            Targets = [new SqsEventTarget(accountingFactsQueue)]
        });
        var refundInventoryDeadLetterQueue = new Queue(this, "RefundInventoryDeadLetterQueue", new QueueProps
        {
            QueueName = $"{profile.ResourcePrefix}-refund-inventory-dlq",
            RetentionPeriod = Duration.Days(14),
            RemovalPolicy = profile.RemovalPolicy
        });
        var refundInventoryQueue = new Queue(this, "RefundInventoryQueue", new QueueProps
        {
            QueueName = $"{profile.ResourcePrefix}-refund-inventory",
            VisibilityTimeout = Duration.Seconds(60),
            DeadLetterQueue = new DeadLetterQueue { Queue = refundInventoryDeadLetterQueue, MaxReceiveCount = 5 },
            RemovalPolicy = profile.RemovalPolicy
        });
        var refundPaymentsDeadLetterQueue = new Queue(this, "RefundPaymentsDeadLetterQueue", new QueueProps
        {
            QueueName = $"{profile.ResourcePrefix}-refund-payments-dlq",
            RetentionPeriod = Duration.Days(14),
            RemovalPolicy = profile.RemovalPolicy
        });
        var refundPaymentsQueue = new Queue(this, "RefundPaymentsQueue", new QueueProps
        {
            QueueName = $"{profile.ResourcePrefix}-refund-payments",
            VisibilityTimeout = Duration.Seconds(60),
            DeadLetterQueue = new DeadLetterQueue { Queue = refundPaymentsDeadLetterQueue, MaxReceiveCount = 5 },
            RemovalPolicy = profile.RemovalPolicy
        });
        _ = new Rule(this, "RefundApprovedFanOut", new RuleProps
        {
            EventBus = commerceFacts,
            EventPattern = new EventPattern { DetailType = ["RefundApproved"] },
            Targets = [new SqsEventTarget(refundInventoryQueue), new SqsEventTarget(refundPaymentsQueue), new SqsEventTarget(accountingFactsQueue)]
        });

        var orderWorkflowRole = new Role(this, "OrderWorkflowRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("states.amazonaws.com")
        });
        orderWorkflowRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = ["states:DescribeExecution"],
            Resources = ["*"]
        }));
        _ = new CfnStateMachine(this, "OrderPaymentAllocationWorkflow", new CfnStateMachineProps
        {
            StateMachineName = $"{profile.ResourcePrefix}-order-payment-allocation",
            RoleArn = orderWorkflowRole.RoleArn,
            StateMachineType = "STANDARD",
            DefinitionString = """
            {"Comment":"ADR-010 local control-flow verification only; owner effects remain contract-owned.","StartAt":"RouteScenario","States":{"RouteScenario":{"Type":"Choice","Choices":[{"Variable":"$.scenario","StringEquals":"Success","Next":"ReservationConfirmed"},{"Variable":"$.scenario","StringEquals":"Declined","Next":"AwaitingPaymentRetry"},{"Variable":"$.scenario","StringEquals":"TimeoutAfterCommit","Next":"WaitBeforeReconciliation"}],"Default":"NeedsAttention"},"ReservationConfirmed":{"Type":"Pass","Result":{"reservation":"Applied","payment":"Captured"},"ResultPath":"$.ownerContractExpectation","Next":"OrderAllocated"},"OrderAllocated":{"Type":"Pass","Result":{"status":"Allocated"},"End":true},"AwaitingPaymentRetry":{"Type":"Pass","Result":{"status":"AwaitingPaymentRetry"},"End":true},"WaitBeforeReconciliation":{"Type":"Wait","Seconds":1,"Next":"ReconciliationProbe"},"ReconciliationProbe":{"Type":"Task","Resource":"arn:aws:states:::aws-sdk:sfn:describeExecution","Parameters":{"executionArn.$":"$$.Execution.Id"},"Retry":[{"ErrorEquals":["States.TaskFailed"],"IntervalSeconds":1,"MaxAttempts":2,"BackoffRate":2.0}],"Catch":[{"ErrorEquals":["States.ALL"],"Next":"NeedsAttention"}],"Next":"NeedsAttention"},"NeedsAttention":{"Type":"Pass","Result":{"status":"NeedsAttention"},"End":true}}}
            """
        });

        // ADR-009 intentionally reserves this stream for the narrowly-scoped
        // work-outbox relay; no cross-domain table access is granted.
        _ = tenancyTable.TableStreamArn;
        _ = onboardingRecoveryQueue.QueueArn;
        _ = pdiCrawlQueue.QueueArn;
        _ = accountingFactsQueue.QueueArn;
        _ = refundInventoryQueue.QueueArn;
        _ = refundPaymentsQueue.QueueArn;
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
