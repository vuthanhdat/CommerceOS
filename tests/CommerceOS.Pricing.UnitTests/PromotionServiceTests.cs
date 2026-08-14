using CommerceOS.Catalog.Contracts;
using CommerceOS.Pricing.Application;
using CommerceOS.Pricing.Contracts;
using CommerceOS.Pricing.Domain;

namespace CommerceOS.Pricing.UnitTests;

public sealed class PromotionServiceTests
{
    private static readonly DateTimeOffset From = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OwnerCanScheduleButStaffCannotAndOverlapsAreRejected()
    {
        var store = new MemoryStore(); var service = new PromotionService(store, new Catalog(100));
        var owner = Command("owner-1", 80, From, From.AddHours(2));
        Assert.Equal(PromotionCommandOutcome.Scheduled, (await service.ScheduleAsync(owner, default)).Outcome);
        Assert.Equal(PromotionCommandOutcome.Forbidden, (await service.ScheduleAsync(owner with { SourceIdentity = "staff", Role = TrustedPricingRole.Staff }, default)).Outcome);
        Assert.Equal(PromotionCommandOutcome.Invalid, (await service.ScheduleAsync(Command("owner-2", 70, From.AddHours(1), From.AddHours(3)), default)).Outcome);
    }

    [Fact]
    public async Task EvaluationUsesOnlyBeneficialActivePromotionAndCancellationRestoresBase()
    {
        var store = new MemoryStore(); var catalog = new Catalog(100); var service = new PromotionService(store, catalog);
        var scheduled = await service.ScheduleAsync(Command("schedule", 90, From, From.AddHours(2)), default);
        var query = new EffectivePriceQueryService(store, catalog);
        var applied = await query.GetEffectivePriceAsync("tenant-a", "product-a", From.AddMinutes(1), "c", default);
        Assert.Equal(90, applied!.EffectiveUnitPriceVnd); Assert.True(applied.HasAppliedPromotion);
        catalog.Price = 80;
        var noLongerBeneficial = await query.GetEffectivePriceAsync("tenant-a", "product-a", From.AddMinutes(1), "c", default);
        Assert.Equal(80, noLongerBeneficial!.EffectiveUnitPriceVnd); Assert.False(noLongerBeneficial.HasAppliedPromotion);
        catalog.Price = 100;
        Assert.Equal(PromotionCommandOutcome.Cancelled, (await service.CancelAsync(new("tenant-a", scheduled.PromotionId!, "cancel", TrustedPricingRole.Admin, "c"), default)).Outcome);
        var cancelled = await query.GetEffectivePriceAsync("tenant-a", "product-a", From.AddMinutes(1), "c", default);
        Assert.Equal(100, cancelled!.EffectiveUnitPriceVnd); Assert.False(cancelled.HasAppliedPromotion);
    }

    [Fact]
    public async Task CrossTenantEvaluationCannotSeePromotion()
    {
        var store = new MemoryStore(); var catalog = new Catalog(100); var service = new PromotionService(store, catalog);
        await service.ScheduleAsync(Command("schedule", 80, From, From.AddHours(2)), default);
        var query = new EffectivePriceQueryService(store, catalog);
        Assert.Equal(100, (await query.GetEffectivePriceAsync("tenant-b", "product-a", From.AddMinutes(1), "c", default))!.EffectiveUnitPriceVnd);
    }

    private static ScheduleProductPromotion Command(string source, long price, DateTimeOffset from, DateTimeOffset until) => new("tenant-a", "product-a", price, from, until, source, TrustedPricingRole.Owner, "c");

    private sealed class Catalog(long price) : IPublicCatalogQuery
    {
        public long Price { get; set; } = price;
        public Task<PublicCatalogPage> ListAsync(string tenant, string? search, string? cursor, int pageSize, CancellationToken ct) => Task.FromResult(new PublicCatalogPage([], null));
        public Task<PublicCatalogProduct?> GetBySlugAsync(string tenant, string slug, CancellationToken ct) => GetSellableAsync(tenant, "product-a", ct);
        public Task<PublicCatalogProduct?> GetSellableAsync(string tenant, string product, CancellationToken ct) => Task.FromResult<PublicCatalogProduct?>(new(product, "tea", "Tea", "SKU", Price, "VND"));
    }

    private sealed class MemoryStore : IPromotionStore
    {
        private readonly object _gate = new(); private readonly Dictionary<(PricingTenantId, PromotionId), Promotion> _promotions = []; private readonly Dictionary<(PricingTenantId, string), PromotionSchedule> _schedules = []; private readonly Dictionary<(PricingTenantId, PromotionId), DateTimeOffset> _cancellations = [];
        public Task<IReadOnlyList<Promotion>> ListAsync(PricingTenantId tenant, CancellationToken ct) { lock (_gate) return Task.FromResult<IReadOnlyList<Promotion>>(_promotions.Where(x => x.Key.Item1 == tenant).Select(x => x.Value).ToArray()); }
        public Task<PromotionSchedule> GetScheduleAsync(PricingTenantId tenant, string product, CancellationToken ct) { lock (_gate) return Task.FromResult(_schedules.GetValueOrDefault((tenant, product), PromotionSchedule.Empty(tenant, product))); }
        public Task<Promotion?> GetAsync(PricingTenantId tenant, PromotionId id, CancellationToken ct) { lock (_gate) return Task.FromResult(_promotions.GetValueOrDefault((tenant, id))); }
        public Task<DateTimeOffset?> GetCancellationAsync(PricingTenantId tenant, PromotionId id, CancellationToken ct) { lock (_gate) return Task.FromResult(_cancellations.TryGetValue((tenant, id), out var cancelled) ? (DateTimeOffset?)cancelled : null); }
        public Task<PromotionCommandOutcome> ScheduleAsync(TrustedPricingMutationContext context, Promotion promotion, PromotionSchedule before, PromotionSchedule after, CancellationToken ct) { lock (_gate) { if (_promotions.TryGetValue((context.TenantId, promotion.Id), out var existing)) return Task.FromResult(existing.SourceIdentity == promotion.SourceIdentity ? PromotionCommandOutcome.AlreadyApplied : PromotionCommandOutcome.Conflict); var current = _schedules.GetValueOrDefault((context.TenantId, promotion.ProductId), PromotionSchedule.Empty(context.TenantId, promotion.ProductId)); if (current.Revision != before.Revision) return Task.FromResult(PromotionCommandOutcome.Conflict); _promotions[(context.TenantId, promotion.Id)] = promotion; _schedules[(context.TenantId, promotion.ProductId)] = after; return Task.FromResult(PromotionCommandOutcome.Scheduled); } }
        public Task<PromotionCommandOutcome> CancelAsync(TrustedPricingMutationContext context, Promotion promotion, PromotionSchedule before, PromotionSchedule after, PromotionCancellation cancellation, CancellationToken ct) { lock (_gate) { if (_cancellations.ContainsKey((context.TenantId, promotion.Id))) return Task.FromResult(PromotionCommandOutcome.AlreadyApplied); var current = _schedules.GetValueOrDefault((context.TenantId, promotion.ProductId), PromotionSchedule.Empty(context.TenantId, promotion.ProductId)); if (current.Revision != before.Revision) return Task.FromResult(PromotionCommandOutcome.Conflict); _schedules[(context.TenantId, promotion.ProductId)] = after; _cancellations[(context.TenantId, promotion.Id)] = cancellation.CancelledAt; return Task.FromResult(PromotionCommandOutcome.Cancelled); } }
    }
}
