using CommerceOS.Procurement.Domain;
namespace CommerceOS.Procurement.Application;

public interface IGoodsReceiptStore
{
    Task<GoodsReceipt?> GetAsync(TrustedProcurementMutationContext context, string id, CancellationToken ct);
    Task<IReadOnlyList<GoodsReceiptCorrection>> ListCorrectionsAsync(TrustedProcurementMutationContext context, string receiptId, CancellationToken ct);
    Task<ProcurementOutcome> ConfirmAsync(TrustedProcurementMutationContext context, GoodsReceipt receipt, string sourceIdentity, CancellationToken ct);
    Task<ProcurementOutcome> RecordCorrectionAsync(TrustedProcurementMutationContext context, GoodsReceiptCorrection correction, CancellationToken ct);
}
public sealed class GoodsReceiptService(IGoodsReceiptStore store)
{
    public static long DeriveNetReceivedQuantity(GoodsReceipt receipt, IReadOnlyList<GoodsReceiptCorrection> corrections, string productId) => receipt.Status is not GoodsReceiptStatus.Confirmed ? 0 : receipt.Lines.Where(x => x.ProductId == productId).Sum(x => x.Quantity) - corrections.Where(x => x.ReceiptId == receipt.Id).SelectMany(x => x.Lines).Where(x => x.ProductId == productId).Sum(x => x.Quantity);
    public async Task<ProcurementOutcome> ConfirmAsync(TrustedProcurementMutationContext context, string receiptId, long expectedRevision, string sourceIdentity, CancellationToken ct) { var receipt = await store.GetAsync(context, receiptId, ct); if (receipt is null) return ProcurementOutcome.NotFound; if (receipt.Status is not GoodsReceiptStatus.Draft) return ProcurementOutcome.Immutable; if (receipt.Revision != expectedRevision || receipt.Lines.Any(x => x.Quantity <= 0)) return ProcurementOutcome.RevisionConflict; return await store.ConfirmAsync(context, receipt with { Status = GoodsReceiptStatus.Confirmed, Revision = receipt.Revision + 1 }, sourceIdentity, ct); }
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
}
