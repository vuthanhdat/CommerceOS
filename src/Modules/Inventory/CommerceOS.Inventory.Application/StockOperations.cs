using CommerceOS.Inventory.Domain;

namespace CommerceOS.Inventory.Application;

public enum StockOperationOutcome { Applied, AlreadyApplied, InsufficientAvailable, ReservationTerminal, RevisionConflict, Invalid }
public interface IStockOperationStore
{
    Task<StockOperationOutcome> ApplyAsync(TrustedInventoryMutationContext context, StockItem before, StockItem after, StockMovement movement, StockReservation? reservationBefore, StockReservation? reservationAfter, CancellationToken cancellationToken);
}
public sealed class StockOperationService(IInventoryStore inventory, IStockOperationStore operations, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public Task<StockOperationOutcome> ReceiveAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, long quantity, string source, CancellationToken cancellationToken) => ApplyAsync(context, productId, warehouseId, StockMovementType.Receive, quantity, source, null, cancellationToken);
    public Task<StockOperationOutcome> AdjustDecreaseAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, long quantity, string source, CancellationToken cancellationToken) => ApplyAsync(context, productId, warehouseId, StockMovementType.AdjustmentDecrease, quantity, source, null, cancellationToken);
    public Task<StockOperationOutcome> ReserveAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, long quantity, string orderId, string source, CancellationToken cancellationToken) => ApplyAsync(context, productId, warehouseId, StockMovementType.Reserve, quantity, source, new StockReservation($"reservation:{source}", context.TenantId, orderId, productId, warehouseId, quantity, StockReservationStatus.Active, 1), cancellationToken);
    public Task<StockOperationOutcome> ReleaseAsync(TrustedInventoryMutationContext context, StockItem stock, StockReservation reservation, string source, CancellationToken cancellationToken) => ApplyKnownAsync(context, stock, StockMovementType.Release, reservation.Quantity, source, reservation, StockReservationStatus.Released, cancellationToken);
    public Task<StockOperationOutcome> IssueAsync(TrustedInventoryMutationContext context, StockItem stock, StockReservation reservation, string source, CancellationToken cancellationToken) => ApplyKnownAsync(context, stock, StockMovementType.Issue, reservation.Quantity, source, reservation, StockReservationStatus.Issued, cancellationToken);
    public Task<StockOperationOutcome> ReturnAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, long quantity, string source, CancellationToken cancellationToken) => ApplyAsync(context, productId, warehouseId, StockMovementType.Return, quantity, source, null, cancellationToken);
    private async Task<StockOperationOutcome> ApplyAsync(TrustedInventoryMutationContext context, InventoryProductId productId, WarehouseId warehouseId, StockMovementType type, long quantity, string source, StockReservation? reservation, CancellationToken ct)
    { var stock = await inventory.GetStockItemAsync(context, productId, warehouseId, ct) ?? StockItem.Create(context.TenantId, productId, warehouseId); return await ApplyKnownAsync(context, stock, type, quantity, source, reservation, reservation?.Status, ct); }
    private async Task<StockOperationOutcome> ApplyKnownAsync(TrustedInventoryMutationContext context, StockItem stock, StockMovementType type, long quantity, string source, StockReservation? reservation, StockReservationStatus? target, CancellationToken ct)
    { if (reservation is not null && reservation.Status is not StockReservationStatus.Active) return StockOperationOutcome.ReservationTerminal; try { var after = StockMath.Apply(stock, type, quantity); var nextReservation = reservation is null ? null : reservation with { Status = target!.Value, Revision = reservation.Revision + 1 }; var movement = new StockMovement($"movement:{source}", context.TenantId, stock.ProductId, stock.WarehouseId, type, quantity, source, context.CorrelationId, _clock.GetUtcNow()); return await operations.ApplyAsync(context, stock, after, movement, reservation, nextReservation, ct); } catch (InventoryRuleException exception) { return exception.Code is "INSUFFICIENT_AVAILABLE_STOCK" ? StockOperationOutcome.InsufficientAvailable : StockOperationOutcome.Invalid; } }
}
