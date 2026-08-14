using System.Security.Cryptography;
using System.Text;
using CommerceOS.Sales.Contracts;
using CommerceOS.Sales.Domain;

namespace CommerceOS.Sales.Application;

public sealed record TrustedSalesContext(SalesTenantId TenantId, string CorrelationId);
public enum SalesStoreOutcome { Applied, Replayed, Conflict }
public sealed record SalesOrderPage(IReadOnlyList<SalesOrder> Items, string? NextCursor);
public interface ISalesOrderStore
{
    Task<SalesStoreOutcome> PlaceAsync(TrustedSalesContext context, SalesOrder order, string idempotencyKey, string requestHash, CancellationToken cancellationToken);
    Task<SalesOrder?> GetAsync(TrustedSalesContext context, SalesOrderId orderId, CancellationToken cancellationToken);
    Task<SalesStoreOutcome> SaveAsync(TrustedSalesContext context, SalesOrder before, SalesOrder after, CancellationToken cancellationToken);
    Task<SalesOrderPage> ListAsync(TrustedSalesContext context, string? cursor, int pageSize, CancellationToken cancellationToken);
}
public sealed class SalesOrderService(ISalesOrderStore store) : ISalesOrderPlacement
{
    public async Task<OrderPlacementResult> PlaceAsync(PlaceAcceptedOrder command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.TrustedTenantId) || string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128 || string.IsNullOrWhiteSpace(command.CorrelationId)) return new(OrderPlacementOutcome.Invalid, null, null);
        try
        {
            var context = new TrustedSalesContext(new(command.TrustedTenantId), command.CorrelationId);
            var hash = Hash(command);
            var token = StableToken($"{command.TrustedTenantId}\n{command.IdempotencyKey}");
            var order = SalesOrder.Place(new($"ord-{token}"), context.TenantId, command.Lines.Select(x => new SalesOrderLine(x.ProductId, x.Sku, x.Name, x.Quantity, x.UnitPriceVnd)).ToArray(), command.TotalVnd, new(command.Guest.Name.Trim(), command.Guest.Email.Trim(), command.Guest.Phone?.Trim(), command.Guest.Address?.Trim()), new($"process-{token}", $"order-{token}", true));
            return await store.PlaceAsync(context, order, command.IdempotencyKey, hash, cancellationToken) switch { SalesStoreOutcome.Applied => new(OrderPlacementOutcome.Accepted, order.Id.Value, order.Status.ToString()), SalesStoreOutcome.Replayed => new(OrderPlacementOutcome.Replayed, order.Id.Value, order.Status.ToString()), _ => new(OrderPlacementOutcome.Conflict, null, null) };
        }
        catch (SalesRuleException) { return new(OrderPlacementOutcome.Invalid, null, null); }
    }
    public async Task<SalesStoreOutcome> ApplyTransitionAsync(TrustedSalesContext context, SalesOrderId orderId, SalesOrderStatus target, string sourceIdentity, long expectedRevision, CancellationToken ct)
    { var before = await store.GetAsync(context, orderId, ct); if (before is null) return SalesStoreOutcome.Conflict; try { return await store.SaveAsync(context, before, before.Apply(target, sourceIdentity, expectedRevision), ct); } catch (SalesRuleException) { return SalesStoreOutcome.Conflict; } }
    public Task<SalesOrderPage> ListAsync(TrustedSalesContext context, string? cursor, int pageSize, CancellationToken ct) => store.ListAsync(context, cursor, Math.Clamp(pageSize, 1, 50), ct);
    private static string Hash(PlaceAcceptedOrder command) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{command.TrustedTenantId}|{string.Join(';', command.Lines.Select(x => $"{x.ProductId}:{x.Quantity}:{x.UnitPriceVnd}"))}|{command.TotalVnd}|{command.Guest.Name}|{command.Guest.Email}|{command.Guest.Phone}|{command.Guest.Address}")));
    private static string StableToken(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..24];
}
