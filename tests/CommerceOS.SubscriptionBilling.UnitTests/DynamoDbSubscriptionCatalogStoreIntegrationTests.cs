using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.SubscriptionBilling.Application.Catalog;
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
}
