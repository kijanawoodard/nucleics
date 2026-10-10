using Kaizen.StaticSite;
using Xunit;

namespace Kaizen.StaticSite.Tests;

public class SitemapTests
{
    private static readonly System.Xml.Linq.XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    [Fact]
    public void Lastmod_is_written_only_where_a_date_is_supplied()
    {
        var doc = StaticSiteExporter.BuildSitemap("https://example.org/", new (string, DateTimeOffset?)[]
        {
            ("/why", new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero)),
            ("/", null),
        });
        var urls = doc.Root!.Elements(Ns + "url").ToList();
        Assert.Equal("https://example.org/", urls[0].Element(Ns + "loc")!.Value);
        Assert.Null(urls[0].Element(Ns + "lastmod"));
        Assert.Equal("https://example.org/why/", urls[1].Element(Ns + "loc")!.Value);
        Assert.Equal("2026-10-10", urls[1].Element(Ns + "lastmod")!.Value);
    }

    [Fact]
    public void Lastmod_uses_the_supplied_calendar_date_not_utc_conversion()
    {
        var doc = StaticSiteExporter.BuildSitemap("https://example.org", new (string, DateTimeOffset?)[]
        {
            ("/p", new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.FromHours(-5))),
        });
        Assert.Equal("2026-10-10", doc.Root!.Element(Ns + "url")!.Element(Ns + "lastmod")!.Value);
    }
}
