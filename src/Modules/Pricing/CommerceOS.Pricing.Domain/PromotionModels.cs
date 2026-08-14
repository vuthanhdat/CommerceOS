namespace CommerceOS.Pricing.Domain;

public readonly record struct PricingTenantId
{
    public PricingTenantId(string value) : this() => Value = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Tenant ID is required.", nameof(value)) : value;
    public string Value { get; }
}

public readonly record struct PromotionId
{
    public PromotionId(string value) : this() => Value = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Promotion ID is required.", nameof(value)) : value;
    public string Value { get; }
}

public sealed record Promotion(PromotionId Id, PricingTenantId TenantId, string ProductId, long PromotionalUnitPriceVnd, DateTimeOffset EffectiveFrom, DateTimeOffset EffectiveUntil, string SourceIdentity, DateTimeOffset AcceptedAt)
{
    public static Promotion Schedule(PromotionId id, PricingTenantId tenantId, string productId, long price, DateTimeOffset from, DateTimeOffset until, string sourceIdentity, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(sourceIdentity) || price < 0 || from < now || from >= until)
            throw new PromotionRuleException("PROMOTION_INVALID");
        return new(id, tenantId, productId, price, from, until, sourceIdentity, now);
    }

    public bool IsActiveAt(DateTimeOffset instant, DateTimeOffset? cancelledAt) => cancelledAt is null && EffectiveFrom <= instant && instant < EffectiveUntil;
}

public sealed record PromotionScheduleEntry(PromotionId PromotionId, DateTimeOffset EffectiveFrom, DateTimeOffset EffectiveUntil);
public sealed record PromotionSchedule(PricingTenantId TenantId, string ProductId, IReadOnlyList<PromotionScheduleEntry> Entries, long Revision)
{
    public static PromotionSchedule Empty(PricingTenantId tenantId, string productId) => new(tenantId, productId, [], 0);

    public PromotionSchedule Add(Promotion promotion, int maximumEntries = 20)
    {
        if (TenantId != promotion.TenantId || !string.Equals(ProductId, promotion.ProductId, StringComparison.Ordinal) || Entries.Count >= maximumEntries)
            throw new PromotionRuleException("PROMOTION_SCHEDULE_INVALID");
        if (Entries.Any(x => promotion.EffectiveFrom < x.EffectiveUntil && x.EffectiveFrom < promotion.EffectiveUntil))
            throw new PromotionRuleException("PROMOTION_OVERLAP");
        return this with { Entries = Entries.Append(new PromotionScheduleEntry(promotion.Id, promotion.EffectiveFrom, promotion.EffectiveUntil)).OrderBy(x => x.EffectiveFrom).ToArray(), Revision = Revision + 1 };
    }

    public PromotionSchedule Remove(PromotionId promotionId) => this with { Entries = Entries.Where(x => x.PromotionId != promotionId).ToArray(), Revision = Revision + 1 };
}

public sealed class PromotionRuleException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}
