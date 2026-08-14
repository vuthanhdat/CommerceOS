namespace CommerceOS.Platform.Application.Observability;

/// <summary>Safe cross-cutting diagnostic shape. Business payloads, tokens and customer details are deliberately absent.</summary>
public sealed record OperationalContext(string CorrelationId, string? CausationId, string? SourceIdentity, string? TenantHash);
public sealed record OperationalMetric(string Name, long Value, OperationalContext Context, DateTimeOffset ObservedAt);
public interface IOperationalTelemetry { void Record(OperationalMetric metric); }
public static class OperationalMetrics
{
    public const string QueueRetry = "queue.retry";
    public const string QueueDlq = "queue.dlq";
    public const string ProviderFailure = "provider.failure";
    public const string OutcomeUnknown = "payment.outcome_unknown";
    public const string NeedsAttention = "workflow.needs_attention";
    public static void Record(IOperationalTelemetry telemetry, string name, OperationalContext context, TimeProvider? clock = null)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(context.CorrelationId)) throw new ArgumentException("Metric name and correlation are required.");
        telemetry.Record(new OperationalMetric(name, 1, context, (clock ?? TimeProvider.System).GetUtcNow()));
    }
}
