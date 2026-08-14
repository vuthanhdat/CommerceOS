using CommerceOS.Customer.Contracts;
using CommerceOS.Customer.Domain;

namespace CommerceOS.Customer.Application;

public sealed record TrustedCustomerContext(CustomerTenantId TenantId, string CorrelationId);
public interface ICustomerProfileStore
{
    Task<CustomerProfile?> GetAsync(TrustedCustomerContext context, CustomerId customerId, CancellationToken cancellationToken);
    Task<CustomerCommandOutcome> CreateAsync(TrustedCustomerContext context, CustomerProfile customer, CancellationToken cancellationToken);
    Task<CustomerCommandOutcome> SaveAsync(TrustedCustomerContext context, CustomerProfile before, CustomerProfile after, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerProfile>> ListAsync(TrustedCustomerContext context, string? search, int pageSize, CancellationToken cancellationToken);
}

/// <summary>Customer is an explicit merchant-created profile; it never derives identity from guest checkout data.</summary>
public sealed class CustomerProfileService(ICustomerProfileStore store) : ICustomerProfileService
{
    public async Task<(CustomerCommandOutcome Outcome, CustomerProfileView? Customer)> CreateAsync(CreateCustomerProfile command, CancellationToken cancellationToken)
    {
        if (!Valid(command.TrustedTenantId, command.CorrelationId)) return (CustomerCommandOutcome.Invalid, null);
        try
        {
            var context = Context(command.TrustedTenantId, command.CorrelationId);
            var profile = CustomerProfile.Create(new($"customer-{Guid.NewGuid():N}"), context.TenantId, command.DisplayName, command.Email, command.Phone, new(command.Preferences.EmailOptIn, command.Preferences.SmsOptIn));
            var outcome = await store.CreateAsync(context, profile, cancellationToken);
            return (outcome, outcome is CustomerCommandOutcome.Applied ? View(profile) : null);
        }
        catch (CustomerRuleException) { return (CustomerCommandOutcome.Invalid, null); }
    }
    public async Task<(CustomerCommandOutcome Outcome, CustomerProfileView? Customer)> UpdateAsync(UpdateCustomerProfile command, CancellationToken cancellationToken)
    {
        if (!Valid(command.TrustedTenantId, command.CorrelationId) || string.IsNullOrWhiteSpace(command.CustomerId)) return (CustomerCommandOutcome.Invalid, null);
        var context = Context(command.TrustedTenantId, command.CorrelationId); var before = await store.GetAsync(context, new(command.CustomerId), cancellationToken);
        if (before is null) return (CustomerCommandOutcome.NotFound, null);
        try { var after = before.Update(command.DisplayName, command.Email, command.Phone, new(command.Preferences.EmailOptIn, command.Preferences.SmsOptIn), command.ExpectedRevision); var outcome = await store.SaveAsync(context, before, after, cancellationToken); return (outcome, outcome is CustomerCommandOutcome.Applied ? View(after) : null); }
        catch (CustomerRuleException) { return (CustomerCommandOutcome.Conflict, null); }
    }
    public async Task<CustomerProfileView?> GetAsync(string trustedTenantId, string customerId, CancellationToken cancellationToken)
        => !Valid(trustedTenantId, "customer-read") || string.IsNullOrWhiteSpace(customerId) ? null : (await store.GetAsync(Context(trustedTenantId, "customer-read"), new(customerId), cancellationToken)) is { } profile ? View(profile) : null;
    public async Task<IReadOnlyList<CustomerProfileView>> ListAsync(string trustedTenantId, string? search, int pageSize, CancellationToken cancellationToken)
        => !Valid(trustedTenantId, "customer-list") ? [] : (await store.ListAsync(Context(trustedTenantId, "customer-list"), search, Math.Clamp(pageSize, 1, 100), cancellationToken)).Select(View).ToArray();
    private static TrustedCustomerContext Context(string tenant, string correlation) => new(new(tenant), correlation);
    private static bool Valid(string tenant, string correlation) => !string.IsNullOrWhiteSpace(tenant) && !string.IsNullOrWhiteSpace(correlation);
    private static CustomerProfileView View(CustomerProfile x) => new(x.Id.Value, x.DisplayName, x.Email, x.Phone, new(x.Preferences.EmailOptIn, x.Preferences.SmsOptIn), x.Revision);
}
