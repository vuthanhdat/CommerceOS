namespace CommerceOS.Inventory.Contracts;

/// <summary>Producer-owned availability fact. It is informative only and never a reservation.</summary>
public sealed record ProductAvailability(string ProductId, long AvailableQuantity);

public interface IInventoryAvailabilityQuery
{
    Task<ProductAvailability> GetAvailabilityAsync(string trustedTenantId, string productId, CancellationToken cancellationToken);
}
