using Dutchie.Models.Products;

namespace Dutchie.Clients;

public interface IProductClient
{
    /// <summary>
    /// Returns the product catalog for the location.
    /// Only products enabled for API access and online availability are included.
    /// Use <paramref name="fromLastModifiedDateUtc"/> for incremental sync after an initial full load.
    /// Rate limit: 120/min.
    /// </summary>
    Task<IReadOnlyList<ProductDetail>> GetProductsAsync(
        DateTimeOffset? fromLastModifiedDateUtc = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);
}
