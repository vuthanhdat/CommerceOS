namespace CommerceOS.Tenancy.Contracts;

/// <summary>Anonymous public-routing authority. It never grants merchant authority.</summary>
public sealed record PublicTenantContext(string TenantId, string StorefrontSlug, string CorrelationId, string? DisplayName = null);

public interface IPublicTenantResolver
{
    Task<PublicTenantContext?> ResolveActiveAsync(string storefrontSlug, string correlationId, CancellationToken cancellationToken);
}
