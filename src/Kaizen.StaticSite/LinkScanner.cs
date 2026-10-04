using System.Text.RegularExpressions;

namespace Kaizen.StaticSite;

/// <summary>Extracts internal URLs from rendered HTML (regex on generated markup; Blazor always quotes attributes).</summary>
public static partial class LinkScanner
{
    [GeneratedRegex(@"<(?<tag>a|link|img|script|source)\b(?<attrs>[^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex TagRegex();
    [GeneratedRegex(@"\b(?<name>href|src)\s*=\s*""(?<v>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex AttrRegex();
    [GeneratedRegex(@"<base\b[^>]*\bhref\s*=\s*""(?<v>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex BaseRegex();

    public sealed record Reference(string Tag, string Attribute, string Raw, string? InternalPath);

    /// <param name="pageUri">The URL the page was fetched from (decides how relative URLs resolve).</param>
    /// <param name="siteUrl">Configured public site URL; absolute URLs under it count as internal.</param>
    public static IReadOnlyList<Reference> Scan(string html, Uri pageUri, string siteUrl)
    {
        var baseMatch = BaseRegex().Match(html);
        var baseUri = baseMatch.Success && Uri.TryCreate(pageUri, baseMatch.Groups["v"].Value, out var b) ? b : pageUri;
        var siteUri = Uri.TryCreate(siteUrl.TrimEnd('/') + "/", UriKind.Absolute, out var su) ? su : null;
        var list = new List<Reference>();
        foreach (Match t in TagRegex().Matches(html))
        foreach (Match a in AttrRegex().Matches(t.Groups["attrs"].Value))
        {
            var raw = System.Net.WebUtility.HtmlDecode(a.Groups["v"].Value).Trim();
            if (raw.Length == 0 || raw.StartsWith('#')) { continue; }
            if (Regex.IsMatch(raw, @"^(mailto|tel|javascript|data|sms):", RegexOptions.IgnoreCase)) continue;
            if (!Uri.TryCreate(baseUri, raw, out var abs)) continue;
            var internalHost = abs.Host == pageUri.Host && abs.Port == pageUri.Port
                               || (siteUri is not null && abs.Host == siteUri.Host);
            list.Add(new Reference(t.Groups["tag"].Value.ToLowerInvariant(), a.Groups["name"].Value.ToLowerInvariant(), raw,
                internalHost ? Canonical(abs) : null));
        }
        return list;
    }

    /// <summary>Path form used for lookups: unescaped, no trailing slash, "/x/index.html" -> "/x".</summary>
    public static string Canonical(Uri uri)
    {
        var p = Uri.UnescapeDataString(uri.AbsolutePath);
        if (p.EndsWith("/index.html", StringComparison.Ordinal)) p = p[..^"index.html".Length];
        return RouteInventoryBuilder.Normalize(p);
    }
}
