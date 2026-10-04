namespace Kaizen.BlogEngine;

/// <summary>A parsed markdown post. <see cref="Html"/> is rendered by Markdig and is TRUSTED author content (raw HTML allowed).</summary>
public sealed record Post(
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
    /// <summary>Site-relative URL with trailing slash, e.g. /blog/hello/. Set by the service from the url prefix.</summary>
    public string Url { get; init; } = "";
}
