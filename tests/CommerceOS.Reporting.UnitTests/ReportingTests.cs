using CommerceOS.Reporting.Application;
using CommerceOS.Reporting.Domain;

namespace CommerceOS.Reporting.UnitTests;

public sealed class ReportingTests
{
    [Fact]
    public async Task ProjectionDedupesFactsAndExcludesUnknownPaymentAttempts()
    {
        var store = new Store(); var consumer = new ReportingProjectionConsumer(store); var confirmed = Envelope("confirmed", "OrderConfirmed", new DateTimeOffset(2026, 3, 8, 17, 30, 0, TimeSpan.Zero));
        var order = new OrderConfirmedReportingFact(confirmed, 100, [new("p1", "Product", 2)], "Asia/Bangkok");
        Assert.Equal(ReportingOutcome.Applied, await consumer.ApplyAsync(order, default));
        Assert.Equal(ReportingOutcome.AlreadyApplied, await consumer.ApplyAsync(order, default));
        Assert.Equal(ReportingOutcome.AlreadyApplied, await consumer.ApplyAsync(new PaymentTerminalReportingFact(Envelope("unknown", "PaymentOutcome", confirmed.OccurredAt), false, true, "Asia/Bangkok"), default));
        Assert.Equal(1, (await new OperationalKpiQuery(store).GetAsync(new(new("tenant")), new(2026, 3, 9), new(2026, 3, 9), default)).OrderCount);
    }
    [Fact]
    public async Task CorrectionsUseTheirOwnBusinessDate()
    {
        var store = new Store(); var consumer = new ReportingProjectionConsumer(store);
        await consumer.ApplyAsync(new CorrectionReportingFact(Envelope("refund", "RefundApproved", new DateTimeOffset(2026, 3, 8, 17, 1, 0, TimeSpan.Zero)), 25, true, "Asia/Bangkok"), default);
        var kpis = await new OperationalKpiQuery(store).GetAsync(new(new("tenant")), new(2026, 3, 9), new(2026, 3, 9), default);
        Assert.Equal(25, kpis.RefundAmountVnd);
    }
    private static ReportingFactEnvelope Envelope(string id, string type, DateTimeOffset at) => new(id, type, 1, "tenant", "aggregate", at, "c", null);
    private sealed class Store : IReportingStore
    {
        private readonly HashSet<string> inbox = []; private readonly Dictionary<DateOnly, OperationalDay> days = []; private readonly Dictionary<string, ProductQuantity> products = [];
        public Task<ReportingOutcome> ApplyAsync(TrustedReportingContext c, string name, string id, DateOnly day, OperationalDay delta, IReadOnlyList<ProductQuantity> productDeltas, DateTimeOffset at, CancellationToken ct) { if (!inbox.Add(id)) return Task.FromResult(ReportingOutcome.AlreadyApplied); var current = days.GetValueOrDefault(day) ?? new(c.TenantId, day, 0, 0, 0, 0, 0, 0); days[day] = current with { ConfirmedOrderCount = current.ConfirmedOrderCount + delta.ConfirmedOrderCount, ConfirmedOrderTotalVnd = current.ConfirmedOrderTotalVnd + delta.ConfirmedOrderTotalVnd, CancelledAmountVnd = current.CancelledAmountVnd + delta.CancelledAmountVnd, RefundedAmountVnd = current.RefundedAmountVnd + delta.RefundedAmountVnd, FailedPayments = current.FailedPayments + delta.FailedPayments, TerminalPayments = current.TerminalPayments + delta.TerminalPayments }; foreach (var p in productDeltas) { var prior = products.GetValueOrDefault(p.ProductId) ?? p with { Quantity = 0 }; products[p.ProductId] = prior with { Quantity = prior.Quantity + p.Quantity }; } return Task.FromResult(ReportingOutcome.Applied); }
        public Task<ProjectionCheckpoint?> GetCheckpointAsync(TrustedReportingContext c, string n, CancellationToken ct) => Task.FromResult<ProjectionCheckpoint?>(new(c.TenantId, n, null, null, false, DateTimeOffset.UtcNow));
        public Task<IReadOnlyList<OperationalDay>> ListDaysAsync(TrustedReportingContext c, DateOnly from, DateOnly through, CancellationToken ct) => Task.FromResult<IReadOnlyList<OperationalDay>>(days.Where(x => x.Key >= from && x.Key <= through).Select(x => x.Value).ToArray());
        public Task<IReadOnlyList<ProductQuantity>> ListProductsAsync(TrustedReportingContext c, DateOnly from, DateOnly through, CancellationToken ct) => Task.FromResult<IReadOnlyList<ProductQuantity>>(products.Values.ToArray());
        public Task<ReportingOutcome> BeginRebuildAsync(TrustedReportingContext c, string n, CancellationToken ct) => Task.FromResult(ReportingOutcome.Applied);
        public Task<ReportingOutcome> CompleteRebuildAsync(TrustedReportingContext c, string n, DateTimeOffset at, CancellationToken ct) => Task.FromResult(ReportingOutcome.Applied);
    }
}
