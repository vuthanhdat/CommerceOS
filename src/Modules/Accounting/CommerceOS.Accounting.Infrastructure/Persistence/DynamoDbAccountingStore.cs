using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Accounting.Application;
using CommerceOS.Accounting.Domain;

namespace CommerceOS.Accounting.Infrastructure.Persistence;

public sealed record DynamoDbAccountingOptions(string TableName);
/// <summary>All source claims, journals and valuation changes are owner-local DynamoDB transactions.</summary>
public sealed class DynamoDbAccountingStore(IAmazonDynamoDB client, DynamoDbAccountingOptions options) : IAccountingStore
{
    public async Task<AccountingOutcome> BootstrapChartAsync(TrustedAccountingContext context, IReadOnlyList<Account> accounts, CancellationToken ct)
    {
        if (accounts.Any(x => x.TenantId != context.TenantId)) return AccountingOutcome.Invalid;
        var missing = new List<Account>();
        foreach (var account in accounts) if (await GetAccountAsync(context, account.Id, ct) is null) missing.Add(account);
        if (missing.Count == 0) return AccountingOutcome.AlreadyApplied;
        try
        {
            await client.TransactWriteItemsAsync(new()
            {
                TransactItems = missing.SelectMany(account => new TransactWriteItem[]
            {
                new() { Put = new Put { TableName = options.TableName, Item = AccountItem(account), ConditionExpression = "attribute_not_exists(PK)" } },
                new() { Put = new Put { TableName = options.TableName, Item = new() { ["PK"] = S(P(context.TenantId)), ["SK"] = S($"ACCOUNT-CODE#{E(account.Code)}"), ["AccountId"] = S(account.Id.Value) }, ConditionExpression = "attribute_not_exists(PK)" } }
            }).ToList()
            }, ct);
            return AccountingOutcome.Applied;
        }
        catch (TransactionCanceledException) { return AccountingOutcome.AlreadyApplied; }
    }
    public async Task<AccountingOutcome> CreateAccountAsync(TrustedAccountingContext context, Account account, CancellationToken ct)
    {
        if (account.TenantId != context.TenantId) return AccountingOutcome.NotFound;
        try
        {
            await client.TransactWriteItemsAsync(new()
            {
                TransactItems =
            [
                new() { Put = new Put { TableName = options.TableName, Item = AccountItem(account), ConditionExpression = "attribute_not_exists(PK)" } },
                new() { Put = new Put { TableName = options.TableName, Item = new() { ["PK"] = S(P(context.TenantId)), ["SK"] = S($"ACCOUNT-CODE#{E(account.Code)}"), ["AccountId"] = S(account.Id.Value) }, ConditionExpression = "attribute_not_exists(PK)" } }
            ]
            }, ct);
            return AccountingOutcome.Applied;
        }
        catch (TransactionCanceledException) { return AccountingOutcome.Conflict; }
    }
    public async Task<Account?> GetAccountAsync(TrustedAccountingContext context, AccountId accountId, CancellationToken ct)
    { var item = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, $"ACCOUNT#{E(accountId.Value)}") }, ct); return item.Item.Count == 0 ? null : ReadAccount(item.Item); }
    public async Task<AccountingOutcome> SaveAccountAsync(TrustedAccountingContext context, Account before, Account after, CancellationToken ct)
    {
        if (before.TenantId != context.TenantId || after.TenantId != context.TenantId || before.Id != after.Id || before.Code != after.Code) return AccountingOutcome.Invalid;
        try { await client.PutItemAsync(new() { TableName = options.TableName, Item = AccountItem(after), ConditionExpression = "Revision = :revision", ExpressionAttributeValues = new() { [":revision"] = N(before.Revision) } }, ct); return AccountingOutcome.Applied; } catch (ConditionalCheckFailedException) { return AccountingOutcome.Conflict; }
    }
    public async Task<AccountingOutcome> PostAsync(TrustedAccountingContext context, Journal journal, IReadOnlyList<ValuationState> valuations, CancellationToken ct)
    {
        if (journal.TenantId != context.TenantId || journal.Lines.Select(x => x.AccountId).Distinct().Count() > 10 || valuations.Count > 8) return AccountingOutcome.Invalid;
        var writes = new List<TransactWriteItem>
        {
            new() { Put = new Put { TableName = options.TableName, Item = JournalItem(journal), ConditionExpression = "attribute_not_exists(PK)" } },
            new() { Put = new Put { TableName = options.TableName, Item = new() { ["PK"] = S(P(context.TenantId)), ["SK"] = S($"SOURCE#{E(journal.SourceIdentity)}"), ["JournalId"] = S(journal.Id), ["CorrelationId"] = S(journal.CorrelationId) }, ConditionExpression = "attribute_not_exists(PK)" } }
        };
        foreach (var accountId in journal.Lines.Select(x => x.AccountId).Distinct()) writes.Add(new() { Update = new Update { TableName = options.TableName, Key = Key(context.TenantId, $"ACCOUNT#{E(accountId.Value)}"), UpdateExpression = "SET HasPostedReference = :true", ConditionExpression = "#status = :active", ExpressionAttributeNames = new() { ["#status"] = "Status" }, ExpressionAttributeValues = new() { [":true"] = new() { BOOL = true }, [":active"] = S(AccountStatus.Active.ToString()) } } });
        foreach (var valuation in valuations)
        {
            if (valuation.TenantId != context.TenantId) return AccountingOutcome.Invalid;
            writes.Add(new() { Put = new Put { TableName = options.TableName, Item = ValuationItem(valuation), ConditionExpression = valuation.Revision <= 1 ? "attribute_not_exists(PK)" : "Revision = :revision", ExpressionAttributeValues = valuation.Revision <= 1 ? null : new() { [":revision"] = N(valuation.Revision - 1) } } });
        }
        try { await client.TransactWriteItemsAsync(new() { TransactItems = writes }, ct); return AccountingOutcome.Applied; }
        catch (TransactionCanceledException) { return AccountingOutcome.AlreadyApplied; }
    }
    public async Task<Journal?> GetJournalAsync(TrustedAccountingContext context, string journalId, CancellationToken ct)
    { var item = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, $"JOURNAL#{E(journalId)}") }, ct); return item.Item.Count == 0 ? null : ReadJournal(item.Item); }
    public async Task<AccountingPage> ListJournalsAsync(TrustedAccountingContext context, DateOnly? from, DateOnly? through, string? cursor, int pageSize, CancellationToken ct)
    {
        var start = string.IsNullOrWhiteSpace(cursor) ? null : new Dictionary<string, AttributeValue> { ["PK"] = S(P(context.TenantId)), ["SK"] = S(cursor) };
        var response = await client.QueryAsync(new() { TableName = options.TableName, KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)", ExpressionAttributeValues = new() { [":pk"] = S(P(context.TenantId)), [":prefix"] = S("JOURNAL#") }, ExclusiveStartKey = start, Limit = pageSize }, ct);
        var items = response.Items.Select(ReadJournal).Where(x => (!from.HasValue || x.EffectiveDate >= from) && (!through.HasValue || x.EffectiveDate <= through)).OrderBy(x => x.EffectiveDate).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        return new(items, response.LastEvaluatedKey.Count == 0 ? null : response.LastEvaluatedKey["SK"].S);
    }
    public async Task<ValuationState?> GetValuationAsync(TrustedAccountingContext context, string productId, CancellationToken ct)
    { var item = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, $"VALUATION#{E(productId)}") }, ct); return item.Item.Count == 0 ? null : new(context.TenantId, item.Item["ProductId"].S, L(item.Item["Quantity"]), L(item.Item["TotalCostVnd"]), L(item.Item["Revision"])); }
    private static Account ReadAccount(Dictionary<string, AttributeValue> x) => new(new(x["AccountId"].S), new(x["TenantId"].S), x["Code"].S, x["Name"].S, Enum.Parse<AccountRole>(x["Role"].S), Enum.Parse<AccountStatus>(x["Status"].S), bool.Parse(x["HasPostedReference"].BOOL.ToString()!), L(x["Revision"]));
    private static Journal ReadJournal(Dictionary<string, AttributeValue> x) => new(x["JournalId"].S, new(x["TenantId"].S), DateOnly.Parse(x["EffectiveDate"].S, CultureInfo.InvariantCulture), DateTimeOffset.Parse(x["PostingTimestamp"].S, CultureInfo.InvariantCulture), x["SourceIdentity"].S, x["CorrelationId"].S, x.TryGetValue("ReversesJournalId", out var reverse) ? reverse.S : null, x["Lines"].L.Select(line => new JournalLine(new(line.M["AccountId"].S), Enum.Parse<JournalSide>(line.M["Side"].S), L(line.M["AmountVnd"]))).ToArray());
    private static Dictionary<string, AttributeValue> AccountItem(Account x) => new() { ["PK"] = S(P(x.TenantId)), ["SK"] = S($"ACCOUNT#{E(x.Id.Value)}"), ["AccountId"] = S(x.Id.Value), ["TenantId"] = S(x.TenantId.Value), ["Code"] = S(x.Code), ["Name"] = S(x.DisplayName), ["Role"] = S(x.Role.ToString()), ["Status"] = S(x.Status.ToString()), ["HasPostedReference"] = new() { BOOL = x.HasPostedReference }, ["Revision"] = N(x.Revision) };
    private static Dictionary<string, AttributeValue> JournalItem(Journal x) => new() { ["PK"] = S(P(x.TenantId)), ["SK"] = S($"JOURNAL#{E(x.Id)}"), ["JournalId"] = S(x.Id), ["TenantId"] = S(x.TenantId.Value), ["EffectiveDate"] = S(x.EffectiveDate.ToString("O", CultureInfo.InvariantCulture)), ["PostingTimestamp"] = S(x.PostingTimestamp.ToString("O", CultureInfo.InvariantCulture)), ["SourceIdentity"] = S(x.SourceIdentity), ["CorrelationId"] = S(x.CorrelationId), ["Lines"] = new() { L = x.Lines.Select(line => new AttributeValue { M = new() { ["AccountId"] = S(line.AccountId.Value), ["Side"] = S(line.Side.ToString()), ["AmountVnd"] = N(line.AmountVnd) } }).ToList() } };
    private static Dictionary<string, AttributeValue> ValuationItem(ValuationState x) => new() { ["PK"] = S(P(x.TenantId)), ["SK"] = S($"VALUATION#{E(x.ProductId)}"), ["ProductId"] = S(x.ProductId), ["Quantity"] = N(x.Quantity), ["TotalCostVnd"] = N(x.TotalCostVnd), ["Revision"] = N(x.Revision) };
    private static Dictionary<string, AttributeValue> Key(AccountingTenantId t, string sk) => new() { ["PK"] = S(P(t)), ["SK"] = S(sk) }; private static string P(AccountingTenantId t) => $"TENANT#{E(t.Value)}"; private static string E(string x) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(x)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static AttributeValue S(string x) => new() { S = x }; private static AttributeValue N(long x) => new() { N = x.ToString(CultureInfo.InvariantCulture) }; private static long L(AttributeValue x) => long.Parse(x.N, CultureInfo.InvariantCulture);
}
