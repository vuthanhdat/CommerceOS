namespace CommerceOS.Pricing.Contracts;

/// <summary>Authoritative result only; it deliberately exposes no generic discount expression.</summary>
public sealed record EffectivePriceDecision(long BaseUnitPriceVnd, long EffectiveUnitPriceVnd, string? PromotionId, long? AppliedPromotionalUnitPriceVnd, DateTimeOffset EvaluatedAt, DateTimeOffset? PromotionEffectiveUntil = null)
{
    public bool HasAppliedPromotion => PromotionId is not null;
}

public interface IEffectivePriceQuery
{
    Task<EffectivePriceDecision?> GetEffectivePriceAsync(string trustedTenantId, string productId, DateTimeOffset evaluatedAt, string correlationId, CancellationToken cancellationToken);
}
