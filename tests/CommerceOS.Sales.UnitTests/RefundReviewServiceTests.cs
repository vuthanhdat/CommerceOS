using CommerceOS.Sales.Application;
using CommerceOS.Sales.Contracts;
using CommerceOS.Sales.Domain;

namespace CommerceOS.Sales.UnitTests;

public sealed class RefundReviewServiceTests
{
    [Fact]
    public async Task OnlyOwnerOrAdminCanDecideAndApprovalWritesOneLogicalDecision()
    {
        var orders = new Orders(); var refunds = new Refunds(); var service = new RefundReviewService(orders, refunds, new FixedTime());
        var request = new RequestSalesRefund("tenant", "order", "payment", 10, "VND", [new("product", 1, "issue")], "request-source", "staff", TrustedRefundRole.Staff, "c");
        var created = await service.RequestAsync(request, default); Assert.Equal(RefundCommandOutcome.Requested, created.Outcome);
        Assert.Equal(RefundCommandOutcome.Forbidden, (await service.DecideAsync(new("tenant", created.RefundRequestId!, 1, "approve", "staff", TrustedRefundRole.Staff, true, "c"), default)).Outcome);
        Assert.Equal(RefundCommandOutcome.Approved, (await service.DecideAsync(new("tenant", created.RefundRequestId!, 1, "approve", "owner", TrustedRefundRole.Owner, true, "c"), default)).Outcome);
        Assert.Equal(RefundCommandOutcome.AlreadyApplied, (await service.DecideAsync(new("tenant", created.RefundRequestId!, 2, "approve", "owner", TrustedRefundRole.Owner, true, "c"), default)).Outcome);
    }
    private sealed class Orders : ISalesOrderStore
    {
        private readonly SalesOrder order = SalesOrder.Place(new("order"), new("tenant"), [new("product", "sku", "name", 2, 10)], 20, new("n", "e", null, null), new("p", "w", false)) with { Status = SalesOrderStatus.Fulfilled };
        public Task<SalesStoreOutcome> PlaceAsync(TrustedSalesContext c, SalesOrder o, string i, string h, CancellationToken ct) => Task.FromResult(SalesStoreOutcome.Applied); public Task<SalesOrder?> GetAsync(TrustedSalesContext c, SalesOrderId id, CancellationToken ct) => Task.FromResult<SalesOrder?>(id == order.Id && c.TenantId == order.TenantId ? order : null); public Task<SalesStoreOutcome> SaveAsync(TrustedSalesContext c, SalesOrder b, SalesOrder a, CancellationToken ct) => Task.FromResult(SalesStoreOutcome.Applied); public Task<SalesOrderPage> ListAsync(TrustedSalesContext c, string? x, int p, CancellationToken ct) => Task.FromResult(new SalesOrderPage([], null));
    }
    private sealed class Refunds : IRefundStore
    {
        private RefundRequest? value; public Task<RefundRequest?> GetRefundAsync(TrustedSalesContext c, string id, CancellationToken ct) => Task.FromResult(value?.Id == id && value.TenantId == c.TenantId ? value : null); public Task<SalesStoreOutcome> CreateRefundAsync(TrustedSalesContext c, RefundRequest r, CancellationToken ct) { if (value is not null) return Task.FromResult(SalesStoreOutcome.Replayed); value = r; return Task.FromResult(SalesStoreOutcome.Applied); }
        public Task<IReadOnlyList<RefundRequest>> ListRefundsAsync(TrustedSalesContext c, CancellationToken ct) => Task.FromResult<IReadOnlyList<RefundRequest>>(value is not null && value.TenantId == c.TenantId ? [value] : []);
        public Task<SalesStoreOutcome> DecideRefundAsync(TrustedSalesContext c, RefundRequest before, RefundRequest after, CancellationToken ct) { value = after; return Task.FromResult(SalesStoreOutcome.Applied); }
    }
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero); }
}
