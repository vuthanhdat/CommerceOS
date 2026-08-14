using CommerceOS.Catalog.Contracts;
using CommerceOS.Procurement.Application;
using CommerceOS.Procurement.Domain;

namespace CommerceOS.Procurement.UnitTests;

public sealed class PurchaseOrderTests
{
    [Fact]
    public async Task SubmissionRequiresActiveSupplierAndPurchasableProductThenFreezesOrder()
    {
        var tenant = new ProcurementTenantId("tenant-a"); var supplier = new Supplier(new("supplier"), tenant, "Supplier", SupplierStatus.Active, 1); var order = new PurchaseOrder(new("po"), tenant, supplier.Id, [PurchaseOrderLine.Create("product", "Tea", "TEA", 2, 100)], PurchaseOrderStatus.Draft, 1); var store = new Store(supplier, order); var service = new PurchaseOrderService(store, new Products(true)); var context = new TrustedProcurementMutationContext(tenant, "c");
        Assert.Equal(ProcurementOutcome.Applied, await service.SubmitAsync(context, order.Id, 1, default));
        Assert.Equal(PurchaseOrderStatus.Submitted, store.Order!.Status);
        Assert.Equal(ProcurementOutcome.Immutable, await service.SubmitAsync(context, order.Id, 2, default));
    }
    private sealed class Products(bool eligible) : ICatalogProductEligibilityQuery { public Task<PurchasableProduct?> GetPurchasableProductAsync(string tenant, string product, CancellationToken ct) => Task.FromResult<PurchasableProduct?>(new(product, tenant, eligible, "Tea", "TEA")); }
    private sealed class Store(Supplier supplier, PurchaseOrder order) : IProcurementStore { public PurchaseOrder? Order { get; private set; } = order; public Task<Supplier?> GetSupplierAsync(TrustedProcurementMutationContext c, SupplierId id, CancellationToken ct) => Task.FromResult<Supplier?>(supplier); public Task<IReadOnlyList<Supplier>> ListSuppliersAsync(TrustedProcurementMutationContext c, CancellationToken ct) => Task.FromResult<IReadOnlyList<Supplier>>([supplier]); public Task<ProcurementOutcome> SaveSupplierAsync(TrustedProcurementMutationContext c, Supplier supplier, long? revision, CancellationToken ct) => Task.FromResult(ProcurementOutcome.Applied); public Task<PurchaseOrder?> GetPurchaseOrderAsync(TrustedProcurementMutationContext c, PurchaseOrderId id, CancellationToken ct) => Task.FromResult(Order); public Task<IReadOnlyList<PurchaseOrder>> ListPurchaseOrdersAsync(TrustedProcurementMutationContext c, CancellationToken ct) => Task.FromResult<IReadOnlyList<PurchaseOrder>>(Order is null ? [] : [Order]); public Task<ProcurementOutcome> SavePurchaseOrderAsync(TrustedProcurementMutationContext c, PurchaseOrder x, long? revision, CancellationToken ct) { Order = x; return Task.FromResult(ProcurementOutcome.Applied); } }
}
