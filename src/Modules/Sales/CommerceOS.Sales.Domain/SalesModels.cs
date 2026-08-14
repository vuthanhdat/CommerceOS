namespace CommerceOS.Sales.Domain;

public readonly record struct SalesTenantId
{
    public SalesTenantId(string value) : this() => Value = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Tenant ID is required.", nameof(value)) : value;
    public string Value { get; }
}
public readonly record struct SalesOrderId
{
    public SalesOrderId(string value) : this() => Value = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Order ID is required.", nameof(value)) : value;
    public string Value { get; }
}
public enum SalesOrderStatus { Placed, Confirmed, Allocated, Fulfilled, Completed, Cancelled }
public sealed record SalesOrderLine(string ProductId, string Sku, string Name, long Quantity, long UnitPriceVnd)
{
    public long LineTotalVnd => checked(Quantity * UnitPriceVnd);
}
public sealed record GuestSnapshot(string Name, string Email, string? Phone, string? Address);
public sealed record OrderProcess(string Id, string WorkflowExecutionIdentity, bool StartPending);
public sealed record SalesOrder(SalesOrderId Id, SalesTenantId TenantId, IReadOnlyList<SalesOrderLine> Lines, long TotalVnd, GuestSnapshot Guest, SalesOrderStatus Status, long Revision, OrderProcess Process, IReadOnlySet<string> AcceptedSources)
{
    public static SalesOrder Place(SalesOrderId id, SalesTenantId tenantId, IReadOnlyList<SalesOrderLine> lines, long totalVnd, GuestSnapshot guest, OrderProcess process)
    {
        if (lines.Count is 0 or > 50 || lines.Any(x => string.IsNullOrWhiteSpace(x.ProductId) || x.Quantity <= 0 || x.UnitPriceVnd < 0) || totalVnd != lines.Sum(x => x.LineTotalVnd)) throw new SalesRuleException("ORDER_INVALID");
        if (string.IsNullOrWhiteSpace(guest.Name) || string.IsNullOrWhiteSpace(guest.Email)) throw new SalesRuleException("GUEST_INVALID");
        return new(id, tenantId, lines, totalVnd, guest, SalesOrderStatus.Placed, 1, process, new HashSet<string>(StringComparer.Ordinal));
    }
    public SalesOrder Apply(SalesOrderStatus target, string sourceIdentity, long expectedRevision)
    {
        if (Revision != expectedRevision) throw new SalesRuleException("ORDER_REVISION_STALE");
        if (AcceptedSources.Contains(sourceIdentity)) return this;
        var approved = (Status, target) switch { (SalesOrderStatus.Placed, SalesOrderStatus.Confirmed) => true, (SalesOrderStatus.Confirmed, SalesOrderStatus.Allocated) => true, (SalesOrderStatus.Allocated, SalesOrderStatus.Fulfilled) => true, (SalesOrderStatus.Fulfilled, SalesOrderStatus.Completed) => true, (SalesOrderStatus.Placed or SalesOrderStatus.Confirmed or SalesOrderStatus.Allocated, SalesOrderStatus.Cancelled) => true, _ => false };
        if (!approved) throw new SalesRuleException("ORDER_TRANSITION_INVALID");
        return this with { Status = target, Revision = Revision + 1, AcceptedSources = new HashSet<string>(AcceptedSources, StringComparer.Ordinal) { sourceIdentity } };
    }
}
public sealed class SalesRuleException(string code) : InvalidOperationException(code) { public string Code { get; } = code; }
