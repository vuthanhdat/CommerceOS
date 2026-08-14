using CommerceOS.Accounting.Application;
using CommerceOS.Accounting.Domain;

namespace CommerceOS.Accounting.UnitTests;

public sealed class AccountingTests
{
    private static readonly TrustedAccountingContext Context = new(new("tenant-a"), "correlation-1");

    [Fact]
    public async Task BootstrapIsIdempotentAndTenantScoped()
    {
        var store = new MemoryStore(); var service = new AccountingChartService(store);
        Assert.Equal(AccountingOutcome.Applied, await service.BootstrapAsync(Context, default));
        Assert.Equal(AccountingOutcome.AlreadyApplied, await service.BootstrapAsync(Context, default));
        Assert.Equal(10, store.AccountsFor(Context.TenantId).Length);
        Assert.Null(await store.GetAccountAsync(new(new("tenant-b"), "c"), new("control:Cash"), default));
    }

    [Fact]
    public void JournalRejectsUnbalancedAndReversalNeverEditsOriginal()
    {
        Assert.Throws<AccountingRuleException>(() => Journal.Create("j", Context.TenantId, new(2026, 8, 14), DateTimeOffset.UtcNow, "source", "c", [JournalLine.Debit(new("a"), 2), JournalLine.Credit(new("b"), 1)]));
        var original = Journal.Create("j", Context.TenantId, new(2026, 8, 14), DateTimeOffset.UtcNow, "source", "c", [JournalLine.Debit(new("a"), 1), JournalLine.Credit(new("b"), 1)]);
        var reversal = original.Reverse("r", DateTimeOffset.UtcNow, new(2026, 8, 15), "reverse-source", "c");
        Assert.Equal("j", reversal.ReversesJournalId); Assert.Equal(JournalSide.Debit, original.Lines[0].Side); Assert.Equal(JournalSide.Credit, reversal.Lines[0].Side);
    }

    [Fact]
    public async Task CapturedPaymentReplaysOnceAndPostsDepositNotRevenue()
    {
        var store = new MemoryStore(); await new AccountingChartService(store).BootstrapAsync(Context, default); var consumer = new AccountingFactConsumer(store);
        var fact = new PaymentCapturedAccountingFact(Envelope("payment-1", "PaymentCaptured"), "payment", 100, new(2026, 8, 14));
        Assert.Equal(AccountingOutcome.Applied, await consumer.ApplyAsync(fact, default));
        Assert.Equal(AccountingOutcome.AlreadyApplied, await consumer.ApplyAsync(fact, default));
        var lines = store.Journals.Single().Lines;
        Assert.Contains(lines, x => x.AccountId.Value == "control:Cash" && x.Side is JournalSide.Debit);
        Assert.Contains(lines, x => x.AccountId.Value == "control:CustomerDeposits" && x.Side is JournalSide.Credit);
    }

    [Fact]
    public async Task ReceiptThenIssueUsesAccountingOwnedMovingAverage()
    {
        var store = new MemoryStore(); await new AccountingChartService(store).BootstrapAsync(Context, default); var consumer = new AccountingFactConsumer(store);
        var receipt = new GoodsReceiptAccountingFact(Envelope("receipt-1", "GoodsReceiptRecorded"), "r", [new("product", 4, 40)], new(2026, 8, 14));
        Assert.Equal(AccountingOutcome.Applied, await consumer.ApplyAsync(receipt, default));
        Assert.Equal(AccountingOutcome.Applied, await consumer.ApplyAsync(new StockIssuedAccountingFact(Envelope("issue-1", "StockIssued"), "i", "product", 3, new(2026, 8, 15)), default));
        var issue = store.Journals.Single(x => x.SourceIdentity == "issue-1");
        Assert.Equal(30, issue.Lines.Single(x => x.Side is JournalSide.Debit).AmountVnd);
        Assert.Equal(1, (await store.GetValuationAsync(Context, "product", default))!.Quantity);
    }

    [Fact]
    public async Task TrialBalanceUsesEffectiveDateAndBalances()
    {
        var store = new MemoryStore(); await new AccountingChartService(store).BootstrapAsync(Context, default); var consumer = new AccountingFactConsumer(store);
        await consumer.ApplyAsync(new PaymentCapturedAccountingFact(Envelope("payment-1", "PaymentCaptured"), "payment", 100, new(2026, 8, 14)), default);
        var balance = await new JournalService(store).TrialBalanceAsync(Context, new(2026, 8, 14), default);
        Assert.Equal(0, balance.Sum(x => x.DebitVnd) - balance.Sum(x => x.CreditVnd));
    }

    [Fact]
    public async Task ApprovedInvoicePaymentAndAdjustmentPostBalancedOwnerLocalEffects()
    {
        var store = new MemoryStore(); await new AccountingChartService(store).BootstrapAsync(Context, default); var consumer = new AccountingFactConsumer(store);
        Assert.Equal(AccountingOutcome.Applied, await consumer.ApplyAsync(new SupplierInvoiceAccountingFact(Envelope("invoice-1", "SupplierInvoiceRecorded"), "invoice", 100, 120, true, new(2026, 8, 14)), default));
        Assert.Equal(AccountingOutcome.Applied, await consumer.ApplyAsync(new SupplierPaymentAccountingFact(Envelope("supplier-payment-1", "SupplierPaymentRecorded"), "payment", 120, new(2026, 8, 15)), default));
        Assert.Equal(AccountingOutcome.NeedsAttention, await consumer.ApplyAsync(new StockAdjustedAccountingFact(Envelope("adjustment-1", "StockAdjusted"), "adjust", "product", 1, null, new(2026, 8, 16)), default));
        Assert.All(store.Journals, journal => Assert.Equal(journal.Lines.Where(x => x.Side is JournalSide.Debit).Sum(x => x.AmountVnd), journal.Lines.Where(x => x.Side is JournalSide.Credit).Sum(x => x.AmountVnd)));
    }
    [Fact]
    public async Task RefundFactsPostAppendOnlyCorrectionsAndReuseAccountingOwnedIssueCost()
    {
        var store = new MemoryStore(); await new AccountingChartService(store).BootstrapAsync(Context, default); var consumer = new AccountingFactConsumer(store);
        await consumer.ApplyAsync(new GoodsReceiptAccountingFact(Envelope("receipt", "GoodsReceiptRecorded"), "receipt", [new("product", 2, 20)], new(2026, 8, 14)), default);
        await consumer.ApplyAsync(new StockIssuedAccountingFact(Envelope("issue", "StockIssued"), "issue", "product", 1, new(2026, 8, 14)), default);
        await consumer.ApplyAsync(new OrderFulfilledAccountingFact(Envelope("sale", "OrderFulfilled"), "order", 10, new(2026, 8, 14)), default);
        Assert.Equal(AccountingOutcome.Applied, await consumer.ApplyAsync(new RefundApprovedAccountingFact(Envelope("approved", "RefundApproved"), "refund", "sale", 10, new(2026, 8, 15)), default));
        Assert.Equal(AccountingOutcome.Applied, await consumer.ApplyAsync(new StockReturnedAccountingFact(Envelope("returned", "StockReturned"), "return", "refund", "issue", "product", 1, new(2026, 8, 15)), default));
        Assert.Equal(AccountingOutcome.Applied, await consumer.ApplyAsync(new PaymentRefundedAccountingFact(Envelope("paid", "PaymentRefunded"), "refund", "payment", 10, new(2026, 8, 15)), default));
        Assert.Equal(6, store.Journals.Count); Assert.All(store.Journals, x => Assert.Equal(x.Lines.Where(y => y.Side is JournalSide.Debit).Sum(y => y.AmountVnd), x.Lines.Where(y => y.Side is JournalSide.Credit).Sum(y => y.AmountVnd)));
    }

    private static AccountingFactEnvelope Envelope(string id, string type) => new(id, type, 1, "tenant-a", "aggregate", DateTimeOffset.UtcNow, "correlation-1", null);

    private sealed class MemoryStore : IAccountingStore
    {
        private readonly Dictionary<string, Account> accounts = []; private readonly Dictionary<string, Journal> sources = []; private readonly Dictionary<string, Journal> journals = []; private readonly Dictionary<string, ValuationState> valuations = [];
        public Dictionary<string, Journal>.ValueCollection Journals => journals.Values;
        public Account[] AccountsFor(AccountingTenantId tenant) => accounts.Values.Where(x => x.TenantId == tenant).ToArray();
        private static string Key(AccountingTenantId tenant, string id) => $"{tenant.Value}:{id}";
        public Task<AccountingOutcome> BootstrapChartAsync(TrustedAccountingContext c, IReadOnlyList<Account> a, CancellationToken ct) { var added = false; foreach (var x in a) if (accounts.TryAdd(Key(c.TenantId, x.Id.Value), x)) added = true; return Task.FromResult(added ? AccountingOutcome.Applied : AccountingOutcome.AlreadyApplied); }
        public Task<AccountingOutcome> CreateAccountAsync(TrustedAccountingContext c, Account a, CancellationToken ct) => Task.FromResult(accounts.TryAdd(Key(c.TenantId, a.Id.Value), a) ? AccountingOutcome.Applied : AccountingOutcome.Conflict);
        public Task<Account?> GetAccountAsync(TrustedAccountingContext c, AccountId id, CancellationToken ct) => Task.FromResult(accounts.GetValueOrDefault(Key(c.TenantId, id.Value)));
        public Task<IReadOnlyList<Account>> ListAccountsAsync(TrustedAccountingContext c, CancellationToken ct) => Task.FromResult<IReadOnlyList<Account>>(accounts.Values.Where(x => x.TenantId == c.TenantId).OrderBy(x => x.Code, StringComparer.Ordinal).ToArray());
        public Task<AccountingOutcome> SaveAccountAsync(TrustedAccountingContext c, Account before, Account after, CancellationToken ct) { accounts[Key(c.TenantId, after.Id.Value)] = after; return Task.FromResult(AccountingOutcome.Applied); }
        public Task<AccountingOutcome> PostAsync(TrustedAccountingContext c, Journal journal, IReadOnlyList<ValuationState> values, CancellationToken ct) { if (sources.ContainsKey(Key(c.TenantId, journal.SourceIdentity))) return Task.FromResult(AccountingOutcome.AlreadyApplied); sources[Key(c.TenantId, journal.SourceIdentity)] = journal; journals[Key(c.TenantId, journal.Id)] = journal; foreach (var value in values) valuations[Key(c.TenantId, value.ProductId)] = value; return Task.FromResult(AccountingOutcome.Applied); }
        public Task<Journal?> GetJournalAsync(TrustedAccountingContext c, string id, CancellationToken ct) => Task.FromResult(journals.GetValueOrDefault(Key(c.TenantId, id)));
        public Task<Journal?> GetJournalBySourceAsync(TrustedAccountingContext c, string source, CancellationToken ct) => Task.FromResult(sources.GetValueOrDefault(Key(c.TenantId, source)));
        public Task<AccountingPage> ListJournalsAsync(TrustedAccountingContext c, DateOnly? from, DateOnly? through, string? cursor, int size, CancellationToken ct) => Task.FromResult(new AccountingPage(journals.Values.Where(x => x.TenantId == c.TenantId && (!from.HasValue || x.EffectiveDate >= from) && (!through.HasValue || x.EffectiveDate <= through)).ToArray(), null));
        public Task<ValuationState?> GetValuationAsync(TrustedAccountingContext c, string product, CancellationToken ct) => Task.FromResult(valuations.GetValueOrDefault(Key(c.TenantId, product)));
    }
}
