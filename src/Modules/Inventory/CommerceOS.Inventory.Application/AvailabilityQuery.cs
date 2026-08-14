using CommerceOS.Inventory.Contracts;
using CommerceOS.Inventory.Domain;

namespace CommerceOS.Inventory.Application;

public interface IStockAvailabilityStore { Task<long> GetAvailableAsync(InventoryTenantId tenantId, InventoryProductId productId, CancellationToken cancellationToken); }
public sealed class InventoryAvailabilityQuery(IStockAvailabilityStore store) : IInventoryAvailabilityQuery
{
    public async Task<ProductAvailability> GetAvailabilityAsync(string trustedTenantId, string productId, CancellationToken cancellationToken) => new(productId, await store.GetAvailableAsync(new(trustedTenantId), new(productId), cancellationToken));
}
