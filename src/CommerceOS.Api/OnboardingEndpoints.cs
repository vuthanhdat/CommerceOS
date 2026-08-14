using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.SubscriptionBilling.Application.Catalog;
using CommerceOS.SubscriptionBilling.Application.Entitlements;
using CommerceOS.SubscriptionBilling.Application.PlatformCharges;
using CommerceOS.SubscriptionBilling.Application.PaidLifecycle;
using CommerceOS.SubscriptionBilling.Application.Trial;
using CommerceOS.SubscriptionBilling.Contracts;
using CommerceOS.SubscriptionBilling.Infrastructure.Persistence;
using CommerceOS.SubscriptionBilling.Infrastructure.Provider;
using CommerceOS.Tenancy.Application.Onboarding;
using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Application.Persistence;
using CommerceOS.Tenancy.Domain;
using CommerceOS.Tenancy.Infrastructure.Persistence;

namespace CommerceOS.Api;

public sealed record RegisterMerchantRequest(string DisplayName, string TimeZoneIana);

public interface IOnboardingIdentityResolver
{
    TrustedOnboardingContext? Resolve(HttpContext context);
}

internal sealed class DisabledOnboardingIdentityResolver : IOnboardingIdentityResolver
{
    public TrustedOnboardingContext? Resolve(HttpContext context) => null;
}

/// <summary>
/// Test-only identity edge. It is installed only by an explicit local setting;
/// the default API refuses onboarding until a real verified-identity adapter is
/// composed.
/// </summary>
internal sealed class DevelopmentHeaderOnboardingIdentityResolver : IOnboardingIdentityResolver
{
    public TrustedOnboardingContext? Resolve(HttpContext context)
    {
        var subject = context.Request.Headers["X-CommerceOS-Test-Subject"].ToString();
        var email = context.Request.Headers["X-CommerceOS-Test-Verified-Email"].ToString();
        return string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email)
            ? null
            : TrustedOnboardingContext.FromVerifiedIdentity(new SubjectId(subject), email);
    }
}

public static class OnboardingEndpoints
{
    public static void AddOnboardingServices(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["COMMERCEOS_LOCALSTACK_ENDPOINT"];
        var tenancyTable = configuration["COMMERCEOS_TENANCY_TABLE"];
        var subscriptionTable = configuration["COMMERCEOS_SUBSCRIPTION_BILLING_TABLE"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(tenancyTable) || string.IsNullOrWhiteSpace(subscriptionTable))
        {
            services.AddSingleton<IOnboardingIdentityResolver, DisabledOnboardingIdentityResolver>();
            return;
        }

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" }));
        services.AddSingleton(new DynamoDbTenancyOptions(tenancyTable));
        services.AddSingleton(new DynamoDbSubscriptionBillingOptions(subscriptionTable));
        services.AddSingleton<ITenantOnboardingStore, DynamoDbTenantOnboardingStore>();
        services.AddSingleton<ITenancyStore, DynamoDbTenancyStore>();
        services.AddSingleton<ITenantAuthorityResolver, TenantAuthorityResolver>();
        services.AddSingleton<ISubscriptionCatalogStore, DynamoDbSubscriptionCatalogStore>();
        services.AddSingleton<ITrialSubscriptionStore, DynamoDbTrialSubscriptionStore>();
        services.AddSingleton<ISubscriptionCatalogQuery, CatalogQueryService>();
        services.AddSingleton<ITrialSubscriptionStarter, TrialSubscriptionService>();
        services.AddSingleton<IEntitlementEvaluator, EntitlementEvaluator>();
        services.AddSingleton<IPlatformChargeStore, DynamoDbPlatformChargeStore>();
        services.AddSingleton<DeterministicSaasBillingProviderState>();
        services.AddSingleton<IPlatformBillingProvider, DeterministicSaasBillingProvider>();
        services.AddSingleton<PlatformChargeService>();
        services.AddSingleton<IPaidSubscriptionStore, DynamoDbPaidSubscriptionStore>();
        services.AddSingleton<ISubscriptionUsageAssessor, FailClosedSubscriptionUsageAssessor>();
        services.AddSingleton<PaidSubscriptionLifecycleService>();
        services.AddSingleton<TenantOnboardingCoordinator>();
        services.AddSingleton<IOnboardingIdentityResolver>(configuration["COMMERCEOS_TEST_IDENTITY_ENABLED"] == "1"
            ? new DevelopmentHeaderOnboardingIdentityResolver()
            : new DisabledOnboardingIdentityResolver());
    }

    public static void MapOnboardingEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/merchant-onboarding", async (
            RegisterMerchantRequest request,
            HttpContext http,
            IOnboardingIdentityResolver identity,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var context = identity.Resolve(http);
            if (context is null)
            {
                return Results.Unauthorized();
            }
            var coordinator = services.GetService<TenantOnboardingCoordinator>();
            if (coordinator is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Onboarding infrastructure is unavailable.");
            }
            var idempotencyKey = http.Request.Headers["Idempotency-Key"].ToString();
            try
            {
                var result = await coordinator.RegisterAsync(
                    context,
                    idempotencyKey,
                    new BusinessProfile(request.DisplayName, request.TimeZoneIana),
                    http.TraceIdentifier,
                    cancellationToken);
                return result.Outcome switch
                {
                    MerchantOnboardingOutcome.Completed => Results.Created($"/api/v1/merchant-onboarding/{result.OperationId}", result),
                    MerchantOnboardingOutcome.PendingTrial => Results.Accepted($"/api/v1/merchant-onboarding/{result.OperationId}", result),
                    _ => Results.Conflict(new { code = "TENANT_REGISTRATION_CONFLICT" })
                };
            }
            catch (ArgumentException error)
            {
                return Results.BadRequest(new { error.Message });
            }
        }).AllowAnonymous();
    }
}
