namespace CommerceOS.Accounting.Domain;

public readonly record struct AccountingTenantId
{
    public AccountingTenantId(string value) => Value = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Tenant ID is required.", nameof(value)) : value;
    public string Value { get; }
}

public readonly record struct AccountId
{
    public AccountId(string value) => Value = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Account ID is required.", nameof(value)) : value;
    public string Value { get; }
}

public enum AccountRole { Cash, CustomerDeposits, SalesRevenue, Inventory, CostOfGoodsSold, AccountsPayable, GoodsReceivedNotInvoiced, PurchasePriceVariance, InventoryAdjustmentGain, InventoryAdjustmentLoss, NonControl }
public enum AccountStatus { Active, Inactive }

public sealed record Account(AccountId Id, AccountingTenantId TenantId, string Code, string DisplayName, AccountRole Role, AccountStatus Status, bool HasPostedReference, long Revision)
{
    public static Account Control(AccountingTenantId tenantId, AccountRole role, string code, string name)
        => new(new($"control:{role}"), tenantId, code, name, role, AccountStatus.Active, false, 1);
    public Account Deactivate() => Role is not AccountRole.NonControl ? throw new AccountingRuleException("CONTROL_ACCOUNT_REQUIRED") : this with { Status = AccountStatus.Inactive, Revision = Revision + 1 };
}

public enum JournalSide { Debit, Credit }
public sealed record JournalLine(AccountId AccountId, JournalSide Side, long AmountVnd)
{
    public static JournalLine Debit(AccountId accountId, long amountVnd) => new(accountId, JournalSide.Debit, amountVnd);
    public static JournalLine Credit(AccountId accountId, long amountVnd) => new(accountId, JournalSide.Credit, amountVnd);
}

public sealed record Journal(string Id, AccountingTenantId TenantId, DateOnly EffectiveDate, DateTimeOffset PostingTimestamp, string SourceIdentity, string CorrelationId, string? ReversesJournalId, IReadOnlyList<JournalLine> Lines)
{
    public static Journal Create(string id, AccountingTenantId tenantId, DateOnly effectiveDate, DateTimeOffset postedAt, string sourceIdentity, string correlationId, IReadOnlyList<JournalLine> lines, string? reversesJournalId = null)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(sourceIdentity) || string.IsNullOrWhiteSpace(correlationId) || lines.Count < 2 || lines.Any(x => x.AmountVnd <= 0)) throw new AccountingRuleException("JOURNAL_INVALID");
        if (lines.Where(x => x.Side is JournalSide.Debit).Sum(x => x.AmountVnd) != lines.Where(x => x.Side is JournalSide.Credit).Sum(x => x.AmountVnd)) throw new AccountingRuleException("JOURNAL_UNBALANCED");
        return new(id, tenantId, effectiveDate, postedAt, sourceIdentity, correlationId, reversesJournalId, lines.ToArray());
    }
    public Journal Reverse(string reversalId, DateTimeOffset postedAt, DateOnly effectiveDate, string sourceIdentity, string correlationId)
        => Create(reversalId, TenantId, effectiveDate, postedAt, sourceIdentity, correlationId, Lines.Select(x => x with { Side = x.Side is JournalSide.Debit ? JournalSide.Credit : JournalSide.Debit }).ToArray(), Id);
}

/// <summary>Moving weighted-average truth is tenant + product; warehouse is intentionally absent.</summary>
public sealed record ValuationState(AccountingTenantId TenantId, string ProductId, long Quantity, long TotalCostVnd, long Revision)
{
    public long UnitCostVnd => Quantity == 0 ? 0 : TotalCostVnd / Quantity;
    public ValuationState Receive(long quantity, long totalCost) => quantity <= 0 || totalCost < 0 ? throw new AccountingRuleException("VALUATION_RECEIPT_INVALID") : this with { Quantity = checked(Quantity + quantity), TotalCostVnd = checked(TotalCostVnd + totalCost), Revision = Revision + 1 };
    public (ValuationState State, long Cost) Issue(long quantity)
    {
        if (quantity <= 0 || quantity > Quantity) throw new AccountingRuleException("VALUATION_INSUFFICIENT_QUANTITY");
        var cost = checked(UnitCostVnd * quantity);
        return (this with { Quantity = Quantity - quantity, TotalCostVnd = TotalCostVnd - cost, Revision = Revision + 1 }, cost);
    }
}

public sealed class AccountingRuleException(string code) : InvalidOperationException(code) { public string Code { get; } = code; }
