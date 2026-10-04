namespace Kaizen.Seo;

/// <summary>Site-wide SEO settings, registered once with <c>services.AddSeo(...)</c>.</summary>
public sealed class SeoOptions
{
    /// <summary>Absolute public URL, no trailing slash (e.g. https://nucleics.org). Canonical and og:url are built from it.</summary>
    public string SiteUrl { get; set; } = "http://localhost";
    public string SiteName { get; set; } = "";
    /// <summary>Default og:image / twitter:image (absolute, or root-relative path). Optional.</summary>
    public string? DefaultImage { get; set; }
    /// <summary>e.g. "@nucleics". Optional.</summary>
    public string? TwitterSite { get; set; }
    public string Locale { get; set; } = "en_US";

    /// <summary>When set, every SeoHead also emits Organization JSON-LD.</summary>
    public OrganizationInfo? Organization { get; set; }

    /// <summary>Canonical URLs end with '/' (matches route/index.html hosting, e.g. Cloudflare Pages). Root stays "/".</summary>
    public bool TrailingSlash { get; set; } = true;
}

public sealed record OrganizationInfo(string Name, string? Logo = null, string[]? SameAs = null, string? Description = null);
