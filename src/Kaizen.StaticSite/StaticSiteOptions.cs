namespace Kaizen.StaticSite;

public sealed class StaticSiteOptions
{
    /// <summary>Directory the export is written to (relative to the current directory unless rooted).</summary>
    public string OutputPath { get; set; } = "output";

    /// <summary>Absolute site URL used in sitemap.xml and robots.txt, e.g. https://nucleics.org (no trailing slash).</summary>
    public string SiteUrl { get; set; } = "http://localhost";

    /// <summary>
    /// Folder holding host files copied verbatim into the output root (see <see cref="PassthroughFiles"/>).
    /// Set by <c>AddStaticSite</c> to the repo/solution root; <c>null</c> disables passthrough.
    /// </summary>
    public string? PassthroughDirectory { get; set; }

    /// <summary>Plain-text host config files (no extension) copied as-is when present. Cloudflare Pages reads both from the output root.</summary>
    public List<string> PassthroughFiles { get; } = new() { "_headers", "_redirects" };

    /// <summary>
    /// Keep the Blazor framework scripts (<c>_framework/*</c>) and render the importmap that names them.
    /// Default false: a script-free SSR site ships neither. CLI: <c>--keep-framework</c>.
    /// </summary>
    public bool KeepFramework { get; set; }

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
    /// Asset routes NOT materialised (glob: '*' wildcard only). Default drops pre-compressed siblings (.gz/.br); 
    /// </summary>
    public List<string> AssetExcludePatterns { get; } = new() { "*.gz", "*.br" };

    /// <summary>Extra asset exclusions applied only while <see cref="KeepFramework"/> is false.</summary>
    public List<string> FrameworkAssetPatterns { get; } = new() { "_framework/*" };

    /// <summary>In check mode, dangling internal links fail the run (exit code 1). Export mode only reports them.</summary>
    public bool FailOnBrokenLinksInCheck { get; set; } = true;
}
