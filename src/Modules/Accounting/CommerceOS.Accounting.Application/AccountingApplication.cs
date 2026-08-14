using CommerceOS.Accounting.Domain;

namespace CommerceOS.Accounting.Application;

public sealed record TrustedAccountingContext(AccountingTenantId TenantId, string CorrelationId);
public enum AccountingOutcome { Applied, AlreadyApplied, NotFound, Conflict, Invalid, NeedsAttention, Forbidden }
public sealed record AccountingPage(IReadOnlyList<Journal> Items, string? NextCursor);
public sealed record TrialBalanceLine(AccountId AccountId, long DebitVnd, long CreditVnd) { public long BalanceVnd => DebitVnd - CreditVnd; }

public interface IAccountingStore
{
    Task<AccountingOutcome> BootstrapChartAsync(TrustedAccountingContext context, IReadOnlyList<Account> accounts, CancellationToken ct);
    Task<AccountingOutcome> CreateAccountAsync(TrustedAccountingContext context, Account account, CancellationToken ct);
    Task<Account?> GetAccountAsync(TrustedAccountingContext context, AccountId accountId, CancellationToken ct);
    Task<AccountingOutcome> SaveAccountAsync(TrustedAccountingContext context, Account before, Account after, CancellationToken ct);
    Task<AccountingOutcome> PostAsync(TrustedAccountingContext context, Journal journal, IReadOnlyList<ValuationState> valuations, CancellationToken ct);
    Task<Journal?> GetJournalAsync(TrustedAccountingContext context, string journalId, CancellationToken ct);
    Task<Journal?> GetJournalBySourceAsync(TrustedAccountingContext context, string sourceIdentity, CancellationToken ct);
    Task<AccountingPage> ListJournalsAsync(TrustedAccountingContext context, DateOnly? from, DateOnly? through, string? cursor, int pageSize, CancellationToken ct);
    Task<ValuationState?> GetValuationAsync(TrustedAccountingContext context, string productId, CancellationToken ct);
}

public static class AccountingChart
{
    public static readonly (AccountRole Role, string Code, string Name)[] Required =
    [
        (AccountRole.Cash, "1000", "Cash"), (AccountRole.CustomerDeposits, "2100", "Customer Deposits"), (AccountRole.SalesRevenue, "4000", "Sales Revenue"),
        (AccountRole.Inventory, "1200", "Inventory"), (AccountRole.CostOfGoodsSold, "5000", "Cost of Goods Sold"), (AccountRole.AccountsPayable, "2000", "Accounts Payable"),
        (AccountRole.GoodsReceivedNotInvoiced, "2101", "Goods Received Not Invoiced"), (AccountRole.PurchasePriceVariance, "5100", "Purchase Price Variance"),
        (AccountRole.InventoryAdjustmentGain, "4100", "Inventory Adjustment Gain"), (AccountRole.InventoryAdjustmentLoss, "5200", "Inventory Adjustment Loss")
    ];
    public static IReadOnlyList<Account> For(AccountingTenantId tenantId) => Required.Select(x => Account.Control(tenantId, x.Role, x.Code, x.Name)).ToArray();
}

public sealed class AccountingChartService(IAccountingStore store)
{
    public Task<AccountingOutcome> BootstrapAsync(TrustedAccountingContext context, CancellationToken cancellationToken) => store.BootstrapChartAsync(context, AccountingChart.For(context.TenantId), cancellationToken);
    public Task<AccountingOutcome> AddNonControlAsync(TrustedAccountingContext context, Account account, CancellationToken cancellationToken)
        => account.TenantId != context.TenantId || account.Role is not AccountRole.NonControl || string.IsNullOrWhiteSpace(account.Code) ? Task.FromResult(AccountingOutcome.Invalid) : store.CreateAccountAsync(context, account, cancellationToken);
    public async Task<AccountingOutcome> DeactivateAsync(TrustedAccountingContext context, AccountId id, CancellationToken cancellationToken)
    {
        var account = await store.GetAccountAsync(context, id, cancellationToken);
        if (account is null) return AccountingOutcome.NotFound;
        try { return await store.SaveAccountAsync(context, account, account.Deactivate(), cancellationToken); } catch (AccountingRuleException) { return AccountingOutcome.Forbidden; }
    }
}

public sealed class JournalService(IAccountingStore store, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public Task<AccountingOutcome> PostAsync(TrustedAccountingContext context, Journal journal, CancellationToken cancellationToken)
        => journal.TenantId != context.TenantId ? Task.FromResult(AccountingOutcome.NotFound) : store.PostAsync(context, journal, [], cancellationToken);
    public async Task<AccountingOutcome> ReverseAsync(TrustedAccountingContext context, string originalId, string reversalId, DateOnly effectiveDate, string sourceIdentity, CancellationToken cancellationToken)
    {
        var original = await store.GetJournalAsync(context, originalId, cancellationToken);
        if (original is null) return AccountingOutcome.NotFound;
        try { return await store.PostAsync(context, original.Reverse(reversalId, _clock.GetUtcNow(), effectiveDate, sourceIdentity, context.CorrelationId), [], cancellationToken); } catch (AccountingRuleException) { return AccountingOutcome.Invalid; }
    }
    public Task<AccountingPage> GeneralLedgerAsync(TrustedAccountingContext context, DateOnly? from, DateOnly? to, string? cursor, int pageSize, CancellationToken cancellationToken)
        => store.ListJournalsAsync(context, from, to, cursor, Math.Clamp(pageSize, 1, 100), cancellationToken);
    public async Task<IReadOnlyList<TrialBalanceLine>> TrialBalanceAsync(TrustedAccountingContext context, DateOnly through, CancellationToken cancellationToken)
    {
        var page = await store.ListJournalsAsync(context, null, through, null, 10_000, cancellationToken);
        return page.Items.SelectMany(x => x.Lines).GroupBy(x => x.AccountId).Select(x => new TrialBalanceLine(x.Key, x.Where(y => y.Side is JournalSide.Debit).Sum(y => y.AmountVnd), x.Where(y => y.Side is JournalSide.Credit).Sum(y => y.AmountVnd))).OrderBy(x => x.AccountId.Value, StringComparer.Ordinal).ToArray();
    }
}
