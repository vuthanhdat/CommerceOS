namespace CommerceOS.Tenancy.Domain;

public readonly record struct TenantId
{
    public TenantId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct MembershipId
{
    public MembershipId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("MembershipId must not be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct SubjectId
{
    public SubjectId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SubjectId must not be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public enum TenantStatus
{
    Active,
    Suspended
}

public enum MembershipStatus
{
    Active,
    Disabled
}

public enum MerchantRole
{
    Owner,
    Admin,
    Staff,
    Viewer
}

public sealed record BusinessProfile(string DisplayName, string TimeZoneIana);

public sealed record Tenant(TenantId Id, TenantStatus Status, BusinessProfile Profile, long Revision);

public sealed record Membership(
    MembershipId Id,
    TenantId TenantId,
    SubjectId SubjectId,
    MerchantRole Role,
    MembershipStatus Status,
    long Revision);
