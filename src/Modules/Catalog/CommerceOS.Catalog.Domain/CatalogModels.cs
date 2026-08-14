namespace CommerceOS.Catalog.Domain;

public readonly record struct CatalogTenantId
{
    public CatalogTenantId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Tenant ID is required.", nameof(value));
        Value = value;
    }
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct ProductId
{
    public ProductId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Product ID is required.", nameof(value));
        Value = value;
    }
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct CategoryId(string Value);
public readonly record struct BrandId(string Value);

public enum CatalogReferenceStatus { Active, Retired }
public sealed record Category(CategoryId Id, CatalogTenantId TenantId, string Name, CatalogReferenceStatus Status, long Revision);
public sealed record Brand(BrandId Id, CatalogTenantId TenantId, string Name, CatalogReferenceStatus Status, long Revision);
public sealed record ProductSpecification(string Name, string Value, string? Unit, int DisplayOrder)
{
    public string NormalizedName => Product.Normalize(Name);
    public static IReadOnlyList<ProductSpecification> Validate(IEnumerable<ProductSpecification> specifications)
    {
        var materialized = specifications.OrderBy(x => x.DisplayOrder).ToArray();
        if (materialized.Any(x => string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.Value))
            || materialized.Select(x => x.NormalizedName).Distinct(StringComparer.Ordinal).Count() != materialized.Length)
            throw new ProductRuleException(ProductRule.InvalidSpecification);
        return materialized;
    }
}

public sealed record ProductMediaAssociation(string AssetId, string AltText, int DisplayOrder)
{
    public static IReadOnlyList<ProductMediaAssociation> Validate(IEnumerable<ProductMediaAssociation> media) => media
        .OrderBy(x => x.DisplayOrder)
        .Where(x => !string.IsNullOrWhiteSpace(x.AssetId))
        .ToArray();
}

public sealed record Money
{
    public Money(long amount, string currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (!string.Equals(currency, "VND", StringComparison.Ordinal)) throw new ArgumentException("Catalog supports VND only.", nameof(currency));
        Amount = amount;
        Currency = "VND";
    }
    public long Amount { get; }
    public string Currency { get; }
}

public enum ProductStatus { Draft, Published, Unpublished, Archived }

public sealed record Product(ProductId Id, CatalogTenantId TenantId, string Name, string? Sku, string? Slug, Money? BasePrice, ProductStatus Status, bool HasBeenPublished, long Revision)
{
    public CategoryId? CategoryId { get; init; }
    public BrandId? BrandId { get; init; }
    public IReadOnlyList<ProductSpecification> Specifications { get; init; } = [];
    public IReadOnlyList<ProductMediaAssociation> Media { get; init; } = [];
    public static Product Draft(ProductId id, CatalogTenantId tenantId, string name, string? sku, Money? price) =>
        new(id, tenantId, RequireName(name), NormalizeOptional(sku), null, price, ProductStatus.Draft, false, 1);

    public Product Change(string name, string? sku, string? slug, Money? price, long expectedRevision)
    {
        EnsureRevision(expectedRevision);
        if (Status is ProductStatus.Archived) throw new ProductRuleException(ProductRule.Archived);
        var changedSku = NormalizeOptional(sku);
        if (HasBeenPublished && !string.Equals(Sku, changedSku, StringComparison.Ordinal)) throw new ProductRuleException(ProductRule.SkuImmutable);
        return this with { Name = RequireName(name), Sku = changedSku, Slug = NormalizeOptional(slug), BasePrice = price, Revision = Revision + 1 };
    }

    public Product Publish(long expectedRevision)
    {
        EnsureRevision(expectedRevision);
        if (Status is ProductStatus.Archived) throw new ProductRuleException(ProductRule.Archived);
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Sku) || BasePrice is null) throw new ProductRuleException(ProductRule.IncompleteForPublication);
        return this with { Status = ProductStatus.Published, HasBeenPublished = true, Revision = Revision + 1 };
    }

    public Product Unpublish(long expectedRevision)
    {
        EnsureRevision(expectedRevision);
        if (Status is not ProductStatus.Published) throw new ProductRuleException(ProductRule.InvalidTransition);
        return this with { Status = ProductStatus.Unpublished, Revision = Revision + 1 };
    }

    public Product Archive(long expectedRevision)
    {
        EnsureRevision(expectedRevision);
        if (Status is ProductStatus.Archived) throw new ProductRuleException(ProductRule.Archived);
        return this with { Status = ProductStatus.Archived, Revision = Revision + 1 };
    }

    public Product AssignReferences(CategoryId? categoryId, BrandId? brandId, long expectedRevision)
    {
        EnsureRevision(expectedRevision);
        if (Status is ProductStatus.Archived) throw new ProductRuleException(ProductRule.Archived);
        return this with { CategoryId = categoryId, BrandId = brandId, Revision = Revision + 1 };
    }

    public Product SetSpecifications(IEnumerable<ProductSpecification> specifications, long expectedRevision)
    {
        EnsureRevision(expectedRevision);
        if (Status is ProductStatus.Archived) throw new ProductRuleException(ProductRule.Archived);
        return this with { Specifications = ProductSpecification.Validate(specifications), Revision = Revision + 1 };
    }

    public Product SetMedia(IEnumerable<ProductMediaAssociation> media, long expectedRevision)
    {
        EnsureRevision(expectedRevision);
        if (Status is ProductStatus.Archived) throw new ProductRuleException(ProductRule.Archived);
        return this with { Media = ProductMediaAssociation.Validate(media), Revision = Revision + 1 };
    }

    public static string Normalize(string value) => string.Join('-', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string RequireName(string value) => string.IsNullOrWhiteSpace(value) ? throw new ProductRuleException(ProductRule.IncompleteForPublication) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private void EnsureRevision(long expected) { if (Revision != expected) throw new ProductRuleException(ProductRule.StaleRevision); }
}

public enum ProductRule { IncompleteForPublication, SkuImmutable, Archived, InvalidTransition, StaleRevision, InvalidSpecification }
public sealed class ProductRuleException(ProductRule rule) : InvalidOperationException(rule.ToString()) { public ProductRule Rule { get; } = rule; }
