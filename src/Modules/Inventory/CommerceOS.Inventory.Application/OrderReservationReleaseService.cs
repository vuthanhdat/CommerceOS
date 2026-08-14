using CommerceOS.Inventory.Contracts;
using CommerceOS.Inventory.Domain;

namespace CommerceOS.Inventory.Application;

public interface IOrderReservationStore { Task<IReadOnlyList<(StockItem Stock, StockReservation Reservation)>> GetActiveForOrderAsync(TrustedInventoryMutationContext context, string orderId, CancellationToken cancellationToken); }
public sealed class OrderReservationReleaseService(IOrderReservationStore reservations, IStockOperationStore operations) : IOrderStockRelease
{
    public async Task<OrderStockOutcome> ReleaseAsync(ReleaseOrderStock command, CancellationToken cancellationToken)
    {
        var context = new TrustedInventoryMutationContext(new(command.TrustedTenantId), command.CorrelationId); var active = await reservations.GetActiveForOrderAsync(context, command.OrderId, cancellationToken);
        foreach (var (stock, reservation) in active) { var after = StockMath.Apply(stock, StockMovementType.Release, reservation.Quantity); var movement = new StockMovement($"movement:{command.SourceIdentity}:{reservation.Id}", context.TenantId, stock.ProductId, stock.WarehouseId, StockMovementType.Release, reservation.Quantity, command.SourceIdentity, command.CorrelationId, DateTimeOffset.UtcNow); if (await operations.ApplyAsync(context, stock, after, movement, reservation, reservation with { Status = StockReservationStatus.Released, Revision = reservation.Revision + 1 }, cancellationToken) is StockOperationOutcome.Invalid) return OrderStockOutcome.NeedsAttention; }
        return active.Count == 0 ? OrderStockOutcome.AlreadyApplied : OrderStockOutcome.Applied;
    }
}
