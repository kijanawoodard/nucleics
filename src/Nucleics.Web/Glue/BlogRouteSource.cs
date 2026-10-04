using Kaizen.BlogEngine;
using Kaizen.StaticSite;

namespace Nucleics.Web.Glue;

/// <summary>
/// The glue: adapts the blog's post service (Kaizen.BlogEngine) to the exporter's route-source contract (Kaizen.StaticSite).
/// The two libraries know nothing about each other; only the site references both.
/// </summary>
public sealed class BlogRouteSource(IPostService posts) : IStaticRouteSource
{
    public string Template => $"{posts.UrlPrefix.TrimEnd('/')}/{{slug}}";

    public async IAsyncEnumerable<IReadOnlyDictionary<string, string>> GetRouteValuesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var p in posts.GetAll()) // GetAll() already hides drafts
            yield return new Dictionary<string, string> { ["slug"] = p.Slug };
        await Task.CompletedTask;
    }
}

/// <summary>
/// The blog index exists only while there is at least one published post, so a site with no posts (or only drafts) ships no /blog page
/// and no dangling links to it. Adapts <see cref="IPostService"/> to the exporter's <see cref="IStaticPageGate"/>.
/// </summary>
public sealed class BlogIndexGate(IPostService posts) : IStaticPageGate
{
    public string Template => posts.UrlPrefix.TrimEnd('/');

    public bool ShouldExport(out string? reason)
    {
        reason = posts.GetAll().Count == 0 ? "no published posts" : null;
        return reason is null;
    }
}
