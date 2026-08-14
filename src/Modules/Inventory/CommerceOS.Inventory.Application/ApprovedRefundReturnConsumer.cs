using CommerceOS.Inventory.Contracts;
using CommerceOS.Inventory.Domain;

namespace CommerceOS.Inventory.Application;

public interface IApprovedRefundReturnEffect
{
    Task<StockOperationOutcome> ReturnAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, long quantity, string sourceIdentity, CancellationToken cancellationToken);
}

public sealed class StockOperationApprovedRefundReturnEffect(StockOperationService stock) : IApprovedRefundReturnEffect
{
    public Task<StockOperationOutcome> ReturnAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, long quantity, string sourceIdentity, CancellationToken cancellationToken)
        => stock.ReturnAsync(context, productId, warehouseId, quantity, sourceIdentity, cancellationToken);
}

/// <summary>Applies only the explicit, Sales-owned approved quantities. The source identity is stable across queue redrive.</summary>
public sealed class ApprovedRefundReturnConsumer(IApprovedRefundReturnEffect effects) : IRefundApprovedInventoryConsumer
{
    public async Task<RefundReturnOutcome> ApplyAsync(RefundApprovedInventoryFact fact, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fact.EventId) || string.IsNullOrWhiteSpace(fact.TenantId) || string.IsNullOrWhiteSpace(fact.RefundApprovalId) || fact.Lines.Count == 0 || fact.Lines.Any(x => string.IsNullOrWhiteSpace(x.ProductId) || string.IsNullOrWhiteSpace(x.WarehouseId) || string.IsNullOrWhiteSpace(x.OriginalIssueReference) || x.Quantity <= 0)) return RefundReturnOutcome.Invalid;
        var context = new TrustedInventoryMutationContext(new(fact.TenantId), fact.CorrelationId);
        var changed = false;
        foreach (var line in fact.Lines.OrderBy(x => x.ProductId, StringComparer.Ordinal).ThenBy(x => x.WarehouseId, StringComparer.Ordinal))
        {
            var source = $"refund-return:{fact.RefundApprovalId}:{line.ProductId}:{line.WarehouseId}:{line.OriginalIssueReference}";
            var outcome = await effects.ReturnAsync(context, new(line.ProductId), new(line.WarehouseId), line.Quantity, source, cancellationToken);
            if (outcome is not StockOperationOutcome.Applied and not StockOperationOutcome.AlreadyApplied) return RefundReturnOutcome.NeedsAttention;
            changed |= outcome is StockOperationOutcome.Applied;
        }
        return changed ? RefundReturnOutcome.Applied : RefundReturnOutcome.AlreadyApplied;
    }
}
