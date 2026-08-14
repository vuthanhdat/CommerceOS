using CommerceOS.Platform.Application.Readiness;
using CommerceOS.Platform.Infrastructure;
using CommerceOS.Platform.Application.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceOS.Platform.UnitTests;

public sealed class PlatformReadinessTests
{
    [Fact]
    public void OperationalMetricsRequireCorrelationAndExcludeBusinessPayloads()
    {
        var telemetry = new Telemetry(); var context = new OperationalContext("correlation", "cause", "source", "tenant-hash");
        OperationalMetrics.Record(telemetry, OperationalMetrics.OutcomeUnknown, context);
        Assert.Equal(OperationalMetrics.OutcomeUnknown, telemetry.Items.Single().Name);
        Assert.Throws<ArgumentException>(() => OperationalMetrics.Record(telemetry, OperationalMetrics.QueueDlq, context with { CorrelationId = "" }));
    }
    [Fact]
    public void PlatformModuleExposesDeterministicHealthSnapshot()
    {
        var services = new ServiceCollection();
        services.AddPlatformModule();

        using var provider = services.BuildServiceProvider();
        var readiness = provider.GetRequiredService<IPlatformReadiness>();

        Assert.Equal(new ReadinessSnapshot("ok", "commerceos-api"), readiness.GetSnapshot());
    }
    private sealed class Telemetry : IOperationalTelemetry { public List<OperationalMetric> Items { get; } = []; public void Record(OperationalMetric metric) => Items.Add(metric); }
}
