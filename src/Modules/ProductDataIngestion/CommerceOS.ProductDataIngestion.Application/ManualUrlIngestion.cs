using System.Security.Cryptography;
using System.Text;
using CommerceOS.ProductDataIngestion.Domain;

namespace CommerceOS.ProductDataIngestion.Application;

public sealed record ManualAcquisitionRequest(string Id, PdiTenantId TenantId, DataSourceId SourceId, Uri Url, string WorkIdentity, string CorrelationId);
public interface IManualAcquisitionWorkStore { Task<PdiOutcome> EnqueueIfAbsentAsync(ManualAcquisitionRequest request, CancellationToken cancellationToken); }
public sealed class ManualUrlIngestionService(IPdiGovernanceStore governance, IManualAcquisitionWorkStore work)
{
    public async Task<PdiOutcome> RequestAsync(TrustedPdiTenantContext context, DataSourceId sourceId, string rawUrl, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || !string.IsNullOrWhiteSpace(url.Query) || !IsAllowedOpenFoodFactsProductUrl(url)) return PdiOutcome.NotEligible;
        var source = await governance.GetSourceAsync(sourceId, cancellationToken);
        if (source is null || !source.PlatformEligible) return PdiOutcome.NotEligible;
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{context.TenantId.Value}|{sourceId.Value}|{idempotencyKey}"))).ToLowerInvariant()[..24];
        return await work.EnqueueIfAbsentAsync(new($"request-{token}", context.TenantId, sourceId, url, $"manual-url:{token}", context.CorrelationId), cancellationToken);
    }
    private static bool IsAllowedOpenFoodFactsProductUrl(Uri url) => url.Host.Equals("world.openfoodfacts.org", StringComparison.OrdinalIgnoreCase) && url.AbsolutePath.StartsWith("/api/v3.6/product/", StringComparison.Ordinal) && url.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
}
