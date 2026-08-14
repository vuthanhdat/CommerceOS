using CommerceOS.Procurement.Application;
using CommerceOS.Procurement.Domain;

namespace CommerceOS.Procurement.UnitTests;

public sealed class GoodsReceiptTests
{
    [Fact]
    public async Task CorrectionIsCompensatingEvidenceAndCannotExceedConfirmedQuantity()
    {
        var receipt = new GoodsReceipt("r1", new("po"), new("tenant"), [new("product", 5)], GoodsReceiptStatus.Confirmed, null, 1);
        var store = new Store(receipt);
        var service = new GoodsReceiptService(store);
        var context = new TrustedProcurementMutationContext(new("tenant"), "c");
        Assert.Equal(ProcurementOutcome.Applied, await service.CorrectAsync(context, new("c1", "r1", new("tenant"), [new("product", 3)], "source-1", 1), default));
        Assert.Equal(ProcurementOutcome.Immutable, await service.CorrectAsync(context, new("c2", "r1", new("tenant"), [new("product", 3)], "source-2", 1), default));
        Assert.Equal(2, GoodsReceiptService.DeriveNetReceivedQuantity(receipt, await store.ListCorrectionsAsync(context, "r1", default), "product"));
    }

    private sealed class Store(GoodsReceipt receipt) : IGoodsReceiptStore
    {
        private readonly List<GoodsReceiptCorrection> corrections = [];
        public Task<PurchaseOrder?> GetPurchaseOrderAsync(TrustedProcurementMutationContext context, PurchaseOrderId id, CancellationToken ct) => Task.FromResult<PurchaseOrder?>(new PurchaseOrder(id, context.TenantId, new SupplierId("supplier"), [PurchaseOrderLine.Create("product", "Tea", null, 5, 100)], PurchaseOrderStatus.Submitted, 1));
        public Task<GoodsReceipt?> GetAsync(TrustedProcurementMutationContext context, string id, CancellationToken ct) => Task.FromResult<GoodsReceipt?>(context.TenantId == receipt.TenantId && id == receipt.Id ? receipt : null);
        public Task<IReadOnlyList<GoodsReceipt>> ListAsync(TrustedProcurementMutationContext context, PurchaseOrderId? purchaseOrderId, CancellationToken ct) => Task.FromResult<IReadOnlyList<GoodsReceipt>>(context.TenantId == receipt.TenantId && (purchaseOrderId is null || purchaseOrderId == receipt.PurchaseOrderId) ? [receipt] : []);
        public Task<ProcurementOutcome> CreateAsync(TrustedProcurementMutationContext context, GoodsReceipt value, CancellationToken ct) => Task.FromResult(ProcurementOutcome.Applied);
        public Task<IReadOnlyList<GoodsReceiptCorrection>> ListCorrectionsAsync(TrustedProcurementMutationContext context, string receiptId, CancellationToken ct) => Task.FromResult<IReadOnlyList<GoodsReceiptCorrection>>(corrections.Where(x => x.ReceiptId == receiptId && x.TenantId == context.TenantId).ToArray());
        public Task<ProcurementOutcome> ConfirmAsync(TrustedProcurementMutationContext context, GoodsReceipt value, string sourceIdentity, CancellationToken ct) => Task.FromResult(ProcurementOutcome.Applied);
        public Task<ProcurementOutcome> RecordCorrectionAsync(TrustedProcurementMutationContext context, GoodsReceiptCorrection correction, CancellationToken ct) { if (corrections.Any(x => x.SourceIdentity == correction.SourceIdentity)) return Task.FromResult(ProcurementOutcome.Immutable); corrections.Add(correction); return Task.FromResult(ProcurementOutcome.Applied); }
    }
}
