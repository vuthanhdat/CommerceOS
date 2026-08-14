using CommerceOS.Procurement.Domain;

namespace CommerceOS.Procurement.Application;

public sealed record TrustedProcurementApprovalContext(ProcurementTenantId TenantId, string ActorId, bool HasInvoiceVarianceApprovalAuthority, string CorrelationId);
public enum SupplierInvoiceOutcome { Applied, AlreadyApplied, NotFound, ReceiptIncomplete, InvoiceConflict, VarianceApprovalRequired, Forbidden, Invalid }
public interface ISupplierInvoiceStore
{
    Task<IReadOnlyList<SupplierInvoice>> ListInvoicesAsync(TrustedProcurementMutationContext context, CancellationToken ct);
    Task<IReadOnlyList<SupplierPayment>> ListPaymentsAsync(TrustedProcurementMutationContext context, string? invoiceId, CancellationToken ct);
    Task<PurchaseOrder?> GetPurchaseOrderAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, CancellationToken ct);
    Task<bool> IsFullyReceivedAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, CancellationToken ct);
    Task<SupplierInvoice?> GetInvoiceForPurchaseOrderAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, CancellationToken ct);
    Task<SupplierInvoice?> GetInvoiceAsync(TrustedProcurementMutationContext context, string id, CancellationToken ct);
    Task<SupplierInvoiceOutcome> CreateInvoiceAsync(TrustedProcurementMutationContext context, SupplierInvoice invoice, CancellationToken ct);
    Task<SupplierInvoiceOutcome> SaveInvoiceAsync(TrustedProcurementMutationContext context, SupplierInvoice before, SupplierInvoice after, CancellationToken ct);
    Task<SupplierInvoiceOutcome> RecordPaymentAsync(TrustedProcurementMutationContext context, SupplierPayment payment, CancellationToken ct);
}

public sealed class SupplierInvoiceService(ISupplierInvoiceStore store)
{
    public Task<IReadOnlyList<SupplierInvoice>> ListAsync(TrustedProcurementMutationContext context, CancellationToken ct) => store.ListInvoicesAsync(context, ct);
    public Task<IReadOnlyList<SupplierPayment>> ListPaymentsAsync(TrustedProcurementMutationContext context, string? invoiceId, CancellationToken ct) => store.ListPaymentsAsync(context, invoiceId, ct);
    public async Task<SupplierInvoiceOutcome> RecordInvoiceAsync(TrustedProcurementMutationContext context, SupplierInvoice invoice, CancellationToken ct)
    {
        if (invoice.TenantId != context.TenantId || string.IsNullOrWhiteSpace(invoice.SupplierReference) || invoice.AmountVnd < 0) return SupplierInvoiceOutcome.Invalid;
        var order = await store.GetPurchaseOrderAsync(context, invoice.PurchaseOrderId, ct);
        if (order is null) return SupplierInvoiceOutcome.NotFound;
        if (order.Status is not PurchaseOrderStatus.Submitted || !await store.IsFullyReceivedAsync(context, order.Id, ct)) return SupplierInvoiceOutcome.ReceiptIncomplete;
        var existing = await store.GetInvoiceForPurchaseOrderAsync(context, order.Id, ct);
        if (existing is not null) return existing.SourceIdentity == invoice.SourceIdentity ? SupplierInvoiceOutcome.AlreadyApplied : SupplierInvoiceOutcome.InvoiceConflict;
        var expected = order.Lines.Sum(x => checked(x.Quantity * x.UnitPriceVnd));
        var status = invoice.AmountVnd == expected ? SupplierInvoiceStatus.Accepted : SupplierInvoiceStatus.PendingVarianceApproval;
        return await store.CreateInvoiceAsync(context, invoice with { ExpectedAmountVnd = expected, Status = status }, ct);
    }

    public async Task<SupplierInvoiceOutcome> ApproveVarianceAsync(TrustedProcurementApprovalContext authority, string invoiceId, long expectedRevision, CancellationToken ct)
    {
        if (!authority.HasInvoiceVarianceApprovalAuthority) return SupplierInvoiceOutcome.Forbidden;
        var context = new TrustedProcurementMutationContext(authority.TenantId, authority.CorrelationId);
        var invoice = await store.GetInvoiceAsync(context, invoiceId, ct);
        if (invoice is null) return SupplierInvoiceOutcome.NotFound;
        if (invoice.Revision != expectedRevision || invoice.Status is not SupplierInvoiceStatus.PendingVarianceApproval) return SupplierInvoiceOutcome.VarianceApprovalRequired;
        return await store.SaveInvoiceAsync(context, invoice, invoice with { Status = SupplierInvoiceStatus.Accepted, Revision = invoice.Revision + 1 }, ct);
    }

    public async Task<SupplierInvoiceOutcome> RecordPaymentAsync(TrustedProcurementMutationContext context, SupplierPayment payment, CancellationToken ct)
    {
        if (payment.TenantId != context.TenantId || payment.AmountVnd < 0 || string.IsNullOrWhiteSpace(payment.ExternalReference)) return SupplierInvoiceOutcome.Invalid;
        var invoice = await store.GetInvoiceAsync(context, payment.SupplierInvoiceId, ct);
        if (invoice is null) return SupplierInvoiceOutcome.NotFound;
        if (invoice.Status is not SupplierInvoiceStatus.Accepted || invoice.AmountVnd != payment.AmountVnd) return SupplierInvoiceOutcome.VarianceApprovalRequired;
        return await store.RecordPaymentAsync(context, payment, ct);
    }
}
