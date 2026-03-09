using AcadiaLogic.Dutchie.Models.Products;
using Microsoft.Extensions.Logging;

namespace AcadiaLogic.Dutchie.Clients;

internal sealed class ProductClient : DutchieClientBase, IProductClient
{
    public ProductClient(HttpClient http, ILogger<ProductClient> logger) : base(http, logger) { }

    public Task<IReadOnlyList<ProductDetail>> GetProductsAsync(
        DateTimeOffset? fromLastModifiedDateUtc = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var qs = BuildQueryString(q =>
        {
            Add(q, "fromLastModifiedDateUTC", fromLastModifiedDateUtc);
            Add(q, "isActive", isActive);
        });
        return GetAsync<IReadOnlyList<ProductDetail>>($"/products{qs}", cancellationToken);
    }
}
