using CommerceOS.Inventory.Application;
using CommerceOS.Inventory.Contracts;
using CommerceOS.Inventory.Domain;

namespace CommerceOS.Inventory.UnitTests;

public sealed class GoodsReceiptInventoryConsumerTests
{
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
}
