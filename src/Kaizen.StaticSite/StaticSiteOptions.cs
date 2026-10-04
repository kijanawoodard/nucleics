namespace Kaizen.StaticSite;

public sealed class StaticSiteOptions
{
    /// <summary>Directory the export is written to (relative to the current directory unless rooted).</summary>
    public string OutputPath { get; set; } = "output";

    /// <summary>Absolute site URL used in sitemap.xml and robots.txt, e.g. https://nucleics.org (no trailing slash).</summary>
    public string SiteUrl { get; set; } = "http://localhost";

    /// <summary>Pages carrying [Authorize] (and no [AllowAnonymous]) are excluded: the export runs anonymous.</summary>
    public bool ExcludeAuthorizedPages { get; set; } = true;

    /// <summary>Path that is requested to capture the 404 page; it must return HTTP 404.</summary>
    public string NotFoundProbePath { get; set; } = "/__kaizen_404_probe__";

    /// <summary>Extra lines appended to robots.txt (after the default allow-all + Sitemap lines).</summary>
    public List<string> RobotsExtraLines { get; } = new();

    /// <summary>Routes (paths) whose non-200 status is tolerated, e.g. "/legacy".</summary>
    public HashSet<string> AllowNon200 { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Extra literal paths to export in addition to the inventory (for example pages not reachable via endpoints).</summary>
    public List<string> ExtraPaths { get; } = new();

    /// <summary>
    /// Asset routes NOT materialised (glob: '*' wildcard only). Default drops pre-compressed siblings (.gz/.br); the importmap lists
    /// the Blazor Server script too, so everything the importmap names must exist.
    /// </summary>
    public List<string> AssetExcludePatterns { get; } = new() { "*.gz", "*.br" };

    /// <summary>In check mode, dangling internal links fail the run (exit code 1). Export mode only reports them.</summary>
    public bool FailOnBrokenLinksInCheck { get; set; } = true;
}
