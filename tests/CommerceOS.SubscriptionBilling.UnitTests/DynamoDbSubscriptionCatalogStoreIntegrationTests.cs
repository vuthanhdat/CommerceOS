using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.SubscriptionBilling.Application.Catalog;
using CommerceOS.SubscriptionBilling.Application.Entitlements;
using CommerceOS.SubscriptionBilling.Application.PlatformCharges;
using CommerceOS.SubscriptionBilling.Application.Trial;
using CommerceOS.SubscriptionBilling.Contracts;
using CommerceOS.SubscriptionBilling.Domain;
using CommerceOS.SubscriptionBilling.Infrastructure.Provider;
using CommerceOS.SubscriptionBilling.Infrastructure.Persistence;

namespace CommerceOS.SubscriptionBilling.UnitTests;

public sealed class DynamoDbSubscriptionCatalogStoreIntegrationTests
{
    [Fact]
    public async Task BootstrapAndCatalogQueryWorkAgainstConfiguredLocalStack()
    {
        var endpoint = Environment.GetEnvironmentVariable("COMMERCEOS_LOCALSTACK_ENDPOINT");
        var tableName = Environment.GetEnvironmentVariable("COMMERCEOS_SUBSCRIPTION_BILLING_TABLE");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        using var client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" });
        var store = new DynamoDbSubscriptionCatalogStore(client, new DynamoDbSubscriptionBillingOptions(tableName));
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "initial-catalog.v1.json"));
        var seed = CatalogSeedLoader.Load(stream);

        var bootstrap = await new CatalogBootstrapService(store).BootstrapAsync(seed, CancellationToken.None);
        var plans = await new CatalogQueryService(store).ListAvailablePlanVersionsAsync(CancellationToken.None);

        Assert.True(bootstrap.Succeeded);
        Assert.Equal(3, plans.Count);
        Assert.Equal(["Business", "Growth", "Starter"], plans.Select(plan => plan.PlanId));
    }

    [Fact]
    public async Task CurrentTrialEntitlementDecisionReadsTheTaskScopedLocalStackTable()
    {
        var endpoint = Environment.GetEnvironmentVariable("COMMERCEOS_LOCALSTACK_ENDPOINT");
        var tableName = Environment.GetEnvironmentVariable("COMMERCEOS_SUBSCRIPTION_BILLING_TABLE");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(tableName)) return;

        using var client = new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" });
        var store = new DynamoDbTrialSubscriptionStore(client, new DynamoDbSubscriptionBillingOptions(tableName));
        var now = DateTimeOffset.UtcNow;
        var tenantId = $"entitlement-{Guid.NewGuid():N}";
        await store.CreateIfAbsentAsync(new TrialSubscription(tenantId, "onboarding", "source", new TrialEntitlementSnapshot("trial-v1", 30, true, 3, 1, true, 500), now, now.AddDays(30)), CancellationToken.None);

        var decision = await new EntitlementEvaluator(store).EvaluateEntitlementAsync(new EvaluateEntitlementRequest(tenantId, EntitlementKey.MaxActiveMemberships, now, "correlation"), CancellationToken.None);

        Assert.Equal(EntitlementDecisionOutcome.Granted, decision.Outcome);
        Assert.Equal(3, decision.Limit);
        Assert.Equal("trial-v1", decision.EntitlementSourceVersion);
    }

    [Fact]
    public async Task PlatformChargeEvidenceDedupesAndReconcilesAgainstConfiguredLocalStack()
    {
        var endpoint = Environment.GetEnvironmentVariable("COMMERCEOS_LOCALSTACK_ENDPOINT");
        var tableName = Environment.GetEnvironmentVariable("COMMERCEOS_SUBSCRIPTION_BILLING_TABLE");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(tableName)) return;

        using var client = new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" });
        var state = new DeterministicSaasBillingProviderState();
        var identity = $"renewal-{Guid.NewGuid():N}";
        state.Configure(identity, SimulatedSaasBillingScenario.TimeoutAfterCommit);
        var store = new DynamoDbPlatformChargeStore(client, new DynamoDbSubscriptionBillingOptions(tableName));
        var service = new PlatformChargeService(store, new DeterministicSaasBillingProvider(state));
        var command = new RecordPlatformChargeAttemptCommand($"tenant-{Guid.NewGuid():N}", "subscription-1", "starter-v1", identity, new VndMoney(199_000), "correlation");

        var attempted = await service.RecordAttemptAsync(command, CancellationToken.None);
        var replay = await service.RecordAttemptAsync(command, CancellationToken.None);
        var settled = await service.ReconcileAsync(command.TenantId, command.LogicalChargeIdentity, CancellationToken.None);
        var duplicate = await service.RecordProviderEvidenceAsync(command.TenantId, command.LogicalChargeIdentity,
            new PlatformChargeEvidence($"provider-evidence:{attempted.Charge.ProviderOperationId}", attempted.Charge.Id, attempted.Charge.ProviderOperationId, PlatformChargeEvidenceKind.VerifiedSuccess, DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.True(attempted.Created);
        Assert.False(replay.Created);
        Assert.Equal(PlatformChargeOutcome.OutcomeUnknown, attempted.Charge.Outcome);
        Assert.Equal(PlatformChargeOutcome.Succeeded, settled.Outcome);
        Assert.Equal(PlatformChargeOutcome.Succeeded, duplicate.Outcome);
    }
}
