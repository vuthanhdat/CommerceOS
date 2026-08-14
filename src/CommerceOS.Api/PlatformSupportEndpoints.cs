using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.Platform.Application.Readiness;
using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Application.PlatformAdministration;
using CommerceOS.Tenancy.Domain;
using CommerceOS.Tenancy.Infrastructure.Persistence;

namespace CommerceOS.Api;

public sealed record PlatformLifecycleInput(long ExpectedRevision, string OperationId, string Reason);
public interface IPlatformSupportIdentityResolver { SubjectId? Resolve(HttpContext context); }
internal sealed class PlatformSupportIdentityResolver(IConfiguration configuration) : IPlatformSupportIdentityResolver { public SubjectId? Resolve(HttpContext context) { var subject = configuration["COMMERCEOS_TEST_PLATFORM_IDENTITY_ENABLED"] == "1" ? context.Request.Headers["X-CommerceOS-Test-Platform-Subject"].ToString() : null; return string.IsNullOrWhiteSpace(subject) ? null : new SubjectId(subject); } }
public static class PlatformSupportEndpoints
{
    public static void AddPlatformSupportServices(this IServiceCollection services, IConfiguration configuration) { var endpoint=configuration["COMMERCEOS_LOCALSTACK_ENDPOINT"];var table=configuration["COMMERCEOS_TENANCY_TABLE"];services.AddSingleton<IPlatformSupportIdentityResolver,PlatformSupportIdentityResolver>();if(string.IsNullOrWhiteSpace(endpoint)||string.IsNullOrWhiteSpace(table))return;services.AddSingleton<IAmazonDynamoDB>(_=>new AmazonDynamoDBClient(new BasicAWSCredentials("test","test"),new AmazonDynamoDBConfig{ServiceURL=endpoint,AuthenticationRegion="us-east-1"}));services.AddSingleton(new DynamoDbTenancyOptions(table));services.AddSingleton<DynamoDbTenancyStore>();services.AddSingleton<IPlatformTenantAdministrationStore>(p=>p.GetRequiredService<DynamoDbTenancyStore>());services.AddSingleton<TenantLifecycleAdministrationService>();services.AddSingleton<TenantPlatformSupportQueryService>(); }
    public static void MapPlatformSupportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/platform/health", (IPlatformReadiness readiness) => Results.Ok(readiness.GetSnapshot()));
        app.MapGet("/api/v1/platform/tenants", async (string? search,int? pageSize,HttpContext h,IPlatformSupportIdentityResolver identity,TenantPlatformSupportQueryService? service,CancellationToken ct)=>{var context=Read(identity,h);if(context is null)return Results.Unauthorized();return service is null?Off(h):Results.Ok(await service.ListTenantsAsync(context,search,pageSize??50,ct));});
        app.MapGet("/api/v1/platform/tenants/{id}", async (string id,HttpContext h,IPlatformSupportIdentityResolver identity,TenantPlatformSupportQueryService? service,CancellationToken ct)=>{var context=Read(identity,h);if(context is null)return Results.Unauthorized();if(service is null)return Off(h);var tenant=await service.GetTenantAsync(context,new(id),ct);return tenant is null?Results.NotFound():Results.Ok(tenant);});
        MapLifecycle(app,"suspend",TenantLifecycleAction.Suspend);MapLifecycle(app,"reactivate",TenantLifecycleAction.Reactivate);
    }
    private static void MapLifecycle(WebApplication app,string action,TenantLifecycleAction kind)=>app.MapPost($"/api/v1/platform/tenants/{{id}}/{action}",async(string id,PlatformLifecycleInput input,HttpContext h,IPlatformSupportIdentityResolver identity,TenantLifecycleAdministrationService? service,CancellationToken ct)=>{var subject=identity.Resolve(h);if(subject is null)return Results.Unauthorized();if(service is null)return Off(h);try{var result=await service.ExecuteAsync(TrustedPlatformAdminContext.FromAuthenticatedPlatformAdmin(subject.Value,h.TraceIdentifier),new(new(id),kind,input.ExpectedRevision,input.OperationId,input.Reason),ct);return result.Outcome is TenantLifecycleOutcome.Applied or TenantLifecycleOutcome.AlreadyApplied?Results.Ok(result):Result(result.Outcome,h.TraceIdentifier);}catch(ArgumentException){return Results.Problem(statusCode:422,title:"PLATFORM_LIFECYCLE_INVALID");}});
    private static TrustedPlatformSupportReadContext? Read(IPlatformSupportIdentityResolver identity,HttpContext h)=>identity.Resolve(h) is { } subject?TrustedPlatformSupportReadContext.FromAuthenticatedPlatformSupport(subject,h.TraceIdentifier):null; private static IResult Result(TenantLifecycleOutcome outcome,string correlation)=>Results.Problem(statusCode:outcome is TenantLifecycleOutcome.NotFound?404:409,title:$"TENANT_{outcome.ToString().ToUpperInvariant()}",extensions:new Dictionary<string,object?>{{"correlationId",correlation}});private static IResult Off(HttpContext h)=>Results.Problem(statusCode:503,title:"PLATFORM_SUPPORT_UNAVAILABLE",extensions:new Dictionary<string,object?>{{"correlationId",h.TraceIdentifier}});
}
