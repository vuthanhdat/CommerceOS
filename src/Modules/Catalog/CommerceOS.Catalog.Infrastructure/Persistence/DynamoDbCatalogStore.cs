using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Catalog.Application;
using CommerceOS.Catalog.Contracts;
using CommerceOS.Catalog.Domain;

namespace CommerceOS.Catalog.Infrastructure.Persistence;

public sealed record DynamoDbCatalogOptions(string TableName);
public sealed class DynamoDbCatalogStore(IAmazonDynamoDB client, DynamoDbCatalogOptions options) : ICatalogStore, ICatalogReferenceStore, ICatalogImportStore, IPublicCatalogProjectionStore
{
    public async Task<IReadOnlyList<Product>> ListPublishedAsync(CatalogTenantId tenantId, string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        var response = await client.QueryAsync(new() { TableName = options.TableName, KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)", FilterExpression = "#status = :published", ExpressionAttributeNames = new() { ["#status"] = "Status" }, ExpressionAttributeValues = new() { [":pk"] = S(P(tenantId)), [":prefix"] = S("PRODUCT#"), [":published"] = S(ProductStatus.Published.ToString()) }, Limit = Math.Clamp(pageSize, 1, 50), ExclusiveStartKey = string.IsNullOrWhiteSpace(cursor) ? null : Key(tenantId, $"PRODUCT#{E(cursor)}") }, cancellationToken);
        return response.Items.Select(Read).ToArray();
    }
    public async Task<Product?> GetPublishedBySlugAsync(CatalogTenantId tenantId, string slug, CancellationToken cancellationToken)
    {
        var claim = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(tenantId, $"SLUG#{E(Product.Normalize(slug))}") }, cancellationToken);
        return claim.Item.Count == 0 ? null : await GetPublishedAsync(tenantId, new ProductId(claim.Item["ProductId"].S), cancellationToken);
    }
    public async Task<Product?> GetPublishedAsync(CatalogTenantId tenantId, ProductId productId, CancellationToken cancellationToken)
    { var product = await GetAsync(new(tenantId, "public-query"), productId, cancellationToken); return product?.Status is ProductStatus.Published ? product : null; }
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
    public async Task<Category?> GetCategoryAsync(TrustedCatalogMutationContext context, CategoryId id, CancellationToken cancellationToken) => await GetReferenceAsync<Category>(context, "CATEGORY", id.Value, cancellationToken);
    public async Task<Brand?> GetBrandAsync(TrustedCatalogMutationContext context, BrandId id, CancellationToken cancellationToken) => await GetReferenceAsync<Brand>(context, "BRAND", id.Value, cancellationToken);
    public Task<CatalogReferenceOutcome> SaveCategoryAsync(TrustedCatalogMutationContext context, Category category, long? expectedRevision, CancellationToken cancellationToken) => SaveReferenceAsync(context, "CATEGORY", category.Id.Value, category.TenantId, category.Name, category.Status, category.Revision, expectedRevision, cancellationToken);
    public Task<CatalogReferenceOutcome> SaveBrandAsync(TrustedCatalogMutationContext context, Brand brand, long? expectedRevision, CancellationToken cancellationToken) => SaveReferenceAsync(context, "BRAND", brand.Id.Value, brand.TenantId, brand.Name, brand.Status, brand.Revision, expectedRevision, cancellationToken);
    public async Task<ImportCandidateApplicationResult?> GetImportApplicationAsync(TrustedCatalogMutationContext context, string candidateId, CancellationToken cancellationToken)
    { var x = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, $"IMPORT#{E(candidateId)}") }, cancellationToken); return x.Item.Count == 0 ? null : new(Enum.Parse<ImportCandidateApplicationOutcome>(x.Item["Outcome"].S), long.Parse(x.Item["ProductRevision"].N, CultureInfo.InvariantCulture)); }
    public async Task<ImportCandidateApplicationOutcome> ApplyImportAsync(TrustedCatalogMutationContext context, Product before, Product after, string candidateId, string sourceId, string sourceProductId, CancellationToken cancellationToken)
    {
        var sourceKey = $"SOURCEPRODUCT#{E(sourceId)}#{E(sourceProductId)}";
        try { await client.TransactWriteItemsAsync(new() { TransactItems = [new() { Put = new() { TableName = options.TableName, Item = Item(after), ConditionExpression = "Revision = :revision", ExpressionAttributeValues = new() { [":revision"] = N(before.Revision) } } }, new() { Put = new() { TableName = options.TableName, Item = new() { ["PK"] = S(P(context.TenantId)), ["SK"] = S(sourceKey), ["ProductId"] = S(after.Id.Value) }, ConditionExpression = "attribute_not_exists(PK)" } }, new() { Put = new() { TableName = options.TableName, Item = new() { ["PK"] = S(P(context.TenantId)), ["SK"] = S($"IMPORT#{E(candidateId)}"), ["Outcome"] = S(ImportCandidateApplicationOutcome.Applied.ToString()), ["ProductRevision"] = N(after.Revision) }, ConditionExpression = "attribute_not_exists(PK)" } }] }, cancellationToken); return ImportCandidateApplicationOutcome.Applied; }
        catch (TransactionCanceledException) { return ImportCandidateApplicationOutcome.Conflict; }
    }
    private async Task<T?> GetReferenceAsync<T>(TrustedCatalogMutationContext context, string kind, string id, CancellationToken cancellationToken) where T : class
    { var x = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, $"{kind}#{E(id)}") }, cancellationToken); if (x.Item.Count == 0) return null; var item = x.Item; object value = kind == "CATEGORY" ? new Category(new(item["Id"].S), new(item["TenantId"].S), item["Name"].S, Enum.Parse<CatalogReferenceStatus>(item["Status"].S), long.Parse(item["Revision"].N, CultureInfo.InvariantCulture)) : new Brand(new(item["Id"].S), new(item["TenantId"].S), item["Name"].S, Enum.Parse<CatalogReferenceStatus>(item["Status"].S), long.Parse(item["Revision"].N, CultureInfo.InvariantCulture)); return (T)value; }
    private async Task<CatalogReferenceOutcome> SaveReferenceAsync(TrustedCatalogMutationContext context, string kind, string id, CatalogTenantId tenant, string name, CatalogReferenceStatus status, long revision, long? expectedRevision, CancellationToken cancellationToken)
    { if (tenant != context.TenantId) return CatalogReferenceOutcome.ReferenceInvalid; var item = new Dictionary<string, AttributeValue> { ["PK"] = S(P(tenant)), ["SK"] = S($"{kind}#{E(id)}"), ["Id"] = S(id), ["TenantId"] = S(tenant.Value), ["Name"] = S(name), ["Status"] = S(status.ToString()), ["Revision"] = N(revision) }; try { await client.PutItemAsync(new() { TableName = options.TableName, Item = item, ConditionExpression = expectedRevision is null ? "attribute_not_exists(PK)" : "Revision = :revision", ExpressionAttributeValues = expectedRevision is null ? null : new() { [":revision"] = N(expectedRevision.Value) } }, cancellationToken); return CatalogReferenceOutcome.Applied; } catch (ConditionalCheckFailedException) { return CatalogReferenceOutcome.RevisionConflict; } }
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
