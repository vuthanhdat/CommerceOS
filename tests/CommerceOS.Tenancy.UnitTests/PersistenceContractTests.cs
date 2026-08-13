using CommerceOS.Tenancy.Application.Persistence;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.UnitTests;

public sealed class PersistenceContractTests
{
    [Fact]
    public void TenantBoundStoreMethodsRequireTrustedTenantPersistenceScope()
    {
        var tenantBoundMethods = typeof(ITenancyStore).GetMethods()
            .Where(method => method.Name is not nameof(ITenancyStore.FindMembershipCandidatesAsync))
            .ToArray();

        Assert.NotEmpty(tenantBoundMethods);
        Assert.All(tenantBoundMethods, method =>
            Assert.Equal(typeof(TrustedTenantPersistenceScope), method.GetParameters()[0].ParameterType));
    }

    [Fact]
    public void SubjectDiscoveryReturnsCandidatesRatherThanAuthority()
    {
        var method = typeof(ITenancyStore).GetMethod(nameof(ITenancyStore.FindMembershipCandidatesAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(SubjectId), method.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(Task<IReadOnlyList<MembershipDiscoveryCandidate>>), method.ReturnType);
    }

    [Fact]
    public void OpaqueIdentifiersRejectEmptyValues()
    {
        Assert.Throws<ArgumentException>(() => new TenantId(""));
        Assert.Throws<ArgumentException>(() => new MembershipId(" "));
        Assert.Throws<ArgumentException>(() => new SubjectId(""));
    }
}
