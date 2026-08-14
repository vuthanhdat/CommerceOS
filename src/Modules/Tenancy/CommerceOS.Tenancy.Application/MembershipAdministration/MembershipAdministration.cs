using System.Security.Cryptography;
using System.Text;
using CommerceOS.SubscriptionBilling.Contracts;
using CommerceOS.Tenancy.Application.Authority;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.Application.MembershipAdministration;

public enum InvitationStatus { Pending, Accepted, Revoked, Expired }
public enum MembershipAdministrationOutcome { Applied, AlreadyApplied, Forbidden, NotFound, RevisionConflict, LastOwnerProtected, LimitReached, InvitationConflict, InvitationExpired, InvitationCredentialInvalid, DisabledMembership }

public sealed record MerchantInvitation(
    string Id,
    TenantId TenantId,
    string NormalizedRecipientEmail,
    MerchantRole Role,
    InvitationStatus Status,
    string CredentialDigest,
    DateTimeOffset ExpiresAt,
    long Revision);

public sealed record InvitationIssueResult(MembershipAdministrationOutcome Outcome, MerchantInvitation? Invitation, string? Credential = null);

public sealed record InvitationAcceptanceContext(SubjectId SubjectId, string VerifiedEmail)
{
    public static InvitationAcceptanceContext FromVerifiedIdentity(SubjectId subjectId, string verifiedEmail) =>
        string.IsNullOrWhiteSpace(verifiedEmail) ? throw new ArgumentException("A verified email is required.", nameof(verifiedEmail)) : new(subjectId, verifiedEmail.Trim());
}

public interface IMembershipAdministrationStore
{
    Task<Membership?> GetMembershipAsync(TenantId tenantId, MembershipId id, CancellationToken ct);
    Task<Membership?> GetMembershipForSubjectAsync(TenantId tenantId, SubjectId subjectId, CancellationToken ct);
    Task<MerchantInvitation?> GetInvitationAsync(TenantId tenantId, string id, CancellationToken ct);
    Task<MerchantInvitation?> GetPendingInvitationForEmailAsync(TenantId tenantId, string email, CancellationToken ct);
    Task<MembershipAdministrationOutcome> ApplyMembershipChangeAsync(Membership previous, Membership updated, int max, CancellationToken ct);
    Task<InvitationIssueResult> IssueOrResendInvitationAsync(MerchantInvitation invitation, CancellationToken ct);
    Task<MembershipAdministrationOutcome> RevokeInvitationAsync(MerchantInvitation invitation, CancellationToken ct);
    Task<MembershipAdministrationOutcome> AcceptInvitationAsync(MerchantInvitation invitation, Membership membership, int max, CancellationToken ct);
}

public sealed class MembershipAdministrationService
{
    private readonly IMembershipAdministrationStore _store;
    private readonly IEntitlementEvaluator _entitlements;
    private readonly TimeProvider _clock;

    public MembershipAdministrationService(IMembershipAdministrationStore store, IEntitlementEvaluator entitlements, TimeProvider? clock = null)
    {
        _store = store;
        _entitlements = entitlements;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<MembershipAdministrationOutcome> ChangeMembershipAsync(TrustedTenantMutationContext actor, MembershipId membershipId, MembershipStatus status, MerchantRole role, long expectedRevision, CancellationToken cancellationToken)
    {
        if (actor.Role is not MerchantRole.Owner and not MerchantRole.Admin) return MembershipAdministrationOutcome.Forbidden;
        var current = await _store.GetMembershipAsync(actor.TenantId, membershipId, cancellationToken);
        if (current is null) return MembershipAdministrationOutcome.NotFound;
        if (current.Revision != expectedRevision) return MembershipAdministrationOutcome.RevisionConflict;
        if (actor.Role is MerchantRole.Admin && (current.Role is MerchantRole.Owner || role is MerchantRole.Owner)) return MembershipAdministrationOutcome.Forbidden;
        if (current.Status == status && current.Role == role) return MembershipAdministrationOutcome.AlreadyApplied;
        var activation = current.Status is MembershipStatus.Disabled && status is MembershipStatus.Active;
        var limit = activation ? await ActiveMembershipLimitAsync(actor.TenantId, actor.CorrelationId, cancellationToken) : int.MaxValue;
        return await _store.ApplyMembershipChangeAsync(current, current with { Status = status, Role = role, Revision = current.Revision + 1 }, limit, cancellationToken);
    }

    public async Task<InvitationIssueResult> IssueInvitationAsync(TrustedTenantMutationContext actor, string recipientEmail, MerchantRole role, CancellationToken cancellationToken)
    {
        if (actor.Role is not MerchantRole.Owner and not MerchantRole.Admin || role is MerchantRole.Owner) return new(MembershipAdministrationOutcome.Forbidden, null);
        var email = NormalizeEmail(recipientEmail);
        var existing = await _store.GetPendingInvitationForEmailAsync(actor.TenantId, email, cancellationToken);
        if (actor.Role is MerchantRole.Admin && role is MerchantRole.Owner) return new(MembershipAdministrationOutcome.Forbidden, null);
        var credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var invitation = new MerchantInvitation(existing?.Id ?? $"inv-{Guid.NewGuid():N}", actor.TenantId, email, role, InvitationStatus.Pending, Digest(credential), _clock.GetUtcNow().AddDays(7), (existing?.Revision ?? 0) + 1);
        var result = await _store.IssueOrResendInvitationAsync(invitation, cancellationToken);
        return result.Outcome is MembershipAdministrationOutcome.Applied
            ? result with { Credential = credential }
            : result;
    }

    public async Task<MembershipAdministrationOutcome> RevokeInvitationAsync(TrustedTenantMutationContext actor, string invitationId, long expectedRevision, CancellationToken cancellationToken)
    {
        if (actor.Role is not MerchantRole.Owner and not MerchantRole.Admin) return MembershipAdministrationOutcome.Forbidden;
        var invitation = await _store.GetInvitationAsync(actor.TenantId, invitationId, cancellationToken);
        if (invitation is null) return MembershipAdministrationOutcome.NotFound;
        if (invitation.Revision != expectedRevision) return MembershipAdministrationOutcome.RevisionConflict;
        if (invitation.Status is not InvitationStatus.Pending) return MembershipAdministrationOutcome.AlreadyApplied;
        return await _store.RevokeInvitationAsync(invitation with { Status = InvitationStatus.Revoked, Revision = invitation.Revision + 1 }, cancellationToken);
    }

    public async Task<MembershipAdministrationOutcome> AcceptInvitationAsync(InvitationAcceptanceContext context, TenantId tenantId, string invitationId, string credential, CancellationToken cancellationToken)
    {
        var invitation = await _store.GetInvitationAsync(tenantId, invitationId, cancellationToken);
        if (invitation is null) return MembershipAdministrationOutcome.NotFound;
        if (invitation.Status is not InvitationStatus.Pending) return MembershipAdministrationOutcome.AlreadyApplied;
        if (invitation.ExpiresAt <= _clock.GetUtcNow()) return MembershipAdministrationOutcome.InvitationExpired;
        if (invitation.NormalizedRecipientEmail != NormalizeEmail(context.VerifiedEmail) || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(invitation.CredentialDigest), Convert.FromHexString(Digest(credential)))) return MembershipAdministrationOutcome.InvitationCredentialInvalid;
        var existing = await _store.GetMembershipForSubjectAsync(tenantId, context.SubjectId, cancellationToken);
        if (existing?.Status is MembershipStatus.Active) return MembershipAdministrationOutcome.AlreadyApplied;
        if (existing?.Status is MembershipStatus.Disabled) return MembershipAdministrationOutcome.DisabledMembership;
        var limit = await ActiveMembershipLimitAsync(tenantId, $"invitation:{invitation.Id}", cancellationToken);
        var membership = new Membership(new MembershipId($"mem-{Guid.NewGuid():N}"), tenantId, context.SubjectId, invitation.Role, MembershipStatus.Active, 1);
        return await _store.AcceptInvitationAsync(invitation with { Status = InvitationStatus.Accepted, Revision = invitation.Revision + 1 }, membership, limit, cancellationToken);
    }

    private async Task<int> ActiveMembershipLimitAsync(TenantId tenantId, string correlationId, CancellationToken cancellationToken)
    {
        var decision = await _entitlements.EvaluateEntitlementAsync(new EvaluateEntitlementRequest(tenantId.Value, EntitlementKey.MaxActiveMemberships, _clock.GetUtcNow(), correlationId), cancellationToken);
        return decision.Outcome is EntitlementDecisionOutcome.Granted && decision.Limit is { } limit && limit > 0 ? limit : 0;
    }

    private static string NormalizeEmail(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Recipient email is required.", nameof(value)) : value.Trim().ToLowerInvariant();
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
