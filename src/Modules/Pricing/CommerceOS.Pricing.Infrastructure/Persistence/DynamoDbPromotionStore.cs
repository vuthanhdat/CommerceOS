using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Pricing.Application;
using CommerceOS.Pricing.Domain;

namespace CommerceOS.Pricing.Infrastructure.Persistence;

public sealed record DynamoDbPricingOptions(string TableName);

/// <summary>Owner-local schedule index plus conditional transaction protect arbitrary interval overlap checks.</summary>
public sealed class DynamoDbPromotionStore(IAmazonDynamoDB client, DynamoDbPricingOptions options) : IPromotionStore
{
    public async Task<PromotionSchedule> GetScheduleAsync(PricingTenantId tenantId, string productId, CancellationToken cancellationToken)
    {
        var response = await client.GetItemAsync(new GetItemRequest { TableName = options.TableName, ConsistentRead = true, Key = Key(tenantId, ScheduleKey(productId)) }, cancellationToken);
        return response.Item is null or { Count: 0 } ? PromotionSchedule.Empty(tenantId, productId) : new(tenantId, productId, JsonSerializer.Deserialize<PromotionScheduleEntry[]>(response.Item["Entries"].S) ?? [], long.Parse(response.Item["Revision"].N, CultureInfo.InvariantCulture));
    }

    public async Task<Promotion?> GetAsync(PricingTenantId tenantId, PromotionId promotionId, CancellationToken cancellationToken)
    {
        var response = await client.GetItemAsync(new GetItemRequest { TableName = options.TableName, ConsistentRead = true, Key = Key(tenantId, PromotionKey(promotionId)) }, cancellationToken);
        return response.Item is null or { Count: 0 } ? null : ReadPromotion(response.Item);
    }

    public async Task<DateTimeOffset?> GetCancellationAsync(PricingTenantId tenantId, PromotionId promotionId, CancellationToken cancellationToken)
    {
        var response = await client.GetItemAsync(new GetItemRequest { TableName = options.TableName, ConsistentRead = true, Key = Key(tenantId, CancellationKey(promotionId)) }, cancellationToken);
        return response.Item is null or { Count: 0 } ? null : DateTimeOffset.Parse(response.Item["CancelledAt"].S, CultureInfo.InvariantCulture);
    }

    public async Task<PromotionCommandOutcome> ScheduleAsync(TrustedPricingMutationContext context, Promotion promotion, PromotionSchedule before, PromotionSchedule after, CancellationToken cancellationToken)
    {
        if (context.TenantId != promotion.TenantId || before.TenantId != promotion.TenantId || after.TenantId != promotion.TenantId) return PromotionCommandOutcome.Conflict;
        var writes = new List<TransactWriteItem>
        {
            new() { Put = new Put { TableName = options.TableName, Item = PromotionItem(promotion), ConditionExpression = "attribute_not_exists(PK)" } },
            new() { Put = new Put { TableName = options.TableName, Item = ScheduleItem(after), ConditionExpression = before.Revision == 0 ? "attribute_not_exists(PK)" : "Revision = :revision", ExpressionAttributeValues = before.Revision == 0 ? null : new() { [":revision"] = N(before.Revision) } } }
        };
        try { await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = writes }, cancellationToken); return PromotionCommandOutcome.Scheduled; }
        catch (TransactionCanceledException)
        {
            var existing = await GetAsync(context.TenantId, promotion.Id, cancellationToken);
            return existing?.SourceIdentity == promotion.SourceIdentity ? PromotionCommandOutcome.AlreadyApplied : PromotionCommandOutcome.Conflict;
        }
    }

    public async Task<PromotionCommandOutcome> CancelAsync(TrustedPricingMutationContext context, Promotion promotion, PromotionSchedule before, PromotionSchedule after, PromotionCancellation cancellation, CancellationToken cancellationToken)
    {
        if (context.TenantId != promotion.TenantId || cancellation.TenantId != promotion.TenantId) return PromotionCommandOutcome.Conflict;
        var writes = new List<TransactWriteItem>
        {
            new() { Put = new Put { TableName = options.TableName, Item = ScheduleItem(after), ConditionExpression = "Revision = :revision", ExpressionAttributeValues = new() { [":revision"] = N(before.Revision) } } },
            new() { Put = new Put { TableName = options.TableName, Item = CancellationItem(cancellation), ConditionExpression = "attribute_not_exists(PK)" } }
        };
        try { await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = writes }, cancellationToken); return PromotionCommandOutcome.Cancelled; }
        catch (TransactionCanceledException) { return await GetCancellationAsync(context.TenantId, promotion.Id, cancellationToken) is not null ? PromotionCommandOutcome.AlreadyApplied : PromotionCommandOutcome.Conflict; }
    }

    private static Promotion ReadPromotion(Dictionary<string, AttributeValue> item) => new(new(item["PromotionId"].S), new(item["TenantId"].S), item["ProductId"].S, long.Parse(item["PromotionalUnitPriceVnd"].N, CultureInfo.InvariantCulture), DateTimeOffset.Parse(item["EffectiveFrom"].S, CultureInfo.InvariantCulture), DateTimeOffset.Parse(item["EffectiveUntil"].S, CultureInfo.InvariantCulture), item["SourceIdentity"].S, DateTimeOffset.Parse(item["AcceptedAt"].S, CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> PromotionItem(Promotion p) => new() { ["PK"] = S(Partition(p.TenantId)), ["SK"] = S(PromotionKey(p.Id)), ["PromotionId"] = S(p.Id.Value), ["TenantId"] = S(p.TenantId.Value), ["ProductId"] = S(p.ProductId), ["PromotionalUnitPriceVnd"] = N(p.PromotionalUnitPriceVnd), ["EffectiveFrom"] = S(p.EffectiveFrom.ToString("O", CultureInfo.InvariantCulture)), ["EffectiveUntil"] = S(p.EffectiveUntil.ToString("O", CultureInfo.InvariantCulture)), ["SourceIdentity"] = S(p.SourceIdentity), ["AcceptedAt"] = S(p.AcceptedAt.ToString("O", CultureInfo.InvariantCulture)) };
    private static Dictionary<string, AttributeValue> ScheduleItem(PromotionSchedule schedule) => new() { ["PK"] = S(Partition(schedule.TenantId)), ["SK"] = S(ScheduleKey(schedule.ProductId)), ["ProductId"] = S(schedule.ProductId), ["Entries"] = S(JsonSerializer.Serialize(schedule.Entries)), ["Revision"] = N(schedule.Revision) };
    private static Dictionary<string, AttributeValue> CancellationItem(PromotionCancellation c) => new() { ["PK"] = S(Partition(c.TenantId)), ["SK"] = S(CancellationKey(c.PromotionId)), ["PromotionId"] = S(c.PromotionId.Value), ["SourceIdentity"] = S(c.SourceIdentity), ["CancelledAt"] = S(c.CancelledAt.ToString("O", CultureInfo.InvariantCulture)) };
    private static Dictionary<string, AttributeValue> Key(PricingTenantId tenant, string sortKey) => new() { ["PK"] = S(Partition(tenant)), ["SK"] = S(sortKey) };
    private static string Partition(PricingTenantId tenant) => $"TENANT#{Encode(tenant.Value)}";
    private static string PromotionKey(PromotionId id) => $"PROMOTION#{Encode(id.Value)}";
    private static string CancellationKey(PromotionId id) => $"CANCELLATION#{Encode(id.Value)}";
    private static string ScheduleKey(string productId) => $"PRODUCT#{Encode(productId)}#SCHEDULE";
    private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static AttributeValue S(string value) => new() { S = value };
    private static AttributeValue N(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
}
