using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Customer.Application;
using CommerceOS.Customer.Contracts;
using CommerceOS.Customer.Domain;

namespace CommerceOS.Customer.Infrastructure.Persistence;

public sealed record DynamoDbCustomerOptions(string TableName);
public sealed class DynamoDbCustomerProfileStore(IAmazonDynamoDB client, DynamoDbCustomerOptions options) : ICustomerProfileStore
{
    public async Task<CustomerProfile?> GetAsync(TrustedCustomerContext context, CustomerId customerId, CancellationToken cancellationToken)
    { var result = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, customerId) }, cancellationToken); return result.Item.Count == 0 ? null : Read(result.Item); }
    public async Task<CustomerCommandOutcome> CreateAsync(TrustedCustomerContext context, CustomerProfile customer, CancellationToken cancellationToken)
    { if (customer.TenantId != context.TenantId) return CustomerCommandOutcome.NotFound; try { await client.PutItemAsync(new() { TableName = options.TableName, Item = Item(customer), ConditionExpression = "attribute_not_exists(PK)" }, cancellationToken); return CustomerCommandOutcome.Applied; } catch (ConditionalCheckFailedException) { return CustomerCommandOutcome.Conflict; } }
    public async Task<CustomerCommandOutcome> SaveAsync(TrustedCustomerContext context, CustomerProfile before, CustomerProfile after, CancellationToken cancellationToken)
    { if (before.TenantId != context.TenantId || after.TenantId != context.TenantId || before.Id != after.Id) return CustomerCommandOutcome.NotFound; try { await client.PutItemAsync(new() { TableName = options.TableName, Item = Item(after), ConditionExpression = "Revision = :revision", ExpressionAttributeValues = new() { [":revision"] = N(before.Revision) } }, cancellationToken); return CustomerCommandOutcome.Applied; } catch (ConditionalCheckFailedException) { return CustomerCommandOutcome.Conflict; } }
    public async Task<IReadOnlyList<CustomerProfile>> ListAsync(TrustedCustomerContext context, string? search, int pageSize, CancellationToken cancellationToken)
    {
        var result = await client.QueryAsync(new() { TableName = options.TableName, KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)", ExpressionAttributeValues = new() { [":pk"] = S(P(context.TenantId)), [":prefix"] = S("CUSTOMER#") }, Limit = pageSize }, cancellationToken);
        var normalized = search?.Trim();
        return result.Items.Select(Read).Where(x => string.IsNullOrWhiteSpace(normalized) || x.DisplayName.Contains(normalized, StringComparison.OrdinalIgnoreCase) || x.Email?.Contains(normalized, StringComparison.OrdinalIgnoreCase) == true || x.Phone?.Contains(normalized, StringComparison.OrdinalIgnoreCase) == true).ToArray();
    }
    private static CustomerProfile Read(Dictionary<string, AttributeValue> x) => new(new(x["CustomerId"].S), new(x["TenantId"].S), x["DisplayName"].S, Empty(x["Email"].S), Empty(x["Phone"].S), new(x["EmailOptIn"].BOOL == true, x["SmsOptIn"].BOOL == true), long.Parse(x["Revision"].N, CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> Item(CustomerProfile x) => new() { ["PK"] = S(P(x.TenantId)), ["SK"] = S($"CUSTOMER#{E(x.Id.Value)}"), ["CustomerId"] = S(x.Id.Value), ["TenantId"] = S(x.TenantId.Value), ["DisplayName"] = S(x.DisplayName), ["Email"] = S(x.Email ?? string.Empty), ["Phone"] = S(x.Phone ?? string.Empty), ["EmailOptIn"] = new() { BOOL = x.Preferences.EmailOptIn }, ["SmsOptIn"] = new() { BOOL = x.Preferences.SmsOptIn }, ["Revision"] = N(x.Revision) };
    private static Dictionary<string, AttributeValue> Key(CustomerTenantId tenant, CustomerId id) => new() { ["PK"] = S(P(tenant)), ["SK"] = S($"CUSTOMER#{E(id.Value)}") };
    private static string P(CustomerTenantId tenant) => $"TENANT#{E(tenant.Value)}"; private static string E(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static string? Empty(string value) => string.IsNullOrEmpty(value) ? null : value; private static AttributeValue S(string value) => new() { S = value }; private static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
}
