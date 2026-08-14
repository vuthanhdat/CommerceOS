using CommerceOS.Inventory.Contracts;
using CommerceOS.Inventory.Domain;

namespace CommerceOS.Inventory.Application;

public interface IGoodsReceiptInventoryEffect
{
    Task<StockOperationOutcome> ReceiveAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, long quantity, string sourceIdentity, CancellationToken cancellationToken);
    Task<StockOperationOutcome> CorrectAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, long quantity, string sourceIdentity, CancellationToken cancellationToken);
}

public sealed class StockOperationGoodsReceiptEffect(StockOperationService stock) : IGoodsReceiptInventoryEffect
{
    public Task<StockOperationOutcome> ReceiveAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, long quantity, string sourceIdentity, CancellationToken cancellationToken) => stock.ReceiveAsync(context, productId, warehouseId, quantity, sourceIdentity, cancellationToken);
    public Task<StockOperationOutcome> CorrectAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, long quantity, string sourceIdentity, CancellationToken cancellationToken) => stock.AdjustDecreaseAsync(context, productId, warehouseId, quantity, sourceIdentity, cancellationToken);
}

/// <summary>Each line receives an immutable Procurement source identity, so redrive/replay cannot duplicate stock movements.</summary>
public sealed class GoodsReceiptInventoryConsumer(IGoodsReceiptInventoryEffect effects) : IConfirmedGoodsReceiptConsumer
{
    public async Task<GoodsReceiptInventoryOutcome> ApplyAsync(ConfirmedGoodsReceiptFact fact, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fact.EventId) || string.IsNullOrWhiteSpace(fact.TenantId) || string.IsNullOrWhiteSpace(fact.ReceiptId) || fact.Lines.Count == 0 || fact.Lines.Any(x => string.IsNullOrWhiteSpace(x.ProductId) || string.IsNullOrWhiteSpace(x.WarehouseId) || x.QuantityDelta == 0)) return GoodsReceiptInventoryOutcome.Invalid;
        var context = new TrustedInventoryMutationContext(new(fact.TenantId), fact.CorrelationId);
        var applied = false;
        foreach (var line in fact.Lines.OrderBy(x => x.ProductId, StringComparer.Ordinal).ThenBy(x => x.WarehouseId, StringComparer.Ordinal))
        {
            var source = $"goods-receipt:{fact.ReceiptId}:{line.ProductId}:{line.WarehouseId}:{fact.EventId}";
            var outcome = line.QuantityDelta > 0
                ? await effects.ReceiveAsync(context, new(line.ProductId), new(line.WarehouseId), line.QuantityDelta, source, cancellationToken)
                : await effects.CorrectAsync(context, new(line.ProductId), new(line.WarehouseId), checked(-line.QuantityDelta), source, cancellationToken);
            if (outcome is not StockOperationOutcome.Applied and not StockOperationOutcome.AlreadyApplied) return GoodsReceiptInventoryOutcome.NeedsAttention;
            applied |= outcome is StockOperationOutcome.Applied;
        }
        return applied ? GoodsReceiptInventoryOutcome.Applied : GoodsReceiptInventoryOutcome.AlreadyApplied;
    }
}
