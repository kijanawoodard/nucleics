using System.Text.Json;

namespace Kaizen.Seo;

/// <summary>Tiny schema.org JSON-LD builders. Each returns a plain dictionary; <see cref="Serialize"/> makes it script-safe.</summary>
public static class JsonLd
{
    private const string Ctx = "https://schema.org";

    public static Dictionary<string, object?> Organization(string name, string url, string? logoUrl = null, IEnumerable<string>? sameAs = null, string? description = null)
        => Clean(new()
        {
            ["@context"] = Ctx, ["@type"] = "Organization", ["name"] = name, ["url"] = url,
            ["logo"] = logoUrl, ["description"] = description, ["sameAs"] = sameAs?.ToArray() is { Length: > 0 } s ? s : null,
        });

    public static Dictionary<string, object?> WebSite(string name, string url)
        => new() { ["@context"] = Ctx, ["@type"] = "WebSite", ["name"] = name, ["url"] = url };

    public static Dictionary<string, object?> BlogPosting(string headline, string url, DateTimeOffset published,
        string? description = null, DateTimeOffset? modified = null, string? authorName = null, string? publisherName = null,
        string? image = null, IEnumerable<string>? keywords = null)
        => Clean(new()
        {
            ["@context"] = Ctx, ["@type"] = "BlogPosting", ["headline"] = headline,
            ["mainEntityOfPage"] = new Dictionary<string, object?> { ["@type"] = "WebPage", ["@id"] = url },
            ["url"] = url, ["datePublished"] = published.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
            ["dateModified"] = (modified ?? published).ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
            ["description"] = description, ["image"] = image,
            ["author"] = authorName is null ? null : new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = authorName },
            ["publisher"] = publisherName is null ? null : new Dictionary<string, object?> { ["@type"] = "Organization", ["name"] = publisherName },
            ["keywords"] = keywords?.ToArray() is { Length: > 0 } k ? string.Join(", ", k) : null,
        });

    public static Dictionary<string, object?> BreadcrumbList(params (string Name, string Url)[] items)
        => new()
        {
            ["@context"] = Ctx, ["@type"] = "BreadcrumbList",
            ["itemListElement"] = items.Select((it, i) => new Dictionary<string, object?>
                { ["@type"] = "ListItem", ["position"] = i + 1, ["name"] = it.Name, ["item"] = it.Url }).ToArray(),
        };

    /// <summary>JSON safe to inline in a script element: the default encoder escapes &lt; &gt; &amp; (no "&lt;/script&gt;" break-out).</summary>
    public static string Serialize(object data) => JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });

    private static Dictionary<string, object?> Clean(Dictionary<string, object?> d)
    {
        foreach (var k in d.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList()) d.Remove(k);
        return d;
    }
}
