using CommerceOS.Procurement.Application;
using CommerceOS.Procurement.Domain;

namespace CommerceOS.Procurement.UnitTests;

public sealed class SupplierInvoiceTests
{
    [Fact]
    public async Task InvoiceRequiresFullReceiptAndVarianceNeedsExplicitAuthorityBeforeFullPayment()
    {
        var tenant = new ProcurementTenantId("tenant"); var po = new PurchaseOrder(new("po"), tenant, new("supplier"), [PurchaseOrderLine.Create("product", "Tea", null, 2, 100)], PurchaseOrderStatus.Submitted, 1);
        var store = new Store(po); var service = new SupplierInvoiceService(store); var context = new TrustedProcurementMutationContext(tenant, "c");
        var invoice = new SupplierInvoice("invoice", po.Id, tenant, "SUP-1", DateOnly.FromDateTime(DateTime.UtcNow), 220, SupplierInvoiceStatus.Accepted, 0, "source", 1);
        Assert.Equal(SupplierInvoiceOutcome.ReceiptIncomplete, await service.RecordInvoiceAsync(context, invoice, default));
        store.FullReceipt = true; Assert.Equal(SupplierInvoiceOutcome.Applied, await service.RecordInvoiceAsync(context, invoice, default));
        Assert.Equal(SupplierInvoiceStatus.PendingVarianceApproval, store.Invoice!.Status);
        Assert.Equal(SupplierInvoiceOutcome.Forbidden, await service.ApproveVarianceAsync(new(tenant, "staff", false, "c"), invoice.Id, 1, default));
        Assert.Equal(SupplierInvoiceOutcome.Applied, await service.ApproveVarianceAsync(new(tenant, "authority", true, "c"), invoice.Id, 1, default));
        Assert.Equal(SupplierInvoiceOutcome.Applied, await service.RecordPaymentAsync(context, new("payment", invoice.Id, tenant, "bank-reference", DateOnly.FromDateTime(DateTime.UtcNow), 220, "payment-source", 1), default));
    }
    private sealed class Store(PurchaseOrder po) : ISupplierInvoiceStore
    {
        public bool FullReceipt; public SupplierInvoice? Invoice; private readonly HashSet<string> payments = [];
        public Task<IReadOnlyList<SupplierInvoice>> ListInvoicesAsync(TrustedProcurementMutationContext c, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierInvoice>>(Invoice is null ? [] : [Invoice]);
        public Task<IReadOnlyList<SupplierPayment>> ListPaymentsAsync(TrustedProcurementMutationContext c, string? invoiceId, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierPayment>>([]);
        public Task<PurchaseOrder?> GetPurchaseOrderAsync(TrustedProcurementMutationContext c, PurchaseOrderId id, CancellationToken ct) => Task.FromResult<PurchaseOrder?>(id == po.Id ? po : null);
        public Task<bool> IsFullyReceivedAsync(TrustedProcurementMutationContext c, PurchaseOrderId id, CancellationToken ct) => Task.FromResult(FullReceipt);
        public Task<SupplierInvoice?> GetInvoiceForPurchaseOrderAsync(TrustedProcurementMutationContext c, PurchaseOrderId id, CancellationToken ct) => Task.FromResult(Invoice?.PurchaseOrderId == id ? Invoice : null);
        public Task<SupplierInvoice?> GetInvoiceAsync(TrustedProcurementMutationContext c, string id, CancellationToken ct) => Task.FromResult(Invoice?.Id == id ? Invoice : null);
        public Task<SupplierInvoiceOutcome> CreateInvoiceAsync(TrustedProcurementMutationContext c, SupplierInvoice invoice, CancellationToken ct) { Invoice = invoice; return Task.FromResult(SupplierInvoiceOutcome.Applied); }
        public Task<SupplierInvoiceOutcome> SaveInvoiceAsync(TrustedProcurementMutationContext c, SupplierInvoice before, SupplierInvoice after, CancellationToken ct) { Invoice = after; return Task.FromResult(SupplierInvoiceOutcome.Applied); }
        public Task<SupplierInvoiceOutcome> RecordPaymentAsync(TrustedProcurementMutationContext c, SupplierPayment payment, CancellationToken ct) => Task.FromResult(payments.Add(payment.SourceIdentity) ? SupplierInvoiceOutcome.Applied : SupplierInvoiceOutcome.AlreadyApplied);
    }
}
