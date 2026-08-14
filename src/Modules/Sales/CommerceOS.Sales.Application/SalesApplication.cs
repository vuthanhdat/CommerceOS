using System.Security.Cryptography;
using System.Text;
using CommerceOS.Sales.Contracts;
using CommerceOS.Sales.Domain;

namespace CommerceOS.Sales.Application;

public sealed record TrustedSalesContext(SalesTenantId TenantId, string CorrelationId);
public enum SalesStoreOutcome { Applied, Replayed, Conflict }
public sealed record SalesOrderPage(IReadOnlyList<SalesOrder> Items, string? NextCursor);
public interface ISalesOrderStore
{
    Task<SalesStoreOutcome> PlaceAsync(TrustedSalesContext context, SalesOrder order, string idempotencyKey, string requestHash, CancellationToken cancellationToken);
    Task<SalesOrder?> GetAsync(TrustedSalesContext context, SalesOrderId orderId, CancellationToken cancellationToken);
    Task<SalesStoreOutcome> SaveAsync(TrustedSalesContext context, SalesOrder before, SalesOrder after, CancellationToken cancellationToken);
    Task<SalesOrderPage> ListAsync(TrustedSalesContext context, string? cursor, int pageSize, CancellationToken cancellationToken);
}
public interface IRefundStore
{
    Task<RefundRequest?> GetRefundAsync(TrustedSalesContext context, string refundRequestId, CancellationToken ct);
    Task<IReadOnlyList<RefundRequest>> ListRefundsAsync(TrustedSalesContext context, CancellationToken ct);
    Task<SalesStoreOutcome> CreateRefundAsync(TrustedSalesContext context, RefundRequest request, CancellationToken ct);
    Task<SalesStoreOutcome> DecideRefundAsync(TrustedSalesContext context, RefundRequest before, RefundRequest after, CancellationToken ct);
}
public sealed class SalesOrderService(ISalesOrderStore store, TimeProvider? clock = null) : ISalesOrderPlacement, ISalesOrderCancellation, ISalesOrderWorkflowProgress
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<OrderPlacementResult> PlaceAsync(PlaceAcceptedOrder command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.TrustedTenantId) || string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128 || string.IsNullOrWhiteSpace(command.CorrelationId)) return new(OrderPlacementOutcome.Invalid, null, null);
        try
        {
            var context = new TrustedSalesContext(new(command.TrustedTenantId), command.CorrelationId);
            var hash = Hash(command);
            var token = StableToken($"{command.TrustedTenantId}\n{command.IdempotencyKey}");
            var order = SalesOrder.Place(new($"ord-{token}"), context.TenantId, command.Lines.Select(x => new SalesOrderLine(x.ProductId, x.Sku, x.Name, x.Quantity, x.UnitPriceVnd) { BaseUnitPriceVnd = x.AcceptedBaseUnitPriceVnd, PromotionId = x.PromotionId, AppliedPromotionalUnitPriceVnd = x.AppliedPromotionalUnitPriceVnd, PriceEvaluatedAt = x.PriceEvaluatedAt }).ToArray(), command.TotalVnd, new(command.Guest.Name.Trim(), command.Guest.Email.Trim(), command.Guest.Phone?.Trim(), command.Guest.Address?.Trim()), new($"process-{token}", $"order-{token}", true));
            order = order with { PlacedAt = _clock.GetUtcNow() };
            return await store.PlaceAsync(context, order, command.IdempotencyKey, hash, cancellationToken) switch { SalesStoreOutcome.Applied => new(OrderPlacementOutcome.Accepted, order.Id.Value, order.Status.ToString()), SalesStoreOutcome.Replayed => new(OrderPlacementOutcome.Replayed, order.Id.Value, order.Status.ToString()), _ => new(OrderPlacementOutcome.Conflict, null, null) };
        }
        catch (SalesRuleException) { return new(OrderPlacementOutcome.Invalid, null, null); }
    }
    public async Task<SalesStoreOutcome> ApplyTransitionAsync(TrustedSalesContext context, SalesOrderId orderId, SalesOrderStatus target, string sourceIdentity, long expectedRevision, CancellationToken ct)
    { var before = await store.GetAsync(context, orderId, ct); if (before is null) return SalesStoreOutcome.Conflict; try { return await store.SaveAsync(context, before, before.Apply(target, sourceIdentity, expectedRevision), ct); } catch (SalesRuleException) { return SalesStoreOutcome.Conflict; } }
    public Task<SalesOrderPage> ListAsync(TrustedSalesContext context, string? cursor, int pageSize, CancellationToken ct) => store.ListAsync(context, cursor, Math.Clamp(pageSize, 1, 50), ct);
    public Task<SalesProgressOutcome> CancelAsync(CancelSalesOrder command, CancellationToken cancellationToken) => TransitionAsync(command.TrustedTenantId, command.OrderId, SalesOrderStatus.Cancelled, command.SourceIdentity, command.ExpectedRevision, command.CorrelationId, cancellationToken);
    public Task<SalesProgressOutcome> ConfirmAsync(string trustedTenantId, string orderId, string sourceIdentity, long expectedRevision, string correlationId, CancellationToken cancellationToken) => TransitionAsync(trustedTenantId, orderId, SalesOrderStatus.Confirmed, sourceIdentity, expectedRevision, correlationId, cancellationToken);
    public Task<SalesProgressOutcome> AllocateAsync(string trustedTenantId, string orderId, string sourceIdentity, long expectedRevision, string correlationId, CancellationToken cancellationToken) => TransitionAsync(trustedTenantId, orderId, SalesOrderStatus.Allocated, sourceIdentity, expectedRevision, correlationId, cancellationToken);
    private async Task<SalesProgressOutcome> TransitionAsync(string tenant, string order, SalesOrderStatus target, string source, long revision, string correlation, CancellationToken ct)
    { var result = await ApplyTransitionAsync(new(new(tenant), correlation), new(order), target, source, revision, ct); return result is SalesStoreOutcome.Applied ? SalesProgressOutcome.Applied : SalesProgressOutcome.Conflict; }
    private static string Hash(PlaceAcceptedOrder command) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{command.TrustedTenantId}|{string.Join(';', command.Lines.Select(x => $"{x.ProductId}:{x.Quantity}:{x.UnitPriceVnd}:{x.AcceptedBaseUnitPriceVnd}:{x.PromotionId}:{x.AppliedPromotionalUnitPriceVnd}"))}|{command.TotalVnd}|{command.Guest.Name}|{command.Guest.Email}|{command.Guest.Phone}|{command.Guest.Address}")));
    private static string StableToken(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..24];
}

public sealed class RefundReviewService(ISalesOrderStore orders, IRefundStore refunds, TimeProvider? clock = null) : ISalesRefundReview
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<RefundCommandResult> RequestAsync(RequestSalesRefund command, CancellationToken ct)
    {
        if (!CanRequest(command.Role) || string.IsNullOrWhiteSpace(command.TrustedTenantId) || string.IsNullOrWhiteSpace(command.SourceIdentity) || string.IsNullOrWhiteSpace(command.ActorId) || string.IsNullOrWhiteSpace(command.CorrelationId)) return new(CanRequest(command.Role) ? RefundCommandOutcome.Invalid : RefundCommandOutcome.Forbidden, null, null);
        var context = new TrustedSalesContext(new(command.TrustedTenantId), command.CorrelationId); var order = await orders.GetAsync(context, new(command.OrderId), ct);
        if (order is null || order.Status is not (SalesOrderStatus.Fulfilled or SalesOrderStatus.Completed) || command.AmountVnd > order.TotalVnd) return new(RefundCommandOutcome.Invalid, null, null);
        try
        {
            var lines = command.Lines.Select(x => RefundLine.Create(x.ProductId, x.Quantity, x.OriginalIssueReference)).ToArray();
            if (lines.GroupBy(x => x.ProductId).Any(x => x.Sum(y => y.Quantity) > order.Lines.Where(line => line.ProductId == x.Key).Sum(line => line.Quantity))) return new(RefundCommandOutcome.Invalid, null, null);
            var id = $"refund:{StableToken($"{command.TrustedTenantId}|{command.SourceIdentity}")}"; var request = RefundRequest.Create(id, context.TenantId, order.Id, command.PaymentId, command.AmountVnd, command.Currency, lines, command.SourceIdentity, command.ActorId, _clock.GetUtcNow());
            var outcome = await refunds.CreateRefundAsync(context, request, ct); return new(outcome is SalesStoreOutcome.Applied ? RefundCommandOutcome.Requested : outcome is SalesStoreOutcome.Replayed ? RefundCommandOutcome.AlreadyApplied : RefundCommandOutcome.Conflict, id, request.Status.ToString());
        }
        catch (SalesRuleException) { return new(RefundCommandOutcome.Invalid, null, null); }
    }
    public async Task<RefundCommandResult> DecideAsync(DecideSalesRefund command, CancellationToken ct)
    {
        if (!CanDecide(command.Role)) return new(RefundCommandOutcome.Forbidden, null, null);
        var context = new TrustedSalesContext(new(command.TrustedTenantId), command.CorrelationId); var before = await refunds.GetRefundAsync(context, command.RefundRequestId, ct); if (before is null) return new(RefundCommandOutcome.NotFound, null, null);
        try
        {
            var target = command.Approve ? RefundRequestStatus.Approved : RefundRequestStatus.Rejected; var after = before.Decide(target, command.SourceIdentity, command.ActorId, _clock.GetUtcNow(), command.ExpectedRevision);
            if (after == before) return new(RefundCommandOutcome.AlreadyApplied, before.Id, before.Status.ToString());
            var saved = await refunds.DecideRefundAsync(context, before, after, ct); return new(saved is SalesStoreOutcome.Applied ? command.Approve ? RefundCommandOutcome.Approved : RefundCommandOutcome.Rejected : RefundCommandOutcome.Conflict, before.Id, after.Status.ToString());
        }
        catch (SalesRuleException) { return new(RefundCommandOutcome.Conflict, before.Id, before.Status.ToString()); }
    }
    private static bool CanRequest(TrustedRefundRole role) => role is TrustedRefundRole.Owner or TrustedRefundRole.Admin or TrustedRefundRole.Staff;
    private static bool CanDecide(TrustedRefundRole role) => role is TrustedRefundRole.Owner or TrustedRefundRole.Admin;
    private static string StableToken(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..24];
}
