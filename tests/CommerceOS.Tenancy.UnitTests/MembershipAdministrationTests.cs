using CommerceOS.SubscriptionBilling.Contracts;
using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Application.MembershipAdministration;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.UnitTests;

public sealed class MembershipAdministrationTests
{
    [Fact]
    public async Task LastActiveOwnerCannotBeDisabledAndActivationHonorsCurrentLimit()
    {
        var store = new Store(1); var tenant = new TenantId("tenant-a"); var owner = Member("owner", "subject-owner", MerchantRole.Owner, MembershipStatus.Active, 1); var disabled = Member("disabled", "subject-disabled", MerchantRole.Staff, MembershipStatus.Disabled, 1); store.Members.AddRange([owner, disabled]);
        var service = new MembershipAdministrationService(store, new Entitlements(1)); var actor = Actor(owner);
        Assert.Equal(MembershipAdministrationOutcome.LastOwnerProtected, await service.ChangeMembershipAsync(actor, owner.Id, MembershipStatus.Disabled, MerchantRole.Owner, 1, default));
        Assert.Equal(MembershipAdministrationOutcome.LimitReached, await service.ChangeMembershipAsync(actor, disabled.Id, MembershipStatus.Active, MerchantRole.Staff, 1, default));
    }

    [Fact]
    public async Task ResendRotatesCredentialAndAcceptanceRequiresMatchingVerifiedEmail()
    {
        var store = new Store(0); var tenant = new TenantId("tenant-a"); var owner = Member("owner", "subject-owner", MerchantRole.Owner, MembershipStatus.Active, 1); store.Members.Add(owner); var service = new MembershipAdministrationService(store, new Entitlements(3), new FixedClock()); var actor = Actor(owner);
        var first = await service.IssueInvitationAsync(actor, "Staff@example.test", MerchantRole.Staff, default); var resend = await service.IssueInvitationAsync(actor, "staff@example.test", MerchantRole.Staff, default);
        Assert.NotEqual(first.Credential, resend.Credential); Assert.Equal(first.Invitation!.Id, resend.Invitation!.Id);
        Assert.Equal(MembershipAdministrationOutcome.InvitationCredentialInvalid, await service.AcceptInvitationAsync(InvitationAcceptanceContext.FromVerifiedIdentity(new("subject-staff"), "other@example.test"), tenant, resend.Invitation.Id, resend.Credential!, default));
        Assert.Equal(MembershipAdministrationOutcome.Applied, await service.AcceptInvitationAsync(InvitationAcceptanceContext.FromVerifiedIdentity(new("subject-staff"), "staff@example.test"), tenant, resend.Invitation.Id, resend.Credential!, default));
    }

    [Fact]
    public async Task InvitationDoesNotSilentlyReactivateDisabledMember()
    {
        var store = new Store(1); var tenant = new TenantId("tenant-a"); var owner = Member("owner", "subject-owner", MerchantRole.Owner, MembershipStatus.Active, 1); var disabled = Member("disabled", "subject-staff", MerchantRole.Staff, MembershipStatus.Disabled, 1); store.Members.AddRange([owner, disabled]); var service = new MembershipAdministrationService(store, new Entitlements(3), new FixedClock()); var actor = Actor(owner);
        var invite = await service.IssueInvitationAsync(actor, "staff@example.test", MerchantRole.Staff, default);
        Assert.Equal(MembershipAdministrationOutcome.DisabledMembership, await service.AcceptInvitationAsync(InvitationAcceptanceContext.FromVerifiedIdentity(disabled.SubjectId, "staff@example.test"), tenant, invite.Invitation!.Id, invite.Credential!, default));
    }

    private static Membership Member(string id, string subject, MerchantRole role, MembershipStatus status, long revision) => new(new(id), new("tenant-a"), new(subject), role, status, revision);
    private static TrustedTenantMutationContext Actor(Membership owner) => new(owner.TenantId, owner.SubjectId, owner.Id, MerchantRole.Owner, 1, owner.Revision, "c");
    private sealed class Entitlements(int limit) : IEntitlementEvaluator { public Task<EffectiveEntitlementDecision> EvaluateEntitlementAsync(EvaluateEntitlementRequest request, CancellationToken cancellationToken) => Task.FromResult(new EffectiveEntitlementDecision(EntitlementDecisionOutcome.Granted, null, limit, "v1", null, null)); }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero); }
    private sealed class Store(int activeCount) : IMembershipAdministrationStore
    {
        private int _activeCount = activeCount; public List<Membership> Members { get; } = []; private readonly Dictionary<string, MerchantInvitation> _invitations = [];
        public Task<Membership?> GetMembershipAsync(TenantId tenantId, MembershipId id, CancellationToken ct) => Task.FromResult(Members.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id));
        public Task<Membership?> GetMembershipForSubjectAsync(TenantId tenantId, SubjectId subjectId, CancellationToken ct) => Task.FromResult(Members.SingleOrDefault(x => x.TenantId == tenantId && x.SubjectId == subjectId));
        public Task<MerchantInvitation?> GetInvitationAsync(TenantId tenantId, string id, CancellationToken ct) => Task.FromResult(_invitations.GetValueOrDefault(id));
        public Task<MerchantInvitation?> GetPendingInvitationForEmailAsync(TenantId tenantId, string email, CancellationToken ct) => Task.FromResult(_invitations.Values.SingleOrDefault(x => x.TenantId == tenantId && x.NormalizedRecipientEmail == email && x.Status is InvitationStatus.Pending));
        public Task<MembershipAdministrationOutcome> ApplyMembershipChangeAsync(Membership before, Membership after, int max, CancellationToken ct) { var owners = Members.Count(x => x.Status is MembershipStatus.Active && x.Role is MerchantRole.Owner); if (before.Status is MembershipStatus.Active && before.Role is MerchantRole.Owner && (after.Status is MembershipStatus.Disabled || after.Role is not MerchantRole.Owner) && owners == 1) return Task.FromResult(MembershipAdministrationOutcome.LastOwnerProtected); if (before.Status is MembershipStatus.Disabled && after.Status is MembershipStatus.Active && _activeCount >= max) return Task.FromResult(MembershipAdministrationOutcome.LimitReached); Members[Members.FindIndex(x => x.Id == before.Id)] = after; _activeCount += before.Status == after.Status ? 0 : after.Status is MembershipStatus.Active ? 1 : -1; return Task.FromResult(MembershipAdministrationOutcome.Applied); }
        public Task<InvitationIssueResult> IssueOrResendInvitationAsync(MerchantInvitation invitation, CancellationToken ct) { _invitations[invitation.Id] = invitation; return Task.FromResult(new InvitationIssueResult(MembershipAdministrationOutcome.Applied, invitation)); }
        public Task<MembershipAdministrationOutcome> RevokeInvitationAsync(MerchantInvitation invitation, CancellationToken ct) { _invitations[invitation.Id] = invitation; return Task.FromResult(MembershipAdministrationOutcome.Applied); }
        public Task<MembershipAdministrationOutcome> AcceptInvitationAsync(MerchantInvitation invitation, Membership membership, int max, CancellationToken ct) { if (_activeCount >= max) return Task.FromResult(MembershipAdministrationOutcome.LimitReached); _invitations[invitation.Id] = invitation; Members.Add(membership); _activeCount++; return Task.FromResult(MembershipAdministrationOutcome.Applied); }
    }
}
