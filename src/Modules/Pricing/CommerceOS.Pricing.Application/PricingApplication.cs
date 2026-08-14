using System.Security.Cryptography;
using System.Text;
using CommerceOS.Catalog.Contracts;
using CommerceOS.Pricing.Contracts;
using CommerceOS.Pricing.Domain;

namespace CommerceOS.Pricing.Application;

public enum TrustedPricingRole { Owner, Admin, Staff, Viewer }
public sealed record TrustedPricingMutationContext(PricingTenantId TenantId, TrustedPricingRole Role, string CorrelationId);
public sealed record ScheduleProductPromotion(string TrustedTenantId, string ProductId, long PromotionalUnitPriceVnd, DateTimeOffset EffectiveFrom, DateTimeOffset EffectiveUntil, string SourceIdentity, TrustedPricingRole Role, string CorrelationId);
public sealed record CancelProductPromotion(string TrustedTenantId, string PromotionId, string SourceIdentity, TrustedPricingRole Role, string CorrelationId);
public enum PromotionCommandOutcome { Scheduled, Cancelled, AlreadyApplied, Forbidden, NotFound, IneligibleProduct, Invalid, Conflict }
public sealed record PromotionCommandResult(PromotionCommandOutcome Outcome, string? PromotionId);
public sealed record PromotionCancellation(PromotionId PromotionId, PricingTenantId TenantId, string SourceIdentity, DateTimeOffset CancelledAt);

public interface IPromotionStore
{
    Task<IReadOnlyList<Promotion>> ListAsync(PricingTenantId tenantId, CancellationToken cancellationToken);
    Task<PromotionSchedule> GetScheduleAsync(PricingTenantId tenantId, string productId, CancellationToken cancellationToken);
    Task<Promotion?> GetAsync(PricingTenantId tenantId, PromotionId promotionId, CancellationToken cancellationToken);
    Task<DateTimeOffset?> GetCancellationAsync(PricingTenantId tenantId, PromotionId promotionId, CancellationToken cancellationToken);
    Task<PromotionCommandOutcome> ScheduleAsync(TrustedPricingMutationContext context, Promotion promotion, PromotionSchedule before, PromotionSchedule after, CancellationToken cancellationToken);
    Task<PromotionCommandOutcome> CancelAsync(TrustedPricingMutationContext context, Promotion promotion, PromotionSchedule before, PromotionSchedule after, PromotionCancellation cancellation, CancellationToken cancellationToken);
}

public sealed record PromotionMerchantView(Promotion Promotion, DateTimeOffset? CancelledAt, string TemporalStatus, long? CurrentBasePriceVnd, bool CurrentlyBeneficial);
public sealed class PromotionMerchantQuery(IPromotionStore store, IPublicCatalogQuery catalog, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<IReadOnlyList<PromotionMerchantView>> ListAsync(string trustedTenantId, CancellationToken ct)
    {
        var tenant = new PricingTenantId(trustedTenantId); var now = _clock.GetUtcNow();
        var promotions = await store.ListAsync(tenant, ct);
        var views = new List<PromotionMerchantView>(promotions.Count);
        foreach (var promotion in promotions)
        {
            var cancelledAt = await store.GetCancellationAsync(tenant, promotion.Id, ct);
            var product = await catalog.GetSellableAsync(tenant.Value, promotion.ProductId, ct);
            var state = cancelledAt is not null ? "Cancelled" : promotion.EffectiveUntil <= now ? "Expired" : promotion.EffectiveFrom > now ? "Upcoming" : "Active";
            views.Add(new(promotion, cancelledAt, state, product?.UnitPriceVnd, product is not null && promotion.IsActiveAt(now, cancelledAt) && promotion.PromotionalUnitPriceVnd < product.UnitPriceVnd));
        }
        return views.OrderByDescending(view => view.Promotion.EffectiveFrom).ToArray();
    }
    public async Task<PromotionMerchantView?> GetAsync(string trustedTenantId, string promotionId, CancellationToken ct)
    {
        var tenant = new PricingTenantId(trustedTenantId); var promotion = await store.GetAsync(tenant, new(promotionId), ct); if (promotion is null) return null;
        var now = _clock.GetUtcNow(); var cancelledAt = await store.GetCancellationAsync(tenant, promotion.Id, ct); var product = await catalog.GetSellableAsync(tenant.Value, promotion.ProductId, ct);
        var state = cancelledAt is not null ? "Cancelled" : promotion.EffectiveUntil <= now ? "Expired" : promotion.EffectiveFrom > now ? "Upcoming" : "Active";
        return new(promotion, cancelledAt, state, product?.UnitPriceVnd, product is not null && promotion.IsActiveAt(now, cancelledAt) && promotion.PromotionalUnitPriceVnd < product.UnitPriceVnd);
    }
}

public sealed class PromotionService(IPromotionStore store, IPublicCatalogQuery catalog, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<PromotionCommandResult> ScheduleAsync(ScheduleProductPromotion command, CancellationToken cancellationToken)
    {
        if (!CanMutate(command.Role)) return new(PromotionCommandOutcome.Forbidden, null);
        if (string.IsNullOrWhiteSpace(command.TrustedTenantId) || string.IsNullOrWhiteSpace(command.ProductId) || string.IsNullOrWhiteSpace(command.SourceIdentity) || string.IsNullOrWhiteSpace(command.CorrelationId)) return new(PromotionCommandOutcome.Invalid, null);
        var tenant = new PricingTenantId(command.TrustedTenantId);
        var product = await catalog.GetSellableAsync(tenant.Value, command.ProductId, cancellationToken);
        if (product is null || product.Currency != "VND" || command.PromotionalUnitPriceVnd >= product.UnitPriceVnd) return new(PromotionCommandOutcome.IneligibleProduct, null);
        try
        {
            var id = new PromotionId($"promotion-{Token($"{tenant.Value}|{command.SourceIdentity}")}");
            var promotion = Promotion.Schedule(id, tenant, command.ProductId, command.PromotionalUnitPriceVnd, command.EffectiveFrom, command.EffectiveUntil, command.SourceIdentity, _clock.GetUtcNow());
            var before = await store.GetScheduleAsync(tenant, command.ProductId, cancellationToken);
            var outcome = await store.ScheduleAsync(new(tenant, command.Role, command.CorrelationId), promotion, before, before.Add(promotion), cancellationToken);
            return new(outcome, outcome is PromotionCommandOutcome.Scheduled or PromotionCommandOutcome.AlreadyApplied ? id.Value : null);
        }
        catch (PromotionRuleException) { return new(PromotionCommandOutcome.Invalid, null); }
    }

    public async Task<PromotionCommandResult> CancelAsync(CancelProductPromotion command, CancellationToken cancellationToken)
    {
        if (!CanMutate(command.Role)) return new(PromotionCommandOutcome.Forbidden, null);
        if (string.IsNullOrWhiteSpace(command.TrustedTenantId) || string.IsNullOrWhiteSpace(command.PromotionId) || string.IsNullOrWhiteSpace(command.SourceIdentity) || string.IsNullOrWhiteSpace(command.CorrelationId)) return new(PromotionCommandOutcome.Invalid, null);
        var tenant = new PricingTenantId(command.TrustedTenantId); var id = new PromotionId(command.PromotionId);
        var promotion = await store.GetAsync(tenant, id, cancellationToken);
        if (promotion is null) return new(PromotionCommandOutcome.NotFound, null);
        if (await store.GetCancellationAsync(tenant, id, cancellationToken) is not null) return new(PromotionCommandOutcome.AlreadyApplied, id.Value);
        var before = await store.GetScheduleAsync(tenant, promotion.ProductId, cancellationToken);
        var outcome = await store.CancelAsync(new(tenant, command.Role, command.CorrelationId), promotion, before, before.Remove(id), new(id, tenant, command.SourceIdentity, _clock.GetUtcNow()), cancellationToken);
        return new(outcome, outcome is PromotionCommandOutcome.Cancelled or PromotionCommandOutcome.AlreadyApplied ? id.Value : null);
    }

    private static bool CanMutate(TrustedPricingRole role) => role is TrustedPricingRole.Owner or TrustedPricingRole.Admin;
    private static string Token(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];
}

public sealed class EffectivePriceQueryService(IPromotionStore store, IPublicCatalogQuery catalog) : IEffectivePriceQuery
{
    public async Task<EffectivePriceDecision?> GetEffectivePriceAsync(string trustedTenantId, string productId, DateTimeOffset evaluatedAt, string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trustedTenantId) || string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(correlationId)) return null;
        var product = await catalog.GetSellableAsync(trustedTenantId, productId, cancellationToken);
        if (product is null || product.Currency != "VND") return null;
        var tenant = new PricingTenantId(trustedTenantId);
        var schedule = await store.GetScheduleAsync(tenant, productId, cancellationToken);
        foreach (var entry in schedule.Entries.Where(x => x.EffectiveFrom <= evaluatedAt && evaluatedAt < x.EffectiveUntil))
        {
            var promotion = await store.GetAsync(tenant, entry.PromotionId, cancellationToken);
            if (promotion is not null && promotion.IsActiveAt(evaluatedAt, await store.GetCancellationAsync(tenant, entry.PromotionId, cancellationToken)) && promotion.PromotionalUnitPriceVnd < product.UnitPriceVnd)
                return new(product.UnitPriceVnd, promotion.PromotionalUnitPriceVnd, promotion.Id.Value, promotion.PromotionalUnitPriceVnd, evaluatedAt, promotion.EffectiveUntil);
        }
        return new(product.UnitPriceVnd, product.UnitPriceVnd, null, null, evaluatedAt);
    }
}
