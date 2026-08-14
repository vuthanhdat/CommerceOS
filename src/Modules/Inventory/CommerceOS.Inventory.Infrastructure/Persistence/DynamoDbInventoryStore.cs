using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CommerceOS.Inventory.Application;
using CommerceOS.Inventory.Domain;

namespace CommerceOS.Inventory.Infrastructure.Persistence;

public sealed record DynamoDbInventoryOptions(string TableName);
public sealed class DynamoDbInventoryStore(IAmazonDynamoDB client, DynamoDbInventoryOptions options) : IInventoryStore, IStockOperationStore
{
    public async Task<Warehouse?> GetWarehouseAsync(TrustedInventoryMutationContext context, WarehouseId id, CancellationToken ct)
    { var x = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, $"WAREHOUSE#{E(id.Value)}") }, ct); return x.Item.Count == 0 ? null : ReadWarehouse(x.Item); }
    public async Task<StockItem?> GetStockItemAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, CancellationToken ct)
    { var x = await client.GetItemAsync(new() { TableName = options.TableName, ConsistentRead = true, Key = Key(context.TenantId, $"STOCK#{E(productId.Value)}#{E(warehouseId.Value)}") }, ct); return x.Item.Count == 0 ? null : new(context.TenantId, productId, warehouseId, long.Parse(x.Item["OnHand"].N, CultureInfo.InvariantCulture), long.Parse(x.Item["Reserved"].N, CultureInfo.InvariantCulture), long.Parse(x.Item["Revision"].N, CultureInfo.InvariantCulture)); }
    public async Task<InventoryOutcome> CreateOrReactivateWarehouseAsync(TrustedInventoryMutationContext context, Warehouse? previous, Warehouse updatedWarehouse, int maxWarehouses, CancellationToken cancellationToken)
    {
        if (maxWarehouses <= 0) return InventoryOutcome.LimitReached;
        var guardKey = Key(context.TenantId, "ACTIVE-WAREHOUSE-GUARD");
        var item = WarehouseItem(updatedWarehouse);
        var put = new Put { TableName = options.TableName, Item = item, ConditionExpression = previous is null ? "attribute_not_exists(PK)" : "Revision = :revision", ExpressionAttributeValues = previous is null ? null : new() { [":revision"] = N(previous.Revision) } };
        var update = new Update { TableName = options.TableName, Key = guardKey, UpdateExpression = "ADD ActiveCount :one", ConditionExpression = "attribute_not_exists(ActiveCount) OR ActiveCount < :limit", ExpressionAttributeValues = new() { [":one"] = N(1), [":limit"] = N(maxWarehouses) } };
        try { await client.TransactWriteItemsAsync(new() { TransactItems = [new() { Put = put }, new() { Update = update }] }, cancellationToken); return InventoryOutcome.Applied; }
        catch (TransactionCanceledException) { return InventoryOutcome.LimitReached; }
    }
    public async Task<InventoryOutcome> DisableWarehouseAsync(TrustedInventoryMutationContext context, Warehouse previous, CancellationToken ct)
    {
        try { await client.TransactWriteItemsAsync(new() { TransactItems = [new() { Put = new() { TableName = options.TableName, Item = WarehouseItem(previous), ConditionExpression = "Revision = :revision", ExpressionAttributeValues = new() { [":revision"] = N(previous.Revision - 1) } } }, new() { Update = new() { TableName = options.TableName, Key = Key(context.TenantId, "ACTIVE-WAREHOUSE-GUARD"), UpdateExpression = "ADD ActiveCount :minus", ConditionExpression = "ActiveCount > :zero", ExpressionAttributeValues = new() { [":minus"] = N(-1), [":zero"] = N(0) } } }] }, ct); return InventoryOutcome.Applied; } catch (TransactionCanceledException) { return InventoryOutcome.RevisionConflict; }
    }
    public async Task<StockOperationOutcome> ApplyAsync(TrustedInventoryMutationContext context, StockItem before, StockItem after, StockMovement movement, StockReservation? reservationBefore, StockReservation? reservationAfter, CancellationToken cancellationToken)
    {
        var writes = new List<TransactWriteItem> { new() { Put = new() { TableName = options.TableName, Item = StockItemItem(after), ConditionExpression = "attribute_not_exists(PK) OR Revision = :revision", ExpressionAttributeValues = new() { [":revision"] = N(before.Revision) } } }, new() { Put = new() { TableName = options.TableName, Item = MovementItem(movement), ConditionExpression = "attribute_not_exists(PK)" } } };
        if (reservationAfter is not null) writes.Add(new() { Put = new() { TableName = options.TableName, Item = ReservationItem(reservationAfter), ConditionExpression = reservationBefore is null ? "attribute_not_exists(PK)" : "Revision = :revision AND #status = :status", ExpressionAttributeNames = reservationBefore is null ? null : new() { ["#status"] = "Status" }, ExpressionAttributeValues = reservationBefore is null ? null : new() { [":revision"] = N(reservationBefore.Revision), [":status"] = S(reservationBefore.Status.ToString()) } } });
        try { await client.TransactWriteItemsAsync(new() { TransactItems = writes }, cancellationToken); return StockOperationOutcome.Applied; }
        catch (TransactionCanceledException) { return StockOperationOutcome.AlreadyApplied; }
    }
    private static Warehouse ReadWarehouse(Dictionary<string, AttributeValue> x) => new(new(x["WarehouseId"].S), new(x["TenantId"].S), x["Name"].S, Enum.Parse<WarehouseStatus>(x["Status"].S), long.Parse(x["Revision"].N, CultureInfo.InvariantCulture));
    private static Dictionary<string, AttributeValue> WarehouseItem(Warehouse x) => new() { ["PK"] = S(P(x.TenantId)), ["SK"] = S($"WAREHOUSE#{E(x.Id.Value)}"), ["WarehouseId"] = S(x.Id.Value), ["TenantId"] = S(x.TenantId.Value), ["Name"] = S(x.Name), ["Status"] = S(x.Status.ToString()), ["Revision"] = N(x.Revision) };
    private static Dictionary<string, AttributeValue> StockItemItem(StockItem x) => new() { ["PK"] = S(P(x.TenantId)), ["SK"] = S($"STOCK#{E(x.ProductId.Value)}#{E(x.WarehouseId.Value)}"), ["OnHand"] = N(x.OnHand), ["Reserved"] = N(x.Reserved), ["Revision"] = N(x.Revision) };
    private static Dictionary<string, AttributeValue> MovementItem(StockMovement x) => new() { ["PK"] = S(P(x.TenantId)), ["SK"] = S($"MOVEMENT#{E(x.Id)}"), ["Type"] = S(x.Type.ToString()), ["Quantity"] = N(x.Quantity), ["Source"] = S(x.SourceIdentity), ["CorrelationId"] = S(x.CorrelationId), ["OccurredAt"] = S(x.OccurredAt.ToString("O", CultureInfo.InvariantCulture)) };
    private static Dictionary<string, AttributeValue> ReservationItem(StockReservation x) => new() { ["PK"] = S(P(x.TenantId)), ["SK"] = S($"RESERVATION#{E(x.Id)}"), ["OrderId"] = S(x.OrderId), ["ProductId"] = S(x.ProductId.Value), ["WarehouseId"] = S(x.WarehouseId.Value), ["Quantity"] = N(x.Quantity), ["Status"] = S(x.Status.ToString()), ["Revision"] = N(x.Revision) };
    private static Dictionary<string, AttributeValue> Key(InventoryTenantId t, string sk) => new() { ["PK"] = S(P(t)), ["SK"] = S(sk) }; private static string P(InventoryTenantId t) => $"TENANT#{E(t.Value)}"; private static string E(string s) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_'); private static AttributeValue S(string s) => new() { S = s }; private static AttributeValue N(long n) => new() { N = n.ToString(CultureInfo.InvariantCulture) };
}
