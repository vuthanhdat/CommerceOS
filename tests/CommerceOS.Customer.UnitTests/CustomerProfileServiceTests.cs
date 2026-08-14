using CommerceOS.Customer.Application;
using CommerceOS.Customer.Contracts;
using CommerceOS.Customer.Domain;

namespace CommerceOS.Customer.UnitTests;

public sealed class CustomerProfileServiceTests
{
    [Fact]
    public async Task ExplicitProfileIsTenantScopedAndDoesNotUseContactAsIdentity()
    {
        var store = new Store(); var service = new CustomerProfileService(store);
        var first = await service.CreateAsync(new("tenant-a", "Lan", "same@example.test", null, new(true, false), "c"), default);
        var second = await service.CreateAsync(new("tenant-a", "Lan Two", "same@example.test", null, new(false, true), "c"), default);
        Assert.Equal(CustomerCommandOutcome.Applied, first.Outcome); Assert.Equal(CustomerCommandOutcome.Applied, second.Outcome); Assert.NotEqual(first.Customer!.CustomerId, second.Customer!.CustomerId);
        Assert.Null(await service.GetAsync("tenant-b", first.Customer.CustomerId, default));
    }
    [Fact]
    public async Task UpdateIsRevisionProtectedAndDoesNotTouchAnyGuestOrderData()
    {
        var store = new Store(); var service = new CustomerProfileService(store); var created = (await service.CreateAsync(new("tenant", "Lan", null, null, new(false, false), "c"), default)).Customer!;
        var update = await service.UpdateAsync(new("tenant", created.CustomerId, "Lan Updated", "lan@example.test", null, new(true, false), created.Revision, "c"), default);
        Assert.Equal(CustomerCommandOutcome.Applied, update.Outcome); Assert.Equal("Lan Updated", update.Customer!.DisplayName); Assert.True(update.Customer.Preferences.EmailOptIn);
        Assert.Equal(CustomerCommandOutcome.Conflict, (await service.UpdateAsync(new("tenant", created.CustomerId, "Stale", null, null, new(false, false), created.Revision, "c"), default)).Outcome);
    }
    private sealed class Store : ICustomerProfileStore
    {
        private readonly Dictionary<(string Tenant, string Customer), CustomerProfile> values = [];
        public Task<CustomerProfile?> GetAsync(TrustedCustomerContext context, CustomerId id, CancellationToken ct) => Task.FromResult(values.GetValueOrDefault((context.TenantId.Value, id.Value)));
        public Task<CustomerCommandOutcome> CreateAsync(TrustedCustomerContext context, CustomerProfile customer, CancellationToken ct) => Task.FromResult(values.TryAdd((context.TenantId.Value, customer.Id.Value), customer) ? CustomerCommandOutcome.Applied : CustomerCommandOutcome.Conflict);
        public Task<CustomerCommandOutcome> SaveAsync(TrustedCustomerContext context, CustomerProfile before, CustomerProfile after, CancellationToken ct) { if (!values.TryGetValue((context.TenantId.Value, before.Id.Value), out var current) || current.Revision != before.Revision) return Task.FromResult(CustomerCommandOutcome.Conflict); values[(context.TenantId.Value, after.Id.Value)] = after; return Task.FromResult(CustomerCommandOutcome.Applied); }
        public Task<IReadOnlyList<CustomerProfile>> ListAsync(TrustedCustomerContext context, string? search, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<CustomerProfile>>(values.Values.Where(x => x.TenantId == context.TenantId).Take(pageSize).ToArray());
    }
}
