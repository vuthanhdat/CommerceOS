using CommerceOS.Catalog.Contracts;
using CommerceOS.Procurement.Domain;

namespace CommerceOS.Procurement.Application;

public sealed record TrustedProcurementMutationContext(ProcurementTenantId TenantId, string CorrelationId);
public enum ProcurementOutcome { Applied, NotFound, SupplierNotActive, ProductNotPurchasable, RevisionConflict, Immutable, CancellationNotAllowed }
public interface IProcurementStore { Task<Supplier?> GetSupplierAsync(TrustedProcurementMutationContext context, SupplierId id, CancellationToken cancellationToken); Task<IReadOnlyList<Supplier>> ListSuppliersAsync(TrustedProcurementMutationContext context, CancellationToken cancellationToken); Task<ProcurementOutcome> SaveSupplierAsync(TrustedProcurementMutationContext context, Supplier supplier, long? expectedRevision, CancellationToken cancellationToken); Task<PurchaseOrder?> GetPurchaseOrderAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, CancellationToken cancellationToken); Task<IReadOnlyList<PurchaseOrder>> ListPurchaseOrdersAsync(TrustedProcurementMutationContext context, CancellationToken cancellationToken); Task<ProcurementOutcome> SavePurchaseOrderAsync(TrustedProcurementMutationContext context, PurchaseOrder order, long? expectedRevision, CancellationToken cancellationToken); }
public sealed class SupplierService(IProcurementStore store)
{
    public Task<IReadOnlyList<Supplier>> ListAsync(TrustedProcurementMutationContext context, CancellationToken ct) => store.ListSuppliersAsync(context, ct);
    public Task<ProcurementOutcome> CreateAsync(TrustedProcurementMutationContext context, Supplier supplier, CancellationToken cancellationToken) => supplier.TenantId != context.TenantId ? Task.FromResult(ProcurementOutcome.NotFound) : store.SaveSupplierAsync(context, supplier, null, cancellationToken);
    public async Task<ProcurementOutcome> ArchiveAsync(TrustedProcurementMutationContext context, SupplierId id, long revision, CancellationToken cancellationToken) { var supplier = await store.GetSupplierAsync(context, id, cancellationToken); return supplier is null ? ProcurementOutcome.NotFound : supplier.Revision != revision ? ProcurementOutcome.RevisionConflict : await store.SaveSupplierAsync(context, supplier with { Status = SupplierStatus.Archived, Revision = revision + 1 }, revision, cancellationToken); }
}
public sealed class PurchaseOrderService(IProcurementStore store, ICatalogProductEligibilityQuery catalog)
{
    public Task<IReadOnlyList<PurchaseOrder>> ListAsync(TrustedProcurementMutationContext context, CancellationToken ct) => store.ListPurchaseOrdersAsync(context, ct);
    public Task<PurchaseOrder?> GetAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, CancellationToken ct) => store.GetPurchaseOrderAsync(context, id, ct);
    public async Task<ProcurementOutcome> CreateDraftAsync(TrustedProcurementMutationContext context, PurchaseOrder order, CancellationToken ct)
    {
        if (order.TenantId != context.TenantId || order.Lines.Count == 0) return ProcurementOutcome.NotFound;
        var snapshots = await SnapshotLinesAsync(context, order.Lines, ct);
        return snapshots is null ? ProcurementOutcome.ProductNotPurchasable : await store.SavePurchaseOrderAsync(context, order with { Lines = snapshots }, null, ct);
    }
    public async Task<ProcurementOutcome> UpdateDraftAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, SupplierId supplierId, IReadOnlyList<PurchaseOrderLine> lines, long expectedRevision, CancellationToken ct) { var order = await store.GetPurchaseOrderAsync(context, id, ct); if (order is null) return ProcurementOutcome.NotFound; var snapshots = await SnapshotLinesAsync(context, lines, ct); if (snapshots is null) return ProcurementOutcome.ProductNotPurchasable; try { return await store.SavePurchaseOrderAsync(context, order.Update(supplierId, snapshots, expectedRevision), expectedRevision, ct); } catch (InvalidOperationException) { return ProcurementOutcome.Immutable; } }
    public async Task<ProcurementOutcome> SubmitAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, long expectedRevision, CancellationToken cancellationToken)
    { var order = await store.GetPurchaseOrderAsync(context, id, cancellationToken); if (order is null) return ProcurementOutcome.NotFound; if (order.Status is not PurchaseOrderStatus.Draft) return ProcurementOutcome.Immutable; var supplier = await store.GetSupplierAsync(context, order.SupplierId, cancellationToken); if (supplier?.Status is not SupplierStatus.Active) return ProcurementOutcome.SupplierNotActive; foreach (var line in order.Lines) { var product = await catalog.GetPurchasableProductAsync(context.TenantId.Value, line.ProductId, cancellationToken); if (product is null || !product.IsPurchasable || product.TenantId != context.TenantId.Value) return ProcurementOutcome.ProductNotPurchasable; } try { return await store.SavePurchaseOrderAsync(context, order.Submit(expectedRevision), expectedRevision, cancellationToken); } catch (InvalidOperationException) { return ProcurementOutcome.RevisionConflict; } }
    public async Task<ProcurementOutcome> CancelAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, long expectedRevision, CancellationToken ct) { var order = await store.GetPurchaseOrderAsync(context, id, ct); if (order is null) return ProcurementOutcome.NotFound; try { return await store.SavePurchaseOrderAsync(context, order.Cancel(expectedRevision), expectedRevision, ct); } catch (InvalidOperationException) { return ProcurementOutcome.CancellationNotAllowed; } }
    private async Task<IReadOnlyList<PurchaseOrderLine>?> SnapshotLinesAsync(TrustedProcurementMutationContext context, IReadOnlyList<PurchaseOrderLine> lines, CancellationToken ct)
    {
        if (lines.Count == 0 || lines.Any(x => x.Quantity <= 0 || x.UnitPriceVnd < 0 || string.IsNullOrWhiteSpace(x.ProductId))) return null;
        var result = new List<PurchaseOrderLine>(lines.Count);
        foreach (var line in lines) { var product = await catalog.GetPurchasableProductAsync(context.TenantId.Value, line.ProductId, ct); if (product is null || !product.IsPurchasable || product.TenantId != context.TenantId.Value) return null; result.Add(PurchaseOrderLine.Create(product.ProductId, product.DisplayName, product.Sku, line.Quantity, line.UnitPriceVnd)); }
        return result;
    }
}
