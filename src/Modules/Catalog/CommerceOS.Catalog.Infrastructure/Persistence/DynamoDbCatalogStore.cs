using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Catalog.Application;
using CommerceOS.Catalog.Domain;

namespace CommerceOS.Catalog.Infrastructure.Persistence;

public sealed record DynamoDbCatalogOptions(string TableName);
public sealed class DynamoDbCatalogStore(IAmazonDynamoDB client, DynamoDbCatalogOptions options) : ICatalogStore
{
    public async Task<Product?> GetAsync(TrustedCatalogMutationContext context, ProductId id, CancellationToken ct)
    {
        var response = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, $"PRODUCT#{E(id.Value)}") }, ct);
        return response.Item.Count == 0 ? null : Read(response.Item);
    }
    public async Task<CatalogOutcome> CreateAsync(TrustedCatalogMutationContext context, Product product, CancellationToken ct)
    {
        var writes = new List<TransactWriteItem> { new() { Put = new() { TableName = options.TableName, Item = Item(product), ConditionExpression = "attribute_not_exists(PK)" } } };
        AddClaim(writes, product.TenantId, "SKU", null, product.Sku, product.Id.Value);
        AddClaim(writes, product.TenantId, "SLUG", null, product.Slug, product.Id.Value);
        try { await client.TransactWriteItemsAsync(new() { TransactItems = writes }, ct); return CatalogOutcome.Applied; }
        catch (TransactionCanceledException) { return CatalogOutcome.RevisionConflict; }
    }
    public async Task<CatalogOutcome> SaveWithClaimsAsync(TrustedCatalogMutationContext context, Product before, Product after, CancellationToken ct)
    {
        if (context.TenantId != before.TenantId || before.TenantId != after.TenantId || before.Id != after.Id) throw new ArgumentException("Trusted scope and product identity must match.");
        var writes = new List<TransactWriteItem> { new() { Put = new() { TableName = options.TableName, Item = Item(after), ConditionExpression = "Revision = :expected", ExpressionAttributeValues = new() { [":expected"] = N(before.Revision) } } } };
        AddClaim(writes, after.TenantId, "SKU", before.Sku, after.Sku, after.Id.Value);
        AddClaim(writes, after.TenantId, "SLUG", before.Slug, after.Slug, after.Id.Value);
        if (!string.IsNullOrWhiteSpace(before.Slug) && !string.Equals(before.Slug, after.Slug, StringComparison.OrdinalIgnoreCase))
            writes.Add(new() { Delete = new() { TableName = options.TableName, Key = Key(after.TenantId, $"SLUG#{E(Product.Normalize(before.Slug))}"), ConditionExpression = "ProductId = :product", ExpressionAttributeValues = new() { [":product"] = S(after.Id.Value) } } });
        try { await client.TransactWriteItemsAsync(new() { TransactItems = writes }, ct); return CatalogOutcome.Applied; }
        catch (TransactionCanceledException) { return CatalogOutcome.RevisionConflict; }
    }
    private void AddClaim(List<TransactWriteItem> writes, CatalogTenantId tenant, string kind, string? oldValue, string? newValue, string productId)
    {
        if (string.IsNullOrWhiteSpace(newValue) || string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase)) return;
        var key = $"{kind}#{E(Product.Normalize(newValue))}";
        writes.Add(new() { Put = new() { TableName = options.TableName, Item = new() { ["PK"] = S(P(tenant)), ["SK"] = S(key), ["ProductId"] = S(productId) }, ConditionExpression = "attribute_not_exists(PK)" } });
    }
    private static Product Read(Dictionary<string, AttributeValue> x) => new(new(x["ProductId"].S), new(x["TenantId"].S), x["Name"].S, x.TryGetValue("Sku", out var sku) ? sku.S : null, x.TryGetValue("Slug", out var slug) ? slug.S : null, x.TryGetValue("Amount", out var amount) ? new Money(long.Parse(amount.N, CultureInfo.InvariantCulture), x["Currency"].S) : null, Enum.Parse<ProductStatus>(x["Status"].S), x["HasBeenPublished"].BOOL ?? false, long.Parse(x["Revision"].N, CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> Item(Product p)
    {
        Dictionary<string, AttributeValue> item = new() { ["PK"] = S(P(p.TenantId)), ["SK"] = S($"PRODUCT#{E(p.Id.Value)}"), ["ProductId"] = S(p.Id.Value), ["TenantId"] = S(p.TenantId.Value), ["Name"] = S(p.Name), ["Status"] = S(p.Status.ToString()), ["HasBeenPublished"] = new() { BOOL = p.HasBeenPublished }, ["Revision"] = N(p.Revision) };
        if (p.Sku is not null) item["Sku"] = S(p.Sku); if (p.Slug is not null) item["Slug"] = S(p.Slug); if (p.BasePrice is not null) { item["Amount"] = N(p.BasePrice.Amount); item["Currency"] = S(p.BasePrice.Currency); }
        return item;
    }
    private static Dictionary<string, AttributeValue> Key(CatalogTenantId t, string sk) => new() { ["PK"] = S(P(t)), ["SK"] = S(sk) };
    private static string P(CatalogTenantId t) => $"TENANT#{E(t.Value)}";
    private static string E(string s) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static AttributeValue S(string s) => new() { S = s }; private static AttributeValue N(long n) => new() { N = n.ToString(CultureInfo.InvariantCulture) };
}
