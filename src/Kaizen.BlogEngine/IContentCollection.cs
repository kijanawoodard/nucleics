namespace Kaizen.BlogEngine;

/// <summary>
/// A collection: a folder of content pages plus rules (url prefix, optional date-sorted index, feed, tags, draft handling).
/// "blog" is one collection; plain web pages are a collection with an empty prefix and no index (served at /{slug}/).
/// </summary>
public interface IContentCollection
{
    string Name { get; }
    /// <summary>Url prefix ("" for top-level pages, "/blog" for posts). Page urls are prefix + "/{slug}/".</summary>
    string UrlPrefix { get; }
    CollectionOptions Options { get; }
    /// <summary>Published pages (drafts hidden unless <see cref="CollectionOptions.IncludeDrafts"/>), newest first.</summary>
    IReadOnlyList<ContentPage> GetAll();
    ContentPage? GetBySlug(string slug);
    IReadOnlyList<ContentPage> GetByTag(string tag);
    IReadOnlyList<string> GetTags();
}

public static class ContentCollectionExtensions
{
    /// <summary>The page route template for this collection, e.g. "/blog/{slug}" or "/{slug}".</summary>
    public static string UrlTemplate(this IContentCollection c) => $"{c.UrlPrefix.TrimEnd('/')}/{{slug}}";
    /// <summary>The index route ("/blog"), or null when the collection has no index.</summary>
    public static string? IndexPath(this IContentCollection c) => c.Options.Index && c.UrlPrefix.Trim('/').Length > 0 ? c.UrlPrefix.TrimEnd('/') : null;
    /// <summary>Sitemap lastmod: the hand-set <c>updated</c> date, else <c>date</c>.</summary>
    public static DateTimeOffset LastModified(this ContentPage p) => p.Updated ?? p.Date;
}

/// <summary>All registered collections, by name.</summary>
public sealed class ContentCatalog(IEnumerable<IContentCollection> collections)
{
    public IReadOnlyList<IContentCollection> Collections { get; } = collections.ToList();
    public IContentCollection this[string name] =>
        Collections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"No content collection named '{name}' is registered (have: {string.Join(", ", Collections.Select(c => c.Name))}).");

    /// <summary>The published page whose url is <paramref name="path"/> ("/blog/hello", "blog/hello/", "/why-nuclear"), from any collection; else null.</summary>
    public ContentPage? FindByUrl(string path)
    {
        var p = "/" + path.Split('?', '#')[0].Trim('/');
        var slash = p.LastIndexOf('/');
        var (prefix, slug) = (p[..slash], p[(slash + 1)..]);
        if (slug.Length == 0) return null;
        return Collections.Where(c => string.Equals(c.UrlPrefix.TrimEnd('/'), prefix, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.GetBySlug(slug)).FirstOrDefault(x => x is not null);
    }

    /// <summary>The collection that loaded <paramref name="page"/>.</summary>
    public IContentCollection CollectionOf(ContentPage page) => this[page.Collection];
}
