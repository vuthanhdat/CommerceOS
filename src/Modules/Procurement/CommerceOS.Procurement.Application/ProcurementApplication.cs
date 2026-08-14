using CommerceOS.Catalog.Contracts;
using CommerceOS.Procurement.Domain;

namespace CommerceOS.Procurement.Application;

public sealed record TrustedProcurementMutationContext(ProcurementTenantId TenantId, string CorrelationId);
public enum ProcurementOutcome { Applied, NotFound, SupplierNotActive, ProductNotPurchasable, RevisionConflict, Immutable, CancellationNotAllowed }
public interface IProcurementStore { Task<Supplier?> GetSupplierAsync(TrustedProcurementMutationContext context, SupplierId id, CancellationToken cancellationToken); Task<ProcurementOutcome> SaveSupplierAsync(TrustedProcurementMutationContext context, Supplier supplier, long? expectedRevision, CancellationToken cancellationToken); Task<PurchaseOrder?> GetPurchaseOrderAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, CancellationToken cancellationToken); Task<ProcurementOutcome> SavePurchaseOrderAsync(TrustedProcurementMutationContext context, PurchaseOrder order, long? expectedRevision, CancellationToken cancellationToken); }
public sealed class SupplierService(IProcurementStore store)
{
    public Task<ProcurementOutcome> CreateAsync(TrustedProcurementMutationContext context, Supplier supplier, CancellationToken cancellationToken) => supplier.TenantId != context.TenantId ? Task.FromResult(ProcurementOutcome.NotFound) : store.SaveSupplierAsync(context, supplier, null, cancellationToken);
    public async Task<ProcurementOutcome> ArchiveAsync(TrustedProcurementMutationContext context, SupplierId id, long revision, CancellationToken cancellationToken) { var supplier = await store.GetSupplierAsync(context, id, cancellationToken); return supplier is null ? ProcurementOutcome.NotFound : supplier.Revision != revision ? ProcurementOutcome.RevisionConflict : await store.SaveSupplierAsync(context, supplier with { Status = SupplierStatus.Archived, Revision = revision + 1 }, revision, cancellationToken); }
}
public sealed class PurchaseOrderService(IProcurementStore store, ICatalogProductEligibilityQuery catalog)
{
    public async Task<ProcurementOutcome> SubmitAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, long expectedRevision, CancellationToken cancellationToken)
    { var order = await store.GetPurchaseOrderAsync(context, id, cancellationToken); if (order is null) return ProcurementOutcome.NotFound; if (order.Status is not PurchaseOrderStatus.Draft) return ProcurementOutcome.Immutable; var supplier = await store.GetSupplierAsync(context, order.SupplierId, cancellationToken); if (supplier?.Status is not SupplierStatus.Active) return ProcurementOutcome.SupplierNotActive; foreach (var line in order.Lines) { var product = await catalog.GetPurchasableProductAsync(context.TenantId.Value, line.ProductId, cancellationToken); if (product is null || !product.IsPurchasable || product.TenantId != context.TenantId.Value) return ProcurementOutcome.ProductNotPurchasable; } try { return await store.SavePurchaseOrderAsync(context, order.Submit(expectedRevision), expectedRevision, cancellationToken); } catch (InvalidOperationException) { return ProcurementOutcome.RevisionConflict; } }
}
