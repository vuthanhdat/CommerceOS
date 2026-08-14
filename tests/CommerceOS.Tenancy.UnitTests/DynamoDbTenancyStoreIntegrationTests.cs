using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Application.Onboarding;
using CommerceOS.Tenancy.Application.Persistence;
using CommerceOS.Tenancy.Application.PlatformAdministration;
using CommerceOS.Tenancy.Domain;
using CommerceOS.Tenancy.Infrastructure.Persistence;
using CommerceOS.SubscriptionBilling.Application.Catalog;
using CommerceOS.SubscriptionBilling.Application.Trial;
using CommerceOS.SubscriptionBilling.Infrastructure.Persistence;

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

    [Fact]
    public async Task MembershipDiscoveryAndAuthorityResolutionWorkAgainstConfiguredLocalStack()
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
        var subjectId = new SubjectId($"subject-{Guid.NewGuid():N}");
        var scope = new TrustedTenantPersistenceScope(tenantId);
        var tenant = new Tenant(tenantId, TenantStatus.Active, new BusinessProfile("Merchant", "Asia/Ho_Chi_Minh"), 1);
        var membership = new Membership(
            new MembershipId($"membership-{Guid.NewGuid():N}"),
            tenantId,
            subjectId,
            MerchantRole.Owner,
            MembershipStatus.Active,
            1);

        Assert.Equal(ConditionalWriteResult.Applied, await store.SaveTenantAsync(scope, tenant, null, CancellationToken.None));
        Assert.Equal(ConditionalWriteResult.Applied, await store.SaveMembershipAsync(scope, membership, null, CancellationToken.None));

        var resolver = new TenantAuthorityResolver(store);
        var result = await resolver.ResolveTenantMutationAuthorityAsync(
            new MerchantAuthorityRequest(new AuthenticatedMerchantPrincipal(subjectId), null, "integration-correlation"),
            CancellationToken.None);

        Assert.True(result.IsAuthorized);
        Assert.Equal(tenantId, result.Context!.TenantId);
        Assert.Equal(MerchantRole.Owner, result.Context.Role);
    }

    [Fact]
    public async Task PlatformLifecycleWritesAuditIntentAndPreservesMerchantRecordsAgainstConfiguredLocalStack()
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
        var tenant = new Tenant(tenantId, TenantStatus.Active, new BusinessProfile("Merchant", "Asia/Ho_Chi_Minh"), 1);
        var membership = new Membership(new MembershipId($"membership-{Guid.NewGuid():N}"), tenantId, new SubjectId($"subject-{Guid.NewGuid():N}"), MerchantRole.Owner, MembershipStatus.Active, 1);
        var scope = new TrustedTenantPersistenceScope(tenantId);
        Assert.Equal(ConditionalWriteResult.Applied, await store.SaveTenantAsync(scope, tenant, null, CancellationToken.None));
        Assert.Equal(ConditionalWriteResult.Applied, await store.SaveMembershipAsync(scope, membership, null, CancellationToken.None));

        var lifecycle = new TenantLifecycleAdministrationService(store);
        var admin = TrustedPlatformAdminContext.FromAuthenticatedPlatformAdmin(new SubjectId("platform-admin"), "platform-correlation");
        var suspended = await lifecycle.ExecuteAsync(admin, new TenantLifecycleCommand(tenantId, TenantLifecycleAction.Suspend, 1, "suspend-1", "support investigation"), CancellationToken.None);
        var replay = await lifecycle.ExecuteAsync(admin, new TenantLifecycleCommand(tenantId, TenantLifecycleAction.Suspend, 1, "suspend-1", "support investigation"), CancellationToken.None);
        var loadedMembership = await store.GetMembershipAsync(scope, membership.Id, CancellationToken.None);

        Assert.Equal(TenantLifecycleOutcome.Applied, suspended.Outcome);
        Assert.Equal(TenantLifecycleOutcome.Applied, replay.Outcome);
        Assert.Equal(TenantStatus.Suspended, (await store.GetForPlatformSupportAsync(tenantId, CancellationToken.None))!.Status);
        Assert.Equal(membership, loadedMembership);
    }

    [Fact]
    public async Task OnboardingCommitsTenantOwnerAndDurablyRecoversOneTrialAgainstConfiguredLocalStack()
    {
        var endpoint = Environment.GetEnvironmentVariable("COMMERCEOS_LOCALSTACK_ENDPOINT");
        var tenancyTable = Environment.GetEnvironmentVariable("COMMERCEOS_TENANCY_TABLE");
        var subscriptionTable = Environment.GetEnvironmentVariable("COMMERCEOS_SUBSCRIPTION_BILLING_TABLE");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(tenancyTable) || string.IsNullOrWhiteSpace(subscriptionTable))
        {
            return;
        }

        using var client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" });
        var tenancy = new DynamoDbTenantOnboardingStore(client, new DynamoDbTenancyOptions(tenancyTable));
        var catalog = new CatalogQueryService(new DynamoDbSubscriptionCatalogStore(client, new DynamoDbSubscriptionBillingOptions(subscriptionTable)));
        var trials = new TrialSubscriptionService(catalog, new DynamoDbTrialSubscriptionStore(client, new DynamoDbSubscriptionBillingOptions(subscriptionTable)));
        var coordinator = new TenantOnboardingCoordinator(tenancy, trials);
        var context = TrustedOnboardingContext.FromVerifiedIdentity(new SubjectId($"subject-{Guid.NewGuid():N}"), "owner@example.test");
        var profile = new BusinessProfile("LocalStack merchant", "Asia/Ho_Chi_Minh");

        var first = await coordinator.RegisterAsync(context, "onboarding-1", profile, "correlation-1", CancellationToken.None);
        var replay = await coordinator.RegisterAsync(context, "onboarding-1", profile, "correlation-2", CancellationToken.None);
        var operation = await tenancy.GetAsync(context, "onboarding-1", CancellationToken.None);
        var trial = await new DynamoDbTrialSubscriptionStore(client, new DynamoDbSubscriptionBillingOptions(subscriptionTable))
            .GetForOnboardingAsync(first.TenantId!, first.OperationId!, CancellationToken.None);

        Assert.Equal(MerchantOnboardingOutcome.Completed, first.Outcome);
        Assert.Equal(first, replay);
        Assert.NotNull(operation);
        Assert.Equal(OnboardingStatus.Completed, operation.Status);
        Assert.NotNull(trial);
        Assert.Equal("trial-v1", trial.Entitlements.TrialTermsVersionId);
        Assert.Equal(30, trial.Entitlements.DurationDays);
        Assert.Equal(3, trial.Entitlements.MaxActiveMemberships);
    }

    [Fact]
    public async Task PendingLocalStackOnboardingRecoversFromTheSameDurableWorkSource()
    {
        var endpoint = Environment.GetEnvironmentVariable("COMMERCEOS_LOCALSTACK_ENDPOINT");
        var tenancyTable = Environment.GetEnvironmentVariable("COMMERCEOS_TENANCY_TABLE");
        var subscriptionTable = Environment.GetEnvironmentVariable("COMMERCEOS_SUBSCRIPTION_BILLING_TABLE");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(tenancyTable) || string.IsNullOrWhiteSpace(subscriptionTable))
        {
            return;
        }

        using var client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" });
        var tenancy = new DynamoDbTenantOnboardingStore(client, new DynamoDbTenancyOptions(tenancyTable));
        var context = TrustedOnboardingContext.FromVerifiedIdentity(new SubjectId($"subject-{Guid.NewGuid():N}"), "owner@example.test");
        var pending = await new TenantOnboardingCoordinator(tenancy, new FailingTrialStarter()).RegisterAsync(
            context,
            "onboarding-recovery-1",
            new BusinessProfile("Recovery merchant", "Asia/Ho_Chi_Minh"),
            "correlation-recovery",
            CancellationToken.None);
        var trialStarter = new TrialSubscriptionService(
            new CatalogQueryService(new DynamoDbSubscriptionCatalogStore(client, new DynamoDbSubscriptionBillingOptions(subscriptionTable))),
            new DynamoDbTrialSubscriptionStore(client, new DynamoDbSubscriptionBillingOptions(subscriptionTable)));
        var work = new TrialBootstrapWorkItem(
            $"trial-work-{pending.OperationId}",
            pending.OperationId!,
            pending.TenantId!,
            $"merchant-onboarding:{pending.OperationId}",
            "correlation-recovery");
        var worker = new OnboardingTrialRecoveryWorker(tenancy, trialStarter);

        var recovered = await worker.ProcessAsync(work, CancellationToken.None);
        var duplicate = await worker.ProcessAsync(work, CancellationToken.None);

        Assert.Equal(MerchantOnboardingOutcome.PendingTrial, pending.Outcome);
        Assert.True(recovered);
        Assert.False(duplicate);
        Assert.Equal(OnboardingStatus.Completed, (await tenancy.GetAsync(context, "onboarding-recovery-1", CancellationToken.None))!.Status);
    }

    private sealed class FailingTrialStarter : CommerceOS.SubscriptionBilling.Contracts.ITrialSubscriptionStarter
    {
        public Task<CommerceOS.SubscriptionBilling.Contracts.TrialSubscriptionStartResult> StartTrialSubscriptionAsync(
            CommerceOS.SubscriptionBilling.Contracts.StartTrialSubscriptionCommand command,
            CancellationToken cancellationToken) => throw new TimeoutException();
    }
}
