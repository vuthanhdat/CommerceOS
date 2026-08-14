namespace CommerceOS.Catalog.Contracts;

/// <summary>Producer-owned PDI-to-Catalog command. The caller supplies a Tenant only from its trusted process context.</summary>
public sealed record ApplyApprovedImportCandidate(string TrustedTenantId, string CandidateId, string SourceId, string SourceProductId, string ProductId, long ExpectedProductRevision, string Name, string? Sku, long? VndPrice, string CorrelationId, string CausationId);
public enum ImportCandidateApplicationOutcome { Applied, AlreadyApplied, Conflict, ProductNotFound, InvalidProduct }
public sealed record ImportCandidateApplicationResult(ImportCandidateApplicationOutcome Outcome, long? ProductRevision);
public interface IApprovedImportCandidateApplier { Task<ImportCandidateApplicationResult> ApplyAsync(ApplyApprovedImportCandidate command, CancellationToken cancellationToken); }
public sealed record PurchasableProduct(string ProductId, string TenantId, bool IsPurchasable, string DisplayName, string? Sku);
public interface ICatalogProductEligibilityQuery { Task<PurchasableProduct?> GetPurchasableProductAsync(string trustedTenantId, string productId, CancellationToken cancellationToken); }
