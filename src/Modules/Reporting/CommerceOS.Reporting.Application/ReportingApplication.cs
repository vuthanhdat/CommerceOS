using System.Globalization;
using ReportingTenantId = CommerceOS.Reporting.Domain.ReportingTenantId;
using CommerceOS.Reporting.Domain;

namespace CommerceOS.Reporting.Application;

public sealed record TrustedReportingContext(ReportingTenantId TenantId);
public enum ReportingOutcome { Applied, AlreadyApplied, Invalid, NeedsAttention }
public sealed record ReportingFactEnvelope(string EventId, string EventType, int EventVersion, string TenantId, string AggregateId, DateTimeOffset OccurredAt, string CorrelationId, string? CausationId);
public sealed record OrderConfirmedReportingFact(ReportingFactEnvelope Envelope, long OrderTotalVnd, IReadOnlyList<ProductQuantity> Lines, string TenantTimeZoneId);
public sealed record PaymentTerminalReportingFact(ReportingFactEnvelope Envelope, bool IsDefinitiveFailure, bool IsOutcomeUnknown, string TenantTimeZoneId);
public sealed record CorrectionReportingFact(ReportingFactEnvelope Envelope, long AmountVnd, bool IsRefund, string TenantTimeZoneId);
public interface IReportingStore
{
    Task<ReportingOutcome> ApplyAsync(TrustedReportingContext context, string projectionName, string eventId, DateOnly businessDate, OperationalDay delta, IReadOnlyList<ProductQuantity> productDeltas, DateTimeOffset occurredAt, CancellationToken ct);
    Task<ProjectionCheckpoint?> GetCheckpointAsync(TrustedReportingContext context, string projectionName, CancellationToken ct);
    Task<IReadOnlyList<OperationalDay>> ListDaysAsync(TrustedReportingContext context, DateOnly from, DateOnly through, CancellationToken ct);
    Task<IReadOnlyList<ProductQuantity>> ListProductsAsync(TrustedReportingContext context, DateOnly from, DateOnly through, CancellationToken ct);
    Task<ReportingOutcome> BeginRebuildAsync(TrustedReportingContext context, string projectionName, CancellationToken ct);
    Task<ReportingOutcome> CompleteRebuildAsync(TrustedReportingContext context, string projectionName, DateTimeOffset completedAt, CancellationToken ct);
}
public sealed class ReportingProjectionConsumer(IReportingStore store)
{
    public Task<ReportingOutcome> ApplyAsync(OrderConfirmedReportingFact fact, CancellationToken ct)
    {
        if (!Valid(fact.Envelope) || fact.OrderTotalVnd < 0 || fact.Lines.Count == 0 || fact.Lines.Any(x => x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.ProductId))) return Task.FromResult(ReportingOutcome.Invalid);
        var context = new TrustedReportingContext(new(fact.Envelope.TenantId)); var day = BusinessDate(fact.Envelope.OccurredAt, fact.TenantTimeZoneId);
        return store.ApplyAsync(context, "operational", fact.Envelope.EventId, day, new(context.TenantId, day, 1, fact.OrderTotalVnd, 0, 0, 0, 0), fact.Lines, fact.Envelope.OccurredAt, ct);
    }
    public Task<ReportingOutcome> ApplyAsync(PaymentTerminalReportingFact fact, CancellationToken ct)
    {
        if (!Valid(fact.Envelope) || fact.IsOutcomeUnknown) return fact.IsOutcomeUnknown ? Task.FromResult(ReportingOutcome.AlreadyApplied) : Task.FromResult(ReportingOutcome.Invalid);
        var context = new TrustedReportingContext(new(fact.Envelope.TenantId)); var day = BusinessDate(fact.Envelope.OccurredAt, fact.TenantTimeZoneId);
        return store.ApplyAsync(context, "operational", fact.Envelope.EventId, day, new(context.TenantId, day, 0, 0, 0, 0, fact.IsDefinitiveFailure ? 1 : 0, 1), [], fact.Envelope.OccurredAt, ct);
    }
    public Task<ReportingOutcome> ApplyAsync(CorrectionReportingFact fact, CancellationToken ct)
    {
        if (!Valid(fact.Envelope) || fact.AmountVnd < 0) return Task.FromResult(ReportingOutcome.Invalid);
        var context = new TrustedReportingContext(new(fact.Envelope.TenantId)); var day = BusinessDate(fact.Envelope.OccurredAt, fact.TenantTimeZoneId);
        return store.ApplyAsync(context, "operational", fact.Envelope.EventId, day, new(context.TenantId, day, 0, 0, fact.IsRefund ? 0 : fact.AmountVnd, fact.IsRefund ? fact.AmountVnd : 0, 0, 0), [], fact.Envelope.OccurredAt, ct);
    }
    private static bool Valid(ReportingFactEnvelope e) => !string.IsNullOrWhiteSpace(e.EventId) && !string.IsNullOrWhiteSpace(e.TenantId) && e.EventVersion == 1 && !string.IsNullOrWhiteSpace(e.CorrelationId);
    private static DateOnly BusinessDate(DateTimeOffset occurredAt, string timeZoneId) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(occurredAt, string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId).DateTime);
}
public sealed class OperationalKpiQuery(IReportingStore store)
{
    public async Task<OperationalKpis> GetAsync(TrustedReportingContext context, DateOnly from, DateOnly through, CancellationToken ct)
    {
        var days = await store.ListDaysAsync(context, from, through, ct); var products = await store.ListProductsAsync(context, from, through, ct);
        var orderCount = days.Sum(x => x.ConfirmedOrderCount); var total = days.Sum(x => x.ConfirmedOrderTotalVnd); var terminal = days.Sum(x => x.TerminalPayments);
        return new(orderCount, orderCount == 0 ? 0 : total / orderCount, products.OrderByDescending(x => x.Quantity).ThenBy(x => x.ProductId, StringComparer.Ordinal).Take(10).ToArray(), terminal == 0 ? 0 : decimal.Divide(days.Sum(x => x.FailedPayments), terminal), total, days.Sum(x => x.CancelledAmountVnd), days.Sum(x => x.RefundedAmountVnd));
    }
}
/// <summary>Reporting consumes a read-only Accounting port; it neither recalculates nor mutates journals.</summary>
public sealed record FinancialReportRequest(DateOnly From, DateOnly Through, string? Cursor, int PageSize);
public sealed record FinancialReportPage(IReadOnlyList<FinancialJournalView> Journals, string? NextCursor, IReadOnlyList<TrialBalanceView> TrialBalance);
public sealed record FinancialJournalView(string JournalId, DateOnly EffectiveDate, string SourceIdentity, long DebitVnd, long CreditVnd);
public sealed record TrialBalanceView(string AccountId, long DebitVnd, long CreditVnd);
public interface IAccountingReportingQuery { Task<FinancialReportPage> GetReadOnlyReportAsync(string trustedTenantId, FinancialReportRequest request, CancellationToken ct); }
public sealed class FinancialBackOfficeQuery(IAccountingReportingQuery accounting) { public Task<FinancialReportPage> GetAsync(TrustedReportingContext context, FinancialReportRequest request, CancellationToken ct) => accounting.GetReadOnlyReportAsync(context.TenantId.Value, request with { PageSize = Math.Clamp(request.PageSize, 1, 100) }, ct); }

/// <summary>A support projection, intentionally not a cross-domain refund-completed authority.</summary>
public sealed record RefundProgress(string RefundApprovalId, bool RevenueCorrected, bool StockReturned, bool PaymentRefunded, DateTimeOffset LastObservedAt)
{
    public bool IsFullyObserved => RevenueCorrected && StockReturned && PaymentRefunded;
}
public interface IRefundProgressStore { Task<RefundProgress?> GetAsync(string trustedTenantId, string refundApprovalId, CancellationToken ct); Task<ReportingOutcome> SaveAsync(string trustedTenantId, RefundProgress progress, CancellationToken ct); }
public sealed class RefundProgressProjection(IRefundProgressStore store)
{
    public Task<ReportingOutcome> ObserveRevenueCorrectionAsync(string tenant, string approval, DateTimeOffset observedAt, CancellationToken ct) => ObserveAsync(tenant, approval, observedAt, x => x with { RevenueCorrected = true }, ct);
    public Task<ReportingOutcome> ObserveStockReturnedAsync(string tenant, string approval, DateTimeOffset observedAt, CancellationToken ct) => ObserveAsync(tenant, approval, observedAt, x => x with { StockReturned = true }, ct);
    public Task<ReportingOutcome> ObservePaymentRefundedAsync(string tenant, string approval, DateTimeOffset observedAt, CancellationToken ct) => ObserveAsync(tenant, approval, observedAt, x => x with { PaymentRefunded = true }, ct);
    private async Task<ReportingOutcome> ObserveAsync(string tenant, string approval, DateTimeOffset at, Func<RefundProgress, RefundProgress> update, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(approval)) return ReportingOutcome.Invalid;
        var current = await store.GetAsync(tenant, approval, ct) ?? new RefundProgress(approval, false, false, false, at);
        return await store.SaveAsync(tenant, update(current) with { LastObservedAt = at }, ct);
    }
}
