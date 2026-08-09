using CommerceOS.Platform.Application.Readiness;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceOS.Platform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformModule(this IServiceCollection services)
    {
        services.AddSingleton<IPlatformReadiness, PlatformReadiness>();
        return services;
    }
}

internal sealed class PlatformReadiness : IPlatformReadiness
{
    public ReadinessSnapshot GetSnapshot() => new("ok", "commerceos-api");
}

