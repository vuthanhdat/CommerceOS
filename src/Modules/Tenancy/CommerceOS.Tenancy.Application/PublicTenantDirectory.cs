using CommerceOS.Tenancy.Contracts;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.Application;

public interface IPublicTenantDirectoryStore { Task<Tenant?> GetByStorefrontSlugAsync(string normalizedSlug, CancellationToken cancellationToken); }
/// <summary>Public routing is separate from merchant authority; suspension always denies anonymous access.</summary>
public sealed class PublicTenantDirectory(IPublicTenantDirectoryStore store) : IPublicTenantResolver
{
    public async Task<PublicTenantContext?> ResolveActiveAsync(string storefrontSlug, string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storefrontSlug) || string.IsNullOrWhiteSpace(correlationId)) return null;
        var normalized = Normalize(storefrontSlug);
        var tenant = await store.GetByStorefrontSlugAsync(normalized, cancellationToken);
        return tenant is { Status: TenantStatus.Active, StorefrontSlug: not null } && string.Equals(Normalize(tenant.StorefrontSlug), normalized, StringComparison.Ordinal) ? new(tenant.Id.Value, normalized, correlationId, tenant.Profile.DisplayName) : null;
    }
    public static string Normalize(string value) => string.Join('-', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
