using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.SubscriptionBilling.Application.Catalog;
using CommerceOS.SubscriptionBilling.Application.Entitlements;
using CommerceOS.SubscriptionBilling.Application.Trial;
using CommerceOS.SubscriptionBilling.Contracts;
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
}
