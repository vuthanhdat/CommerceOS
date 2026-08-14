using CommerceOS.Accounting.Domain;

namespace CommerceOS.Accounting.Application;

/// <summary>Inbound, versioned producer facts; handlers depend on facts rather than producer persistence.</summary>
public sealed record AccountingFactEnvelope(string EventId, string EventType, int EventVersion, string TenantId, string AggregateId, DateTimeOffset OccurredAt, string CorrelationId, string? CausationId);
public sealed record PaymentCapturedAccountingFact(AccountingFactEnvelope Envelope, string PaymentId, long AmountVnd, DateOnly EffectiveDate);
public sealed record OrderFulfilledAccountingFact(AccountingFactEnvelope Envelope, string OrderId, long AmountVnd, DateOnly EffectiveDate);
public sealed record StockIssuedAccountingFact(AccountingFactEnvelope Envelope, string IssueId, string ProductId, long Quantity, DateOnly EffectiveDate);
public sealed record GoodsReceiptAccountingLine(string ProductId, long Quantity, long AcceptedTotalCostVnd);
public sealed record GoodsReceiptAccountingFact(AccountingFactEnvelope Envelope, string ReceiptId, IReadOnlyList<GoodsReceiptAccountingLine> Lines, DateOnly EffectiveDate);
public sealed record SupplierInvoiceAccountingFact(AccountingFactEnvelope Envelope, string InvoiceId, long ReceiptAcceptedAmountVnd, long InvoiceAmountVnd, bool VarianceApproved, DateOnly EffectiveDate);
public sealed record SupplierPaymentAccountingFact(AccountingFactEnvelope Envelope, string PaymentId, long AmountVnd, DateOnly EffectiveDate);
public sealed record StockAdjustedAccountingFact(AccountingFactEnvelope Envelope, string AdjustmentId, string ProductId, long QuantityDelta, long? ApprovedIncreaseTotalCostVnd, DateOnly EffectiveDate);

public sealed class AccountingFactConsumer(IAccountingStore store, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public Task<AccountingOutcome> ApplyAsync(PaymentCapturedAccountingFact fact, CancellationToken ct) => PostSimpleAsync(fact.Envelope, fact.EffectiveDate, fact.AmountVnd, AccountRole.Cash, AccountRole.CustomerDeposits, ct);
    public Task<AccountingOutcome> ApplyAsync(OrderFulfilledAccountingFact fact, CancellationToken ct) => PostSimpleAsync(fact.Envelope, fact.EffectiveDate, fact.AmountVnd, AccountRole.CustomerDeposits, AccountRole.SalesRevenue, ct);
    public async Task<AccountingOutcome> ApplyAsync(StockIssuedAccountingFact fact, CancellationToken ct)
    {
        var context = Context(fact.Envelope); if (!Valid(fact.Envelope) || fact.Quantity <= 0 || string.IsNullOrWhiteSpace(fact.ProductId)) return AccountingOutcome.Invalid;
        var valuation = await store.GetValuationAsync(context, fact.ProductId, ct) ?? new(context.TenantId, fact.ProductId, 0, 0, 0);
        try { var (after, cost) = valuation.Issue(fact.Quantity); return await PostAsync(context, fact.Envelope, fact.EffectiveDate, [JournalLine.Debit(Id(AccountRole.CostOfGoodsSold), cost), JournalLine.Credit(Id(AccountRole.Inventory), cost)], [after], ct); } catch (AccountingRuleException) { return AccountingOutcome.NeedsAttention; }
    }
    public async Task<AccountingOutcome> ApplyAsync(GoodsReceiptAccountingFact fact, CancellationToken ct)
    {
        var context = Context(fact.Envelope); if (!Valid(fact.Envelope) || fact.Lines.Count is 0 or > 8 || fact.Lines.Any(x => string.IsNullOrWhiteSpace(x.ProductId) || x.Quantity <= 0 || x.AcceptedTotalCostVnd < 0)) return AccountingOutcome.Invalid;
        var updates = new List<ValuationState>(); long total = 0;
        foreach (var line in fact.Lines) { var current = await store.GetValuationAsync(context, line.ProductId, ct) ?? new(context.TenantId, line.ProductId, 0, 0, 0); updates.Add(current.Receive(line.Quantity, line.AcceptedTotalCostVnd)); total = checked(total + line.AcceptedTotalCostVnd); }
        return await PostAsync(context, fact.Envelope, fact.EffectiveDate, [JournalLine.Debit(Id(AccountRole.Inventory), total), JournalLine.Credit(Id(AccountRole.GoodsReceivedNotInvoiced), total)], updates, ct);
    }
    public async Task<AccountingOutcome> ApplyAsync(SupplierInvoiceAccountingFact fact, CancellationToken ct)
    {
        if (!Valid(fact.Envelope) || fact.ReceiptAcceptedAmountVnd < 0 || fact.InvoiceAmountVnd < 0 || (fact.ReceiptAcceptedAmountVnd != fact.InvoiceAmountVnd && !fact.VarianceApproved)) return AccountingOutcome.Invalid;
        var lines = new List<JournalLine> { JournalLine.Debit(Id(AccountRole.GoodsReceivedNotInvoiced), fact.ReceiptAcceptedAmountVnd), JournalLine.Credit(Id(AccountRole.AccountsPayable), fact.InvoiceAmountVnd) };
        var difference = fact.InvoiceAmountVnd - fact.ReceiptAcceptedAmountVnd;
        if (difference > 0) lines.Add(JournalLine.Debit(Id(AccountRole.PurchasePriceVariance), difference)); else if (difference < 0) lines.Add(JournalLine.Credit(Id(AccountRole.PurchasePriceVariance), -difference));
        return await PostAsync(Context(fact.Envelope), fact.Envelope, fact.EffectiveDate, lines, [], ct);
    }
    public Task<AccountingOutcome> ApplyAsync(SupplierPaymentAccountingFact fact, CancellationToken ct) => PostSimpleAsync(fact.Envelope, fact.EffectiveDate, fact.AmountVnd, AccountRole.AccountsPayable, AccountRole.Cash, ct);
    public async Task<AccountingOutcome> ApplyAsync(StockAdjustedAccountingFact fact, CancellationToken ct)
    {
        var context = Context(fact.Envelope); if (!Valid(fact.Envelope) || fact.QuantityDelta == 0 || string.IsNullOrWhiteSpace(fact.ProductId)) return AccountingOutcome.Invalid;
        var current = await store.GetValuationAsync(context, fact.ProductId, ct) ?? new(context.TenantId, fact.ProductId, 0, 0, 0);
        try
        {
            if (fact.QuantityDelta < 0) { var (after, cost) = current.Issue(-fact.QuantityDelta); return await PostAsync(context, fact.Envelope, fact.EffectiveDate, [JournalLine.Debit(Id(AccountRole.InventoryAdjustmentLoss), cost), JournalLine.Credit(Id(AccountRole.Inventory), cost)], [after], ct); }
            if (fact.ApprovedIncreaseTotalCostVnd is null || fact.ApprovedIncreaseTotalCostVnd < 0) return AccountingOutcome.NeedsAttention;
            var afterIncrease = current.Receive(fact.QuantityDelta, fact.ApprovedIncreaseTotalCostVnd.Value); return await PostAsync(context, fact.Envelope, fact.EffectiveDate, [JournalLine.Debit(Id(AccountRole.Inventory), fact.ApprovedIncreaseTotalCostVnd.Value), JournalLine.Credit(Id(AccountRole.InventoryAdjustmentGain), fact.ApprovedIncreaseTotalCostVnd.Value)], [afterIncrease], ct);
        }
        catch (AccountingRuleException) { return AccountingOutcome.NeedsAttention; }
    }
    private Task<AccountingOutcome> PostSimpleAsync(AccountingFactEnvelope e, DateOnly date, long amount, AccountRole debit, AccountRole credit, CancellationToken ct) => !Valid(e) || amount <= 0 ? Task.FromResult(AccountingOutcome.Invalid) : PostAsync(Context(e), e, date, [JournalLine.Debit(Id(debit), amount), JournalLine.Credit(Id(credit), amount)], [], ct);
    private Task<AccountingOutcome> PostAsync(TrustedAccountingContext c, AccountingFactEnvelope e, DateOnly date, IReadOnlyList<JournalLine> lines, IReadOnlyList<ValuationState> valuations, CancellationToken ct) => store.PostAsync(c, Journal.Create($"journal:{e.EventId}", c.TenantId, date, _clock.GetUtcNow(), e.EventId, e.CorrelationId, lines), valuations, ct);
    private static AccountId Id(AccountRole role) => new($"control:{role}");
    private static TrustedAccountingContext Context(AccountingFactEnvelope e) => new(new(e.TenantId), e.CorrelationId);
    private static bool Valid(AccountingFactEnvelope e) => !string.IsNullOrWhiteSpace(e.EventId) && !string.IsNullOrWhiteSpace(e.EventType) && e.EventVersion == 1 && !string.IsNullOrWhiteSpace(e.TenantId) && !string.IsNullOrWhiteSpace(e.AggregateId) && !string.IsNullOrWhiteSpace(e.CorrelationId);
}
