namespace Kaizen.StaticSite;

/// <summary>
/// Decides at export time whether ONE page template is exported at all (for example "/blog" only when at least one post is published).
/// A gated-off page is left out of the output, the sitemap and link checks, and is listed as excluded. The exporter knows nothing about blogs:
/// the site adapts whatever service it has to this contract. Pages without a gate are always exported.
/// </summary>
public interface IStaticPageGate
{
    /// <summary>The endpoint route template this gate applies to, exactly as written in @page (e.g. "/blog").</summary>
    string Template { get; }

    /// <summary>True to export the page, false to leave it out. Also returns a reason shown in the excluded list.</summary>
    bool ShouldExport(out string? reason);
}
