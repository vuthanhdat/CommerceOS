using CommerceOS.Platform.Application.Readiness;
using CommerceOS.Platform.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceOS.Platform.UnitTests;

public sealed class PlatformReadinessTests
{
    [Fact]
    public void PlatformModuleExposesDeterministicHealthSnapshot()
    {
        var services = new ServiceCollection();
        services.AddPlatformModule();

        using var provider = services.BuildServiceProvider();
        var readiness = provider.GetRequiredService<IPlatformReadiness>();

        Assert.Equal(new ReadinessSnapshot("ok", "commerceos-api"), readiness.GetSnapshot());
    }
}
