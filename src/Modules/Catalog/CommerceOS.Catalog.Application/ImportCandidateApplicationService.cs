using CommerceOS.Catalog.Contracts;
using CommerceOS.Catalog.Domain;

namespace CommerceOS.Catalog.Application;

public interface ICatalogImportStore
{
    Task<ImportCandidateApplicationResult?> GetImportApplicationAsync(TrustedCatalogMutationContext context, string candidateId, CancellationToken cancellationToken);
    Task<ImportCandidateApplicationOutcome> ApplyImportAsync(TrustedCatalogMutationContext context, Product before, Product after, string candidateId, string sourceId, string sourceProductId, CancellationToken cancellationToken);
}
public sealed class ImportCandidateApplicationService(ICatalogStore products, ICatalogImportStore imports) : IApprovedImportCandidateApplier
{
    public async Task<ImportCandidateApplicationResult> ApplyAsync(ApplyApprovedImportCandidate command, CancellationToken cancellationToken)
    {
        var context = new TrustedCatalogMutationContext(new CatalogTenantId(command.TrustedTenantId), command.CorrelationId);
        var replay = await imports.GetImportApplicationAsync(context, command.CandidateId, cancellationToken);
        if (replay is not null) return replay;
        var product = await products.GetAsync(context, new ProductId(command.ProductId), cancellationToken);
        if (product is null) return new(ImportCandidateApplicationOutcome.ProductNotFound, null);
        try
        {
            var changed = product.Change(command.Name, command.Sku, product.Slug, command.VndPrice is null ? product.BasePrice : new Money(command.VndPrice.Value, "VND"), command.ExpectedProductRevision);
            var outcome = await imports.ApplyImportAsync(context, product, changed, command.CandidateId, command.SourceId, command.SourceProductId, cancellationToken);
            return new(outcome, outcome is ImportCandidateApplicationOutcome.Applied ? changed.Revision : null);
        }
        catch (ProductRuleException) { return new(ImportCandidateApplicationOutcome.InvalidProduct, null); }
    }
}
