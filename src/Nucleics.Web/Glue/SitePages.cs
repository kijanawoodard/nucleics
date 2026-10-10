using Kaizen.BlogEngine;
using Kaizen.StaticSite;

namespace Nucleics.Web.Glue;

/// <summary>
/// Evergreen web pages (docs/ia-roadmap.md: "markdown with layout: page"). Reuses the blog's markdown service over content/pages
/// with an empty url prefix, so content/pages/why-nuclear.md is served at /why-nuclear/ and never appears in the blog listing.
/// </summary>
public sealed class SitePages(IPostService inner)
{
    public IReadOnlyList<Post> GetAll() => inner.GetAll();
    public Post? GetBySlug(string slug) => inner.GetBySlug(slug);
}

/// <summary>Expands the "/{slug}" page template to one route per published web page, so each lands in the export and the sitemap.</summary>
public sealed class PageRouteSource(SitePages pages) : IStaticRouteSource
{
    public string Template => "/{slug}";

    public async IAsyncEnumerable<IReadOnlyDictionary<string, string>> GetRouteValuesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var p in pages.GetAll())
            yield return new Dictionary<string, string> { ["slug"] = p.Slug };
        await Task.CompletedTask;
    }
}
