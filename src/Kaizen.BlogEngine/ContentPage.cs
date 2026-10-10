namespace Kaizen.BlogEngine;

/// <summary>A parsed markdown content page (a blog post, a plain web page, ...). <see cref="Html"/> is rendered by Markdig and is TRUSTED author content (raw HTML allowed).</summary>
public sealed record ContentPage(
    string Slug,
    string Title,
    DateTimeOffset Date,
    DateTimeOffset? Updated,
    string? Description,
    string? Author,
    IReadOnlyList<string> Tags,
    bool Draft,
    string Html,
    string SourcePath)
{
    /// <summary>Site-relative URL with trailing slash, e.g. /blog/hello/ or /why-nuclear/. Set by the collection from its url prefix.</summary>
    public string Url { get; init; } = "";
    /// <summary>Layout from front matter (e.g. "page"); null for the default post layout.</summary>
    public string? Layout { get; init; }
    /// <summary>Name of the collection that loaded this page (e.g. "blog", "pages").</summary>
    public string Collection { get; init; } = "";
}
