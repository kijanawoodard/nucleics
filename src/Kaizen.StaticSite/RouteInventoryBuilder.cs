using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.DependencyInjection;

namespace Kaizen.StaticSite;

/// <summary>
/// Builds the single route inventory that feeds export, sitemap, and check mode.
/// Page endpoints = RouteEndpoint + ComponentTypeMetadata. Asset endpoints = StaticAssetDescriptor metadata.
/// Call only after the app is built (endpoints are created lazily).
/// </summary>
public static partial class RouteInventoryBuilder
{
    // {id}, {id?}, {id:int}, {*slug}, {**slug}
    [GeneratedRegex(@"\{(\*{0,2})(\w+)(:[^}?]*)?(\?)?\}")]
    private static partial Regex ParamRegex();

    public static async Task<RouteInventory> BuildAsync(WebApplication app, StaticSiteOptions options, CancellationToken ct)
    {
        var inv = new RouteInventory();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).ToList();
        var sources = app.Services.GetServices<IStaticRouteSource>().ToList();
        var gates = app.Services.GetServices<IStaticPageGate>().ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var ep in endpoints.OfType<RouteEndpoint>())
        {
            var cm = ep.Metadata.GetMetadata<ComponentTypeMetadata>();
            if (cm is not null)
            {
                await AddPageAsync(inv, ep, cm.Type, options, sources, gates, seen, ct);
                continue;
            }

            var sa = ep.Metadata.GetMetadata<StaticAssetDescriptor>();
            if (sa is not null && sa.Selectors.Count == 0)
            {
                var route = sa.Route.TrimStart('/');
                if (inv.Assets.Any(a => a.Route == route) || inv.ExcludedAssets.Any(a => a.Route == route)) continue; // selector duplicates
                var patterns = options.KeepFramework ? options.AssetExcludePatterns : options.AssetExcludePatterns.Concat(options.FrameworkAssetPatterns);
                if (patterns.Any(p => GlobMatch(p, route)))
                {
                    if (!route.EndsWith(".gz") && !route.EndsWith(".br")) inv.ExcludedAssets.Add(new AssetRoute(route, sa.AssetPath));
                    continue;
                }
                inv.Assets.Add(new AssetRoute(route, sa.AssetPath));
            }
        }

        foreach (var extra in options.ExtraPaths)
            if (seen.Add(Normalize(extra)))
                inv.Routes.Add(new StaticRoute(Normalize(extra), extra, RouteOrigin.Extra, null));

        foreach (var g in gates)
            if (!endpoints.OfType<RouteEndpoint>().Any(e => e.Metadata.GetMetadata<ComponentTypeMetadata>() is not null
                    && string.Equals(e.RoutePattern.RawText, g.Template, StringComparison.OrdinalIgnoreCase)))
                inv.Warnings.Add($"IStaticPageGate for template '{g.Template}' matches no page endpoint.");

        // Sources whose template matches no page: almost certainly a typo.
        foreach (var s in sources)
            if (!endpoints.OfType<RouteEndpoint>().Any(e => e.Metadata.GetMetadata<ComponentTypeMetadata>() is not null
                    && string.Equals(e.RoutePattern.RawText, s.Template, StringComparison.OrdinalIgnoreCase)))
                inv.Warnings.Add($"IStaticRouteSource for template '{s.Template}' matches no page endpoint.");

        inv.Routes.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        inv.Assets.Sort((a, b) => string.CompareOrdinal(a.Route, b.Route));
        return inv;
    }

    private static async Task AddPageAsync(RouteInventory inv, RouteEndpoint ep, Type component, StaticSiteOptions options,
        List<IStaticRouteSource> sources, List<IStaticPageGate> gates, HashSet<string> seen, CancellationToken ct)
    {
        var template = ep.RoutePattern.RawText ?? "/";

        // Opt-out attribute (copied into endpoint metadata AND readable from the type; read the type: stable).
        var excl = component.GetCustomAttributes(typeof(ExcludeFromStaticExportAttribute), inherit: true)
            .OfType<ExcludeFromStaticExportAttribute>().FirstOrDefault();
        if (excl is not null)
        {
            inv.Excluded.Add(new ExcludedRoute(template, "ExcludeFromStaticExport" + (excl.Reason is null ? "" : $" ({excl.Reason})"), component));
            return;
        }

        // Page gate: the site may switch a page off at export time (e.g. no blog index without published posts).
        foreach (var g in gates.Where(g => string.Equals(g.Template, template, StringComparison.OrdinalIgnoreCase)))
            if (!g.ShouldExport(out var why))
            {
                inv.Excluded.Add(new ExcludedRoute(template, "IStaticPageGate" + (why is null ? "" : $" ({why})"), component));
                return;
            }

        // Auth gate: export runs anonymous; a login redirect would only ever be an error.
        if (options.ExcludeAuthorizedPages
            && ep.Metadata.GetOrderedMetadata<IAuthorizeData>().Any()
            && ep.Metadata.GetMetadata<IAllowAnonymous>() is null)
        {
            inv.Excluded.Add(new ExcludedRoute(template, "requires authorization ([Authorize])", component));
            return;
        }

        var parms = ParamRegex().Matches(template);
        if (parms.Count == 0)
        {
            Add(inv, seen, Normalize(template), template, RouteOrigin.Direct, component);
            return;
        }

        if (parms.All(m => m.Groups[4].Success)) // only optional parameters: emit the bare form
        {
            var bare = Normalize(ParamRegex().Replace(template, "").Replace("//", "/"));
            Add(inv, seen, bare, template, RouteOrigin.Direct, component);
            inv.Warnings.Add($"'{template}' has optional parameters; only the parameter-less URL '{bare}' is exported.");
            return;
        }

        var mine = sources.Where(s => string.Equals(s.Template, template, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mine.Count == 0)
        {
            inv.Warnings.Add($"LOUD: parameterised route '{template}' ({component.Name}) has no IStaticRouteSource; it is NOT exported.");
            inv.Excluded.Add(new ExcludedRoute(template, "parameterised route without IStaticRouteSource", component));
            return;
        }

        var names = parms.Select(m => m.Groups[2].Value).ToList();
        foreach (var src in mine)
            await foreach (var values in src.GetRouteValuesAsync(ct).WithCancellation(ct))
            {
                var missing = names.Where(n => !values.ContainsKey(n) && !parms.First(m => m.Groups[2].Value == n).Groups[4].Success).ToList();
                if (missing.Count > 0)
                {
                    inv.Warnings.Add($"Route values for '{template}' missing parameter(s): {string.Join(", ", missing)}; skipped.");
                    continue;
                }
                var path = ParamRegex().Replace(template, m =>
                    values.TryGetValue(m.Groups[2].Value, out var v)
                        ? (m.Groups[1].Length > 0 ? v : Uri.EscapeDataString(v))
                        : "");
                Add(inv, seen, Normalize(path), template, RouteOrigin.Expanded, component);
            }
    }

    private static void Add(RouteInventory inv, HashSet<string> seen, string path, string template, RouteOrigin origin, Type? c)
    {
        if (seen.Add(path)) inv.Routes.Add(new StaticRoute(path, template, origin, c));
    }

    /// <summary>"/about/" and "about" both become "/about"; the root stays "/".</summary>
    public static string Normalize(string path)
    {
        var p = path.StartsWith('/') ? path : "/" + path;
        return p.Length > 1 ? p.TrimEnd('/') : p;
    }

    public static bool GlobMatch(string pattern, string text) =>
        Regex.IsMatch(text, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
}
