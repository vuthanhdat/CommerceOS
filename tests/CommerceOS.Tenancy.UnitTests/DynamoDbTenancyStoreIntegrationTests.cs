using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.Tenancy.Application.Persistence;
using CommerceOS.Tenancy.Domain;
using CommerceOS.Tenancy.Infrastructure.Persistence;

namespace CommerceOS.Tenancy.UnitTests;

public sealed class DynamoDbTenancyStoreIntegrationTests
{
    [Fact]
    public async Task ConditionalTenantWriteAndStrongReadWorkAgainstConfiguredLocalStack()
    {
        var endpoint = Environment.GetEnvironmentVariable("COMMERCEOS_LOCALSTACK_ENDPOINT");
        var tableName = Environment.GetEnvironmentVariable("COMMERCEOS_TENANCY_TABLE");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        using var client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" });
        var store = new DynamoDbTenancyStore(client, new DynamoDbTenancyOptions(tableName));
        var tenantId = new TenantId($"tenant-{Guid.NewGuid():N}");
        var scope = new TrustedTenantPersistenceScope(tenantId);
        var tenant = new Tenant(tenantId, TenantStatus.Active, new BusinessProfile("Merchant", "Asia/Ho_Chi_Minh"), 1);
        var updatedTenant = tenant with { Status = TenantStatus.Suspended, Revision = 2 };

        var created = await store.SaveTenantAsync(scope, tenant, null, CancellationToken.None);
        var duplicate = await store.SaveTenantAsync(scope, tenant, null, CancellationToken.None);
        var updated = await store.SaveTenantAsync(scope, updatedTenant, tenant.Revision, CancellationToken.None);
        var stale = await store.SaveTenantAsync(scope, tenant, tenant.Revision, CancellationToken.None);
        var loaded = await store.GetTenantAsync(scope, CancellationToken.None);

        Assert.Equal(ConditionalWriteResult.Applied, created);
        Assert.Equal(ConditionalWriteResult.RevisionConflict, duplicate);
        Assert.Equal(ConditionalWriteResult.Applied, updated);
        Assert.Equal(ConditionalWriteResult.RevisionConflict, stale);
        Assert.Equal(updatedTenant, loaded);
    }
}
