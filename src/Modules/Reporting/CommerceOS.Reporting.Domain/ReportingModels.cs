namespace CommerceOS.Reporting.Domain;

public readonly record struct ReportingTenantId(string Value);
public sealed record ProjectionCheckpoint(ReportingTenantId TenantId, string ProjectionName, string? LastEventId, DateTimeOffset? LastOccurredAt, bool RebuildInProgress, DateTimeOffset UpdatedAt);
public sealed record OperationalDay(ReportingTenantId TenantId, DateOnly BusinessDate, long ConfirmedOrderCount, long ConfirmedOrderTotalVnd, long CancelledAmountVnd, long RefundedAmountVnd, long FailedPayments, long TerminalPayments);
public sealed record ProductQuantity(string ProductId, string NameSnapshot, long Quantity);
public sealed record OperationalKpis(long OrderCount, long AverageOrderValueVnd, IReadOnlyList<ProductQuantity> TopProducts, decimal FailedPaymentRate, long OperationalGrossSalesVnd, long CancellationAmountVnd, long RefundAmountVnd)
{
    public const string GrossSalesLabel = "Operational Gross Sales (not accounting revenue)";
}
