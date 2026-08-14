using CommerceOS.Procurement.Domain;
namespace CommerceOS.Procurement.Application;

public interface IGoodsReceiptStore
{
    Task<PurchaseOrder?> GetPurchaseOrderAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, CancellationToken ct);
    Task<GoodsReceipt?> GetAsync(TrustedProcurementMutationContext context, string id, CancellationToken ct);
    Task<IReadOnlyList<GoodsReceipt>> ListAsync(TrustedProcurementMutationContext context, PurchaseOrderId? purchaseOrderId, CancellationToken ct);
    Task<ProcurementOutcome> CreateAsync(TrustedProcurementMutationContext context, GoodsReceipt receipt, CancellationToken ct);
    Task<IReadOnlyList<GoodsReceiptCorrection>> ListCorrectionsAsync(TrustedProcurementMutationContext context, string receiptId, CancellationToken ct);
    Task<ProcurementOutcome> ConfirmAsync(TrustedProcurementMutationContext context, GoodsReceipt receipt, string sourceIdentity, CancellationToken ct);
    Task<ProcurementOutcome> RecordCorrectionAsync(TrustedProcurementMutationContext context, GoodsReceiptCorrection correction, CancellationToken ct);
}
public sealed class GoodsReceiptService(IGoodsReceiptStore store)
{
    public Task<IReadOnlyList<GoodsReceipt>> ListAsync(TrustedProcurementMutationContext context, PurchaseOrderId? purchaseOrderId, CancellationToken ct) => store.ListAsync(context, purchaseOrderId, ct);
    public Task<GoodsReceipt?> GetAsync(TrustedProcurementMutationContext context, string id, CancellationToken ct) => store.GetAsync(context, id, ct);
    public async Task<ProcurementOutcome> CreateDraftAsync(TrustedProcurementMutationContext context, GoodsReceipt receipt, CancellationToken ct)
    {
        if (receipt.TenantId != context.TenantId || receipt.Status is not GoodsReceiptStatus.Draft || receipt.Lines.Count == 0 || receipt.Lines.Any(x => x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.WarehouseId))) return ProcurementOutcome.Immutable;
        var order = await store.GetPurchaseOrderAsync(context, receipt.PurchaseOrderId, ct);
        return IsEligibleReceipt(order, receipt) ? await store.CreateAsync(context, receipt, ct) : ProcurementOutcome.Immutable;
    }
    public static long DeriveNetReceivedQuantity(GoodsReceipt receipt, IReadOnlyList<GoodsReceiptCorrection> corrections, string productId) => receipt.Status is not GoodsReceiptStatus.Confirmed ? 0 : receipt.Lines.Where(x => x.ProductId == productId).Sum(x => x.Quantity) - corrections.Where(x => x.ReceiptId == receipt.Id).SelectMany(x => x.Lines).Where(x => x.ProductId == productId).Sum(x => x.Quantity);
    public async Task<ProcurementOutcome> ConfirmAsync(TrustedProcurementMutationContext context, string receiptId, long expectedRevision, string sourceIdentity, CancellationToken ct) { var receipt = await store.GetAsync(context, receiptId, ct); if (receipt is null) return ProcurementOutcome.NotFound; if (receipt.Status is not GoodsReceiptStatus.Draft) return ProcurementOutcome.Immutable; if (receipt.Revision != expectedRevision || receipt.Lines.Any(x => x.Quantity <= 0)) return ProcurementOutcome.RevisionConflict; var order = await store.GetPurchaseOrderAsync(context, receipt.PurchaseOrderId, ct); return IsEligibleReceipt(order, receipt) ? await store.ConfirmAsync(context, receipt with { Status = GoodsReceiptStatus.Confirmed, Revision = receipt.Revision + 1 }, sourceIdentity, ct) : ProcurementOutcome.Immutable; }
    public async Task<ProcurementOutcome> CorrectAsync(TrustedProcurementMutationContext context, GoodsReceiptCorrection correction, CancellationToken ct)
    {
        var receipt = await store.GetAsync(context, correction.ReceiptId, ct);
        if (receipt is null) return ProcurementOutcome.NotFound;
        if (receipt.Status is not GoodsReceiptStatus.Confirmed || correction.TenantId != context.TenantId || correction.Lines.Any(x => x.Quantity <= 0)) return ProcurementOutcome.Immutable;
        var alreadyCorrected = await store.ListCorrectionsAsync(context, correction.ReceiptId, ct);
        var original = receipt.Lines.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        var corrected = alreadyCorrected.SelectMany(x => x.Lines).GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        if (correction.Lines.Any(x => !original.TryGetValue(x.ProductId, out var quantity) || x.Quantity + corrected.GetValueOrDefault(x.ProductId) > quantity)) return ProcurementOutcome.Immutable;
        return await store.RecordCorrectionAsync(context, correction, ct);
    }
    private static bool IsEligibleReceipt(PurchaseOrder? order, GoodsReceipt receipt) => order?.Status is PurchaseOrderStatus.Submitted && receipt.Lines.All(receiptLine => order.Lines.Any(orderLine => orderLine.ProductId == receiptLine.ProductId && orderLine.Quantity >= receiptLine.Quantity));
}
