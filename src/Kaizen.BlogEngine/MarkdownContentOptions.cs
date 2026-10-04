namespace Kaizen.BlogEngine;

public sealed class MarkdownContentOptions
{
    /// <summary>Folder of *.md files; relative paths resolve against the content root.</summary>
    public string ContentPath { get; set; } = "content/posts";
    /// <summary>URL prefix used to build <see cref="Post.Url"/> (the catch-all page is @page "/blog/{slug}").</summary>
    public string UrlPrefix { get; set; } = "/blog";
    /// <summary>Drafts are hidden unless this is true (e.g. in a dev server).</summary>
    public bool IncludeDrafts { get; set; }
}
