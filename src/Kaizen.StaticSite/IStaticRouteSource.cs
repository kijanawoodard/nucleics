namespace Kaizen.StaticSite;

/// <summary>
/// Supplies route values for ONE parameterised page template (for example "/blog/{slug}").
/// The exporter knows nothing about blogs: the site adapts whatever service it has to this contract.
/// </summary>
public interface IStaticRouteSource
{
    /// <summary>The endpoint route template this source expands, exactly as written in @page (e.g. "/blog/{slug}").</summary>
    string Template { get; }

    /// <summary>One dictionary per page to export, keyed by route parameter name (e.g. { "slug": "hello" }).</summary>
    IAsyncEnumerable<IReadOnlyDictionary<string, string>> GetRouteValuesAsync(CancellationToken cancellationToken);
}
