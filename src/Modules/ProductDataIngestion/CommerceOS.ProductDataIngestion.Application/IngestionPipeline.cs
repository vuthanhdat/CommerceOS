using System.Security.Cryptography;
using System.Text;
using CommerceOS.ProductDataIngestion.Domain;

namespace CommerceOS.ProductDataIngestion.Application;

public enum PdiFailureKind { None, Transient, RateLimited, NotFound, PolicyBlocked, ParserRejected, UnexpectedContent }
public enum PdiWorkerDisposition { Completed, Retry, DeadLettered, Duplicate }

/// <summary>Immutable evidence produced by an approved source adapter. Raw payload is retained separately and never sent to Catalog.</summary>
public sealed record SourceSnapshot(string Id, PdiTenantId TenantId, DataSourceId SourceId, string SourceProductId, Uri SourceUrl, string RawObjectKey, string ContentHash, string AdapterVersion, DateTimeOffset CapturedAt);
public sealed record NormalizedSourceSnapshot(string Id, string SourceSnapshotId, PdiTenantId TenantId, DataSourceId SourceId, string SourceProductId, string Name, string? SourceSku, long? VndPrice, string NormalizedHash, string SchemaVersion, DateTimeOffset CapturedAt);
public sealed record AdapterFetchResult(PdiFailureKind FailureKind, string? RawPayload, string? SourceProductId, string? Name, string? SourceSku, long? VndPrice, string AdapterVersion, string? Detail = null)
{
    public bool Succeeded => FailureKind is PdiFailureKind.None && RawPayload is not null && SourceProductId is not null && Name is not null;
}
public sealed record PdiWorkerResult(PdiWorkerDisposition Disposition, PdiFailureKind FailureKind, string WorkIdentity, string? Detail = null);

public interface IApprovedSourceAdapter
{
    Task<AdapterFetchResult> FetchAsync(ManualAcquisitionRequest work, CancellationToken cancellationToken);
}
public interface IRawPdiSnapshotStore
{
    /// <summary>Writes the raw payload at an immutable, retention-managed object key and returns that key.</summary>
    Task<string> StoreIfAbsentAsync(ManualAcquisitionRequest work, string contentHash, string rawPayload, CancellationToken cancellationToken);
}
public interface ISourceSnapshotStore
{
    Task<bool> SaveSourceSnapshotIfAbsentAsync(SourceSnapshot snapshot, CancellationToken cancellationToken);
    Task<bool> SaveNormalizedSnapshotIfAbsentAsync(NormalizedSourceSnapshot snapshot, CancellationToken cancellationToken);
}
public interface IPdiDeadLetterSink
{
    Task SendAsync(ManualAcquisitionRequest work, PdiFailureKind failureKind, int attempt, string? detail, CancellationToken cancellationToken);
}

/// <summary>
/// Bounded worker use case. Transport redelivery is safe because the evidence identifiers are derived from the logical work identity.
/// The host enforces the source's configured concurrency/rate limit before invoking this use case.
/// </summary>
public sealed class PdiCrawlWorker(IAuthorizedSourceAdapterResolver adapters, IRawPdiSnapshotStore raw, ISourceSnapshotStore snapshots, IPdiDeadLetterSink deadLetters, int maximumAttempts = 3, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<PdiWorkerResult> ProcessAsync(ManualAcquisitionRequest work, int attempt, CancellationToken cancellationToken)
    {
        var result = await adapters.GetAdapterAsync(work.SourceId, cancellationToken);
        if (result is null) return await TerminalAsync(work, attempt, PdiFailureKind.PolicyBlocked, "No approved adapter is enabled.", cancellationToken);
        var fetched = await result.FetchAsync(work, cancellationToken);
        if (!fetched.Succeeded)
        {
            if ((fetched.FailureKind is PdiFailureKind.Transient or PdiFailureKind.RateLimited) && attempt < maximumAttempts)
                return new(PdiWorkerDisposition.Retry, fetched.FailureKind, work.WorkIdentity, fetched.Detail);
            return await TerminalAsync(work, attempt, fetched.FailureKind, fetched.Detail, cancellationToken);
        }

        var contentHash = Hash(fetched.RawPayload!);
        var rawKey = await raw.StoreIfAbsentAsync(work, contentHash, fetched.RawPayload!, cancellationToken);
        var capturedAt = _clock.GetUtcNow();
        var sourceSnapshot = new SourceSnapshot($"source-{Hash($"{work.WorkIdentity}|{contentHash}")}", work.TenantId, work.SourceId, fetched.SourceProductId!, work.Url, rawKey, contentHash, fetched.AdapterVersion, capturedAt);
        var savedSource = await snapshots.SaveSourceSnapshotIfAbsentAsync(sourceSnapshot, cancellationToken);
        var normalizedHash = Hash($"{fetched.SourceProductId}|{fetched.Name}|{fetched.SourceSku}|{fetched.VndPrice}");
        var normalized = new NormalizedSourceSnapshot($"normalized-{Hash($"{sourceSnapshot.Id}|{normalizedHash}")}", sourceSnapshot.Id, work.TenantId, work.SourceId, fetched.SourceProductId!, fetched.Name!, fetched.SourceSku, fetched.VndPrice, normalizedHash, "pdi-normalized-v1", capturedAt);
        var savedNormalized = await snapshots.SaveNormalizedSnapshotIfAbsentAsync(normalized, cancellationToken);
        return new(savedSource || savedNormalized ? PdiWorkerDisposition.Completed : PdiWorkerDisposition.Duplicate, PdiFailureKind.None, work.WorkIdentity);
    }
    private async Task<PdiWorkerResult> TerminalAsync(ManualAcquisitionRequest work, int attempt, PdiFailureKind kind, string? detail, CancellationToken ct)
    {
        await deadLetters.SendAsync(work, kind, attempt, detail, ct);
        return new(PdiWorkerDisposition.DeadLettered, kind, work.WorkIdentity, detail);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
public interface IAuthorizedSourceAdapterResolver { Task<IApprovedSourceAdapter?> GetAdapterAsync(DataSourceId sourceId, CancellationToken cancellationToken); }
