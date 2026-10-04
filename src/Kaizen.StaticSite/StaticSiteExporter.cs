using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Kaizen.StaticSite;

public enum StaticSiteMode { Export, Check }

/// <summary>
/// Boots the real app on a random loopback port, fetches every inventoried route with HttpClient, and (in Export mode)
/// writes route/index.html, 404.html, sitemap.xml, robots.txt, a manifest and every static asset to the output folder.
/// Check mode does the same fetching and validation but writes nothing.
/// </summary>
public sealed class StaticSiteExporter
{
    private readonly WebApplication _app;
    private readonly StaticSiteOptions _o;
    private readonly TextWriter _log;

    public StaticSiteExporter(WebApplication app, StaticSiteOptions options, TextWriter log)
    {
        _app = app; _o = options; _log = log;
    }

    /// <returns>Process exit code: 0 ok, 1 failures.</returns>
    public async Task<int> RunAsync(StaticSiteMode mode, CancellationToken ct = default)
    {
        _app.Urls.Clear();
        _app.Urls.Add("http://127.0.0.1:0");
        await _app.StartAsync(ct);
        try { return await RunCoreAsync(mode, ct); }
        finally { await _app.StopAsync(CancellationToken.None); }
    }

    private async Task<int> RunCoreAsync(StaticSiteMode mode, CancellationToken ct)
    {
        var baseAddress = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        var baseUri = new Uri(baseAddress.Replace("[::]", "127.0.0.1"));
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None })
        { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(30) };

        var inv = await RouteInventoryBuilder.BuildAsync(_app, _o, ct);
        var failures = new List<string>();
        if (inv.Routes.Count == 0) failures.Add("route inventory is empty: no exportable page endpoints were discovered");
        _log.WriteLine($"[{mode}] {baseUri}  pages={inv.Routes.Count} excluded={inv.Excluded.Count} assets={inv.Assets.Count}");
        foreach (var w in inv.Warnings) _log.WriteLine($"  WARN {w}");
        foreach (var x in inv.Excluded) _log.WriteLine($"  skip {x.Template}  -- {x.Reason}");

        var pages = new Dictionary<string, string>(StringComparer.Ordinal); // path -> html
        var outRoot = Path.GetFullPath(_o.OutputPath);

        // 1) pages
        foreach (var r in inv.Routes)
        {
            using var resp = await http.GetAsync(r.Path, ct);
            var status = (int)resp.StatusCode;
            if (status != 200 && !_o.AllowNon200.Contains(r.Path))
            {
                failures.Add($"route {r.Path} -> HTTP {status} (template {r.Template})");
                _log.WriteLine($"  FAIL {r.Path} -> {status}");
                continue;
            }
            var html = await resp.Content.ReadAsStringAsync(ct);
            pages[r.Path] = html;
            _log.WriteLine($"  ok   {r.Path} -> {FileFor(r.Path)} ({status})");
        }

        // 2) 404 probe
        string? notFoundHtml = null;
        using (var resp = await http.GetAsync(_o.NotFoundProbePath, ct))
        {
            if ((int)resp.StatusCode != 404) failures.Add($"404 probe {_o.NotFoundProbePath} -> HTTP {(int)resp.StatusCode} (expected 404)");
            else { notFoundHtml = await resp.Content.ReadAsStringAsync(ct); _log.WriteLine("  ok   404 probe -> 404.html"); }
        }

        // 3) assets
        var assetBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var a in inv.Assets)
        {
            using var resp = await http.GetAsync("/" + a.Route, ct);
            if (resp.StatusCode != HttpStatusCode.OK) { failures.Add($"asset /{a.Route} -> HTTP {(int)resp.StatusCode}"); continue; }
            assetBytes[a.Route] = await resp.Content.ReadAsByteArrayAsync(ct);
        }
        _log.WriteLine($"  assets fetched: {assetBytes.Count}/{inv.Assets.Count}");

        // 4) link report: every internal reference must resolve to a generated file
        var known = new HashSet<string>(inv.RoutePaths.Select(p => p), StringComparer.Ordinal);
        foreach (var a in inv.Assets) known.Add(RouteInventoryBuilder.Normalize(a.Route));
        known.UnionWith(new[] { "/404.html", "/sitemap.xml", "/robots.txt" });
        var dangling = new List<string>();
        var droppedRoutes = inv.ExcludedAssets.Select(a => RouteInventoryBuilder.Normalize(a.Route)).ToHashSet(StringComparer.Ordinal);
        foreach (var (path, html) in pages)
            foreach (var re in LinkScanner.Scan(html, new Uri(baseUri, path), _o.SiteUrl))
                if (re.InternalPath is not null && droppedRoutes.Contains(re.InternalPath))
                    failures.Add($"{path}: <{re.Tag} {re.Attribute}=\"{re.Raw}\"> references {re.InternalPath}, an asset that is excluded from the output (use --keep-framework or stop referencing it)");
                else if (re.InternalPath is not null && !known.Contains(re.InternalPath))
                    dangling.Add($"{path}: <{re.Tag} {re.Attribute}=\"{re.Raw}\"> -> {re.InternalPath} (not generated)");
        dangling = dangling.Distinct().ToList();
        foreach (var d in dangling) _log.WriteLine($"  LINK {d}");
        var linkFailures = mode == StaticSiteMode.Check && _o.FailOnBrokenLinksInCheck ? dangling : new List<string>();

        // 4b) compare against an existing export (report-only): routes the inventory knows but output/ lacks, and vice versa
        var manifestPath = Path.Combine(outRoot, "kaizen-manifest.json");
        if (mode == StaticSiteMode.Check && File.Exists(manifestPath))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var written = doc.RootElement.GetProperty("pages").EnumerateArray().Select(e => e.GetProperty("Path").GetString()!).ToHashSet(StringComparer.Ordinal);
            foreach (var p in pages.Keys.Except(written).Order(StringComparer.Ordinal)) _log.WriteLine($"  STALE route {p} is discovered but missing from {_o.OutputPath} (re-run export)");
            foreach (var p in written.Except(pages.Keys).Order(StringComparer.Ordinal)) _log.WriteLine($"  STALE route {p} is in {_o.OutputPath} but no longer discovered");
        }

        // 5) write
        if (mode == StaticSiteMode.Export && failures.Count == 0)
        {
            var passthrough = WriteOutput(outRoot, inv, pages, notFoundHtml, assetBytes);
            _log.WriteLine($"  wrote {outRoot}: {pages.Count} pages, {assetBytes.Count} assets, 404.html, sitemap.xml, robots.txt, kaizen-manifest.json" + (passthrough.Count > 0 ? ", " + string.Join(", ", passthrough) : ""));
            if (inv.ExcludedAssets.Count > 0) _log.WriteLine($"  dropped {inv.ExcludedAssets.Count} unused framework asset(s) (pass --keep-framework to keep)");
        }

        _log.WriteLine($"[{mode}] pages_ok={pages.Count}/{inv.Routes.Count} http_failures={failures.Count} dangling_links={dangling.Count}");
        foreach (var f in failures) _log.WriteLine($"  ERROR {f}");
        foreach (var f in linkFailures) _log.WriteLine($"  ERROR dangling link {f}");
        var bad = failures.Count + linkFailures.Count;
        _log.WriteLine(bad == 0 ? $"[{mode}] PASS" : $"[{mode}] FAIL ({bad} problem(s))");
        return bad == 0 ? 0 : 1;
    }

    private static string FileFor(string routePath) => routePath == "/" ? "index.html" : routePath.TrimStart('/') + "/index.html";

    private List<string> WriteOutput(string outRoot, RouteInventory inv, Dictionary<string, string> pages, string? notFoundHtml,
        Dictionary<string, byte[]> assets)
    {
        if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        Directory.CreateDirectory(outRoot);
        string Safe(string rel)
        {
            var full = Path.GetFullPath(Path.Combine(outRoot, Uri.UnescapeDataString(rel)));
            if (!full.StartsWith(outRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException($"path escapes output: {rel}");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            return full;
        }

        foreach (var (path, html) in pages) File.WriteAllText(Safe(FileFor(path)), html, new UTF8Encoding(false));
        if (notFoundHtml is not null) File.WriteAllText(Safe("404.html"), notFoundHtml, new UTF8Encoding(false));
        foreach (var (route, bytes) in assets) File.WriteAllBytes(Safe(route), bytes);

        var site = _o.SiteUrl.TrimEnd('/');
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var sitemap = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "urlset", pages.Keys.OrderBy(p => p, StringComparer.Ordinal).Select(p =>
                new XElement(ns + "url", new XElement(ns + "loc", site + (p == "/" ? "/" : p + "/"))))));
        File.WriteAllText(Safe("sitemap.xml"), sitemap.Declaration + Environment.NewLine + sitemap.ToString(), new UTF8Encoding(false));

        var robots = new StringBuilder().AppendLine("User-agent: *").AppendLine("Allow: /").AppendLine($"Sitemap: {site}/sitemap.xml");
        foreach (var l in _o.RobotsExtraLines) robots.AppendLine(l);
        File.WriteAllText(Safe("robots.txt"), robots.ToString(), new UTF8Encoding(false));

        var manifest = new
        {
            site,
            pages = inv.Routes.Where(r => pages.ContainsKey(r.Path)).Select(r => new { r.Path, file = FileFor(r.Path), r.Template, origin = r.Origin.ToString() }),
            assets = assets.Keys.OrderBy(k => k, StringComparer.Ordinal),
            excluded = inv.Excluded.Select(e => new { e.Template, e.Reason }),
        };
        File.WriteAllText(Safe("kaizen-manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

        return PassthroughFiles.Copy(_o, outRoot, _log, Console.Error);
    }
}
