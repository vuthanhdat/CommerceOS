using CommerceOS.Inventory.Application;
using CommerceOS.Inventory.Contracts;
using CommerceOS.Inventory.Domain;

namespace CommerceOS.Inventory.UnitTests;

public sealed class GoodsReceiptInventoryConsumerTests
{
    [Fact]
    public async Task ApprovedRefundReplayCreatesOneReturnPerOriginalIssueReference()
    {
        var effect = new ReturnEffect(); var consumer = new ApprovedRefundReturnConsumer(effect);
        var fact = new RefundApprovedInventoryFact("event-1", "tenant", "refund", "order", [new("product", "warehouse", 2, "issue-1")], "c", DateTimeOffset.UtcNow);
        Assert.Equal(RefundReturnOutcome.Applied, await consumer.ApplyAsync(fact, default));
        Assert.Equal(RefundReturnOutcome.AlreadyApplied, await consumer.ApplyAsync(fact, default));
        Assert.Equal(2, effect.Returned);
    }
    [Fact]
    public async Task RedeliveredReceiptAppliesOneSourceIdentifiedInventoryEffect()
    {
        var effect = new Effect(); var consumer = new GoodsReceiptInventoryConsumer(effect);
        var fact = new ConfirmedGoodsReceiptFact("event-1", "tenant", "receipt", [new("product", "warehouse", 3)], "c", DateTimeOffset.UtcNow);
        Assert.Equal(GoodsReceiptInventoryOutcome.Applied, await consumer.ApplyAsync(fact, default));
        Assert.Equal(GoodsReceiptInventoryOutcome.AlreadyApplied, await consumer.ApplyAsync(fact, default));
        Assert.Equal(3, effect.Received);
    }
    private sealed class Effect : IGoodsReceiptInventoryEffect
    {
        private readonly HashSet<string> sources = []; public long Received { get; private set; }
        public Task<StockOperationOutcome> ReceiveAsync(TrustedInventoryMutationContext c, InventoryProductId p, WarehouseId w, long q, string source, CancellationToken ct) { if (!sources.Add(source)) return Task.FromResult(StockOperationOutcome.AlreadyApplied); Received += q; return Task.FromResult(StockOperationOutcome.Applied); }
        public Task<StockOperationOutcome> CorrectAsync(TrustedInventoryMutationContext c, InventoryProductId p, WarehouseId w, long q, string source, CancellationToken ct) => Task.FromResult(StockOperationOutcome.Invalid);
    }
    private sealed class ReturnEffect : IApprovedRefundReturnEffect
    {
        private readonly HashSet<string> sources = []; public long Returned { get; private set; }
        public Task<StockOperationOutcome> ReturnAsync(TrustedInventoryMutationContext context, InventoryProductId product, WarehouseId warehouse, long quantity, string source, CancellationToken ct) { if (!sources.Add(source)) return Task.FromResult(StockOperationOutcome.AlreadyApplied); Returned += quantity; return Task.FromResult(StockOperationOutcome.Applied); }
    }
}
