using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.Pricing.Application;
using CommerceOS.Pricing.Domain;
using CommerceOS.Pricing.Infrastructure.Persistence;

namespace CommerceOS.Pricing.UnitTests;

public sealed class DynamoDbPromotionStoreIntegrationTests
{
    [Fact]
    public async Task ConditionalScheduleWriteRejectsAStaleOverlappingScheduleAgainstConfiguredLocalStack()
    {
        var endpoint = Environment.GetEnvironmentVariable("COMMERCEOS_LOCALSTACK_ENDPOINT");
        var table = Environment.GetEnvironmentVariable("COMMERCEOS_PRICING_TABLE");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(table)) return;

        using var client = new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" });
        var store = new DynamoDbPromotionStore(client, new DynamoDbPricingOptions(table));
        var tenant = new PricingTenantId($"pricing-{Guid.NewGuid():N}");
        var now = DateTimeOffset.UtcNow; var first = Promotion.Schedule(new("promotion-a"), tenant, "product-a", 80, now.AddMinutes(1), now.AddHours(1), "source-a", now);
        var second = Promotion.Schedule(new("promotion-b"), tenant, "product-a", 70, now.AddHours(2), now.AddHours(3), "source-b", now);
        var before = await store.GetScheduleAsync(tenant, first.ProductId, default);
        var firstOutcome = await store.ScheduleAsync(new(tenant, TrustedPricingRole.Owner, "c"), first, before, before.Add(first), default);
        var staleOutcome = await store.ScheduleAsync(new(tenant, TrustedPricingRole.Owner, "c"), second, before, before.Add(second), default);
        var loaded = await store.GetScheduleAsync(tenant, first.ProductId, default);

        Assert.Equal(PromotionCommandOutcome.Scheduled, firstOutcome);
        Assert.Equal(PromotionCommandOutcome.Conflict, staleOutcome);
        Assert.Single(loaded.Entries);
    }
}
