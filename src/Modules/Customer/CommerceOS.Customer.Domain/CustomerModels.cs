namespace CommerceOS.Customer.Domain;

public readonly record struct CustomerTenantId
{
    public CustomerTenantId(string value) => Value = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Tenant ID is required.", nameof(value)) : value;
    public string Value { get; }
}
public readonly record struct CustomerId
{
    public CustomerId(string value) => Value = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Customer ID is required.", nameof(value)) : value;
    public string Value { get; }
}
public sealed record CommunicationPreferences(bool EmailOptIn, bool SmsOptIn);
public sealed record CustomerProfile(CustomerId Id, CustomerTenantId TenantId, string DisplayName, string? Email, string? Phone, CommunicationPreferences Preferences, long Revision)
{
    public static CustomerProfile Create(CustomerId id, CustomerTenantId tenantId, string displayName, string? email, string? phone, CommunicationPreferences preferences)
        => new(id, tenantId, Name(displayName), Optional(email, nameof(email)), Optional(phone, nameof(phone)), preferences, 1);
    public CustomerProfile Update(string displayName, string? email, string? phone, CommunicationPreferences preferences, long expectedRevision)
    {
        if (Revision != expectedRevision) throw new CustomerRuleException("CUSTOMER_REVISION_STALE");
        return this with { DisplayName = Name(displayName), Email = Optional(email, nameof(email)), Phone = Optional(phone, nameof(phone)), Preferences = preferences, Revision = Revision + 1 };
    }
    private static string Name(string value) => string.IsNullOrWhiteSpace(value) ? throw new CustomerRuleException("CUSTOMER_NAME_REQUIRED") : value.Trim();
    private static string? Optional(string? value, string parameter) => value is null ? null : string.IsNullOrWhiteSpace(value) ? throw new CustomerRuleException("CUSTOMER_CONTACT_INVALID") : value.Trim();
}
public sealed class CustomerRuleException(string code) : InvalidOperationException(code) { public string Code { get; } = code; }
