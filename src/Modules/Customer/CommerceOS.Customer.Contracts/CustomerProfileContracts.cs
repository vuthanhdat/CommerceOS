namespace CommerceOS.Customer.Contracts;

public sealed record CustomerCommunicationPreferences(bool EmailOptIn, bool SmsOptIn);
public sealed record CreateCustomerProfile(string TrustedTenantId, string DisplayName, string? Email, string? Phone, CustomerCommunicationPreferences Preferences, string CorrelationId);
public sealed record UpdateCustomerProfile(string TrustedTenantId, string CustomerId, string DisplayName, string? Email, string? Phone, CustomerCommunicationPreferences Preferences, long ExpectedRevision, string CorrelationId);
public sealed record CustomerProfileView(string CustomerId, string DisplayName, string? Email, string? Phone, CustomerCommunicationPreferences Preferences, long Revision);
public enum CustomerCommandOutcome { Applied, NotFound, Conflict, Invalid }
public interface ICustomerProfileService
{
    Task<(CustomerCommandOutcome Outcome, CustomerProfileView? Customer)> CreateAsync(CreateCustomerProfile command, CancellationToken cancellationToken);
    Task<(CustomerCommandOutcome Outcome, CustomerProfileView? Customer)> UpdateAsync(UpdateCustomerProfile command, CancellationToken cancellationToken);
    Task<CustomerProfileView?> GetAsync(string trustedTenantId, string customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerProfileView>> ListAsync(string trustedTenantId, string? search, int pageSize, CancellationToken cancellationToken);
}
